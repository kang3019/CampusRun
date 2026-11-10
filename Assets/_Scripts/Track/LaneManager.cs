using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Core;

namespace CampusRun.Track
{
    /// <summary>
    /// 길건너 친구들의 핵심인 절차적 레인(행) 생성 및 오브젝트 풀링을 총괄하는 매니저입니다.
    /// 플레이어의 전진 이벤트(OnPlayerHopped)에 반응하여 동적으로 전방에 레인을 깔고 후방 레인을 회수합니다.
    /// </summary>
    public class LaneManager : MonoBehaviour
    {
        [Header("--- 레인 프리팹 설정 ---")]
        [Tooltip("안전 보도블록/잔디 레인 프리팹")]
        [SerializeField] private SafeLane _safeLanePrefab;

        [Tooltip("1단계 셔틀버스 도로 레인 프리팹")]
        [SerializeField] private RoadLane _roadLanePrefab;

        [Tooltip("200m 이후 등장하는 배달 오토바이 급습 레인 프리팹")]
        [SerializeField] private MotorcycleLane _motorcycleLanePrefab;

        [Tooltip("거대 물웅덩이(강물) 레인 프리팹")]
        [SerializeField] private WaterLane _waterLanePrefab;

        [Header("--- 맵 생성 밸런스 설정 ---")]
        [Tooltip("게임 시작 시 시작점 주변 안전 레인 수 (Z=-2 ~ Z=3)")]
        [SerializeField] private int _initialSafeLaneCount = 6;

        [Tooltip("플레이어 위치 기준 전방에 유지할 레인 거리")]
        [SerializeField] private int _forwardViewDistance = 16;

        [Tooltip("플레이어 위치 기준 후방에서 레인을 회수할 거리")]
        [SerializeField] private int _backwardCullDistance = 6;

        [Tooltip("연속으로 생성 가능한 최대 도로 레인 수 (난이도 조절)")]
        [SerializeField] private int _maxConsecutiveRoads = 3;

        [Tooltip("5줄 이상 대형 연속 차도(고속도로 패턴)가 등장할 확률 (기본 0.10 = 10%)")]
        [Range(0f, 1f)]
        [SerializeField] private float _highwaySpawnChance = 0.10f;

        [Tooltip("대형 연속 차도 패턴의 최소/최대 차선 수 (기본 5~7줄 랜덤)")]
        [SerializeField] private int _highwayMinLanes = 5;
        [SerializeField] private int _highwayMaxLanes = 7;

        [Header("--- 배달 오토바이 기믹 설정 ---")]
        [Tooltip("오토바이 레인이 등장하기 시작하는 최소 Z 좌표 (기본 8 = 80m 맛보기 출현)")]
        [SerializeField] private int _motorcycleMinZ = 8;

        [Tooltip("최소 Z 좌표 이후 도로 생성 시 오토바이 레인으로 대체될 확률 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _motorcycleSpawnChance = 0.35f;

        [Header("--- 거대 물웅덩이 기믹 설정 ---")]
        [Tooltip("물웅덩이 레인이 등장하기 시작하는 최소 Z 좌표 (기본 12 = 120m)")]
        [SerializeField] private int _waterLaneMinZ = 12;

        [Tooltip("물웅덩이 레인 출현 확률 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _waterLaneSpawnChance = 0.28f;

        // 런타임 추적 변수
        private int _currentMaxSpawnedZ = -3;
        private readonly List<BaseLane> _activeLanes = new List<BaseLane>();

        // 레인 오브젝트 풀
        private IObjectPool<SafeLane> _safeLanePool;
        private IObjectPool<RoadLane> _roadLanePool;
        private IObjectPool<MotorcycleLane> _motorcycleLanePool;
        private IObjectPool<WaterLane> _waterLanePool;

        private void Awake()
        {
            InitializePools();
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerHopped += HandlePlayerHopped;
            GameEvents.OnGameRestarted += ResetAllLanes;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerHopped -= HandlePlayerHopped;
            GameEvents.OnGameRestarted -= ResetAllLanes;
        }

        private void Start()
        {
            SpawnInitialLanes();
        }

        private void InitializePools()
        {
            if (_safeLanePrefab != null)
            {
                _safeLanePool = new ObjectPool<SafeLane>(
                    createFunc: () =>
                    {
                        SafeLane lane = Instantiate(_safeLanePrefab, transform);
                        lane.gameObject.SetActive(false);
                        return lane;
                    },
                    actionOnGet: (lane) => { },
                    actionOnRelease: (lane) => { if (lane != null) lane.Recycle(); },
                    actionOnDestroy: (lane) => { if (lane != null) Destroy(lane.gameObject); },
                    defaultCapacity: 20,
                    maxSize: 40
                );
                PrewarmPool(_safeLanePool, 18);
            }

            if (_roadLanePrefab != null)
            {
                _roadLanePool = new ObjectPool<RoadLane>(
                    createFunc: () =>
                    {
                        RoadLane lane = Instantiate(_roadLanePrefab, transform);
                        lane.gameObject.SetActive(false);
                        return lane;
                    },
                    actionOnGet: (lane) => { },
                    actionOnRelease: (lane) => { if (lane != null) lane.Recycle(); },
                    actionOnDestroy: (lane) => { if (lane != null) Destroy(lane.gameObject); },
                    defaultCapacity: 20,
                    maxSize: 60
                );
                PrewarmPool(_roadLanePool, 14);
            }

            if (_motorcycleLanePrefab != null)
            {
                _motorcycleLanePool = new ObjectPool<MotorcycleLane>(
                    createFunc: () =>
                    {
                        MotorcycleLane lane = Instantiate(_motorcycleLanePrefab, transform);
                        lane.gameObject.SetActive(false);
                        return lane;
                    },
                    actionOnGet: (lane) => { },
                    actionOnRelease: (lane) => { if (lane != null) lane.Recycle(); },
                    actionOnDestroy: (lane) => { if (lane != null) Destroy(lane.gameObject); },
                    defaultCapacity: 5,
                    maxSize: 15
                );
                PrewarmPool(_motorcycleLanePool, 4);
            }

            if (_waterLanePrefab != null)
            {
                _waterLanePool = new ObjectPool<WaterLane>(
                    createFunc: () =>
                    {
                        WaterLane lane = Instantiate(_waterLanePrefab, transform);
                        lane.gameObject.SetActive(false);
                        return lane;
                    },
                    actionOnGet: (lane) => { },
                    actionOnRelease: (lane) => { if (lane != null) lane.Recycle(); },
                    actionOnDestroy: (lane) => { if (lane != null) Destroy(lane.gameObject); },
                    defaultCapacity: 6,
                    maxSize: 18
                );
                PrewarmPool(_waterLanePool, 4);
            }
        }

        private static void PrewarmPool<T>(IObjectPool<T> pool, int count) where T : class
        {
            if (pool == null || count <= 0) return;
            T[] temp = new T[count];
            for (int i = 0; i < count; i++)
            {
                temp[i] = pool.Get();
            }
            for (int i = 0; i < count; i++)
            {
                if (temp[i] != null) pool.Release(temp[i]);
            }
        }

        private void SpawnInitialLanes()
        {
            // Z = -3 부터 시작하여 초기 안전 구간 생성
            for (int z = -3; z <= _initialSafeLaneCount; z++)
            {
                SpawnSafeLane(z);
            }
            _currentMaxSpawnedZ = _initialSafeLaneCount;

            // 전방 시야 거리만큼 초기 레인 추가 생성
            ExtendLanesUntil(_forwardViewDistance);
        }

        private void HandlePlayerHopped(int playerZ)
        {
            // 플레이어가 전진함에 따라 전방 레인 연장
            int targetZ = playerZ + _forwardViewDistance;
            ExtendLanesUntil(targetZ);

            // 뒤처진 오래된 레인 회수
            CullOldLanes(playerZ - _backwardCullDistance);
        }

        private void ExtendLanesUntil(int targetZ)
        {
            while (_currentMaxSpawnedZ < targetZ)
            {
                _currentMaxSpawnedZ++;
                SpawnNextLane(_currentMaxSpawnedZ);
            }
        }

        // 리듬감 있는 그룹 스폰 상태 변수
        private int _currentRoadGroupLeft = 1;    // 첫 도로는 무조건 1칸 단독 도로
        private int _currentWaterGroupLeft = 0;   // 남은 물웅덩이 수
        private int _currentSafeGroupLeft = 0;    // 남은 쉼터 수
        private float _lastRoadDirection = 1f;    // 직전 도로 방향 (+1: 오른쪽, -1: 왼쪽)
        private float _lastWaterDirection = 1f;   // 직전 물웅덩이 표류 방향 (+1: 오른쪽, -1: 왼쪽)

        private void SpawnNextLane(int zIndex)
        {
            // 1. 안전 쉼터 구간이어야 할 때
            if (_currentSafeGroupLeft > 0)
            {
                SpawnSafeLane(zIndex);
                _currentSafeGroupLeft--;

                // 쉼터 구간이 끝나면 진행도(zIndex)에 맞춰 다음 위험 구간(도로 or 거대 물웅덩이) 준비
                if (_currentSafeGroupLeft <= 0)
                {
                    // 120m(zIndex >= _waterLaneMinZ) 이후 일정 확률로 거대 물웅덩이 구간 스폰
                    if (zIndex >= _waterLaneMinZ && _waterLanePrefab != null && _waterLanePool != null &&
                        Random.value < _waterLaneSpawnChance)
                    {
                        _currentWaterGroupLeft = (zIndex < 40) ? 1 : Random.Range(1, 3);
                        _currentRoadGroupLeft = 0;
                    }
                    else
                    {
                        _currentWaterGroupLeft = 0;

                        // 10% 확률(_highwaySpawnChance)로 5줄 이상(5~7줄 랜덤) 대형 연속 차도 패턴 등장
                        if (Random.value < _highwaySpawnChance)
                        {
                            int minLanes = Mathf.Max(5, _highwayMinLanes);
                            int maxLanes = Mathf.Max(minLanes, _highwayMaxLanes);
                            _currentRoadGroupLeft = Random.Range(minLanes, maxLanes + 1);
                        }
                        else if (zIndex < 35)
                        {
                            // [초반/중반 구간]: 도로 1~2칸 (1차선 단독 도로와 2차선 연속 도로가 자연스럽게 교차 등장)
                            _currentRoadGroupLeft = Random.Range(1, 3);
                        }
                        else
                        {
                            // [심화 구간]: 도로 2~3칸 (인스펙터 _maxConsecutiveRoads 반영)
                            _currentRoadGroupLeft = Random.Range(2, Mathf.Max(3, _maxConsecutiveRoads + 1));
                        }
                    }
                }
                return;
            }

            // 2. 거대 물웅덩이 구간일 때
            if (_currentWaterGroupLeft > 0 && _waterLanePool != null)
            {
                SpawnWaterLane(zIndex);
                _currentWaterGroupLeft--;

                // 물웅덩이 묶음이 끝나면 다음 안전 쉼터 보장
                if (_currentWaterGroupLeft <= 0)
                {
                    _currentSafeGroupLeft = Random.Range(1, 3);
                }
                return;
            }

            // 3. 도로 구간일 때
            if (_currentRoadGroupLeft > 0 && _roadLanePool != null)
            {
                // 바로 다음 행(Z+1)에도 차도가 계속 이어지는 경우(2차선 이상 연속 차도)에만
                // 현재 차도와 다음 차도 사이의 경계선에 흰색 점선을 표시합니다.
                bool hasNextRoadLane = _currentRoadGroupLeft > 1;
                SpawnRoadLane(zIndex, hasNextRoadLane);
                _currentRoadGroupLeft--;

                // 도로 묶음이 끝나면 다음 안전 쉼터 준비
                if (_currentRoadGroupLeft <= 0)
                {
                    // 초반(Z < 30)에는 안전 쉼터를 2~3칸 보장하여 여유롭게 호흡 조절
                    _currentSafeGroupLeft = (zIndex < 30) ? Random.Range(2, 4) : Random.Range(1, 3);
                }
                return;
            }

            // 기본 안전 처리
            SpawnSafeLane(zIndex);
        }

        private void SpawnSafeLane(int zIndex)
        {
            if (_safeLanePool == null) return;

            SafeLane lane = _safeLanePool.Get();
            lane.Initialize(zIndex);
            _activeLanes.Add(lane);
        }

        private void SpawnRoadLane(int zIndex, bool hasNextRoadLane = false)
        {
            // 일정 Z 이후 확률적으로 오토바이(급습 차량) 레인 스폰
            if (_motorcycleLanePrefab != null && _motorcycleLanePool != null &&
                zIndex >= _motorcycleMinZ && Random.value < _motorcycleSpawnChance)
            {
                SpawnMotorcycleLane(zIndex, hasNextRoadLane);
                return;
            }

            if (_roadLanePool == null) return;

            // 인접한 도로는 방향을 반대로 교차하여 리듬감과 시각적 안정감 부여
            _lastRoadDirection = -_lastRoadDirection;

            RoadLane lane = _roadLanePool.Get();
            lane.SetDesiredDirection(_lastRoadDirection);
            lane.SetHasNextRoadLane(hasNextRoadLane);
            lane.Initialize(zIndex);
            _activeLanes.Add(lane);
        }

        private void SpawnMotorcycleLane(int zIndex, bool hasNextRoadLane = false)
        {
            if (_motorcycleLanePool == null) return;

            MotorcycleLane lane = _motorcycleLanePool.Get();
            lane.SetHasNextRoadLane(hasNextRoadLane);
            lane.Initialize(zIndex);
            _activeLanes.Add(lane);
        }

        private void SpawnWaterLane(int zIndex)
        {
            if (_waterLanePool == null) return;

            // 인접한 물웅덩이는 표류 방향을 교차하여 리듬감과 도전 요소 제공
            _lastWaterDirection = -_lastWaterDirection;

            WaterLane lane = _waterLanePool.Get();
            lane.SetDesiredDirection(_lastWaterDirection);
            lane.Initialize(zIndex);
            _activeLanes.Add(lane);
        }

        private void CullOldLanes(int minZ)
        {
            for (int i = _activeLanes.Count - 1; i >= 0; i--)
            {
                BaseLane lane = _activeLanes[i];
                if (lane.LaneZIndex < minZ)
                {
                    _activeLanes.RemoveAt(i);

                    if (lane is SafeLane safeLane && _safeLanePool != null)
                    {
                        _safeLanePool.Release(safeLane);
                    }
                    else if (lane is RoadLane roadLane && _roadLanePool != null)
                    {
                        _roadLanePool.Release(roadLane);
                    }
                    else if (lane is MotorcycleLane motorcycleLane && _motorcycleLanePool != null)
                    {
                        _motorcycleLanePool.Release(motorcycleLane);
                    }
                    else if (lane is WaterLane waterLane && _waterLanePool != null)
                    {
                        _waterLanePool.Release(waterLane);
                    }
                }
            }
        }

        private void ResetAllLanes()
        {
            // 활성화된 모든 레인 회수
            for (int i = _activeLanes.Count - 1; i >= 0; i--)
            {
                BaseLane lane = _activeLanes[i];
                if (lane is SafeLane safeLane && _safeLanePool != null)
                {
                    _safeLanePool.Release(safeLane);
                }
                else if (lane is RoadLane roadLane && _roadLanePool != null)
                {
                    _roadLanePool.Release(roadLane);
                }
                else if (lane is MotorcycleLane motorcycleLane && _motorcycleLanePool != null)
                {
                    _motorcycleLanePool.Release(motorcycleLane);
                }
                else if (lane is WaterLane waterLane && _waterLanePool != null)
                {
                    _waterLanePool.Release(waterLane);
                }
            }
            _activeLanes.Clear();

            _currentMaxSpawnedZ = -3;
            _currentRoadGroupLeft = 1;
            _currentWaterGroupLeft = 0;
            _currentSafeGroupLeft = 0;
            _lastRoadDirection = 1f;
            _lastWaterDirection = 1f;

            SpawnInitialLanes();
        }
    }
}
