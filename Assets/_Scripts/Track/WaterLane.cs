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

        [Tooltip("스폰 시작 X축 좌표 (하천 폭 60m 기준 ±26.0m)")]
        [SerializeField] private float _spawnBoundaryX = 26.0f;

        // 런타임 상태 변수
        private float _currentDirection = 1f;
        private float _currentSpeed = 3.5f;
        private Coroutine _spawnRoutine;

        // 널판지 전역 공유 오브젝트 풀 (모든 WaterLane이 공유하여 중복 생성 방지)
        private static IObjectPool<FloatingPlank> _sharedPlankPool;
        private readonly List<FloatingPlank> _activePlanks = new List<FloatingPlank>();

        // [핵심!] WaterLane의 Transform 스케일(20, 0.12, 1) 왜곡을 널판지가 상속받지 않도록 독립 컨테이너 사용
        private static Transform _plankRootContainer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearSharedPool();
        }

        public static void ClearSharedPool()
        {
            if (_sharedPlankPool != null)
            {
                try { _sharedPlankPool.Clear(); } catch { }
                _sharedPlankPool = null;
            }
            _plankRootContainer = null;
        }

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
            EnsureSharedPool();
        }

        private void OnEnable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted += ClearSharedPool;
        }

        private void OnDisable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted -= ClearSharedPool;
        }

        private void EnsureSharedPool()
        {
            if (_plankPrefab == null || _sharedPlankPool != null) return;

            Transform container = GetOrCreateContainer();
            FloatingPlank prefabRef = _plankPrefab;

            _sharedPlankPool = new ObjectPool<FloatingPlank>(
                createFunc: () =>
                {
                    if (container == null) container = GetOrCreateContainer();
                    FloatingPlank plank = Instantiate(prefabRef, container);
                    plank.gameObject.SetActive(false);
                    return plank;
                },
                actionOnGet: (plank) => { },
                actionOnRelease: (plank) =>
                {
                    if (plank != null) plank.gameObject.SetActive(false);
                },
                actionOnDestroy: (plank) =>
                {
                    if (plank != null) Destroy(plank.gameObject);
                },
                collectionCheck: false,
                defaultCapacity: 12,
                maxSize: 36
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

            EnsureSharedPool();

            // [핵심] 레인이 생성되자마자 플레이어가 건널 수 있도록 레인 위에 널판지 1~2개를 미리 배치
            PrewarmPlanks();

            // 주기적 스폰 코루틴 가동
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(PlankSpawnRoutine());
        }

        private void PrewarmPlanks()
        {
            if (_sharedPlankPool == null) return;

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
                FloatingPlank plank = _activePlanks[i];
                if (plank != null && _sharedPlankPool != null)
                {
                    _sharedPlankPool.Release(plank);
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
            EnsureSharedPool();
            if (_plankPrefab == null || _sharedPlankPool == null) return;

            FloatingPlank plank = null;
            try
            {
                plank = _sharedPlankPool.Get();
            }
            catch
            {
                _sharedPlankPool = null;
                EnsureSharedPool();
                if (_sharedPlankPool != null) plank = _sharedPlankPool.Get();
            }

            if (plank == null) return;

            _activePlanks.Add(plank);
            plank.transform.position = new Vector3(xPos, 0.15f, LaneZIndex);

            plank.Initialize(_currentDirection, _currentSpeed, (p) =>
            {
                _activePlanks.Remove(p);
                if (p != null && _sharedPlankPool != null)
                {
                    _sharedPlankPool.Release(p);
                }
            });
        }
    }
}
