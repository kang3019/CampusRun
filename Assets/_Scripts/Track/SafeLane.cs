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
        [SerializeField] private float _obstacleSpawnChance = 0.5f;

        [Tooltip("한 레인에 배치될 수 있는 최대 장애물 수")]
        [SerializeField] private int _maxObstaclesPerLane = 2;

        [Tooltip("장애물이 스폰될 수 있는 X축 그리드 범위 (최소~최대)")]
        [SerializeField] private int _minGridX = -3;
        [SerializeField] private int _maxGridX = 3;

        // 런타임 추적 및 풀링
        private IObjectPool<StationaryObstacle> _obstaclePool;
        private readonly List<StationaryObstacle> _activeObstacles = new List<StationaryObstacle>();
        private readonly List<int> _availableXPositions = new List<int>();

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

            // 시작 지점(Z <= 3)은 플레이어가 스폰되는 초기 안전 구간이므로 장애물 미배치
            if (LaneZIndex > 3 && _obstaclePrefab != null && _obstaclePool != null)
            {
                if (Random.value < _obstacleSpawnChance)
                {
                    SpawnObstacles();
                }
            }
        }

        private void SpawnObstacles()
        {
            // 사용 가능한 X 그리드 좌표 목록 수집 (GC 방지)
            _availableXPositions.Clear();
            for (int x = _minGridX; x <= _maxGridX; x++)
            {
                _availableXPositions.Add(x);
            }

            int spawnCount = Random.Range(1, Mathf.Min(_maxObstaclesPerLane + 1, _availableXPositions.Count));

            for (int i = 0; i < spawnCount; i++)
            {
                int randomIndex = Random.Range(0, _availableXPositions.Count);
                int chosenX = _availableXPositions[randomIndex];
                _availableXPositions.RemoveAt(randomIndex); // 동일 레인 내 중복 좌표 방지

                StationaryObstacle obstacle = _obstaclePool.Get();
                Vector3 spawnPosition = new Vector3(chosenX, 0.25f, LaneZIndex);

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
