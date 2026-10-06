using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Obstacles;

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

        [Tooltip("스폰 시작 X축 좌표 (도로 폭 20m 기준 ±14.5m)")]
        [SerializeField] private float _spawnBoundaryX = 14.5f;

        [Header("--- 횡단보도 및 도로 표식 설정 ---")]
        [Tooltip("횡단보도 장식 그룹 (지브라 스트라이프 + 정지선)")]
        [SerializeField] private GameObject _crosswalkGroup;

        [Tooltip("일반 도로 점선 차선 그룹")]
        [SerializeField] private GameObject _standardDashesGroup;

        [Tooltip("이 도로에 횡단보도가 등장할 확률 (기본 0.3 = 30% 확률)")]
        [Range(0f, 1f)]
        [SerializeField] private float _crosswalkChance = 0.3f;

        // 런타임 상태 변수
        private float _currentDirection = 1f;
        private float _currentSpeed = 4.2f;
        private float _currentMinClearWindow = 4.2f;
        private float _currentMaxClearWindow = 6.8f;
        private MovingVehicle _currentLaneVehiclePrefab;
        private Coroutine _spawnRoutine;

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

            if (!_sharedVehiclePools.TryGetValue(prefab, out IObjectPool<MovingVehicle> pool))
            {
                Transform container = GetOrCreateContainer();
                pool = new ObjectPool<MovingVehicle>(
                    createFunc: () =>
                    {
                        MovingVehicle vehicle = Instantiate(prefab, container);
                        vehicle.gameObject.SetActive(false);
                        return vehicle;
                    },
                    actionOnGet: (vehicle) => { },
                    actionOnRelease: (vehicle) => { if (vehicle != null) vehicle.gameObject.SetActive(false); },
                    actionOnDestroy: (vehicle) => { if (vehicle != null) Destroy(vehicle.gameObject); },
                    collectionCheck: true,
                    defaultCapacity: 3,
                    maxSize: 10
                );
                _sharedVehiclePools[prefab] = pool;
            }

            return pool;
        }

        private void Awake()
        {
            _isSafeLane = false;
        }

        private void OnEnable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted += HandleGameRestarted;
        }

        private void OnDisable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted -= HandleGameRestarted;
        }

        private static void HandleGameRestarted()
        {
            _sharedVehiclePools.Clear();
            _vehicleRootContainer = null;
        }

        /// <summary>
        /// 레인 매니저에서 방향을 번갈아(교차) 지정할 때 호출할 수 있는 설정 메서드입니다.
        /// </summary>
        public void SetDesiredDirection(float direction)
        {
            _fixedDirection = Mathf.Sign(direction);
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

            // 1-1. 횡단보도 확률 표시 (횡단보도 활성화 시 다양한 위치와 방향으로 스폰)
            bool isCrosswalk = Random.value < _crosswalkChance;
            if (_crosswalkGroup != null)
            {
                _crosswalkGroup.SetActive(isCrosswalk);
                if (isCrosswalk)
                {
                    RandomizeCrosswalkVariation();
                }
            }
            if (_standardDashesGroup != null) _standardDashesGroup.SetActive(!isCrosswalk);

            // 2. 이 레인에서 달릴 차량 프리팹 선택 (다양한 차종 중 랜덤)
            SelectLaneVehicle();

            // 3. 차종에 맞는 고유 속도 및 안전 통과 시간(Clear Window) 배정
            TuneVehicleSpeedAndWindow();

            // 4. 차량 스폰 루틴 시작
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(VehicleSpawnRoutine());
        }

        /// <summary>
        /// 횡단보도가 스폰될 때 위치(X축 오프셋)와 방향/스타일 변형을 다양화합니다.
        /// </summary>
        private void RandomizeCrosswalkVariation()
        {
            if (_crosswalkGroup == null) return;

            // 1. 위치(X축 좌/우/중앙) 다양화: 중앙, 약간 좌측, 약간 우측, 좌측 끝, 우측 끝
            float[] possibleOffsetsX = new float[] { 0f, -2.0f, 2.0f, -3.5f, 3.5f };
            float chosenOffsetX = possibleOffsetsX[Random.Range(0, possibleOffsetsX.Length)];
            float invX = 1f / 20f;
            _crosswalkGroup.transform.localPosition = new Vector3(chosenOffsetX * invX, 0f, 0f);

            // 2. 여러 방향/스타일 변형(정방향, 좌사선, 우사선, 듀얼 통로) 중 1개 선택 활성화
            int childCount = _crosswalkGroup.transform.childCount;
            if (childCount > 0)
            {
                int chosenStyleIndex = Random.Range(0, childCount);
                for (int i = 0; i < childCount; i++)
                {
                    _crosswalkGroup.transform.GetChild(i).gameObject.SetActive(i == chosenStyleIndex);
                }
            }
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
                // 캠퍼스 총알 택시: 1교시 지각생을 태우고 쏜살같이 질주 (속도 11.0~13.5m/s, 창문 3.5~5.0s)
                _currentSpeed = Random.Range(11.0f, 13.5f);
                _currentMinClearWindow = 3.5f;
                _currentMaxClearWindow = 5.0f;
            }
            else if (prefabName.Contains("Truck"))
            {
                // 택배/생협 1톤 탑차: 묵직하고 느긋하게 주행 (속도 6.8~8.8m/s, 창문 4.2~6.0s)
                _currentSpeed = Random.Range(6.8f, 8.8f);
                _currentMinClearWindow = 4.2f;
                _currentMaxClearWindow = 6.0f;
            }
            else if (prefabName.Contains("Sedan") || prefabName.Contains("Car"))
            {
                // 학생/교직원 세단 승용차: 경쾌하고 표준적인 주행 (속도 8.8~11.2m/s, 창문 3.8~5.5s)
                _currentSpeed = Random.Range(8.8f, 11.2f);
                _currentMinClearWindow = 3.8f;
                _currentMaxClearWindow = 5.5f;
            }
            else
            {
                // 대형 셔틀버스: 차체가 길고 묵직함 (속도 7.5~9.5m/s, 창문 4.5~6.5s)
                _currentSpeed = Random.Range(_minSpeed, _maxSpeed);
                _currentMinClearWindow = _minClearWindow;
                _currentMaxClearWindow = _maxClearWindow;
            }
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();

            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            // 활성화되어 있던 모든 차량을 원래의 풀로 안전하게 회수
            for (int i = _activeVehicles.Count - 1; i >= 0; i--)
            {
                ActiveVehicleInfo info = _activeVehicles[i];
                if (info.Vehicle != null && info.Pool != null)
                {
                    info.Pool.Release(info.Vehicle);
                }
            }
            _activeVehicles.Clear();
        }

        private IEnumerator VehicleSpawnRoutine()
        {
            // 1. 레인 생성 직후 초기 시차 분산:
            // 인접한 도로끼리 동시에 차가 튀어나와 통행을 막지 않도록 Z 인덱스 기반 시차 부여
            float initialPhaseDelay = ((Mathf.Abs(LaneZIndex) * 2.3f) % 3.5f) + Random.Range(0.6f, 2.0f);
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

            MovingVehicle vehicle = pool.Get();
            if (vehicle == null) return;

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

                pool.Release(v);
            });
        }
    }
}
