using System;
using UnityEngine;
using CampusRun.Player;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 거대 물웅덩이 레인 위를 일정한 방향과 속도로 떠다니는 통나무/널판지 컴포넌트입니다.
    /// 플레이어가 통나무 위에 올라타면 함께 X축으로 표류(Drift)하며,
    /// 양 끝이나 중간 어디를 밟더라도 해당 부위가 수면 아래로 '살짝 풍덩' 가라앉으며 탄성 있게 흔들리는 연출을 수행합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingPlank : MonoBehaviour
    {
        [Header("--- 이동 및 경계 설정 ---")]
        [Tooltip("경계를 벗어나면 풀로 반환될 X축 한계 좌표 (하천 폭 60m 기준 ±28.0m)")]
        [SerializeField] private float _despawnBoundaryX = 28.0f;

        [Tooltip("통나무의 길이(X축 크기)")]
        [SerializeField] private float _plankLength = 2.2f;

        [Tooltip("통나무 윗면 탑승 표면 높이 오프셋 (캐릭터가 끼지 않고 위에 올라타도록 설정)")]
        [SerializeField] private float _surfaceHeightOffset = 0.18f;

        [Header("--- 통나무 탑승 '살짝 풍덩' 침강 & 위치별 흔들림 설정 ---")]
        [Tooltip("통나무에 착지할 때 수면 아래로 풍덩 가라앉는 기본 수직 깊이")]
        [SerializeField] private float _stepSinkDepth = 0.115f;

        [Tooltip("밟은 부위(양 끝 또는 중간)가 국소적으로 움푹 내려앉으며 출렁이는 깊이")]
        [SerializeField] private float _localSectionDipDepth = 0.085f;

        [Tooltip("밟은 부위가 순간적으로 눌렸다가 탱글하게 복원되는 탄성 스쿼시 비율")]
        [SerializeField] private float _localSectionSquash = 0.15f;

        [Tooltip("양 끝을 밟았을 때 해당 끝단이 물속으로 기울어지는 최대 롤링 각도(도)")]
        [SerializeField] private float _endTiltAngle = 11.5f;

        [Tooltip("중간을 밟았을 때도 통나무가 좌우로 갸우뚱 출렁이는 롤링 각도(도)")]
        [SerializeField] private float _centerRollWobbleAngle = 5.5f;

        [Tooltip("착지 순간 통나무가 앞뒤(X축)로 출렁이는 피칭 각도(도)")]
        [SerializeField] private float _pitchWobbleAngle = 8.5f;

        [Tooltip("풍덩 침강 후 수면 탄성 반동 주파수")]
        [SerializeField] private float _wobbleFrequency = 21.0f;

        [Tooltip("흔들림 감쇠 속도 (낮을수록 오래 출렁임)")]
        [SerializeField] private float _wobbleDamping = 4.6f;

        [Tooltip("플레이어가 올라타 있는 동안 무게로 인해 살짝 가라앉아 있는 깊이")]
        [SerializeField] private float _mountedWeightSink = 0.025f;

        [Tooltip("플레이어가 양 끝에 서 있는 동안 그쪽 끝이 살짝 기울어져 있는 각도(도)")]
        [SerializeField] private float _mountedWeightTilt = 3.2f;

        private float _direction = 1f; // +1: 오른쪽, -1: 왼쪽
        private float _speed = 3.5f;
        private Action<FloatingPlank> _onDespawn;
        private bool _isInitialized = false;
        private float _baseY = 0.15f;
        private float _bobbingPhase = 0f;

        // 현재 통나무 위에 탑승한 플레이어
        private GridPlayerController _mountedPlayer;

        // --- 착지 충격(풍덩 & 흔들림) 런타임 상태 변수 ---
        private bool _impactActive = false;
        private float _impactElapsed = 0f;
        private float _stepLocalX = 0f;
        private float _stepNormalizedX = 0f;
        private float _impactIntensity = 1f;
        private float _centerWobbleSign = 1f;
        private float _pitchWobbleSign = 1f;

        // 지속 탑승 무게에 의한 부드러운 침강/기울기 보간값
        private float _currentSteadySink = 0f;
        private float _currentSteadyTiltZ = 0f;
        private float _currentTotalRollZ = 0f;
        private float _currentLocalDip = 0f;
        private float _currentLocalSquash = 0f;
        private bool _isMeshDeformed = false;

        // --- GC 할당 0바이트를 위한 절차적 분할 원기둥 메시 및 자식 트랜스폼 캐시 ---
        private const int RadialSegments = 18;
        private const int HeightSegments = 20;
        private const float LocalGaussianSigma = 0.48f;

        private static Mesh _sharedTemplateLogMesh;
        private static Vector3[] _sharedBaseVertices;

        private struct DeformableLogNode
        {
            public Transform Target;
            public Mesh WorkingMesh;
            public Vector3 BaseLocalScale;
            public float InvScaleX;
            public float ScaleY;
        }

        private struct ChildPropNode
        {
            public Transform Target;
            public Vector3 BaseLocalPos;
            public Vector3 BaseLocalScale;
            public bool IsEndCap;
            public bool IsRopeBand;
            public bool IsTopAttachment;
        }

        private DeformableLogNode[] _logNodes;
        private int _logNodeCount = 0;
        private Vector3[] _workingVertices;

        private ChildPropNode[] _childProps;
        private int _childPropCount = 0;
        private bool _isVisualCached = false;

        public float Speed => _speed;
        public float Direction => _direction;
        public float PlankLength => _plankLength;
        public float TopSurfaceY => transform.position.y + _surfaceHeightOffset;
        public float RestingTopSurfaceY => _baseY + _surfaceHeightOffset;

        private void Awake()
        {
            CacheAndPrepareVisualNodes();
        }

        private void OnDestroy()
        {
            if (_logNodes != null)
            {
                for (int i = 0; i < _logNodeCount; i++)
                {
                    if (_logNodes[i].WorkingMesh != null)
                    {
                        Destroy(_logNodes[i].WorkingMesh);
                    }
                }
            }
        }

        /// <summary>
        /// 레인에서 풀을 통해 꺼낼 때 방향과 속도를 지정하여 초기화합니다.
        /// </summary>
        public void Initialize(float direction, float speed, Action<FloatingPlank> onDespawn)
        {
            if (!_isVisualCached)
            {
                CacheAndPrepareVisualNodes();
            }

            _direction = Mathf.Sign(direction);
            _speed = speed;
            _onDespawn = onDespawn;
            _mountedPlayer = null;
            _baseY = transform.position.y;
            _bobbingPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            _impactActive = false;
            _impactElapsed = 0f;
            _stepLocalX = 0f;
            _stepNormalizedX = 0f;
            _currentSteadySink = 0f;
            _currentSteadyTiltZ = 0f;
            _currentTotalRollZ = 0f;
            _currentLocalDip = 0f;
            _currentLocalSquash = 0f;

            ResetVisualDeformation();

            _isInitialized = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isInitialized) return;

            float dt = Time.deltaTime;

            // 1. X축으로 지속 이동
            float moveDelta = _direction * _speed * dt;

            // 2. 물결에 따른 통나무 기본 미세 부유(Bobbing) 및 롤링 연출
            float bobbingOffset = Mathf.Sin(Time.time * 2.2f + _bobbingPhase) * 0.015f;
            float ambientRollZ = Mathf.Cos(Time.time * 1.8f + _bobbingPhase) * 1.2f;

            // 3. 플레이어가 탑승 중일 때 서 있는 위치에 따른 지속 무게 침강 및 기울기 갱신
            float targetSteadySink = 0f;
            float targetSteadyTiltZ = 0f;

            if (_mountedPlayer != null && _mountedPlayer.IsAlive)
            {
                float halfLen = Mathf.Max(0.5f, _plankLength * 0.5f);
                float playerLocalX = Mathf.Clamp(_mountedPlayer.transform.position.x - (transform.position.x + moveDelta), -halfLen, halfLen);
                float playerNormX = Mathf.Clamp(playerLocalX / halfLen, -1f, 1f);

                // 지속적으로 밟고 있는 위치도 최신화하여 잔여 흔들림이 발밑을 중심으로 유지되도록 설정
                _stepLocalX = Mathf.Lerp(_stepLocalX, playerLocalX, dt * 10f);

                targetSteadySink = -_mountedWeightSink;
                // 왼쪽(playerNormX < 0)을 밟으면 +Z 회전으로 왼쪽이 내려가고, 오른쪽(playerNormX > 0)을 밟으면 -Z 회전으로 오른쪽이 내려감
                targetSteadyTiltZ = -playerNormX * _mountedWeightTilt;
            }

            _currentSteadySink = Mathf.Lerp(_currentSteadySink, targetSteadySink, dt * 10f);
            _currentSteadyTiltZ = Mathf.Lerp(_currentSteadyTiltZ, targetSteadyTiltZ, dt * 10f);

            // 4. 착지/이탈 순간의 '살짝 풍덩' 수면 침강 및 위치별(양 끝 + 중간) 탄성 흔들림 계산
            float impactSinkY = 0f;
            float impactRollZ = 0f;
            float impactPitchX = 0f;
            _currentLocalDip = 0f;
            _currentLocalSquash = 0f;

            if (_impactActive)
            {
                _impactElapsed += dt;
                float t = _impactElapsed;
                float env = Mathf.Exp(-_wobbleDamping * t) * _impactIntensity;

                if (env < 0.004f && t > 0.25f)
                {
                    _impactActive = false;
                    ResetVisualDeformation();
                }
                else
                {
                    // sin 파형을 이용해 착지 직후 빠르게 물속으로 '풍덩' 내려앉았다가(-sin) 부력으로 통통 튀어오르는 곡선 생성
                    float primaryWave = Mathf.Sin(t * _wobbleFrequency);
                    float secondaryWave = Mathf.Sin(t * (_wobbleFrequency * 1.22f));

                    float absNormX = Mathf.Abs(_stepNormalizedX);
                    float centerFactor = 1f - absNormX; // 정중앙에서 1.0, 양 끝단에서 0.0

                    // (A) 전체 수직 침강(Y): 양 끝과 중간 모두 풍덩 내려앉으며, 중간을 밟을 때는 수직으로 힘이 집중되어 살짝 더 깊게 침강
                    float sinkMultiplier = 1.0f + centerFactor * 0.30f;
                    impactSinkY = -primaryWave * _stepSinkDepth * sinkMultiplier * env;

                    // (B) Z축 롤링(좌우 기울기 흔들림):
                    // - 양 끝을 밟았을 때: 밟은 쪽 끝이 확 물속으로 기울어졌다가 반동으로 출렁임
                    // - 중간을 밟았을 때: 양 끝이 번갈아 갸우뚱거리며 수면 위에서 활기차게 출렁임 ("양 끝이랑 중간도 다")
                    float endTiltComponent = (-_stepNormalizedX) * _endTiltAngle * primaryWave;
                    float centerWobbleComponent = _centerWobbleSign * _centerRollWobbleAngle * centerFactor * secondaryWave;
                    impactRollZ = (endTiltComponent + centerWobbleComponent) * env;

                    // (C) X축 피칭(앞뒤 롤링 흔들림):
                    // 두 개의 통나무가 나란히 묶인 뗏목 구조이므로 밟는 순간 앞뒤로도 출렁이며 생동감 부여 (특히 중간 밟을 때 더욱 부각)
                    float pitchMultiplier = 0.85f + centerFactor * 0.35f;
                    impactPitchX = _pitchWobbleSign * _pitchWobbleAngle * pitchMultiplier * secondaryWave * env;

                    // (D) 밟은 그 부분(_stepLocalX)의 국소 침강 및 탄성 스쿼시 변형:
                    // 양 끝이든 중간이든 캐릭터 발이 닿은 정확한 부위가 움푹 들어갔다가 탱글하게 튀어오름
                    float localDipMultiplier = 1.0f + centerFactor * 0.25f;
                    _currentLocalDip = -primaryWave * _localSectionDipDepth * localDipMultiplier * env;
                    _currentLocalSquash = primaryWave * _localSectionSquash * env;

                    ApplyVisualDeformation();
                }
            }

            // 5. 통나무 루트 위치 및 회전 적용
            float finalY = _baseY + bobbingOffset + _currentSteadySink + impactSinkY;
            _currentTotalRollZ = ambientRollZ + _currentSteadyTiltZ + impactRollZ;

            transform.position = new Vector3(transform.position.x + moveDelta, finalY, transform.position.z);
            transform.localRotation = Quaternion.Euler(impactPitchX, 0f, _currentTotalRollZ);

            // 6. 탑승 중인 플레이어가 있다면 동일한 이동량 및 밟고 있는 부위의 실제 표면 높이에 맞춰 함께 움직임
            if (_mountedPlayer != null && _mountedPlayer.IsAlive)
            {
                float playerSurfaceY = GetSurfaceYAtWorldX(_mountedPlayer.transform.position.x);
                _mountedPlayer.ApplyPlankDrift(moveDelta, playerSurfaceY);
            }

            // 7. 소멸 경계 도달 검사
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        /// <summary>
        /// 지정한 월드 X 좌표에서 기울기와 국소 침강이 반영된 통나무 윗면의 실제 Y 높이를 반환합니다.
        /// 플레이어가 양 끝이나 중간에 서 있을 때 통나무의 흔들림에 맞춰 캐릭터도 함께 위아래로 출렁이게 합니다.
        /// </summary>
        public float GetSurfaceYAtWorldX(float worldX)
        {
            float halfLen = Mathf.Max(0.5f, _plankLength * 0.5f);
            float localX = Mathf.Clamp(worldX - transform.position.x, -halfLen, halfLen);

            // Z축 롤링 회전에 따른 해당 X 위치의 높이 변화
            float tiltOffsetY = localX * Mathf.Sin(_currentTotalRollZ * Mathf.Deg2Rad);

            // 밟은 부위의 국소 침강 높이 변화
            EvaluateLocalSectionEffect(localX, out float localDipY, out _);

            return transform.position.y + tiltOffsetY + (localDipY * 0.75f) + _surfaceHeightOffset;
        }

        /// <summary>
        /// 플레이어가 이 통나무에 착지했을 때 탑승 등록 및 착지 위치 기반 '살짝 풍덩' 흔들림을 발동합니다.
        /// </summary>
        public void MountPlayer(GridPlayerController player)
        {
            _mountedPlayer = player;
            if (player != null)
            {
                TriggerStepImpact(player.transform.position.x, 1.0f);
            }
        }

        /// <summary>
        /// 플레이어가 이 통나무의 특정 월드 X 좌표에 착지했을 때 탑승 등록 및 해당 부위 흔들림을 발동합니다.
        /// </summary>
        public void MountPlayer(GridPlayerController player, float landedWorldX)
        {
            _mountedPlayer = player;
            TriggerStepImpact(landedWorldX, 1.0f);
        }

        /// <summary>
        /// 플레이어가 다른 칸으로 점프하여 이탈했을 때 탑승 해제하며, 발을 구른 위치에 가벼운 반동 출렁임을 남깁니다.
        /// </summary>
        public void UnmountPlayer(GridPlayerController player)
        {
            if (_mountedPlayer == player)
            {
                if (player != null)
                {
                    // 점프하며 박차고 나갈 때도 밟았던 부위가 살짝 반동으로 출렁임
                    TriggerStepImpact(player.transform.position.x, 0.55f);
                }
                _mountedPlayer = null;
            }
        }

        /// <summary>
        /// 통나무의 특정 월드 X 위치를 밟았을 때(양 끝, 중간 등) 풍덩 침강 및 부위별 탄성 흔들림을 시작합니다.
        /// </summary>
        public void TriggerStepImpact(float worldStepX, float intensity = 1.0f)
        {
            float halfLen = Mathf.Max(0.5f, _plankLength * 0.5f);
            _stepLocalX = Mathf.Clamp(worldStepX - transform.position.x, -halfLen, halfLen);
            _stepNormalizedX = Mathf.Clamp(_stepLocalX / halfLen, -1f, 1f);

            _impactIntensity = Mathf.Clamp(intensity, 0.2f, 1.5f);
            _impactElapsed = 0f;
            _impactActive = true;

            // 중간을 밟았을 때 좌우/앞뒤 흔들림 방향이 매번 자연스럽게 교차되도록 부호 반전
            _centerWobbleSign = (_centerWobbleSign > 0f) ? -1f : 1f;
            _pitchWobbleSign = (_pitchWobbleSign > 0f) ? -1f : 1f;
        }

        public void Despawn()
        {
            if (_mountedPlayer != null)
            {
                // 플레이어가 타고 있는 채로 화면 밖으로 나가면 화면 밖 표류 탈락
                if (_mountedPlayer.IsAlive)
                {
                    _mountedPlayer.Die("[!] 공사 안전 발판을 타고 공사장 밖으로 밀려나 1교시 지각했습니다!");
                }
                _mountedPlayer = null;
            }

            _impactActive = false;
            ResetVisualDeformation();

            _isInitialized = false;
            _onDespawn?.Invoke(this);
        }

        #region --- 국소 부위(양 끝 / 중간) 흔들림 & 탄성 변형 처리 (무할당) ---

        /// <summary>
        /// 통나무 내 특정 로컬 X 좌표(-1.1 ~ +1.1)에서의 국소 침강량(localDipY)과 스쿼시 비율(localSquash)을 계산합니다.
        /// </summary>
        private void EvaluateLocalSectionEffect(float plankLocalX, out float localDipY, out float localSquash)
        {
            if (!_impactActive || (Mathf.Abs(_currentLocalDip) < 0.0001f && Mathf.Abs(_currentLocalSquash) < 0.0001f))
            {
                localDipY = 0f;
                localSquash = 0f;
                return;
            }

            float dist = plankLocalX - _stepLocalX;
            float gaussian = Mathf.Exp(-(dist * dist) / (2f * LocalGaussianSigma * LocalGaussianSigma));

            // 밟은 지점에서 양옆으로 퍼져나가는 잔물결 탄성 파동
            float rippleWave = Mathf.Cos(dist * 3.6f - _impactElapsed * 15.0f) * 0.20f * gaussian;

            localDipY = _currentLocalDip * (gaussian + rippleWave);
            localSquash = _currentLocalSquash * gaussian;
        }

        /// <summary>
        /// 매 프레임 힙 할당(GC) 없이 밟힌 위치(양 끝 또는 중간) 주변의 통나무 메시와 부속 파츠들을 탄성 변형시킵니다.
        /// </summary>
        private void ApplyVisualDeformation()
        {
            if (!_isVisualCached || _sharedBaseVertices == null || _workingVertices == null) return;

            int vertCount = _sharedBaseVertices.Length;

            // 1. 앞/뒤 메인 통나무 원기둥 메시의 밟힌 부위 변형
            for (int n = 0; n < _logNodeCount; n++)
            {
                DeformableLogNode node = _logNodes[n];
                if (node.WorkingMesh == null) continue;

                float scaleY = node.ScaleY;
                float invScaleX = node.InvScaleX;

                for (int v = 0; v < vertCount; v++)
                {
                    Vector3 baseV = _sharedBaseVertices[v];
                    // 원기둥이 Z축으로 +90도 회전되어 있으므로:
                    // plankLocalX = -baseV.y * scaleY
                    // plankLocalY = +baseV.x * scaleX
                    // plankLocalZ = +baseV.z * scaleZ
                    float plankLocalX = -baseV.y * scaleY;
                    EvaluateLocalSectionEffect(plankLocalX, out float localDipY, out float localSquash);

                    float deformedX = baseV.x * (1f - localSquash) + (localDipY * invScaleX);
                    float deformedZ = baseV.z * (1f + localSquash);

                    _workingVertices[v] = new Vector3(deformedX, baseV.y, deformedZ);
                }

                node.WorkingMesh.SetVertices(_workingVertices);
            }

            // 2. 통나무 양 끝 단면 캡, 밧줄 매듭, 나무 옹이 등 자식 파츠들을 밟힌 부위 변형에 완벽히 동기화
            for (int i = 0; i < _childPropCount; i++)
            {
                ChildPropNode prop = _childProps[i];
                if (prop.Target == null) continue;

                EvaluateLocalSectionEffect(prop.BaseLocalPos.x, out float localDipY, out float localSquash);

                float posY = prop.BaseLocalPos.y + localDipY;
                if (prop.IsTopAttachment)
                {
                    posY -= prop.BaseLocalPos.y * localSquash;
                }

                prop.Target.localPosition = new Vector3(prop.BaseLocalPos.x, posY, prop.BaseLocalPos.z);

                if (prop.IsEndCap)
                {
                    // Z축 90도 회전된 양 끝 단면 캡: 로컬 X가 수직 높이, 로컬 Z가 수평 폭
                    prop.Target.localScale = new Vector3(
                        prop.BaseLocalScale.x * (1f - localSquash),
                        prop.BaseLocalScale.y,
                        prop.BaseLocalScale.z * (1f + localSquash));
                }
                else if (prop.IsRopeBand)
                {
                    // 기본 회전 상태인 밧줄 밴드: 로컬 Y가 수직 높이, 로컬 Z가 수평 폭
                    prop.Target.localScale = new Vector3(
                        prop.BaseLocalScale.x,
                        prop.BaseLocalScale.y * (1f - localSquash),
                        prop.BaseLocalScale.z * (1f + localSquash));
                }
            }

            _isMeshDeformed = true;
        }

        private void ResetVisualDeformation()
        {
            if (!_isVisualCached || !_isMeshDeformed) return;

            if (_sharedBaseVertices != null)
            {
                for (int n = 0; n < _logNodeCount; n++)
                {
                    if (_logNodes[n].WorkingMesh != null)
                    {
                        _logNodes[n].WorkingMesh.SetVertices(_sharedBaseVertices);
                    }
                }
            }

            for (int i = 0; i < _childPropCount; i++)
            {
                ChildPropNode prop = _childProps[i];
                if (prop.Target == null) continue;
                prop.Target.localPosition = prop.BaseLocalPos;
                prop.Target.localScale = prop.BaseLocalScale;
            }

            _isMeshDeformed = false;
        }

        /// <summary>
        /// 최초 1회 Awake 시점에 통나무 원기둥을 다중 링(Subdivided) 메시로 교체하고 자식 파츠들을 캐싱합니다.
        /// </summary>
        private void CacheAndPrepareVisualNodes()
        {
            if (_isVisualCached) return;

            EnsureSharedTemplateLogMesh();

            int childCount = transform.childCount;
            _logNodes = new DeformableLogNode[childCount];
            _childProps = new ChildPropNode[childCount];
            _logNodeCount = 0;
            _childPropCount = 0;

            for (int i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null) continue;

                string cName = child.name;
                MeshFilter mf = child.GetComponent<MeshFilter>();

                // 메인 가로 통나무 몸통 (Log_Front, Log_Back)
                if (mf != null && (cName == "Log_Front" || cName == "Log_Back"))
                {
                    Mesh workingMesh = Instantiate(_sharedTemplateLogMesh);
                    workingMesh.name = $"{cName}_DeformableMesh";
                    workingMesh.MarkDynamic();
                    mf.sharedMesh = workingMesh;

                    Vector3 lScale = child.localScale;
                    _logNodes[_logNodeCount] = new DeformableLogNode
                    {
                        Target = child,
                        WorkingMesh = workingMesh,
                        BaseLocalScale = lScale,
                        InvScaleX = 1f / Mathf.Max(0.001f, lScale.x),
                        ScaleY = lScale.y
                    };
                    _logNodeCount++;
                }
                else
                {
                    bool isCap = cName.Contains("_Cap_");
                    bool isRope = cName.Contains("Rope_Band");
                    bool isTop = cName.Contains("Knot") || child.localPosition.y > 0.08f;

                    _childProps[_childPropCount] = new ChildPropNode
                    {
                        Target = child,
                        BaseLocalPos = child.localPosition,
                        BaseLocalScale = child.localScale,
                        IsEndCap = isCap,
                        IsRopeBand = isRope,
                        IsTopAttachment = isTop
                    };
                    _childPropCount++;
                }
            }

            if (_sharedBaseVertices != null)
            {
                _workingVertices = new Vector3[_sharedBaseVertices.Length];
                Array.Copy(_sharedBaseVertices, _workingVertices, _sharedBaseVertices.Length);
            }

            _isVisualCached = true;
        }

        /// <summary>
        /// Unity 기본 Cylinder(반지름 0.5, 높이 2.0, Y: -1~+1)와 동일한 규격이면서
        /// 길이 방향(중간 및 양 끝)으로 부드럽게 휘어질 수 있도록 20단 링으로 분할된 공유 원기둥 메시를 생성합니다.
        /// </summary>
        private static void EnsureSharedTemplateLogMesh()
        {
            if (_sharedTemplateLogMesh != null && _sharedBaseVertices != null) return;

            int ringVertCount = RadialSegments + 1;
            int sideVertCount = (HeightSegments + 1) * ringVertCount;
            int capVertCount = (RadialSegments + 2) * 2;
            int totalVerts = sideVertCount + capVertCount;

            Vector3[] verts = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];

            int sideQuadCount = HeightSegments * RadialSegments;
            int capTriCount = RadialSegments * 2;
            int[] tris = new int[sideQuadCount * 6 + capTriCount * 3];

            int vIdx = 0;
            int tIdx = 0;

            // 1. 원기둥 옆면 (Y: -1.0 ~ +1.0 구간을 HeightSegments 단으로 세분화)
            for (int h = 0; h <= HeightSegments; h++)
            {
                float vRatio = (float)h / HeightSegments;
                float py = -1.0f + vRatio * 2.0f;

                for (int r = 0; r <= RadialSegments; r++)
                {
                    float uRatio = (float)r / RadialSegments;
                    float angle = uRatio * Mathf.PI * 2.0f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    verts[vIdx] = new Vector3(cos * 0.5f, py, sin * 0.5f);
                    normals[vIdx] = new Vector3(cos, 0f, sin);
                    uvs[vIdx] = new Vector2(uRatio, vRatio);
                    vIdx++;
                }
            }

            for (int h = 0; h < HeightSegments; h++)
            {
                int rowStart = h * ringVertCount;
                int nextRowStart = (h + 1) * ringVertCount;

                for (int r = 0; r < RadialSegments; r++)
                {
                    int i0 = rowStart + r;
                    int i1 = rowStart + r + 1;
                    int i2 = nextRowStart + r;
                    int i3 = nextRowStart + r + 1;

                    tris[tIdx++] = i0;
                    tris[tIdx++] = i2;
                    tris[tIdx++] = i1;

                    tris[tIdx++] = i1;
                    tris[tIdx++] = i2;
                    tris[tIdx++] = i3;
                }
            }

            // 2. 상단 캡 (Y = +1.0)
            int topCenterIdx = vIdx;
            verts[vIdx] = new Vector3(0f, 1.0f, 0f);
            normals[vIdx] = Vector3.up;
            uvs[vIdx] = new Vector2(0.5f, 0.5f);
            vIdx++;

            for (int r = 0; r <= RadialSegments; r++)
            {
                float uRatio = (float)r / RadialSegments;
                float angle = uRatio * Mathf.PI * 2.0f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts[vIdx] = new Vector3(cos * 0.5f, 1.0f, sin * 0.5f);
                normals[vIdx] = Vector3.up;
                uvs[vIdx] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
                vIdx++;
            }

            for (int r = 0; r < RadialSegments; r++)
            {
                tris[tIdx++] = topCenterIdx;
                tris[tIdx++] = topCenterIdx + 1 + r + 1;
                tris[tIdx++] = topCenterIdx + 1 + r;
            }

            // 3. 하단 캡 (Y = -1.0)
            int bottomCenterIdx = vIdx;
            verts[vIdx] = new Vector3(0f, -1.0f, 0f);
            normals[vIdx] = Vector3.down;
            uvs[vIdx] = new Vector2(0.5f, 0.5f);
            vIdx++;

            for (int r = 0; r <= RadialSegments; r++)
            {
                float uRatio = (float)r / RadialSegments;
                float angle = uRatio * Mathf.PI * 2.0f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts[vIdx] = new Vector3(cos * 0.5f, -1.0f, sin * 0.5f);
                normals[vIdx] = Vector3.down;
                uvs[vIdx] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
                vIdx++;
            }

            for (int r = 0; r < RadialSegments; r++)
            {
                tris[tIdx++] = bottomCenterIdx;
                tris[tIdx++] = bottomCenterIdx + 1 + r;
                tris[tIdx++] = bottomCenterIdx + 1 + r + 1;
            }

            _sharedBaseVertices = new Vector3[totalVerts];
            Array.Copy(verts, _sharedBaseVertices, totalVerts);

            _sharedTemplateLogMesh = new Mesh();
            _sharedTemplateLogMesh.name = "Shared_SubdividedLogCylinder";
            _sharedTemplateLogMesh.vertices = verts;
            _sharedTemplateLogMesh.normals = normals;
            _sharedTemplateLogMesh.uv = uvs;
            _sharedTemplateLogMesh.triangles = tris;
            _sharedTemplateLogMesh.RecalculateBounds();
        }

        #endregion
    }
}
