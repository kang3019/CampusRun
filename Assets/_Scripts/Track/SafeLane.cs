using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Obstacles;

namespace CampusRun.Track
{
    /// <summary>
    /// 플레이어가 잠시 서서 타이밍을 잴 수 있는 안전 보도블록 레인입니다.
    /// 한국 도심 보도블록 머티리얼과 연석/점자블록이 적용되어 있으며,
    /// 가로등이 맵 곳곳에 듬성듬성 분산 배치되고 킥보드/볼라드가 장애물로 등장합니다.
    /// </summary>
    public class SafeLane : BaseLane
    {
        [Header("--- 바닥 머티리얼 및 장식 ---")]
        [Tooltip("한국 도심 보도블록 머티리얼")]
        [SerializeField] private Material _sidewalkMaterial;

        [Tooltip("연석 (화강암 경계석 턱)")]
        [SerializeField] private GameObject _curbDecorations;

        [Tooltip("노란색 점자블록 그룹")]
        [SerializeField] private GameObject _brailleBlockGroup;

        [Tooltip("점자블록이 등장할 확률 (기본 0.45 = 약 2~3레인마다 1개 꼴로 자연스럽게 출현)")]
        [Range(0f, 1f)]
        [SerializeField] private float _brailleBlockChance = 0.45f;

        [Header("--- 가로등 분산 스폰 설정 ---")]
        [Tooltip("가로등 프리팹 (StreetLight)")]
        [SerializeField] private GameObject _streetLightPrefab;

        [Tooltip("가로등이 등장할 확률 (기본 0.12 = 약 8레인마다 1개)")]
        [Range(0f, 1f)]
        [SerializeField] private float _streetLightSpawnChance = 0.12f;

        [Header("--- 고정 길막 장애물 설정 (방치된 킥보드 / 볼라드) ---")]
        [Tooltip("스폰할 단일 기본 고정 장애물 프리팹 (하위 호환)")]
        [SerializeField] private StationaryObstacle _obstaclePrefab;

        [Tooltip("스폰 가능한 고정 장애물 프리팹 목록 (킥보드, 볼라드 등)")]
        [SerializeField] private StationaryObstacle[] _obstaclePrefabs;

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

        // 런타임 캐싱 및 풀링
        private MeshRenderer _meshRenderer;
        private static Transform _obstacleRootContainer;
        private static Transform _decorRootContainer;

        // 장애물 풀
        private static readonly Dictionary<StationaryObstacle, IObjectPool<StationaryObstacle>> _sharedObstaclePools
            = new Dictionary<StationaryObstacle, IObjectPool<StationaryObstacle>>();

        private struct SpawnedObstacleInfo
        {
            public StationaryObstacle Obstacle;
            public IObjectPool<StationaryObstacle> Pool;
        }
        private readonly List<SpawnedObstacleInfo> _activeObstacleInfos = new List<SpawnedObstacleInfo>();

        // 가로등 풀
        private static IObjectPool<GameObject> _sharedStreetLightPool;
        private GameObject _activeStreetLight;

        private readonly List<int> _availableXPositions = new List<int>();
        private static int _lastSpawnedX = 999;

        private static Transform GetOrCreateObstacleContainer()
        {
            if (_obstacleRootContainer == null)
            {
                GameObject container = GameObject.Find("[Obstacle_Pool_Container]");
                if (container == null)
                {
                    container = new GameObject("[Obstacle_Pool_Container]");
                    container.transform.position = Vector3.zero;
                    container.transform.rotation = Quaternion.identity;
                    container.transform.localScale = Vector3.one;
                }
                _obstacleRootContainer = container.transform;
            }
            return _obstacleRootContainer;
        }

        private static Transform GetOrCreateDecorContainer()
        {
            if (_decorRootContainer == null)
            {
                GameObject container = GameObject.Find("[Decor_Pool_Container]");
                if (container == null)
                {
                    container = new GameObject("[Decor_Pool_Container]");
                    container.transform.position = Vector3.zero;
                    container.transform.rotation = Quaternion.identity;
                    container.transform.localScale = Vector3.one;
                }
                _decorRootContainer = container.transform;
            }
            return _decorRootContainer;
        }

        private void Awake()
        {
            _isSafeLane = true;
            _meshRenderer = GetComponent<MeshRenderer>();
            InitializeStreetLightPool();
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
            try { _sharedStreetLightPool?.Clear(); } catch { }
            _sharedStreetLightPool = null;

            foreach (var kvp in _sharedObstaclePools)
            {
                try { kvp.Value?.Clear(); } catch { }
            }
            _sharedObstaclePools.Clear();

            _obstacleRootContainer = null;
            _decorRootContainer = null;
        }

        private void InitializeStreetLightPool()
        {
            if (_streetLightPrefab == null || _sharedStreetLightPool != null) return;

            Transform container = GetOrCreateDecorContainer();
            _sharedStreetLightPool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    GameObject obj = Instantiate(_streetLightPrefab, container);
                    obj.SetActive(false);
                    return obj;
                },
                actionOnGet: (obj) => { },
                actionOnRelease: (obj) => { if (obj != null) obj.SetActive(false); },
                actionOnDestroy: (obj) => { if (obj != null) Destroy(obj); },
                collectionCheck: true,
                defaultCapacity: 6,
                maxSize: 20
            );
        }

        private IObjectPool<StationaryObstacle> GetOrCreateObstaclePool(StationaryObstacle prefab)
        {
            if (prefab == null) return null;
            if (_sharedObstaclePools.TryGetValue(prefab, out var pool)) return pool;

            Transform container = GetOrCreateObstacleContainer();
            pool = new ObjectPool<StationaryObstacle>(
                createFunc: () =>
                {
                    StationaryObstacle obs = Instantiate(prefab, container);
                    obs.gameObject.SetActive(false);
                    return obs;
                },
                actionOnGet: (obs) => { },
                actionOnRelease: (obs) => { if (obs != null) obs.gameObject.SetActive(false); },
                actionOnDestroy: (obs) => { if (obs != null) Destroy(obs.gameObject); },
                collectionCheck: true,
                defaultCapacity: 4,
                maxSize: 15
            );
            _sharedObstaclePools[prefab] = pool;
            return pool;
        }

        protected override void OnLaneSpawned()
        {
            base.OnLaneSpawned();

            // 1. 바닥 보도블록 머티리얼 및 장식 적용
            if (_meshRenderer != null && _sidewalkMaterial != null)
            {
                _meshRenderer.sharedMaterial = _sidewalkMaterial;
            }
            if (_curbDecorations != null)
            {
                _curbDecorations.SetActive(true);
            }

            // 점자블록은 가끔씩만(25% 확률) 등장
            bool showBraille = Random.value < _brailleBlockChance;
            if (_brailleBlockGroup != null)
            {
                _brailleBlockGroup.SetActive(showBraille);
            }

            // 2. 가로등 분산 스폰 (12% 확률로 좌/우 무작위 한 쪽에만 듬성듬성 배치)
            SpawnStreetLightIfLucky();

            // 3. 고정 길막 장애물 스폰 (킥보드 / 볼라드 등)
            if (LaneZIndex > _safeStartZoneZ)
            {
                if (Random.value < _obstacleSpawnChance)
                {
                    SpawnObstacles();
                }
            }
        }

        private void SpawnStreetLightIfLucky()
        {
            if (_streetLightPrefab == null || _sharedStreetLightPool == null) return;

            if (Random.value < _streetLightSpawnChance)
            {
                try
                {
                    _activeStreetLight = _sharedStreetLightPool.Get();
                }
                catch
                {
                    _activeStreetLight = null;
                }

                if (_activeStreetLight == null) return;

                bool isLeft = Random.value < 0.5f;
                float posX = isLeft ? -5.2f : 5.2f;

                _activeStreetLight.transform.position = new Vector3(posX, 0.1f, LaneZIndex);
                _activeStreetLight.transform.rotation = Quaternion.Euler(0f, isLeft ? 90f : -90f, 0f);
                _activeStreetLight.SetActive(true);
            }
        }

        private void SpawnObstacles()
        {
            StationaryObstacle chosenPrefab = GetRandomObstaclePrefab();
            if (chosenPrefab == null) return;

            var pool = GetOrCreateObstaclePool(chosenPrefab);
            if (pool == null) return;

            _availableXPositions.Clear();
            for (int x = _minGridX; x <= _maxGridX; x++)
            {
                if (x == _lastSpawnedX) continue;
                if ((_lastSpawnedX == -3 && x == -2) || (_lastSpawnedX == 3 && x == 2)) continue;
                _availableXPositions.Add(x);
            }

            if (_availableXPositions.Count == 0 || _maxObstaclesPerLane <= 0) return;

            int chosenIndex = Random.Range(0, _availableXPositions.Count);
            int chosenX = _availableXPositions[chosenIndex];
            _lastSpawnedX = chosenX;

            StationaryObstacle obstacle = pool.Get();
            if (obstacle == null) return;

            Vector3 spawnPosition = new Vector3(chosenX, 0.16f, LaneZIndex);

            _activeObstacleInfos.Add(new SpawnedObstacleInfo { Obstacle = obstacle, Pool = pool });

            obstacle.Initialize(spawnPosition, (obs) =>
            {
                if (pool != null) pool.Release(obs);
            });
        }

        private StationaryObstacle GetRandomObstaclePrefab()
        {
            if (_obstaclePrefabs != null && _obstaclePrefabs.Length > 0)
            {
                return _obstaclePrefabs[Random.Range(0, _obstaclePrefabs.Length)];
            }
            return _obstaclePrefab;
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();

            if (_activeStreetLight != null)
            {
                try
                {
                    if (_sharedStreetLightPool != null)
                    {
                        _sharedStreetLightPool.Release(_activeStreetLight);
                    }
                }
                catch { }
                _activeStreetLight = null;
            }

            for (int i = _activeObstacleInfos.Count - 1; i >= 0; i--)
            {
                var info = _activeObstacleInfos[i];
                if (info.Obstacle != null && info.Pool != null)
                {
                    info.Obstacle.Recycle();
                }
            }
            _activeObstacleInfos.Clear();
        }
    }
}
