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

        [Header("--- 맵 생성 밸런스 설정 ---")]
        [Tooltip("게임 시작 시 시작점 주변 안전 레인 수 (Z=-2 ~ Z=3)")]
        [SerializeField] private int _initialSafeLaneCount = 6;

        [Tooltip("플레이어 위치 기준 전방에 유지할 레인 거리")]
        [SerializeField] private int _forwardViewDistance = 25;

        [Tooltip("플레이어 위치 기준 후방에서 레인을 회수할 거리")]
        [SerializeField] private int _backwardCullDistance = 10;

        [Tooltip("연속으로 생성 가능한 최대 도로 레인 수 (난이도 조절)")]
        [SerializeField] private int _maxConsecutiveRoads = 3;

        // 런타임 추적 변수
        private int _currentMaxSpawnedZ = -3;
        private int _consecutiveRoadCount = 0;
        private readonly List<BaseLane> _activeLanes = new List<BaseLane>();

        // 레인 오브젝트 풀
        private IObjectPool<SafeLane> _safeLanePool;
        private IObjectPool<RoadLane> _roadLanePool;

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
                    createFunc: () => Instantiate(_safeLanePrefab, transform),
                    actionOnGet: (lane) => { },
                    actionOnRelease: (lane) => lane.Recycle(),
                    actionOnDestroy: (lane) => { if (lane != null) Destroy(lane.gameObject); },
                    defaultCapacity: 15,
                    maxSize: 40
                );
            }

            if (_roadLanePrefab != null)
            {
                _roadLanePool = new ObjectPool<RoadLane>(
                    createFunc: () => Instantiate(_roadLanePrefab, transform),
                    actionOnGet: (lane) => { },
                    actionOnRelease: (lane) => lane.Recycle(),
                    actionOnDestroy: (lane) => { if (lane != null) Destroy(lane.gameObject); },
                    defaultCapacity: 20,
                    maxSize: 60
                );
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
        private int _currentRoadGroupLeft = 1;   // 첫 도로는 무조건 1칸 단독 도로
        private int _currentSafeGroupLeft = 0;   // 남은 쉼터 수
        private float _lastRoadDirection = 1f;   // 직전 도로 방향 (+1: 오른쪽, -1: 왼쪽)

        private void SpawnNextLane(int zIndex)
        {
            // 1. 안전 쉼터 구간이어야 할 때
            if (_currentSafeGroupLeft > 0)
            {
                SpawnSafeLane(zIndex);
                _currentSafeGroupLeft--;

                // 쉼터 구간이 끝나면 진행도(zIndex)에 맞춰 다음 도로 묶음 준비
                if (_currentSafeGroupLeft <= 0)
                {
                    if (zIndex < 30)
                    {
                        // [초반 완벽 적응 구간]: 도로는 무조건 1칸 단독! (지나갈 수 있는 확실한 1차선 도로)
                        _currentRoadGroupLeft = 1;
                    }
                    else if (zIndex < 55)
                    {
                        // [중반 구간]: 도로 1~2칸
                        _currentRoadGroupLeft = Random.Range(1, 3);
                    }
                    else
                    {
                        // [심화 구간]: 도로 2~3칸
                        _currentRoadGroupLeft = Random.Range(2, 4);
                    }
                }
                return;
            }

            // 2. 도로 구간일 때
            if (_currentRoadGroupLeft > 0 && _roadLanePool != null)
            {
                SpawnRoadLane(zIndex);
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

        private void SpawnRoadLane(int zIndex)
        {
            if (_roadLanePool == null) return;

            // 인접한 도로는 방향을 반대로 교차하여 리듬감과 시각적 안정감 부여
            _lastRoadDirection = -_lastRoadDirection;

            RoadLane lane = _roadLanePool.Get();
            lane.SetDesiredDirection(_lastRoadDirection);
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
            }
            _activeLanes.Clear();

            _currentMaxSpawnedZ = -3;
            _consecutiveRoadCount = 0;
            _currentRoadGroupLeft = 1;
            _currentSafeGroupLeft = 0;

            SpawnInitialLanes();
        }
    }
}
