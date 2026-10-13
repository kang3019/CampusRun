using System;
using UnityEngine;
using CampusRun.Audio;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 도로 레인 위를 좌우 일정한 속도로 횡이동하는 차량/장애물 컴포넌트입니다.
    /// 3D 입체 음향을 탑재하여 멀리서 다가올 때는 작게, 코앞에서는 크게, 멀어질 때는 다시 작아집니다.
    /// </summary>
    public class MovingVehicle : MonoBehaviour
    {
        [Header("--- 이동 속성 ---")]
        [Tooltip("이동 방향 (+1: 오른쪽, -1: 왼쪽)")]
        [SerializeField] private float _direction = 1f;

        [Tooltip("초당 이동 속도")]
        [SerializeField] private float _speed = 8.5f;

        [Tooltip("길의 양 끝에 도달 시 사라지는 X축 경계선 (도로 폭 60m 기준 ±28.0m)")]
        [SerializeField] private float _despawnBoundaryX = 28.0f;

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

            // 쿼터뷰 카메라(X=0, Y=6.4m, Z=-6.8m)에 최적화된 3D 공간 음향
            _audioSource.spatialBlend = 0.88f;             // 88% 3D 입체 음향 (좌우 패닝 및 거리감 생생 유지)
            _audioSource.rolloffMode = AudioRolloffMode.Linear; // 선형 거리 감쇠
            _audioSource.minDistance = 8.0f;               // 카메라/플레이어 앞을 지날 때 최대 100% 볼륨 도달
            _audioSource.maxDistance = 50.0f;              // 멀리서부터 부르릉 다가오다가 코앞에서 극대화
            _audioSource.dopplerLevel = 0.8f;              // 자연스러운 질주 도플러 효과
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
                _audioSource.volume = 1.0f;
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

            // 플레이어 코앞/도로 중심(X=0 부근)을 지날 때 35% 확률로 "빵빵!" 경적 울림
            if (!_hasHonked && Mathf.Abs(transform.position.x) < 4.0f)
            {
                _hasHonked = true;
                if (UnityEngine.Random.value < 0.35f)
                {
                    var audioMgr = AudioManager.Instance;
                    AudioClip klaxonClip = audioMgr != null ? audioMgr.CarKlaxonClip : null;
                    if (klaxonClip != null && _audioSource != null && _audioSource.isActiveAndEnabled)
                    {
                        _audioSource.PlayOneShot(klaxonClip, 1.0f);
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
