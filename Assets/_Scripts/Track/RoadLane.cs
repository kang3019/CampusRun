using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Obstacles;
using CampusRun.Audio;

namespace CampusRun.Track
{
    /// <summary>
    /// 1단계 도로 레인입니다. 셔틀버스나 승용차가 일정한 방향과 속도로 횡이동하며 지나갑니다.
    /// 차량 스폰 시 ObjectPool을 사용하여 런타임 메모리 할당을 방지합니다.
    /// </summary>
    public class RoadLane : BaseLane
    {
        [Header("--- 차량 스폰 설정 ---")]
        [Tooltip("스폰할 단일 기본 차량 프리팹 (하위 호환)")]
        [SerializeField] private MovingVehicle _vehiclePrefab;

        [Tooltip("도로에 등장할 수 있는 다양한 차량 프리팹 목록 (셔틀버스, 승용차, 택시, 트럭 등)")]
        [SerializeField] private MovingVehicle[] _vehiclePrefabs;

        [Tooltip("차량 이동 방향 (+1: 왼쪽->오른쪽, -1: 오른쪽->왼쪽, 0: 랜덤)")]
        [SerializeField] private float _fixedDirection = 0f;

        [Tooltip("차량 속도 범위 (최소~최대) - 빠르게 시원하게 지나가는 버스 속도")]
        [SerializeField] private float _minSpeed = 7.5f;
        [SerializeField] private float _maxSpeed = 10.5f;

        [Tooltip("차량 통과 후 주어지는 충분한 안전 통과 시간(Clear Window) 범위(초)")]
        [SerializeField] private float _minClearWindow = 4.2f;
        [SerializeField] private float _maxClearWindow = 6.8f;

        [Tooltip("스폰 시작 X축 좌표 (도로 폭 60m 기준 ±26.0m)")]
        [SerializeField] private float _spawnBoundaryX = 26.0f;

        [Header("--- 일자 페인트 패턴 설정 ---")]
        [Tooltip("도로에 등장할 수 있는 다양한 일자 점선 패턴 그룹 목록 (통일된 일자 페인트)")]
        [SerializeField] private GameObject[] _dashPatternGroups;

        [Tooltip("일반 도로 기본 일자 점선 그룹 (하위 호환용)")]
        [SerializeField] private GameObject _standardDashesGroup;

        [Tooltip("구버전 횡단보도 그룹 (영구 비활성화)")]
        [SerializeField] private GameObject _crosswalkGroup;

        // 런타임 상태 변수
        private float _currentDirection = 1f;
        private float _currentSpeed = 4.2f;
        private float _currentMinClearWindow = 4.2f;
        private float _currentMaxClearWindow = 6.8f;
        private MovingVehicle _currentLaneVehiclePrefab;
        private Coroutine _spawnRoutine;
        private Transform[] _dashChildren;
        private bool _hasNextRoadLane = false;

        private struct ActiveVehicleInfo
        {
            public MovingVehicle Vehicle;
            public IObjectPool<MovingVehicle> Pool;
        }

        private readonly List<ActiveVehicleInfo> _activeVehicles = new List<ActiveVehicleInfo>();

        // [핵심!] 차종별 전역 공유 오브젝트 풀 (풀 중복 생성 방지 및 메모리 최적화)
        private static readonly Dictionary<MovingVehicle, IObjectPool<MovingVehicle>> _sharedVehiclePools
            = new Dictionary<MovingVehicle, IObjectPool<MovingVehicle>>();

        // [핵심!] RoadLane의 Transform 스케일 (20, 0.2, 1) 왜곡을 차량이 상속받지 않도록 독립 컨테이너 사용
        private static Transform _vehicleRootContainer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearSharedPools();
        }

        /// <summary>
        /// 씬 리로드 또는 게임 재시작 시 정적 풀에 남은 파괴된 오브젝트들을 깨끗이 비웁니다.
        /// </summary>
        public static void ClearSharedPools()
        {
            _sharedVehiclePools.Clear();
            _vehicleRootContainer = null;
        }

        private static Transform GetOrCreateContainer()
        {
            if (_vehicleRootContainer == null)
            {
                GameObject container = GameObject.Find("[Vehicle_Pool_Container]");
                if (container == null)
                {
                    container = new GameObject("[Vehicle_Pool_Container]");
                    container.transform.position = Vector3.zero;
                    container.transform.rotation = Quaternion.identity;
                    container.transform.localScale = Vector3.one;
                }
                _vehicleRootContainer = container.transform;
            }
            return _vehicleRootContainer;
        }

        private static IObjectPool<MovingVehicle> GetOrCreatePool(MovingVehicle prefab)
        {
            if (prefab == null) return null;

            if (!_sharedVehiclePools.TryGetValue(prefab, out IObjectPool<MovingVehicle> pool) || pool == null)
            {
                Transform container = GetOrCreateContainer();
                pool = new ObjectPool<MovingVehicle>(
                    createFunc: () =>
                    {
                        if (container == null) container = GetOrCreateContainer();
                        MovingVehicle vehicle = Instantiate(prefab, container);
                        vehicle.gameObject.SetActive(false);
                        return vehicle;
                    },
                    actionOnGet: (vehicle) => { },
                    actionOnRelease: (vehicle) => { if (vehicle != null) vehicle.gameObject.SetActive(false); },
                    actionOnDestroy: (vehicle) => { if (vehicle != null) Destroy(vehicle.gameObject); },
                    collectionCheck: false,
                    defaultCapacity: 5,
                    maxSize: 20
                );
                _sharedVehiclePools[prefab] = pool;
            }

            return pool;
        }

        private void Awake()
        {
            _isSafeLane = false;
            CacheDashChildren();
        }

        private void CacheDashChildren()
        {
            if (_standardDashesGroup == null && _dashPatternGroups != null && _dashPatternGroups.Length > 0)
            {
                _standardDashesGroup = _dashPatternGroups[0];
            }

            if (_standardDashesGroup != null && _dashChildren == null)
            {
                int count = _standardDashesGroup.transform.childCount;
                _dashChildren = new Transform[count];
                for (int i = 0; i < count; i++)
                {
                    _dashChildren[i] = _standardDashesGroup.transform.GetChild(i);
                }
            }
        }

        private void OnEnable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted += ClearSharedPools;
        }

        private void OnDisable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted -= ClearSharedPools;
        }

        /// <summary>
        /// 레인 매니저에서 방향을 번갈아(교차) 지정할 때 호출할 수 있는 설정 메서드입니다.
        /// </summary>
        public void SetDesiredDirection(float direction)
        {
            _fixedDirection = Mathf.Sign(direction);
        }

        /// <summary>
        /// 연속된 차도(2차선 이상)에서 바로 다음 행(Z+1)도 차도인지 설정합니다.
        /// true일 때만 현재 차도와 다음 차도 사이의 경계선(localZ = +0.5f)에 일정한 흰색 점선을 표시합니다.
        /// </summary>
        public void SetHasNextRoadLane(bool hasNextRoadLane)
        {
            _hasNextRoadLane = hasNextRoadLane;
        }

        protected override void OnLaneSpawned()
        {
            base.OnLaneSpawned();

            // 1. 방향 결정
            if (_fixedDirection == 0f)
            {
                _currentDirection = (Random.value > 0.5f) ? 1f : -1f;
            }
            else
            {
                _currentDirection = Mathf.Sign(_fixedDirection);
            }

            // 1-1. 횡단보도는 완전히 비활성화 (사선/잔선 횡단보도 절대 미출현)
            if (_crosswalkGroup != null)
            {
                _crosswalkGroup.SetActive(false);
            }

            // 1-2. 길건너 친구들 원작 스타일:
            // 제각각이던 변형 패턴을 모두 끄고, 연속된 차도(2차선 이상) 사이의 경계선(localZ = +0.5f)에만
            // 일정한 길이(1.6m)와 간격(3.6m)의 단일 흰색 점선(_standardDashesGroup)을 표시합니다.
            // (1차선 단독 도로나 연속 차도의 마지막 행에는 흰색 선을 표시하지 않습니다.)
            if (_dashPatternGroups != null)
            {
                for (int i = 0; i < _dashPatternGroups.Length; i++)
                {
                    if (_dashPatternGroups[i] != null)
                    {
                        _dashPatternGroups[i].SetActive(false);
                    }
                }
            }

            CacheDashChildren();

            if (_standardDashesGroup != null)
            {
                if (_hasNextRoadLane)
                {
                    _standardDashesGroup.SetActive(true);
                    // X축 랜덤 시프트 없이 정중앙(0f) 고정 정렬, Z축은 현재 차도와 다음 차도 사이 경계(+0.5f)에 배치
                    _standardDashesGroup.transform.localPosition = new Vector3(0f, 0f, 0.5f);

                    if (_dashChildren != null)
                    {
                        for (int i = 0; i < _dashChildren.Length; i++)
                        {
                            if (_dashChildren[i] != null)
                            {
                                _dashChildren[i].gameObject.SetActive(true);
                            }
                        }
                    }
                }
                else
                {
                    _standardDashesGroup.SetActive(false);
                }
            }

            // 2. 이 레인에서 달릴 차량 프리팹 선택 (다양한 차종 중 랜덤)
            SelectLaneVehicle();

            // 3. 차종에 맞는 고유 속도 및 안전 통과 시간(Clear Window) 배정
            TuneVehicleSpeedAndWindow();

            // 4. 차량 스폰 루틴 시작
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(VehicleSpawnRoutine());
        }

        private void SelectLaneVehicle()
        {
            if (_vehiclePrefabs != null && _vehiclePrefabs.Length > 0)
            {
                int randomIndex = Random.Range(0, _vehiclePrefabs.Length);
                _currentLaneVehiclePrefab = _vehiclePrefabs[randomIndex];
            }
            else
            {
                _currentLaneVehiclePrefab = _vehiclePrefab;
            }
        }

        private void TuneVehicleSpeedAndWindow()
        {
            if (_currentLaneVehiclePrefab == null)
            {
                _currentSpeed = Random.Range(_minSpeed, _maxSpeed);
                _currentMinClearWindow = _minClearWindow;
                _currentMaxClearWindow = _maxClearWindow;
                return;
            }

            string prefabName = _currentLaneVehiclePrefab.name;

            if (prefabName.Contains("Taxi"))
            {
                // 캠퍼스 총알 택시: 1교시 지각생을 태우고 쏜살같이 질주 (속도 11.0~13.5m/s, 창문 2.8~4.2s)
                _currentSpeed = Random.Range(11.0f, 13.5f);
                _currentMinClearWindow = 2.8f;
                _currentMaxClearWindow = 4.2f;
            }
            else if (prefabName.Contains("Truck"))
            {
                // 택배/생협 1톤 탑차: 묵직하고 느긋하게 주행 (속도 6.8~8.8m/s, 창문 3.4~4.8s)
                _currentSpeed = Random.Range(6.8f, 8.8f);
                _currentMinClearWindow = 3.4f;
                _currentMaxClearWindow = 4.8f;
            }
            else if (prefabName.Contains("Sedan") || prefabName.Contains("Car"))
            {
                // 학생/교직원 세단 승용차: 경쾌하고 표준적인 주행 (속도 8.8~11.2m/s, 창문 3.0~4.5s)
                _currentSpeed = Random.Range(8.8f, 11.2f);
                _currentMinClearWindow = 3.0f;
                _currentMaxClearWindow = 4.5f;
            }
            else
            {
                // 대형 셔틀버스: 차체가 길고 묵직함 (속도 7.5~9.5m/s, 창문 3.5~5.0s)
                _currentSpeed = Random.Range(_minSpeed, _maxSpeed);
                _currentMinClearWindow = _minClearWindow;
                _currentMaxClearWindow = _maxClearWindow;
            }
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();
            _hasNextRoadLane = false;

            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            // 활성화되어 있던 모든 차량을 원래의 풀로 안전하게 회수
            for (int i = _activeVehicles.Count - 1; i >= 0; i--)
            {
                ActiveVehicleInfo info = _activeVehicles[i];
                if (info.Vehicle != null)
                {
                    if (info.Vehicle.gameObject.activeSelf)
                    {
                        info.Vehicle.Despawn();
                    }
                }
            }
            _activeVehicles.Clear();
        }

        private IEnumerator VehicleSpawnRoutine()
        {
            // 1. 레인 생성 직후 초기 시차 분산:
            // 인접한 도로끼리 동시에 차가 튀어나와 통행을 막지 않도록 Z 인덱스 기반 시차 부여 (0.3초 ~ 2.2초)
            float initialPhaseDelay = ((Mathf.Abs(LaneZIndex) * 1.5f) % 1.8f) + Random.Range(0.2f, 0.6f);
            yield return new WaitForSeconds(initialPhaseDelay);

            while (true)
            {
                // 각각의 차량 1대가 정해진 궤적을 지나감
                SpawnVehicle();

                // 차종별 맞춤형 안전 통과 시간(Clear Window) 대기
                float clearWindow = Random.Range(_currentMinClearWindow, _currentMaxClearWindow);
                yield return new WaitForSeconds(clearWindow);
            }
        }

        private void SpawnVehicle()
        {
            if (_currentLaneVehiclePrefab == null) return;

            IObjectPool<MovingVehicle> pool = GetOrCreatePool(_currentLaneVehiclePrefab);
            if (pool == null) return;

            MovingVehicle vehicle = null;
            try
            {
                vehicle = pool.Get();
            }
            catch
            {
                // 풀 내부 객체가 파괴된 경우 풀을 재생성하여 안전 획득
                _sharedVehiclePools.Remove(_currentLaneVehiclePrefab);
                pool = GetOrCreatePool(_currentLaneVehiclePrefab);
                if (pool != null) vehicle = pool.Get();
            }

            // 혹시라도 꺼낸 인스턴스가 파괴된 객체인 경우 (MissingReference 방지)
            if (vehicle == null)
            {
                Transform container = GetOrCreateContainer();
                vehicle = Instantiate(_currentLaneVehiclePrefab, container);
            }

            ActiveVehicleInfo info = new ActiveVehicleInfo
            {
                Vehicle = vehicle,
                Pool = pool
            };
            _activeVehicles.Add(info);

            // 스폰 위치 계산 (진행 방향의 반대쪽 끝 X=±14.5m 에서 출발, 도로 위 Y=0.1f 착지)
            float startX = (_currentDirection > 0) ? -_spawnBoundaryX : _spawnBoundaryX;
            vehicle.transform.position = new Vector3(startX, 0.1f, LaneZIndex);
            vehicle.transform.localScale = Vector3.one;

            // 차량 초기화 (반대편 끝 도착 시 풀 반환 콜백 연결)
            vehicle.Initialize(_currentDirection, _currentSpeed, (v) =>
            {
                for (int i = _activeVehicles.Count - 1; i >= 0; i--)
                {
                    if (_activeVehicles[i].Vehicle == v)
                    {
                        _activeVehicles.RemoveAt(i);
                        break;
                    }
                }

                if (v != null && pool != null)
                {
                    pool.Release(v);
                }
            });
        }
    }
}
