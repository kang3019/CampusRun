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
    /// [단일 전역 사이렌 채널(Global Single Siren Channel)] 아키텍처를 사용하여
    /// 게임 전체에 오직 1개의 3D 오디오 소스만 존재하도록 물리적으로 강제합니다.
    /// 어떤 상황에서도 2대 이상의 사이렌이 겹치거나 중복 재생되는 일이 100% 원천 차단됩니다.
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

        // ===================================================================================
        // [핵심] 전역 단일 경찰차 사이렌 오디오 채널 (Global Single Siren Channel)
        // 개별 차량마다 AudioSource를 두지 않고, 게임 전체에 딱 1개의 AudioSource만 생성하여
        // 현재 가장 가까운 경찰차의 위치를 실시간 추적(3D 패닝 유지)합니다.
        // ===================================================================================
        private static readonly List<FastMotorcycle> s_activeCars = new List<FastMotorcycle>(16);
        private static FastMotorcycle s_targetCar = null;
        private static AudioSource s_globalSirenSource = null;
        private static int s_lastAudioFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_activeCars.Clear();
            s_targetCar = null;
            if (s_globalSirenSource != null)
            {
                try { s_globalSirenSource.Stop(); } catch { }
            }
            s_globalSirenSource = null;
            s_lastAudioFrame = -1;
        }

        // 런타임 상태 변수 (GC 할당 방지)
        private float _direction = 1f;
        private float _currentSpeed = 30f;
        private bool _isActive = false;
        private Action<FastMotorcycle> _onDespawnCallback;

        private void OnEnable()
        {
            GameEvents.OnGameRestarted += HandleGameRestarted;
        }

        private void OnDisable()
        {
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            UnregisterActiveCar();
        }

        private static void HandleGameRestarted()
        {
            s_activeCars.Clear();
            s_targetCar = null;
            s_lastAudioFrame = -1;

            if (s_globalSirenSource != null && s_globalSirenSource.isPlaying)
            {
                s_globalSirenSource.Stop();
            }
        }

        /// <summary>
        /// 게임 전체에서 오직 단 1개만 존재하는 전역 사이렌 AudioSource를 생성 및 캐싱합니다.
        /// </summary>
        private static AudioSource GetOrCreateGlobalSiren()
        {
            if (s_globalSirenSource != null) return s_globalSirenSource;

            GameObject sirenObj = GameObject.Find("[Global_Police_Siren]");
            if (sirenObj == null)
            {
                sirenObj = new GameObject("[Global_Police_Siren]");
                UnityEngine.Object.DontDestroyOnLoad(sirenObj);
            }

            s_globalSirenSource = sirenObj.GetComponent<AudioSource>();
            if (s_globalSirenSource == null)
            {
                s_globalSirenSource = sirenObj.AddComponent<AudioSource>();
            }

            // 쿼터뷰 시점에 최적화된 3D 공간 음향 설정
            s_globalSirenSource.spatialBlend = 0.88f;             // 88% 3D 입체 음향
            s_globalSirenSource.rolloffMode = AudioRolloffMode.Linear; // 선형 거리 감쇠
            s_globalSirenSource.minDistance = 9.0f;               // 카메라/플레이어 앞을 지날 때 최대 100% 음량
            s_globalSirenSource.maxDistance = 60.0f;              // 멀리서부터 다가오는 입체감 유지
            s_globalSirenSource.dopplerLevel = 1.2f;              // 초고속 질주 도플러 효과
            s_globalSirenSource.playOnAwake = false;
            s_globalSirenSource.loop = true;

            // 사이렌 클립 로드
            var audioMgr = AudioManager.Instance;
            AudioClip policeClip = audioMgr != null ? audioMgr.PoliceCarClip : null;
            if (policeClip == null)
            {
                policeClip = Resources.Load<AudioClip>("SFX/policecar");
            }
            s_globalSirenSource.clip = policeClip;

            return s_globalSirenSource;
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

            // 오브젝트 활성화
            gameObject.SetActive(true);

            // 글로벌 단일 사이렌 채널 즉시 갱신
            s_lastAudioFrame = -1;
            UpdateGlobalSirenChannel();
        }

        private void Update()
        {
            if (!_isActive) return;

            // X축 방향으로 초고속 직선 이동 (GC Free)
            transform.position += new Vector3(_direction * _currentSpeed * Time.deltaTime, 0f, 0f);

            // [핵심] 전역 단일 사이렌 위치 동기화 및 타겟 추적 (1프레임당 단 1회 실행)
            UpdateGlobalSirenChannel();

            // 경계선 도달 시 풀에 안전하게 반환
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        /// <summary>
        /// 전체 게임에서 단 1개의 사이렌 AudioSource가 현재 가장 가까운 단 1대의 경찰차를 추적합니다.
        /// 물리적으로 AudioSource가 1개뿐이므로 2대 이상 겹쳐 울리는 현상이 100% 원천 차단됩니다.
        /// </summary>
        private static void UpdateGlobalSirenChannel()
        {
            if (Time.frameCount == s_lastAudioFrame) return;
            s_lastAudioFrame = Time.frameCount;

            AudioSource siren = GetOrCreateGlobalSiren();
            if (siren == null) return;

            // 1. 활성 경찰차가 없으면 즉시 소리 정지
            if (s_activeCars.Count == 0)
            {
                s_targetCar = null;
                if (siren.isPlaying) siren.Stop();
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

            // 2. 현재 추적 중인 경찰차가 여전히 유효한지 확인
            bool needNewTarget = false;
            if (s_targetCar == null || !s_targetCar._isActive || !s_activeCars.Contains(s_targetCar))
            {
                needNewTarget = true;
            }
            else
            {
                // 현재 경찰차가 이미 플레이어 앞을 완전히 지나쳐 멀어지고 있는가?
                // 플레이어와의 거리가 10m 이상 멀어졌고, 더 가까운 새 경찰차가 있다면 교체
                float currentDistSqr = (s_targetCar.transform.position - targetPos).sqrMagnitude;
                if (currentDistSqr > 64.0f) // 8m 이상 멀어진 상태
                {
                    for (int i = 0; i < s_activeCars.Count; i++)
                    {
                        FastMotorcycle car = s_activeCars[i];
                        if (car == null || !car._isActive || car == s_targetCar) continue;

                        float distSqr = (car.transform.position - targetPos).sqrMagnitude;
                        // 현재 차보다 8m(64 sqr) 이상 더 가까운 차가 있으면 인계
                        if (distSqr + 64.0f < currentDistSqr)
                        {
                            s_targetCar = car;
                            break;
                        }
                    }
                }
            }

            // 3. 새 타겟 선정 (기존 타겟이 사라졌을 때)
            if (needNewTarget)
            {
                FastMotorcycle closestCar = null;
                float minSqrDist = float.MaxValue;

                for (int i = 0; i < s_activeCars.Count; i++)
                {
                    FastMotorcycle car = s_activeCars[i];
                    if (car == null || !car._isActive) continue;

                    float sqrDist = (car.transform.position - targetPos).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        closestCar = car;
                    }
                }

                s_targetCar = closestCar;
            }

            // 4. 단 1개의 전역 사이렌을 타겟 경찰차의 위치로 이동 및 재생
            if (s_targetCar != null)
            {
                siren.transform.position = s_targetCar.transform.position;
                if (!siren.isPlaying && siren.clip != null)
                {
                    siren.volume = 1.0f;
                    siren.mute = false;
                    siren.Play();
                }
            }
            else
            {
                if (siren.isPlaying) siren.Stop();
            }
        }

        public void Despawn()
        {
            if (!_isActive) return;

            UnregisterActiveCar();

            _isActive = false;
            gameObject.SetActive(false);
            _onDespawnCallback?.Invoke(this);

            // 타겟 차량이 사라졌으면 즉시 다음 활성 차량으로 전역 사이렌 인계
            s_lastAudioFrame = -1;
            UpdateGlobalSirenChannel();
        }

        private void UnregisterActiveCar()
        {
            s_activeCars.Remove(this);
            if (s_targetCar == this)
            {
                s_targetCar = null;
            }
        }
    }
}
