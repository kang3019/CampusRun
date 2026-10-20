using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using CampusRun.Core;
using CampusRun.Obstacles;
using CampusRun.Track;
using CampusRun.Effects;

namespace CampusRun.Player
{
    /// <summary>
    /// 길건너 친구들(Crossy Road) 스타일의 이산 격자 홉(Hop) 이동을 제어하는 플레이어 컨트롤러입니다.
    /// New Input System과 연동되며, 점프 시 포물선 궤적 및 바운스 연출을 수행합니다.
    /// </summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    public class GridPlayerController : MonoBehaviour
    {
        [Header("--- 그리드 및 이동 설정 ---")]
        [Tooltip("격자 1칸의 크기 (World Unit)")]
        [SerializeField] private float _gridSize = 1.0f;

        [Tooltip("1칸 점프에 걸리는 시간(초) - 날렵하고 즉각적인 반응성")]
        [SerializeField] private float _hopDuration = 0.16f;

        [Tooltip("점프 시 Y축 최고 정점 높이 - 과도한 붕 뜸 방지")]
        [SerializeField] private float _jumpPeakHeight = 0.35f;

        [Tooltip("좌/우 이동 가능한 최소/최대 X 격자 한계")]
        [SerializeField] private int _minGridX = -4;
        [SerializeField] private int _maxGridX = 4;

        [Tooltip("최고 기록 대비 뒤로 후퇴할 수 있는 최대 허용 격자 수")]
        [SerializeField] private int _maxBackwardAllowance = 2;

        [Tooltip("일반 도로/인도/안전구역 블럭의 상단 표면 높이 (블럭 두께 0.2m 기준 Y=0.10m 안착하여 발 빠짐 완전 방지)")]
        [SerializeField] private float _groundSurfaceY = 0.10f;

        [Header("--- 조작 민감도 및 인풋 버퍼링 설정 ---")]
        [Tooltip("점프 중 키를 눌렀을 때 착지 즉시 반응하도록 기억하는 선입력 유효 시간(초)")]
        [SerializeField] private float _inputBufferTime = 0.18f;

        [Tooltip("방향키를 꾹 누르고 있을 때의 자동 연속 전진 활성화 여부")]
        [SerializeField] private bool _enableAutoRepeat = true;

        [Tooltip("연속 전진 시작 전 첫 대기 시간(초)")]
        [SerializeField] private float _repeatInitialDelay = 0.20f;

        [Tooltip("연속 전진 반복 간격(초)")]
        [SerializeField] private float _repeatInterval = 0.14f;

        [Header("--- 애니메이션 및 연출 ---")]
        [Tooltip("바운스 시 캐릭터 메시를 담고 있는 자식 트랜스폼 (스쿼시 연출용)")]
        [SerializeField] private Transform _visualModelTransform;

        [Tooltip("점프 시 일시적 스쿼시/스트레치 비율 (X, Y, Z)")]
        [SerializeField] private Vector3 _jumpStretchScale = new Vector3(0.85f, 1.18f, 0.85f);
        [SerializeField] private Vector3 _landSquashScale = new Vector3(1.15f, 0.85f, 1.15f);

        [Header("--- 타임아웃 (지체 감지 / 교수님의 매의 눈 기믹) ---")]
        [Tooltip("한 자리에 오래 머물 때 시간초과 탈락 활성화 여부")]
        [SerializeField] private bool _enableInactivityTimeout = true;

        [Tooltip("한 자리에 오래 머물 경우 시간초과 탈락까지의 제한시간(초) - 기본 6.0초")]
        [SerializeField] private float _inactivityTimeout = 6.0f;

        [Tooltip("지각 위기 경고가 발생하기 시작하는 정체 시간(초) - 기본 3.5초")]
        [SerializeField] private float _warningThreshold = 3.5f;

        // 내부 그리드 좌표 상태
        private int _currentGridX = 0;
        private int _currentGridZ = 0;
        private int _maxReachedGridZ = 0;

        // 상태 플래그
        private bool _isHopping = false;
        private bool _isAlive = true;
        private bool _isControlEnabled = true;
        private float _idleTimer = 0f;
        private bool _hasMovedAtLeastOnce = false;
        private bool _isWarningTriggered = false;

        // 인풋 버퍼링 및 연속 조작 상태 변수
        private Vector3Int? _bufferedHopDirection = null;
        private float _bufferedHopTimer = 0f;
        private float _holdTimer = 0f;
        private float _nextRepeatThreshold = 0f;
        private Vector3Int _currentHeldDirection = Vector3Int.zero;

        // 캐싱 변수
        private Vector3 _originalModelScale = Vector3.one;
        private Coroutine _hopCoroutine;
        private Coroutine _landingBounceCoroutine;
        private Coroutine _drownCoroutine;
        private Coroutine _snatchCoroutine;
        private FloatingPlank _currentMountedPlank;
        private readonly Collider[] _obstacleCheckHits = new Collider[6];

        // 터치 및 스와이프 입력 감지 변수
        private Vector2 _touchStartPos;
        private bool _isSwiping = false;
        private const float MinSwipeDistance = 40f;

        public int CurrentGridX => _currentGridX;
        public int CurrentGridZ => _currentGridZ;
        public int MaxReachedZ => _maxReachedGridZ;
        public bool IsAlive => _isAlive;

        private void Awake()
        {
            if (_visualModelTransform == null && transform.childCount > 0)
            {
                _visualModelTransform = transform.GetChild(0);
            }

            if (_visualModelTransform != null)
            {
                // 사용자 요청: 캐릭터 스케일을 0.86으로 설정
                _visualModelTransform.localScale = new Vector3(0.86f, 0.86f, 0.86f);
                _originalModelScale = _visualModelTransform.localScale;
                _jumpStretchScale = new Vector3(_originalModelScale.x * 0.85f, _originalModelScale.y * 1.25f, _originalModelScale.z * 0.85f);
                _landSquashScale = new Vector3(_originalModelScale.x * 1.20f, _originalModelScale.y * 0.80f, _originalModelScale.z * 1.20f);
            }

            // 시작 좌표를 그리드에 스냅 및 바닥 블럭 상단 표면 높이로 배치
            _currentGridX = Mathf.RoundToInt(transform.position.x / _gridSize);
            _currentGridZ = Mathf.RoundToInt(transform.position.z / _gridSize);
            transform.position = new Vector3(_currentGridX * _gridSize, _groundSurfaceY, _currentGridZ * _gridSize);
        }

        private void OnEnable()
        {
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnGameRestarted += ResetPlayer;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnGameRestarted -= ResetPlayer;
        }

        private void Update()
        {
            if (!_isAlive || !_isControlEnabled) return;

            // 지체 시간 체크 (오랫동안 망설이면 타임아웃 위험)
            CheckInactivityTimer();

            // 1. 선입력 버퍼 수명 타이머 차감
            if (_bufferedHopTimer > 0f)
            {
                _bufferedHopTimer -= Time.deltaTime;
                if (_bufferedHopTimer <= 0f)
                {
                    _bufferedHopDirection = null;
                }
            }

            // 2. 입력 처리 (점프 중이어도 선입력 버퍼에 담을 수 있도록 상시 수신)
            ProcessKeyboardInput();
            ProcessTouchInput();

            // 3. 점프 중이 아니라면 대기 중인 버퍼 입력 즉시 실행
            if (!_isHopping)
            {
                ConsumeBufferedInputIfReady();
            }
        }

        #region Input Processing (키보드 & 터치 입력 및 인풋 버퍼링)

        /// <summary>
        /// 키보드 방향키 입력을 감지합니다. 즉시 단발 입력 및 꾹 누르고 있을 때의 자동 연속 홉을 모두 지원합니다.
        /// </summary>
        private void ProcessKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector3Int pressedDir = Vector3Int.zero;
            bool wasPressedThisFrame = false;

            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
            {
                pressedDir = Vector3Int.forward;
                wasPressedThisFrame = true;
            }
            else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                pressedDir = Vector3Int.back;
                wasPressedThisFrame = true;
            }
            else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                pressedDir = Vector3Int.left;
                wasPressedThisFrame = true;
            }
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                pressedDir = Vector3Int.right;
                wasPressedThisFrame = true;
            }

            // 이번 프레임에 키가 새로 눌린 경우: 즉시 반응 또는 선입력 버퍼 등록
            if (wasPressedThisFrame)
            {
                RegisterHopInput(pressedDir);
                _currentHeldDirection = pressedDir;
                _holdTimer = 0f;
                _nextRepeatThreshold = _repeatInitialDelay;
                return;
            }

            // 방향키를 꾹 누르고 있을 때의 연속 홉 처리
            if (_enableAutoRepeat)
            {
                Vector3Int heldDir = Vector3Int.zero;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed || keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed) heldDir = Vector3Int.forward;
                else if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) heldDir = Vector3Int.back;
                else if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) heldDir = Vector3Int.left;
                else if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) heldDir = Vector3Int.right;

                if (heldDir != Vector3Int.zero && heldDir == _currentHeldDirection)
                {
                    _holdTimer += Time.deltaTime;
                    if (_holdTimer >= _nextRepeatThreshold)
                    {
                        RegisterHopInput(heldDir);
                        _nextRepeatThreshold += _repeatInterval;
                    }
                }
                else if (heldDir != Vector3Int.zero)
                {
                    _currentHeldDirection = heldDir;
                    _holdTimer = 0f;
                    _nextRepeatThreshold = _repeatInitialDelay;
                }
                else
                {
                    _currentHeldDirection = Vector3Int.zero;
                    _holdTimer = 0f;
                }
            }
        }

        private void ProcessTouchInput()
        {
            var touchScreen = Touchscreen.current;
            if (touchScreen == null || !touchScreen.primaryTouch.press.isPressed)
            {
                if (_isSwiping)
                {
                    _isSwiping = false;
                }
                return;
            }

            // 터치 시작 시점 기록
            if (touchScreen.primaryTouch.press.wasPressedThisFrame)
            {
                _touchStartPos = touchScreen.primaryTouch.position.ReadValue();
                _isSwiping = true;
                return;
            }

            // 스와이프 도달 여부 검사
            if (_isSwiping)
            {
                Vector2 currentPos = touchScreen.primaryTouch.position.ReadValue();
                Vector2 delta = currentPos - _touchStartPos;

                if (delta.magnitude >= MinSwipeDistance)
                {
                    _isSwiping = false; // 1회 스와이프 소비

                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                    {
                        // 좌우 스와이프
                        if (delta.x > 0) RegisterHopInput(Vector3Int.right);
                        else RegisterHopInput(Vector3Int.left);
                    }
                    else
                    {
                        // 상하 스와이프 (탭 포함 기본 위로 전진)
                        if (delta.y > 0) RegisterHopInput(Vector3Int.forward);
                        else RegisterHopInput(Vector3Int.back);
                    }
                }
            }
        }

        /// <summary>
        /// 사용자 입력을 전달받아, 현재 점프 중이면 선입력 버퍼에 저장하고 여유 상태이면 즉시 뜁니다.
        /// </summary>
        private void RegisterHopInput(Vector3Int gridDirection)
        {
            if (!_isAlive || !_isControlEnabled) return;

            if (!_isHopping)
            {
                TryHop(gridDirection);
            }
            else
            {
                // 점프 체공 중에 들어온 입력은 버퍼에 보관 -> 착지 순간 0ms 딜레이로 즉시 도약!
                _bufferedHopDirection = gridDirection;
                _bufferedHopTimer = _inputBufferTime;
            }
        }

        /// <summary>
        /// 버퍼에 유효한 선입력이 대기 중이고 캐릭터가 착지 상태라면 즉시 다음 홉을 실행합니다.
        /// </summary>
        private bool ConsumeBufferedInputIfReady()
        {
            if (_isHopping || !_isAlive || !_isControlEnabled) return false;

            if (_bufferedHopDirection.HasValue && _bufferedHopTimer > 0f)
            {
                Vector3Int dir = _bufferedHopDirection.Value;
                _bufferedHopDirection = null;
                _bufferedHopTimer = 0f;
                return TryHop(dir);
            }

            return false;
        }

        #endregion

        #region Hop Movement Logic (포물선 홉 이동 로직)

        /// <summary>
        /// 지정한 그리드 방향으로의 점프 이동을 시도합니다.
        /// </summary>
        public bool TryHop(Vector3Int gridDirection)
        {
            if (_isHopping || !_isAlive || !_isControlEnabled) return false;

            // 이전 착지 바운스 연출이 돌고 있었다면 즉시 중단하고 새 점프 도약으로 전환
            if (_landingBounceCoroutine != null)
            {
                StopCoroutine(_landingBounceCoroutine);
                _landingBounceCoroutine = null;
            }

            int targetX = _currentGridX + gridDirection.x;
            int targetZ = _currentGridZ + gridDirection.z;

            // 좌우 한계선 검증
            if (targetX < _minGridX || targetX > _maxGridX)
            {
                return false;
            }

            // 뒤로 후퇴 가능한 한계선 검증 (최고 기록 대비 너무 뒤로 가지 못하게 방지)
            if (targetZ < _maxReachedGridZ - _maxBackwardAllowance)
            {
                return false;
            }

            // 기존에 탑승 중이던 널판지에서 이탈
            if (_currentMountedPlank != null)
            {
                _currentMountedPlank.UnmountPlayer(this);
                _currentMountedPlank = null;
            }

            // 이동 방향으로 캐릭터 회전 목표 계산
            Vector3 worldDirection = new Vector3(gridDirection.x, 0f, gridDirection.z);
            Quaternion startRot = transform.rotation;
            Quaternion targetRot = (worldDirection != Vector3.zero)
                ? Quaternion.LookRotation(worldDirection, Vector3.up)
                : transform.rotation;

            // 목표 위치 및 높이 계산 (통나무 위는 통나무 윗면, 일반 지상은 도로/인도 블럭 상단 표면 높이로 점프)
            Vector3 startPos = transform.position;
            float targetY = _groundSurfaceY;

            Vector3 checkCenter = new Vector3(targetX * _gridSize, 0.25f, targetZ * _gridSize);
            int targetHits = Physics.OverlapSphereNonAlloc(checkCenter, 0.45f, _obstacleCheckHits);
            for (int i = 0; i < targetHits; i++)
            {
                Collider hit = _obstacleCheckHits[i];
                if (hit == null) continue;
                FloatingPlank plank = hit.GetComponentInParent<FloatingPlank>();
                if (plank != null)
                {
                    targetY = plank.TopSurfaceY;
                    break;
                }
            }

            Vector3 targetPos = new Vector3(targetX * _gridSize, targetY, targetZ * _gridSize);

            // 목표 칸에 방치된 킥보드 등 고정 장애물이 있는지 비할당 검사
            if (IsObstacleAt(new Vector3(targetPos.x, _groundSurfaceY, targetPos.z)))
            {
                if (_hopCoroutine != null) StopCoroutine(_hopCoroutine);
                _hopCoroutine = StartCoroutine(BlockedHopRoutine(startPos, worldDirection));
                return false;
            }

            // 상태 갱신
            _currentGridX = targetX;
            _currentGridZ = targetZ;

            // 첫 방향키 입력으로 게임 시작 트리거 ("지각 5분전" 타이틀 닫고 인게임 시작)
            if (!_hasMovedAtLeastOnce)
            {
                _hasMovedAtLeastOnce = true;
                GameEvents.TriggerGameStarted();
                GameEvents.TriggerGameStateChanged(GameState.Playing);
            }

            _idleTimer = 0f; // 이동 시 활동 타이머 리셋

            // 기존에 타고 있던 널판지가 있다면 탑승 해제 (새로운 칸으로 도약)
            if (_currentMountedPlank != null)
            {
                _currentMountedPlank.UnmountPlayer(this);
                _currentMountedPlank = null;
            }

            if (_isWarningTriggered)
            {
                _isWarningTriggered = false;
                GameEvents.TriggerInactivityWarning(false);
            }

            // 최고 기록 전진 여부 확인 (앞으로 최고 기록 갱신 시 1칸당 10m 누적)
            if (_currentGridZ > _maxReachedGridZ)
            {
                _maxReachedGridZ = _currentGridZ;
                GameEvents.TriggerPlayerHopped(_maxReachedGridZ); // 레인 생성을 위한 그리드 Z 좌표 전달
                GameEvents.TriggerScoreChanged(_maxReachedGridZ * 10); // UI 표시를 위한 거리 단위(1칸당 10m)
            }

            GameEvents.TriggerPlayerGridMoved(new Vector3Int(_currentGridX, 0, _currentGridZ));

            // 점프 코루틴 실행
            if (_hopCoroutine != null) StopCoroutine(_hopCoroutine);
            _hopCoroutine = StartCoroutine(HopRoutine(startPos, targetPos, startRot, targetRot));

            return true;
        }

        private bool IsObstacleAt(Vector3 targetPos)
        {
            Vector3 checkCenter = targetPos + Vector3.up * 0.3f;
            int hitCount = Physics.OverlapSphereNonAlloc(checkCenter, 0.35f, _obstacleCheckHits);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _obstacleCheckHits[i];
                if (hit == null) continue;

                if (hit.GetComponent<StationaryObstacle>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator BlockedHopRoutine(Vector3 startPos, Vector3 direction)
        {
            _isHopping = true;
            float duration = _hopDuration * 0.7f;
            float elapsed = 0f;
            Vector3 nudgePos = startPos + direction * 0.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 부딪히며 제자리로 튕김
                float forwardT = Mathf.Sin(t * Mathf.PI);
                transform.position = Vector3.Lerp(startPos, nudgePos, forwardT);

                // 스쿼시 연출
                if (_visualModelTransform != null)
                {
                    _visualModelTransform.localScale = Vector3.Lerp(_originalModelScale, _landSquashScale, forwardT);
                }

                yield return null;
            }

            transform.position = startPos;
            if (_visualModelTransform != null)
            {
                _visualModelTransform.localScale = _originalModelScale;
            }
            _isHopping = false;

            // 막혀서 튕겨 돌아온 후에도 버퍼에 다른 유효 입력이 있으면 즉시 시도
            ConsumeBufferedInputIfReady();
        }

        private IEnumerator HopRoutine(Vector3 startPos, Vector3 targetPos, Quaternion startRot, Quaternion targetRot)
        {
            _isHopping = true;
            float elapsed = 0f;

            while (elapsed < _hopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _hopDuration);

                // 1. 회전 보간: 점프 시작 직후 타겟 방향으로 부드럽고 신속하게 회전
                float rotT = Mathf.Clamp01(t * 2.5f);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, rotT);

                // 2. 수평 위치: 부드러운 가감속(SmoothStep)으로 로봇 같은 등속 이동 방지
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 currentHorizontal = Vector3.Lerp(startPos, targetPos, smoothT);

                // 3. Y축 포물선 계산 (시작 높이와 목표 착지 높이 사이를 보간하여 통나무 등 높이 차이 자연스럽게 극복)
                float baseY = Mathf.Lerp(startPos.y, targetPos.y, smoothT);
                float currentY = baseY + Mathf.Sin(t * Mathf.PI) * _jumpPeakHeight;
                transform.position = new Vector3(currentHorizontal.x, currentY, currentHorizontal.z);

                // 4. 모델 스쿼시/스트레치 및 전방 틸트(기울기) 연출
                if (_visualModelTransform != null)
                {
                    if (t < 0.5f)
                    {
                        // 상승 중: 세로로 늘어남 (Stretch)
                        _visualModelTransform.localScale = Vector3.Lerp(_originalModelScale, _jumpStretchScale, t * 2f);
                    }
                    else
                    {
                        // 하강 중: 기본 크기로 복귀
                        _visualModelTransform.localScale = Vector3.Lerp(_jumpStretchScale, _originalModelScale, (t - 0.5f) * 2f);
                    }

                    // 점프 시 진행 방향으로 살짝 몸을 숙였다가(최대 6도) 착지 시 세움
                    float tiltAngle = Mathf.Sin(t * Mathf.PI) * 6f;
                    _visualModelTransform.localRotation = Quaternion.Euler(tiltAngle, 0f, 0f);
                }

                yield return null;
            }

            // 착지 스냅
            transform.position = targetPos;
            transform.rotation = targetRot;

            // 착지 즉시 조작 잠금을 해제하여 반응성 극대화 (입력 지연 0ms)
            _isHopping = false;

            // 착지점의 지형(물웅덩이/널판지) 검사
            CheckLandingGround(targetPos);

            if (_isAlive)
            {
                // 생존 상태로 지면에 착지했을 때 은은하고 작은 미니 도트 모래먼지 연출
                ImpactVFXManager.PlayDustPuff(targetPos);

                // 선입력 버퍼에 대기 중인 다음 입력이 있다면 지체 없이 즉시 다음 점프 개시
                if (!ConsumeBufferedInputIfReady())
                {
                    // 대기 중인 다음 입력이 없을 때만 쫀득한 착지 젤리 바운스 연출 실행
                    if (_landingBounceCoroutine != null) StopCoroutine(_landingBounceCoroutine);
                    _landingBounceCoroutine = StartCoroutine(LandingBounceRoutine());
                }
            }
        }

        /// <summary>
        /// 착지 순간 시각적 만족감을 주는 쫀득한 젤리 스쿼시/리바운드 연출입니다.
        /// 조작 플래그(_isHopping)를 잠그지 않으므로 연타 시 조작감이 씹히지 않습니다.
        /// </summary>
        private IEnumerator LandingBounceRoutine()
        {
            if (_visualModelTransform == null) yield break;

            _visualModelTransform.localRotation = Quaternion.identity;

            // 1단계: 바닥에 닿는 순간 쿵! 찰떡처럼 바닥으로 눌림 (Squash, 0.04초)
            float squashDuration = 0.04f;
            float sqElapsed = 0f;
            while (sqElapsed < squashDuration)
            {
                sqElapsed += Time.deltaTime;
                float st = Mathf.Clamp01(sqElapsed / squashDuration);
                _visualModelTransform.localScale = Vector3.Lerp(_originalModelScale, _landSquashScale, st);
                yield return null;
            }

            // 2단계: 원래 크기로 뿅! 탄성 있게 복귀 (Rebound, 0.05초)
            float bounceDuration = 0.05f;
            float bElapsed = 0f;
            while (bElapsed < bounceDuration)
            {
                bElapsed += Time.deltaTime;
                float bt = Mathf.Clamp01(bElapsed / bounceDuration);
                _visualModelTransform.localScale = Vector3.Lerp(_landSquashScale, _originalModelScale, Mathf.SmoothStep(0f, 1f, bt));
                yield return null;
            }

            _visualModelTransform.localScale = _originalModelScale;
            _landingBounceCoroutine = null;
        }

        /// <summary>
        /// 착지한 칸의 바닥이 거대 물웅덩이인지, 떠다니는 널판지 위인지 판정합니다.
        /// </summary>
        private void CheckLandingGround(Vector3 landedPos)
        {
            if (!_isAlive) return;

            Vector3 checkCenter = landedPos + Vector3.up * 0.25f;
            int hitCount = Physics.OverlapSphereNonAlloc(checkCenter, 0.45f, _obstacleCheckHits);

            FloatingPlank foundPlank = null;
            bool isWaterGround = false;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _obstacleCheckHits[i];
                if (hit == null) continue;

                FloatingPlank plank = hit.GetComponentInParent<FloatingPlank>();
                if (plank != null)
                {
                    foundPlank = plank;
                }

                if (hit.GetComponentInParent<WaterLane>() != null)
                {
                    isWaterGround = true;
                }
            }

            // 1. 널판지 위에 착지 성공 -> 탑승 및 표류 동기화 (몸이 끼지 않고 통나무 윗면에 정확히 발을 딛음)
            if (foundPlank != null)
            {
                _currentMountedPlank = foundPlank;
                _currentMountedPlank.MountPlayer(this);
                transform.position = new Vector3(transform.position.x, foundPlank.TopSurfaceY, transform.position.z);
                return;
            }

            // 2. 널판지가 없는데 거대 물웅덩이 레인에 떨어진 경우 -> 퐁당 익사 탈락
            if (isWaterGround)
            {
                ImpactVFXManager.PlayWaterSplash(landedPos);
                CameraShake.ShakeOnWaterSplash();

                if (_drownCoroutine != null) StopCoroutine(_drownCoroutine);
                _drownCoroutine = StartCoroutine(DrownRoutine());
                Die("🌊 비 온 뒤 거대 물웅덩이에 빠져 지각 탈락했습니다!");
                return;
            }

            // 3. 일반 지상 블럭(도로, 인도, 안전구역) 착지 -> 바닥 블럭 상단 표면(0.10m) 위로 발바닥 완벽 안착
            transform.position = new Vector3(transform.position.x, _groundSurfaceY, transform.position.z);
        }

        /// <summary>
        /// 널판지가 이동할 때 플레이어도 동일한 이동량만큼 X축으로 부드럽게 표류(Drift)시키며 통나무 윗면 높이를 유지합니다.
        /// </summary>
        public void ApplyPlankDrift(float deltaX, float plankSurfaceY)
        {
            if (!_isAlive || _isHopping) return;

            transform.position = new Vector3(transform.position.x + deltaX, plankSurfaceY, transform.position.z);

            // 화면 좌우 한계를 너무 많이 벗어나면 화면 밖 표류 탈락
            float currentX = transform.position.x;
            if (currentX < (_minGridX - 1.2f) * _gridSize || currentX > (_maxGridX + 1.2f) * _gridSize)
            {
                Die("[!] 널판지를 타고 화면 밖으로 떠내려가 지각 탈락했습니다!");
                return;
            }

            // 현재 위치를 가장 가까운 그리드 정수로 동기화 (다음 점프 기준점)
            _currentGridX = Mathf.RoundToInt(currentX / _gridSize);
        }

        public void ApplyPlankDrift(float deltaX)
        {
            float targetY = (_currentMountedPlank != null) ? _currentMountedPlank.TopSurfaceY : transform.position.y;
            ApplyPlankDrift(deltaX, targetY);
        }

        private IEnumerator DrownRoutine()
        {
            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 sinkPos = startPos + Vector3.down * 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 물속으로 가라앉음
                transform.position = Vector3.Lerp(startPos, sinkPos, t);

                // 스케일 축소
                if (_visualModelTransform != null)
                {
                    _visualModelTransform.localScale = Vector3.Lerp(_originalModelScale, Vector3.zero, t);
                }

                yield return null;
            }
        }

        #endregion

        #region Inactivity & Lifecycle Management

        private void CheckInactivityTimer()
        {
            if (!_enableInactivityTimeout || !_hasMovedAtLeastOnce || !_isAlive) return;

            // 이미 매에 낚아채이는 중이면 타이머 중단
            if (_snatchCoroutine != null) return;

            _idleTimer += Time.deltaTime;

            // 1단계: 3.5초 이상 지체 시 경고 이벤트 발행 (교수님 접근) 및 실시간 남은 초 갱신
            if (_idleTimer >= _warningThreshold)
            {
                if (!_isWarningTriggered)
                {
                    _isWarningTriggered = true;
                    GameEvents.TriggerInactivityWarning(true);
                }

                float remaining = Mathf.Max(0f, _inactivityTimeout - _idleTimer);
                float progress = Mathf.Clamp01((_idleTimer - _warningThreshold) / (_inactivityTimeout - _warningThreshold));
                GameEvents.TriggerInactivityTimerUpdated(remaining, progress);
            }

            // 2단계: 6.0초 제한시간 초과 시 '교수님의 매의 눈' 급습 낚아채기 연출 발동 후 F학점 탈락
            if (_idleTimer >= _inactivityTimeout)
            {
                _snatchCoroutine = StartCoroutine(HawkSnatchRoutine());
            }
        }

        /// <summary>
        /// 단독 빌드(.exe)에서도 셰이더 스트리핑으로 인해 핑크색(마젠타)으로 깨지지 않도록
        /// 씬에 이미 로드된 플레이어의 URP 셰이더를 기반으로 단색 머티리얼을 안전하게 생성합니다.
        /// </summary>
        private Material CreateHawkMaterial(Shader baseShader, Color color)
        {
            Material mat = new Material(baseShader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", 0.1f);
            }
            return mat;
        }

        private GameObject CreateHawkPart(string partName, Transform parent, Vector3 localPos, Vector3 localScale, Material sharedMat)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;

            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null && sharedMat != null)
            {
                rend.sharedMaterial = sharedMat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return part;
        }

        /// <summary>
        /// 길건너 친구들 특유의 '독수리 낚아채기'를 패러디한 '교수님의 매 급습' 탈락 연출입니다.
        /// 하늘 뒤편에서 매가 쏜살같이 급강하하여 플레이어를 발톱으로 낚아채 하늘 위로 데려갑니다.
        /// </summary>
        private IEnumerator HawkSnatchRoutine()
        {
            _isControlEnabled = false;

            if (_isWarningTriggered)
            {
                _isWarningTriggered = false;
                GameEvents.TriggerInactivityWarning(false);
            }

            // .exe 빌드에서도 절대 깨지지 않도록 현재 캐릭터가 사용 중인 검증된 URP 셰이더를 우선 참조
            Shader validShader = null;
            Renderer playerRend = GetComponentInChildren<Renderer>();
            if (playerRend != null && playerRend.sharedMaterial != null && playerRend.sharedMaterial.shader != null)
            {
                validShader = playerRend.sharedMaterial.shader;
            }
            if (validShader == null) validShader = Shader.Find("Universal Render Pipeline/Lit");
            if (validShader == null) validShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (validShader == null) validShader = Shader.Find("Sprites/Default");

            Material matDarkBrown = CreateHawkMaterial(validShader, new Color(0.26f, 0.16f, 0.09f));
            Material matWingBrown = CreateHawkMaterial(validShader, new Color(0.20f, 0.12f, 0.06f));
            Material matWhiteHead = CreateHawkMaterial(validShader, new Color(0.95f, 0.94f, 0.90f));
            Material matGoldBeak = CreateHawkMaterial(validShader, new Color(1.0f, 0.76f, 0.12f));
            Material matBlackEye = CreateHawkMaterial(validShader, new Color(0.08f, 0.08f, 0.08f));

            // 1. 교수님의 매(독수리) 복셀 모델 조립
            GameObject hawkObj = new GameObject("[Hawk_Professor_Snatcher]");

            // 몸통 & 가슴
            CreateHawkPart("Body", hawkObj.transform, new Vector3(0f, 0f, 0f), new Vector3(0.75f, 0.48f, 1.15f), matDarkBrown);
            // 흰색 독수리 머리
            CreateHawkPart("Head", hawkObj.transform, new Vector3(0f, 0.22f, 0.52f), new Vector3(0.56f, 0.46f, 0.54f), matWhiteHead);
            // 매서운 황금 부리 (윗부리 + 갈고리 끝)
            CreateHawkPart("Beak_Upper", hawkObj.transform, new Vector3(0f, 0.16f, 0.88f), new Vector3(0.26f, 0.20f, 0.32f), matGoldBeak);
            CreateHawkPart("Beak_Hook", hawkObj.transform, new Vector3(0f, 0.06f, 0.98f), new Vector3(0.20f, 0.16f, 0.14f), matGoldBeak);
            // 양쪽 눈
            CreateHawkPart("Eye_L", hawkObj.transform, new Vector3(-0.29f, 0.26f, 0.60f), new Vector3(0.05f, 0.10f, 0.10f), matBlackEye);
            CreateHawkPart("Eye_R", hawkObj.transform, new Vector3(0.29f, 0.26f, 0.60f), new Vector3(0.05f, 0.10f, 0.10f), matBlackEye);
            // 흰색 꼬리 날개
            CreateHawkPart("Tail", hawkObj.transform, new Vector3(0f, 0.05f, -0.72f), new Vector3(0.65f, 0.14f, 0.45f), matWhiteHead);
            // 발톱 (좌/우)
            CreateHawkPart("Talon_L", hawkObj.transform, new Vector3(-0.22f, -0.32f, 0.15f), new Vector3(0.14f, 0.22f, 0.18f), matGoldBeak);
            CreateHawkPart("Talon_R", hawkObj.transform, new Vector3(0.22f, -0.32f, 0.15f), new Vector3(0.14f, 0.22f, 0.18f), matGoldBeak);

            // 좌/우 날개 피벗 (날개짓 애니메이션용)
            GameObject leftWingPivot = new GameObject("WingPivot_L");
            leftWingPivot.transform.SetParent(hawkObj.transform, false);
            leftWingPivot.transform.localPosition = new Vector3(-0.35f, 0.12f, 0.05f);
            CreateHawkPart("Wing_L_Inner", leftWingPivot.transform, new Vector3(-0.55f, 0f, 0f), new Vector3(1.15f, 0.12f, 0.72f), matWingBrown);
            CreateHawkPart("Wing_L_Tip", leftWingPivot.transform, new Vector3(-1.25f, 0.02f, -0.06f), new Vector3(0.55f, 0.09f, 0.54f), matDarkBrown);

            GameObject rightWingPivot = new GameObject("WingPivot_R");
            rightWingPivot.transform.SetParent(hawkObj.transform, false);
            rightWingPivot.transform.localPosition = new Vector3(0.35f, 0.12f, 0.05f);
            CreateHawkPart("Wing_R_Inner", rightWingPivot.transform, new Vector3(0.55f, 0f, 0f), new Vector3(1.15f, 0.12f, 0.72f), matWingBrown);
            CreateHawkPart("Wing_R_Tip", rightWingPivot.transform, new Vector3(1.25f, 0.02f, -0.06f), new Vector3(0.55f, 0.09f, 0.54f), matDarkBrown);

            Vector3 playerPos = transform.position;
            Vector3 startPos = playerPos + new Vector3(-2.5f, 7.5f, -6.5f);
            Vector3 snatchPos = playerPos + new Vector3(0f, 0.95f, 0f);
            Vector3 escapePos = playerPos + new Vector3(2.5f, 11.5f, 10.5f);

            hawkObj.transform.position = startPos;
            hawkObj.transform.LookAt(snatchPos);

            // 1단계: 0.28초 만에 하늘 뒤편에서 급강하(Swoop Down)
            float swoopDuration = 0.28f;
            float elapsed = 0f;
            while (elapsed < swoopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swoopDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                hawkObj.transform.position = Vector3.Lerp(startPos, snatchPos, smoothT);
                hawkObj.transform.LookAt(snatchPos);

                // 급강하 중 날개 각도 연출
                float flapAngle = Mathf.Sin(elapsed * 45f) * 22f;
                leftWingPivot.transform.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);
                rightWingPivot.transform.localRotation = Quaternion.Euler(0f, 0f, flapAngle);
                yield return null;
            }

            // 2단계: 플레이어 낚아채기 (SetParent로 인한 회전/스케일 깨짐 없이 발톱 위치에 직접 동기화)
            hawkObj.transform.position = snatchPos;
            hawkObj.transform.LookAt(escapePos);
            CameraShake.ShakeOnCarCrash();

            // 3단계: 0.40초 동안 힘차게 날개짓하며 하늘 높이 솟구쳐 도주(Escape Skyward)
            float flyDuration = 0.40f;
            elapsed = 0f;
            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 currentHawkPos = Vector3.Lerp(snatchPos, escapePos, smoothT);
                hawkObj.transform.position = currentHawkPos;

                // 플레이어가 독수리 발톱 아래에 매달려 함께 올라감 (회전/스케일 왜곡 방지)
                transform.position = currentHawkPos + new Vector3(0f, -0.75f, 0f);

                float flapAngle = Mathf.Sin(elapsed * 55f) * 32f;
                leftWingPivot.transform.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);
                rightWingPivot.transform.localRotation = Quaternion.Euler(0f, 0f, flapAngle);
                yield return null;
            }

            // 4단계: 탈락 확정 및 임시 머티리얼/오브젝트 메모리 정리
            if (hawkObj != null) Destroy(hawkObj);
            Destroy(matDarkBrown);
            Destroy(matWingBrown);
            Destroy(matWhiteHead);
            Destroy(matGoldBeak);
            Destroy(matBlackEye);

            Die("[!] 교수님의 매의 눈: 1교시 지각으로 강제 F학점을 받았습니다!");
            _snatchCoroutine = null;
        }

        /// <summary>
        /// 장애물 충돌 또는 시간초과로 인한 플레이어 사망 처리
        /// </summary>
        public void Die(string deathReason)
        {
            if (!_isAlive) return;

            _isAlive = false;
            _isControlEnabled = false;

            if (_currentMountedPlank != null)
            {
                _currentMountedPlank.UnmountPlayer(this);
                _currentMountedPlank = null;
            }

            if (_isWarningTriggered)
            {
                _isWarningTriggered = false;
                GameEvents.TriggerInactivityWarning(false);
            }

            Debug.LogWarning($"[CampusRun] 게임오버! {deathReason} (최종 거리: {_maxReachedGridZ * 10}m)");

            if (_hopCoroutine != null)
            {
                StopCoroutine(_hopCoroutine);
            }

            if (_visualModelTransform != null)
            {
                _visualModelTransform.localRotation = Quaternion.identity;
                _visualModelTransform.localScale = _originalModelScale;
            }

            // 사망 이벤트 발생 (앞으로 전진한 1칸당 10m 기준으로 전달)
            GameEvents.TriggerPlayerDied(deathReason, _maxReachedGridZ * 10);
            GameEvents.TriggerGameStateChanged(GameState.GameOver);
        }

        private void HandleGameStateChanged(GameState state)
        {
            _isControlEnabled = (state == GameState.Playing);
        }

        private void ResetPlayer()
        {
            if (_hopCoroutine != null) StopCoroutine(_hopCoroutine);
            if (_landingBounceCoroutine != null)
            {
                StopCoroutine(_landingBounceCoroutine);
                _landingBounceCoroutine = null;
            }
            if (_drownCoroutine != null) StopCoroutine(_drownCoroutine);

            if (_snatchCoroutine != null)
            {
                StopCoroutine(_snatchCoroutine);
                _snatchCoroutine = null;
                GameObject existingHawk = GameObject.Find("[Hawk_Professor_Snatcher]");
                if (existingHawk != null) Destroy(existingHawk);
                transform.SetParent(null, true);
            }

            if (_currentMountedPlank != null)
            {
                _currentMountedPlank.UnmountPlayer(this);
                _currentMountedPlank = null;
            }

            _currentGridX = 0;
            _currentGridZ = 0;
            _maxReachedGridZ = 0;
            _idleTimer = 0f;
            _hasMovedAtLeastOnce = false;
            _isWarningTriggered = false;
            GameEvents.TriggerInactivityWarning(false);

            _bufferedHopDirection = null;
            _bufferedHopTimer = 0f;
            _holdTimer = 0f;
            _nextRepeatThreshold = 0f;
            _currentHeldDirection = Vector3Int.zero;

            _isHopping = false;
            _isAlive = true;
            _isControlEnabled = true;

            transform.position = new Vector3(0f, _groundSurfaceY, 0f);
            transform.rotation = Quaternion.identity;

            if (_visualModelTransform != null)
            {
                _visualModelTransform.localRotation = Quaternion.identity;
                _visualModelTransform.localScale = _originalModelScale;
            }
        }

        #endregion
    }
}
