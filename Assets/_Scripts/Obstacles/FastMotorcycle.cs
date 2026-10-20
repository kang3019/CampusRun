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
        [Tooltip("전체 게임에서 플레이어와 가장 가까운 단 1대의 경찰차만 사이렌을 재생하고, 2대 이상 절대 동시에 울리지 않도록 강제합니다.")]
        [SerializeField] private bool _enableExclusiveSingleCarAudio = true;

        [Tooltip("현재 사이렌을 울리는 경찰차가 다른 경찰차로 전환되기 위해 필요한 최소 거리 차이 (지터링/혼선 방지용 히스테리시스, 단위: m)")]
        [SerializeField] private float _switchHysteresisDistance = 3.0f;

        // 전역 단일 독점 경찰차 오디오 관리 (GC 할당 0)
        private static readonly List<FastMotorcycle> s_activeCars = new List<FastMotorcycle>(16);
        private static FastMotorcycle s_soloAudioCar = null;
        private static int s_lastAudioFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_activeCars.Clear();
            s_soloAudioCar = null;
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

            if (_enableExclusiveSingleCarAudio)
            {
                s_lastAudioFrame = -1;
                UpdateSoloAudioSelection();
            }
        }

        private static void HandleGameRestarted()
        {
            s_activeCars.Clear();
            s_soloAudioCar = null;
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

            // 3D 경찰차 사이렌 사운드 설정 및 스폰 직후 즉시 단일 독점 상태 계산
            Play3DPoliceSiren();

            if (_enableExclusiveSingleCarAudio)
            {
                s_lastAudioFrame = -1;
                UpdateSoloAudioSelection();
            }
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

                // 스폰 시점에 이미 다른 독점 경찰차가 존재한다면 소리를 완전 차단(mute)한 상태로 대기
                bool isSolo = (!_enableExclusiveSingleCarAudio) || (s_soloAudioCar == null || s_soloAudioCar == this);
                _audioSource.mute = !isSolo;
                _audioSource.volume = isSolo ? 1.0f : 0f;
                _audioSource.Play();
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // X축 방향으로 초고속 직선 이동 (GC Free)
            transform.position += new Vector3(_direction * _currentSpeed * Time.deltaTime, 0f, 0f);

            // [핵심] 게임 전체에서 단 1대의 경찰차만 소리를 내도록 엄격히 독점 갱신
            if (_enableExclusiveSingleCarAudio)
            {
                UpdateSoloAudioSelection();
            }

            // 경계선 도달 시 풀에 안전하게 반환
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        /// <summary>
        /// 1프레임당 단 한 번만 실행되어 플레이어와 가장 가까운 단 1대의 경찰차를 독점(Solo) 지정하고,
        /// 그 외의 모든 경찰차는 즉시 음소거(mute = true, volume = 0)하여 2개 이상 동시에 울리지 않도록 차단합니다.
        /// </summary>
        private void UpdateSoloAudioSelection()
        {
            if (Time.frameCount == s_lastAudioFrame) return;
            s_lastAudioFrame = Time.frameCount;

            if (s_activeCars.Count == 0)
            {
                s_soloAudioCar = null;
                return;
            }

            // 플레이어 위치 가져오기 (GridPlayerController 캐싱 싱글톤 또는 메인 카메라)
            Vector3 targetPos = Vector3.zero;
            if (GridPlayerController.Instance != null)
            {
                targetPos = GridPlayerController.Instance.transform.position;
            }
            else if (Camera.main != null)
            {
                targetPos = Camera.main.transform.position;
            }

            // 1. 현재 독점 경찰차가 여전히 유효한지 확인하고 거리 계산
            FastMotorcycle bestCar = null;
            float currentSoloDist = float.MaxValue;

            if (s_soloAudioCar != null && s_soloAudioCar._isActive && s_activeCars.Contains(s_soloAudioCar))
            {
                float dx = s_soloAudioCar.transform.position.x - targetPos.x;
                float dz = s_soloAudioCar.transform.position.z - targetPos.z;
                currentSoloDist = Mathf.Sqrt(dx * dx + dz * dz);
                bestCar = s_soloAudioCar;
            }

            // 2. 다른 경찰차와의 거리 비교
            // 기존 독점 차량이 있는 경우, 새 차량이 최소 _switchHysteresisDistance(3m) 이상 더 가까워야만 독점권을 넘겨받음 (혼선/깜빡임 방지)
            float thresholdDist = (bestCar != null) ? (currentSoloDist - _switchHysteresisDistance) : float.MaxValue;

            for (int i = 0; i < s_activeCars.Count; i++)
            {
                FastMotorcycle car = s_activeCars[i];
                if (car == null || !car._isActive || car == bestCar) continue;

                float dx = car.transform.position.x - targetPos.x;
                float dz = car.transform.position.z - targetPos.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);

                if (dist < thresholdDist)
                {
                    thresholdDist = dist;
                    bestCar = car;
                }
            }

            s_soloAudioCar = bestCar;

            // 3. [철저한 단일 독점 강제]
            // 오직 s_soloAudioCar 단 1대만 mute = false, volume = 1.0f 로 재생
            // 나머지 모든 경찰차는 mute = true, volume = 0f 로 즉각 차단하여 2개 이상 절대 겹치지 않음
            for (int i = 0; i < s_activeCars.Count; i++)
            {
                FastMotorcycle car = s_activeCars[i];
                if (car == null || car._audioSource == null) continue;

                bool isSolo = (car == s_soloAudioCar);
                car._audioSource.mute = !isSolo;
                car._audioSource.volume = isSolo ? 1.0f : 0f;
            }
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

            // 독점 차량이 사라졌으므로 즉시 남아있는 가장 가까운 차에게 독점권 인계
            if (_enableExclusiveSingleCarAudio)
            {
                s_lastAudioFrame = -1;
                UpdateSoloAudioSelection();
            }
        }

        private void UnregisterActiveCar()
        {
            s_activeCars.Remove(this);
            if (s_soloAudioCar == this)
            {
                s_soloAudioCar = null;
            }
        }
    }
}
