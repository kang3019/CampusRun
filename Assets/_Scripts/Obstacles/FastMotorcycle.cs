using System;
using System.Collections.Generic;
using UnityEngine;
using CampusRun.Audio;
using CampusRun.Core;
using CampusRun.Player;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 원작 길건너 친구들의 '기차'에 해당하는 대학 캠퍼스 초고속 배달 오토바이(경찰차) 장애물입니다.
    /// 3D 공간 음향이 탑재되어 멀리서 다가올 때부터 코앞을 쏜살같이 지나갈 때까지 입체적으로 사이렌 소리가 변화합니다.
    /// 플레이어와 가장 가까운 경찰차만 사이렌 소리를 들려주어 사운드 난립을 방지하고 몰입감을 극대화합니다.
    /// </summary>
    [SelectionBase]
    public class FastMotorcycle : MonoBehaviour
    {
        [Header("--- 차량 식별 ---")]
        [Tooltip("사망 메시지에 표시될 차량 명칭 (예: 경찰차, 오토바이)")]
        [SerializeField] private string _vehicleDisplayName = "경찰차";

        public string VehicleDisplayName => _vehicleDisplayName;

        [Header("--- 이동 및 경계 설정 ---")]
        [Tooltip("기본 초고속 이동 속도 (m/s)")]
        [SerializeField] private float _rushSpeed = 30f;

        [Tooltip("레인 중심 기준 비활성화되는 X축 경계선 (도로 폭 60m 기준 ±30.0m)")]
        [SerializeField] private float _despawnBoundaryX = 30.0f;

        [Header("--- 오디오 우선순위 설정 ---")]
        [Tooltip("플레이어와 가장 가까운 1대의 경찰차만 사이렌 소리를 들려주고 다른 경찰차는 음소거합니다.")]
        [SerializeField] private bool _enableClosestCarAudioOnly = true;

        [Tooltip("사이렌 소리 전환(크로스페이드) 속도")]
        [SerializeField] private float _audioFadeSpeed = 8.0f;

        // 전역 경찰차 오디오 포커스 관리 (GC 할당 0)
        private static readonly List<FastMotorcycle> s_activeCars = new List<FastMotorcycle>(16);
        private static FastMotorcycle s_closestCar = null;
        private static int s_lastAudioFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_activeCars.Clear();
            s_closestCar = null;
            s_lastAudioFrame = -1;
        }

        // 런타임 상태 변수 (GC 할당 방지)
        private float _direction = 1f;
        private float _currentSpeed = 30f;
        private bool _isActive = false;
        private Action<FastMotorcycle> _onDespawnCallback;
        private AudioSource _audioSource;

        private void Awake()
        {
            SetupSpatialAudioSource();
        }

        private void OnEnable()
        {
            GameEvents.OnGameRestarted += HandleGameRestarted;
        }

        private void OnDisable()
        {
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            UnregisterActiveCar();

            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        private static void HandleGameRestarted()
        {
            s_activeCars.Clear();
            s_closestCar = null;
            s_lastAudioFrame = -1;
        }

        private void SetupSpatialAudioSource()
        {
            if (_audioSource != null) return;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            // 초고속 질주 및 쿼터뷰 시점에 최적화된 3D 공간 음향 설정
            _audioSource.spatialBlend = 0.88f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 9.0f;  // 카메라/플레이어 앞을 지날 때 최대 100% 음량 보장
            _audioSource.maxDistance = 60.0f; // 먼 거리에서부터 사이렌이 다가옴
            _audioSource.dopplerLevel = 1.2f; // 초고속 질주 도플러 효과 강조
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
        }

        /// <summary>
        /// 오브젝트 풀에서 꺼내질 때 오토바이의 진행 방향과 속도를 초기화합니다.
        /// </summary>
        public void Initialize(float direction, float speed, Action<FastMotorcycle> onDespawn)
        {
            _direction = Mathf.Sign(direction);
            _currentSpeed = speed > 0 ? speed : _rushSpeed;
            _onDespawnCallback = onDespawn;
            _isActive = true;

            // 활성 경찰차 목록 등록
            if (!s_activeCars.Contains(this))
            {
                s_activeCars.Add(this);
            }

            // 이동 방향에 맞춰 전면 회전
            Vector3 lookDir = (_direction > 0) ? Vector3.right : Vector3.left;
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            // [중요!] 먼저 오브젝트를 활성화해야 AudioSource.Play()가 안전하게 작동
            gameObject.SetActive(true);

            // 3D 경찰차 사이렌 사운드 재생
            Play3DPoliceSiren();
        }

        private void Play3DPoliceSiren()
        {
            if (_audioSource == null) return;

            var audioMgr = AudioManager.Instance;
            AudioClip policeClip = audioMgr != null ? audioMgr.PoliceCarClip : null;
            if (policeClip == null)
            {
                policeClip = Resources.Load<AudioClip>("SFX/policecar");
            }

            if (policeClip != null)
            {
                _audioSource.clip = policeClip;

                // 이미 다른 가장 가까운 경찰차가 울리고 있으면 0에서 시작하여 크로스페이드
                if (_enableClosestCarAudioOnly && s_closestCar != null && s_closestCar != this)
                {
                    _audioSource.volume = 0f;
                }
                else
                {
                    _audioSource.volume = 1.0f;
                }

                _audioSource.Play();
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // X축 방향으로 초고속 직선 이동 (GC Free)
            transform.position += new Vector3(_direction * _currentSpeed * Time.deltaTime, 0f, 0f);

            // 가장 가까운 1대의 경찰차만 오디오 포커스 갱신 및 볼륨 페이드
            if (_enableClosestCarAudioOnly)
            {
                UpdateClosestCarAudioFocus();
                UpdateAudioVolume();
            }

            // 경계선 도달 시 풀에 안전하게 반환
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        private static void UpdateClosestCarAudioFocus()
        {
            if (Time.frameCount == s_lastAudioFrame) return;
            s_lastAudioFrame = Time.frameCount;

            if (s_activeCars.Count == 0)
            {
                s_closestCar = null;
                return;
            }

            // 플레이어 위치 가져오기 (GridPlayerController 캐싱 싱글톤 또는 카메라)
            Vector3 targetPos = Vector3.zero;
            if (GridPlayerController.Instance != null)
            {
                targetPos = GridPlayerController.Instance.transform.position;
            }
            else if (Camera.main != null)
            {
                targetPos = Camera.main.transform.position;
            }

            FastMotorcycle bestCar = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < s_activeCars.Count; i++)
            {
                FastMotorcycle car = s_activeCars[i];
                if (car == null || !car._isActive) continue;

                // 2D 평면(X, Z) 거리 기준
                float dx = car.transform.position.x - targetPos.x;
                float dz = car.transform.position.z - targetPos.z;
                float sqrDist = dx * dx + dz * dz;

                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    bestCar = car;
                }
            }

            s_closestCar = bestCar;
        }

        private void UpdateAudioVolume()
        {
            if (_audioSource == null) return;

            bool isClosest = (this == s_closestCar);
            float targetVolume = isClosest ? 1.0f : 0.0f;

            // 팝 노이즈 없이 부드럽게 크로스페이드 전환
            _audioSource.volume = Mathf.MoveTowards(_audioSource.volume, targetVolume, Time.deltaTime * _audioFadeSpeed);
        }

        public void Despawn()
        {
            if (!_isActive) return;

            UnregisterActiveCar();

            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }

            _isActive = false;
            gameObject.SetActive(false);
            _onDespawnCallback?.Invoke(this);
        }

        private void UnregisterActiveCar()
        {
            s_activeCars.Remove(this);
            if (s_closestCar == this)
            {
                s_closestCar = null;
            }
        }
    }
}
