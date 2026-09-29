using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Obstacles;

namespace CampusRun.Track
{
    /// <summary>
    /// 플레이어가 잠시 서서 타이밍을 잴 수 있는 안전 보도블록/잔디 레인입니다.
    /// 1자 직진 런을 방지하고 좌우 우회를 유도하기 위해 일정 확률로 방치된 킥보드 등 고정 장애물이 배치됩니다.
    /// </summary>
    public class SafeLane : BaseLane
    {
        [Header("--- 고정 길막 장애물 설정 (방치된 킥보드) ---")]
        [Tooltip("스폰할 고정 장애물 프리팹 (StationaryObstacle 컴포넌트 필수)")]
        [SerializeField] private StationaryObstacle _obstaclePrefab;

        [Tooltip("이 안전 레인에 고정 장애물이 배치될 확률 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float _obstacleSpawnChance = 0.35f;

        [Tooltip("한 레인에 배치될 수 있는 최대 장애물 수 (통행로 보장을 위해 1개 권장)")]
        [SerializeField] private int _maxObstaclesPerLane = 1;

        [Tooltip("장애물이 스폰될 수 있는 X축 그리드 범위 (최소~최대)")]
        [SerializeField] private int _minGridX = -3;
        [SerializeField] private int _maxGridX = 3;

        [Tooltip("게임 시작 후 장애물이 전혀 나오지 않는 초기 튜토리얼 Z 거리")]
        [SerializeField] private int _safeStartZoneZ = 6;

        // 런타임 추적 및 풀링
        private IObjectPool<StationaryObstacle> _obstaclePool;
        private readonly List<StationaryObstacle> _activeObstacles = new List<StationaryObstacle>();
        private readonly List<int> _availableXPositions = new List<int>();

        // 연속 차단 방지를 위한 정적 직전 스폰 위치 추적
        private static int _lastSpawnedX = 999;

        private void Awake()
        {
            _isSafeLane = true;
            InitializePool();
        }

        private void InitializePool()
        {
            if (_obstaclePrefab == null) return;

            _obstaclePool = new ObjectPool<StationaryObstacle>(
                createFunc: () =>
                {
                    StationaryObstacle obstacle = Instantiate(_obstaclePrefab, transform);
                    obstacle.gameObject.SetActive(false);
                    return obstacle;
                },
                actionOnGet: (obstacle) =>
                {
                    _activeObstacles.Add(obstacle);
                },
                actionOnRelease: (obstacle) =>
                {
                    _activeObstacles.Remove(obstacle);
                    obstacle.gameObject.SetActive(false);
                },
                actionOnDestroy: (obstacle) =>
                {
                    if (obstacle != null) Destroy(obstacle.gameObject);
                },
                collectionCheck: true,
                defaultCapacity: 4,
                maxSize: 12
            );
        }

        protected override void OnLaneSpawned()
        {
            base.OnLaneSpawned();

            if (_obstaclePool == null)
            {
                InitializePool();
            }

            // 시작 지점(Z <= _safeStartZoneZ)은 조작 학습 구간이므로 무조건 100% 안전하게 비워둠
            if (LaneZIndex > _safeStartZoneZ && _obstaclePrefab != null && _obstaclePool != null)
            {
                if (Random.value < _obstacleSpawnChance)
                {
                    SpawnObstacles();
                }
            }
        }

        private void SpawnObstacles()
        {
            // 통행 가능성(Pathability) 보장:
            // 1. 후보 좌표 수집 (직전 레인에서 스폰된 X 좌표는 제외하여 일자 경로 차단 방지)
            _availableXPositions.Clear();
            for (int x = _minGridX; x <= _maxGridX; x++)
            {
                // 직전 레인과 동일한 X는 제외
                if (x == _lastSpawnedX) continue;
                _availableXPositions.Add(x);
            }

            if (_availableXPositions.Count == 0) return;

            int targetSpawnCount = Mathf.Clamp(_maxObstaclesPerLane, 1, 2);
            int previousChosenX = 999;

            for (int i = 0; i < targetSpawnCount; i++)
            {
                if (_availableXPositions.Count == 0) break;

                // 후보 중 랜덤 선택하되, 2개 이상일 경우 최소 3칸 이상 떨어지도록 필터링
                int chosenIndex = -1;
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    int candidateIndex = Random.Range(0, _availableXPositions.Count);
                    int candidateX = _availableXPositions[candidateIndex];

                    // 첫 번째 장애물이거나 이전 선택과 3칸 이상 떨어져 벽을 만들지 않을 때
                    if (previousChosenX == 999 || Mathf.Abs(candidateX - previousChosenX) >= 3)
                    {
                        chosenIndex = candidateIndex;
                        break;
                    }
                }

                if (chosenIndex == -1) break;

                int chosenX = _availableXPositions[chosenIndex];
                _availableXPositions.RemoveAt(chosenIndex);
                previousChosenX = chosenX;
                _lastSpawnedX = chosenX;

                StationaryObstacle obstacle = _obstaclePool.Get();
                Vector3 spawnPosition = new Vector3(chosenX, 0.16f, LaneZIndex);

                obstacle.Initialize(spawnPosition, (obs) =>
                {
                    if (gameObject.activeInHierarchy && _obstaclePool != null)
                    {
                        _obstaclePool.Release(obs);
                    }
                });
            }
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();

            // 활성화되어 있던 모든 고정 장애물 회수
            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                if (_activeObstacles[i] != null && _obstaclePool != null)
                {
                    _obstaclePool.Release(_activeObstacles[i]);
                }
            }
            _activeObstacles.Clear();
        }
    }
}
