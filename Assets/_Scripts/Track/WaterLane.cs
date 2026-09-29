using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Obstacles;

namespace CampusRun.Track
{
    /// <summary>
    /// 거대 물웅덩이 레인입니다. (원작 길건너 친구들의 '강물' 기믹)
    /// 떠다니는 널판지/나무상자를 스폰하여 일정한 방향과 속도로 표류시킵니다.
    /// 플레이어가 널판지를 밟지 않고 물에 빠지면 익사(Drown) 탈락합니다.
    /// </summary>
    public class WaterLane : BaseLane
    {
        [Header("--- 널판지 스폰 설정 ---")]
        [Tooltip("스폰할 널판지 프리팹 (FloatingPlank 컴포넌트 필수)")]
        [SerializeField] private FloatingPlank _plankPrefab;

        [Tooltip("널판지 이동 방향 (+1: 왼쪽->오른쪽, -1: 오른쪽->왼쪽, 0: 랜덤)")]
        [SerializeField] private float _fixedDirection = 0f;

        [Tooltip("널판지 표류 속도 범위")]
        [SerializeField] private float _minSpeed = 3.0f;
        [SerializeField] private float _maxSpeed = 5.0f;

        [Tooltip("널판지 스폰 간격(초) 범위")]
        [SerializeField] private float _minSpawnInterval = 2.2f;
        [SerializeField] private float _maxSpawnInterval = 4.0f;

        [Tooltip("스폰 시작 X축 좌표")]
        [SerializeField] private float _spawnBoundaryX = 14f;

        // 런타임 상태 변수
        private float _currentDirection = 1f;
        private float _currentSpeed = 3.5f;
        private Coroutine _spawnRoutine;

        // 널판지 오브젝트 풀
        private IObjectPool<FloatingPlank> _plankPool;
        private readonly List<FloatingPlank> _activePlanks = new List<FloatingPlank>();

        // [핵심!] WaterLane의 Transform 스케일(20, 0.12, 1) 왜곡을 널판지가 상속받지 않도록 독립 컨테이너 사용
        private static Transform _plankRootContainer;

        private static Transform GetOrCreateContainer()
        {
            if (_plankRootContainer == null)
            {
                GameObject container = GameObject.Find("[Plank_Pool_Container]");
                if (container == null)
                {
                    container = new GameObject("[Plank_Pool_Container]");
                    container.transform.position = Vector3.zero;
                    container.transform.rotation = Quaternion.identity;
                    container.transform.localScale = Vector3.one;
                }
                _plankRootContainer = container.transform;
            }
            return _plankRootContainer;
        }

        public float CurrentDirection => _currentDirection;
        public float CurrentSpeed => _currentSpeed;

        private void Awake()
        {
            _isSafeLane = false;
            InitializePool();
        }

        private void InitializePool()
        {
            if (_plankPrefab == null) return;

            Transform container = GetOrCreateContainer();

            _plankPool = new ObjectPool<FloatingPlank>(
                createFunc: () =>
                {
                    FloatingPlank plank = Instantiate(_plankPrefab, container);
                    plank.gameObject.SetActive(false);
                    return plank;
                },
                actionOnGet: (plank) =>
                {
                    _activePlanks.Add(plank);
                },
                actionOnRelease: (plank) =>
                {
                    _activePlanks.Remove(plank);
                    plank.gameObject.SetActive(false);
                },
                actionOnDestroy: (plank) =>
                {
                    if (plank != null) Destroy(plank.gameObject);
                },
                collectionCheck: true,
                defaultCapacity: 6,
                maxSize: 18
            );
        }

        /// <summary>
        /// 레인 매니저에서 널판지 표류 방향을 번갈아(교차) 지정할 때 호출합니다.
        /// </summary>
        public void SetDesiredDirection(float direction)
        {
            _fixedDirection = Mathf.Sign(direction);
        }

        protected override void OnLaneSpawned()
        {
            base.OnLaneSpawned();

            // 방향 결정
            if (_fixedDirection == 0f)
            {
                _currentDirection = (Random.value > 0.5f) ? 1f : -1f;
            }
            else
            {
                _currentDirection = Mathf.Sign(_fixedDirection);
            }

            _currentSpeed = Random.Range(_minSpeed, _maxSpeed);

            if (_plankPool == null)
            {
                InitializePool();
            }

            // [핵심] 레인이 생성되자마자 플레이어가 건널 수 있도록 레인 위에 널판지 1~2개를 미리 배치
            PrewarmPlanks();

            // 주기적 스폰 코루틴 가동
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(PlankSpawnRoutine());
        }

        private void PrewarmPlanks()
        {
            if (_plankPool == null) return;

            // 중앙 부근 및 진입로 주변에 널판지 2개 사전 배치
            float firstX = (_currentDirection > 0) ? -2f : 2f;
            float secondX = (_currentDirection > 0) ? 5f : -5f;

            SpawnPlankAt(firstX);
            SpawnPlankAt(secondX);
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();

            if (_spawnRoutine != null)
            {
                StopCoroutine(_spawnRoutine);
                _spawnRoutine = null;
            }

            for (int i = _activePlanks.Count - 1; i >= 0; i--)
            {
                if (_activePlanks[i] != null && _plankPool != null)
                {
                    _plankPool.Release(_activePlanks[i]);
                }
            }
            _activePlanks.Clear();
        }

        private IEnumerator PlankSpawnRoutine()
        {
            while (true)
            {
                float waitTime = Random.Range(_minSpawnInterval, _maxSpawnInterval);
                yield return new WaitForSeconds(waitTime);

                float startX = (_currentDirection > 0) ? -_spawnBoundaryX : _spawnBoundaryX;
                SpawnPlankAt(startX);
            }
        }

        private void SpawnPlankAt(float xPos)
        {
            if (_plankPrefab == null || _plankPool == null) return;

            FloatingPlank plank = _plankPool.Get();
            plank.transform.position = new Vector3(xPos, 0.15f, LaneZIndex);

            plank.Initialize(_currentDirection, _currentSpeed, (p) =>
            {
                if (gameObject.activeInHierarchy && _plankPool != null)
                {
                    _plankPool.Release(p);
                }
            });
        }
    }
}
