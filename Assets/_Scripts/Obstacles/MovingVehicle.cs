using System;
using UnityEngine;
using CampusRun.Audio;
using CampusRun.Player;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 도로 레인 위를 좌우 일정한 속도로 횡이동하는 차량/장애물 컴포넌트입니다.
    /// 3D 입체 음향을 탑재하여 멀리서 다가올 때는 작게, 코앞에서는 크게, 멀어질 때는 다시 작아집니다.
    /// </summary>
    public class MovingVehicle : MonoBehaviour
    {
        [Header("--- 차량 식별 ---")]
        [Tooltip("사망 메시지에 표시될 차량 명칭 (예: 버스, 트럭, 승용차)")]
        [SerializeField] private string _vehicleDisplayName = "승용차";

        public string VehicleDisplayName => _vehicleDisplayName;

        [Header("--- 이동 속성 ---")]
        [Tooltip("이동 방향 (+1: 오른쪽, -1: 왼쪽)")]
        [SerializeField] private float _direction = 1f;

        [Tooltip("초당 이동 속도")]
        [SerializeField] private float _speed = 8.5f;

        [Tooltip("길의 양 끝에 도달 시 사라지는 X축 경계선 (도로 폭 60m 기준 ±28.0m)")]
        [SerializeField] private float _despawnBoundaryX = 28.0f;

        [Header("--- 경적(클락션) 설정 ---")]
        [Tooltip("화면에 보이는 도로를 지날 때 경적을 울릴 확률 (기본 0.20 = 20%, 약 5대 중 1대꼴로 적절히 발생)")]
        [Range(0f, 1f)]
        [SerializeField] private float _honkChance = 0.20f;

        [Tooltip("경적 소리 볼륨 (기본 0.85, 엔진음 위로 명확히 들리는 선명한 음량)")]
        [Range(0f, 1f)]
        [SerializeField] private float _honkVolume = 0.85f;

        [Tooltip("플레이어와 Z축(화면 내 도로) 거리 허용 범위 (단위: m, 기본 14.0m = 화면에 보이는 모든 도로 포함)")]
        [SerializeField] private float _honkTriggerZDistance = 14.0f;

        [Tooltip("전체 차량 경적 전역 최소 쿨다운 (초, 연속 소음 방지)")]
        [SerializeField] private float _honkGlobalCooldown = 1.8f;

        // 전역 경적 쿨다운 관리 (GC 할당 0)
        private static float s_nextGlobalHonkTime = 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetHonkStatics()
        {
            s_nextGlobalHonkTime = 0f;
        }

        private Action<MovingVehicle> _onDespawnCallback;
        private bool _isActive = false;
        private AudioSource _audioSource;
        private bool _hasHonked = false;

        private void Awake()
        {
            SetupSpatialAudioSource();
        }

        private void SetupSpatialAudioSource()
        {
            if (_audioSource != null) return;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            // 쿼터뷰 카메라에 최적화된 3D 공간 음향
            _audioSource.spatialBlend = 0.80f;             // 3D 입체 음향
            _audioSource.rolloffMode = AudioRolloffMode.Linear; // 선형 거리 감쇠
            _audioSource.minDistance = 9.0f;               // 카메라/플레이어 앞을 지날 때 최대 음량
            _audioSource.maxDistance = 50.0f;              // 적절한 거리 감쇠
            _audioSource.dopplerLevel = 0.0f;              // 도플러 왜곡 제거로 깨끗한 음색 유지
            _audioSource.playOnAwake = false;
        }

        public void Initialize(float direction, float speed, Action<MovingVehicle> onDespawn)
        {
            _direction = Mathf.Sign(direction);
            _speed = speed;
            _onDespawnCallback = onDespawn;
            _isActive = true;
            _hasHonked = false;

            // 스케일 정규화 (부모 스케일 왜곡 방지)
            transform.localScale = Vector3.one;

            // 이동 방향에 맞춰 차량 머리 회전
            Vector3 lookDir = (_direction > 0) ? Vector3.right : Vector3.left;
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            // [중요!] 먼저 오브젝트를 활성화해야 AudioSource.Play()가 안전하게 실행됨
            gameObject.SetActive(true);

            // 3D 엔진 주행음 시작 (다가올 때부터 커짐)
            Play3DVehicleSound();
        }

        private void Play3DVehicleSound()
        {
            if (_audioSource == null) return;

            var audioMgr = AudioManager.Instance;
            AudioClip engineClip = audioMgr != null ? audioMgr.CarEngineClip : null;
            if (engineClip == null)
            {
                engineClip = Resources.Load<AudioClip>("SFX/car_engine");
            }

            if (engineClip != null)
            {
                _audioSource.clip = engineClip;
                _audioSource.volume = 0.70f;
                _audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                _audioSource.loop = true; // 주행 내내 엔진음 지속
                _audioSource.Play();
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // X축 방향으로 등속 직선 이동 (월드 좌표 기준)
            transform.position += new Vector3(_direction * _speed * Time.deltaTime, 0f, 0f);

            // 도로 중심(X=0 부근)을 지날 때 조건부 경적 (화면 내 차량 중 20% 확률로 빵!)
            if (!_hasHonked && Mathf.Abs(transform.position.x) < 4.0f)
            {
                _hasHonked = true;

                // 1. 전체 차량 경적 쿨다운 체크 (최소 1.8초 간격 유지)
                if (Time.time >= s_nextGlobalHonkTime)
                {
                    // 2. 화면 내(Z축 14m 이내) 도로 여부 체크
                    Vector3 playerPos = GridPlayerController.Instance != null
                        ? GridPlayerController.Instance.transform.position
                        : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

                    float zDiff = Mathf.Abs(transform.position.z - playerPos.z);
                    if (zDiff <= _honkTriggerZDistance)
                    {
                        // 3. 20%의 적절한 확률 검사 (원래 35%에서 절반으로 하향, 5대 중 1대꼴로 빵!)
                        if (UnityEngine.Random.value < _honkChance)
                        {
                            s_nextGlobalHonkTime = Time.time + _honkGlobalCooldown;

                            var audioMgr = AudioManager.Instance;
                            AudioClip klaxonClip = audioMgr != null ? audioMgr.CarKlaxonClip : null;
                            if (klaxonClip == null)
                            {
                                klaxonClip = Resources.Load<AudioClip>("SFX/car_klaxon");
                            }

                            if (klaxonClip != null && _audioSource != null && _audioSource.isActiveAndEnabled)
                            {
                                _audioSource.PlayOneShot(klaxonClip, _honkVolume);
                                Debug.Log($"[CampusRun] 차량 경적 울림! ({_vehicleDisplayName}, 볼륨: {_honkVolume})");
                            }
                        }
                    }
                }
            }

            // 길의 양 끝쪽 경계선에 도달 시 즉시 비활성화 및 회수
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        public void Despawn()
        {
            if (!_isActive) return;

            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }

            _isActive = false;
            gameObject.SetActive(false);

            var callback = _onDespawnCallback;
            _onDespawnCallback = null;
            callback?.Invoke(this);
        }
    }
}
