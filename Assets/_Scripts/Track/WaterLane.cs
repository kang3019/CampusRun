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
    /// 양옆 폭포/급류 경계의 흰색 포말 띠와 기포들이 역동적으로 끓어오르며 움직입니다.
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

        [Header("--- 폭포 물거품 & 기포 애니메이션 설정 ---")]
        [Tooltip("폭포 흰색 물거품 띠 및 기포 애니메이션 활성화 여부")]
        [SerializeField] private bool _enableFoamAnimation = true;

        [Tooltip("물거품 일렁임 속도")]
        [SerializeField] private float _foamWaveSpeed = 7.5f;

        [Tooltip("기포 생성 및 터짐 순환 속도 배율")]
        [SerializeField] private float _bubbleCycleRate = 1.85f;

        // 런타임 상태 변수
        private float _currentDirection = 1f;
        private float _currentSpeed = 3.5f;
        private Coroutine _spawnRoutine;

        // 널판지 전역 공유 오브젝트 풀 (모든 WaterLane이 공유하여 중복 생성 방지)
        private static IObjectPool<FloatingPlank> _sharedPlankPool;
        private readonly List<FloatingPlank> _activePlanks = new List<FloatingPlank>();

        // [핵심!] WaterLane의 Transform 스케일(60, 0.12, 1) 왜곡을 널판지가 상속받지 않도록 독립 컨테이너 사용
        private static Transform _plankRootContainer;

        // --- 폭포 포말 & 기포 캐싱 데이터 (Update 무할당 연산용) ---
        private struct FoamBandNode
        {
            public Transform Target;
            public Vector3 BaseLocalPos;
            public Vector3 BaseLocalScale;
            public float Phase;
            public float SideSign;
        }

        private struct FoamBubbleNode
        {
            public Transform Target;
            public Vector3 OriginLocalPos;
            public Vector3 BaseLocalScale;
            public float CycleSpeed;
            public float PhaseOffset;
            public float DriftOffsetX;
            public float DriftOffsetZ;
            public float RiseOffsetY;
        }

        private FoamBandNode[] _foamBands;
        private int _foamBandCount = 0;

        private FoamBubbleNode[] _foamBubbles;
        private int _foamBubbleCount = 0;

        private bool _isFoamCached = false;
        private float _cachedInvX = 1f / 60f;
        private float _cachedInvY = 1f / 0.12f;

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

        private void Update()
        {
            if (_enableFoamAnimation && _isFoamCached)
            {
                AnimateWaterfallFoam();
            }
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

            // 폭포 흰색 포말 띠 및 보글거리는 기포 트랜스폼 최초 1회 캐싱
            if (!_isFoamCached)
            {
                CacheAndPrepareWaterfallFoam();
            }

            // [핵심] 레인이 생성되자마자 플레이어가 건널 수 있도록 레인 위에 널판지 1~2개를 미리 배치
            PrewarmPlanks();

            // 주기적 스폰 코루틴 가동
            if (_spawnRoutine != null) StopCoroutine(_spawnRoutine);
            _spawnRoutine = StartCoroutine(PlankSpawnRoutine());
        }

        /// <summary>
        /// Natural_Boundaries_V5 하위의 흰색 물거품 띠(Water_PixelBand)와 기포 점(Water_PixelDot)을 캐싱하고,
        /// 폭포에서 기포가 풍성하게 보글보글 피어오르도록 추가 기포 조각들을 최초 1회 생성합니다.
        /// </summary>
        private void CacheAndPrepareWaterfallFoam()
        {
            Transform boundaryRoot = transform.Find("Natural_Boundaries_V5");
            if (boundaryRoot == null) return;

            Vector3 parentScale = transform.localScale;
            _cachedInvX = 1f / Mathf.Max(0.001f, parentScale.x);
            _cachedInvY = 1f / Mathf.Max(0.001f, parentScale.y);

            // 기존 자식 중 공유 흰색 포말 머티리얼 참조 획득
            Material foamMat = null;
            Transform sampleBand = boundaryRoot.Find("Water_PixelBand_L_0");
            if (sampleBand != null)
            {
                MeshRenderer sampleMr = sampleBand.GetComponent<MeshRenderer>();
                if (sampleMr != null) foamMat = sampleMr.sharedMaterial;
            }

            // 폭포 느낌을 극대화할 추가 기포 8개(좌 4, 우 4)를 최초 1회 생성
            if (boundaryRoot.Find("Water_Bubble_Extra_L_0") == null && foamMat != null)
            {
                float baseBoundaryX = 4.85f;
                float foamLocalY = 0.516f;
                float foamThicknessY = 0.016f * _cachedInvY;

                float[] extraOffsetX = { 0.30f, -0.32f, 0.52f, -0.54f };
                float[] extraOffsetZ = { -0.42f, -0.05f, 0.22f, 0.44f };
                float[] extraSize = { 0.14f, 0.15f, 0.12f, 0.13f };

                for (int i = 0; i < extraOffsetX.Length; i++)
                {
                    float ox = extraOffsetX[i];
                    float oz = extraOffsetZ[i];
                    float sz = extraSize[i];

                    CreateExtraBubble($"Water_Bubble_Extra_L_{i}", boundaryRoot, foamMat,
                        new Vector3((-baseBoundaryX + ox) * _cachedInvX, foamLocalY, oz),
                        new Vector3(sz * _cachedInvX, foamThicknessY, sz));

                    CreateExtraBubble($"Water_Bubble_Extra_R_{i}", boundaryRoot, foamMat,
                        new Vector3((baseBoundaryX - ox) * _cachedInvX, foamLocalY, -oz),
                        new Vector3(sz * _cachedInvX, foamThicknessY, sz));
                }
            }

            int childCount = boundaryRoot.childCount;
            _foamBands = new FoamBandNode[childCount];
            _foamBubbles = new FoamBubbleNode[childCount];
            _foamBandCount = 0;
            _foamBubbleCount = 0;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = boundaryRoot.GetChild(i);
                if (child == null) continue;

                string childName = child.name;
                if (childName.StartsWith("Water_PixelBand_"))
                {
                    bool isLeft = childName.Contains("_L_");
                    _foamBands[_foamBandCount] = new FoamBandNode
                    {
                        Target = child,
                        BaseLocalPos = child.localPosition,
                        BaseLocalScale = child.localScale,
                        Phase = i * 0.85f,
                        SideSign = isLeft ? -1f : 1f
                    };
                    _foamBandCount++;
                }
                else if (childName.StartsWith("Water_PixelDot_") || childName.StartsWith("Water_Bubble_Extra_"))
                {
                    Vector3 basePos = child.localPosition;
                    bool isLeft = basePos.x < 0f;
                    float boundaryCenterLocalX = (isLeft ? -4.85f : 4.85f) * _cachedInvX;

                    // 기포가 폭포 중심선 근처에서 피어올라 바깥/안쪽으로 퍼져나가는 방향과 거리 계산
                    float offsetFromCenter = basePos.x - boundaryCenterLocalX;
                    float dirSign = (Mathf.Abs(offsetFromCenter) > 0.0001f) ? Mathf.Sign(offsetFromCenter) : (isLeft ? 1f : -1f);

                    // 시작 원점은 폭포 흰색 띠 바로 옆으로 설정하고, 기포가 부풀며 퍼져나가도록 구성
                    Vector3 originPos = new Vector3(
                        boundaryCenterLocalX + dirSign * (0.18f * _cachedInvX),
                        basePos.y,
                        basePos.z);

                    float driftWorldX = 0.42f + (_foamBubbleCount % 3) * 0.14f;
                    float driftLocalX = dirSign * driftWorldX * _cachedInvX;
                    float driftLocalZ = ((_foamBubbleCount & 1) == 0 ? 1f : -1f) * 0.12f;

                    _foamBubbles[_foamBubbleCount] = new FoamBubbleNode
                    {
                        Target = child,
                        OriginLocalPos = originPos,
                        BaseLocalScale = child.localScale,
                        CycleSpeed = 1.35f + (_foamBubbleCount % 5) * 0.28f,
                        PhaseOffset = _foamBubbleCount * 0.37f,
                        DriftOffsetX = driftLocalX,
                        DriftOffsetZ = driftLocalZ,
                        RiseOffsetY = 0.022f * _cachedInvY
                    };
                    _foamBubbleCount++;
                }
            }

            _isFoamCached = true;
        }

        private static void CreateExtraBubble(string name, Transform parent, Material mat, Vector3 localPos, Vector3 localScale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);

            Collider col = cube.GetComponent<Collider>();
            if (col != null) Destroy(col);

            MeshRenderer mr = cube.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
        }

        /// <summary>
        /// 매 프레임 힙 할당(GC) 없이 폭포의 흰색 물거품 띠를 출렁이게 하고 기포가 보글보글 생겼다 터지는 연출을 수행합니다.
        /// </summary>
        private void AnimateWaterfallFoam()
        {
            float time = Time.time;
            float lanePhase = LaneZIndex * 1.15f;
            float waveTime = time * _foamWaveSpeed;

            // 1. 폭포 중심 흰색 포말 띠 (Water_PixelBand): 끊임없이 일렁이며 거품 폭이 넓어졌다 좁아지는 폭포수 물살 연출
            for (int i = 0; i < _foamBandCount; i++)
            {
                FoamBandNode band = _foamBands[i];
                if (band.Target == null) continue;

                float p = waveTime + band.Phase + lanePhase;
                float wave = Mathf.Sin(p) * 0.65f + Mathf.Cos(p * 1.6f) * 0.35f;

                // 도트/복셀 감성을 살린 4단계 양자화 느낌과 부드러운 출렁임 결합
                float widthFactor = 1.05f + wave * 0.38f;
                float heightFactor = 1.0f + Mathf.Abs(wave) * 0.55f;
                float shiftX = wave * band.SideSign * (0.09f * _cachedInvX);
                float shiftY = Mathf.Abs(wave) * (0.008f * _cachedInvY);

                band.Target.localScale = new Vector3(
                    band.BaseLocalScale.x * widthFactor,
                    band.BaseLocalScale.y * heightFactor,
                    band.BaseLocalScale.z);

                band.Target.localPosition = new Vector3(
                    band.BaseLocalPos.x + shiftX,
                    band.BaseLocalPos.y + shiftY,
                    band.BaseLocalPos.z);
            }

            // 2. 폭포 주변 기포 조각들 (Water_PixelDot & Extra): 보글보글 생성 -> 팽창하며 물살을 타고 이동 -> 톡 터지며 소멸 반복
            float bubbleBaseTime = time * _bubbleCycleRate;
            for (int i = 0; i < _foamBubbleCount; i++)
            {
                FoamBubbleNode bubble = _foamBubbles[i];
                if (bubble.Target == null) continue;

                float t = Mathf.Repeat(bubbleBaseTime * bubble.CycleSpeed + bubble.PhaseOffset + lanePhase * 0.3f, 1f);

                // 기포 스케일 곡선:
                // [0.0 ~ 0.22]: 폭포 수면에서 뽕! 하고 빠르게 기포가 솟아오름
                // [0.22 ~ 0.72]: 보글보글 떨리며 최대 크기로 물 위를 떠다님
                // [0.72 ~ 1.0]: 톡! 하고 터지며 작아져서 사라짐
                float scaleMultiplier;
                if (t < 0.22f)
                {
                    float growT = t / 0.22f;
                    scaleMultiplier = growT * 1.35f;
                }
                else if (t < 0.72f)
                {
                    float boilT = (t - 0.22f) / 0.50f;
                    scaleMultiplier = 1.25f + Mathf.Sin(boilT * Mathf.PI * 3f) * 0.22f;
                }
                else
                {
                    float popT = (t - 0.72f) / 0.28f;
                    scaleMultiplier = Mathf.Max(0f, (1f - popT) * 1.25f);
                }

                // 폭포 포말에서 바깥/안쪽으로 밀려나며 흔들리는 위치 계산
                float moveT = Mathf.SmoothStep(0f, 1f, t);
                float curX = bubble.OriginLocalPos.x + bubble.DriftOffsetX * moveT;
                float curZ = bubble.OriginLocalPos.z + Mathf.Sin(t * Mathf.PI * 2f + bubble.PhaseOffset) * bubble.DriftOffsetZ;
                float curY = bubble.OriginLocalPos.y + Mathf.Sin(t * Mathf.PI) * bubble.RiseOffsetY;

                bubble.Target.localPosition = new Vector3(curX, curY, curZ);
                bubble.Target.localScale = new Vector3(
                    bubble.BaseLocalScale.x * scaleMultiplier,
                    bubble.BaseLocalScale.y * (0.8f + scaleMultiplier * 0.5f),
                    bubble.BaseLocalScale.z * scaleMultiplier);
            }
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
