using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using CampusRun.Core;

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

        [Tooltip("1칸 점프에 걸리는 시간(초)")]
        [SerializeField] private float _hopDuration = 0.18f;

        [Tooltip("점프 시 Y축 최고 정점 높이")]
        [SerializeField] private float _jumpPeakHeight = 0.5f;

        [Tooltip("좌/우 이동 가능한 최소/최대 X 격자 한계")]
        [SerializeField] private int _minGridX = -4;
        [SerializeField] private int _maxGridX = 4;

        [Tooltip("최고 기록 대비 뒤로 후퇴할 수 있는 최대 허용 격자 수")]
        [SerializeField] private int _maxBackwardAllowance = 2;

        [Header("--- 애니메이션 및 연출 ---")]
        [Tooltip("바운스 시 캐릭터 메시를 담고 있는 자식 트랜스폼 (스쿼시 연출용)")]
        [SerializeField] private Transform _visualModelTransform;

        [Tooltip("점프 시 일시적 스쿼시/스트레치 비율 (X, Y, Z)")]
        [SerializeField] private Vector3 _jumpStretchScale = new Vector3(0.8f, 1.25f, 0.8f);
        [SerializeField] private Vector3 _landSquashScale = new Vector3(1.25f, 0.75f, 1.25f);

        [Header("--- 타임아웃 (지체 감지 / 독수리 기믹) ---")]
        [Tooltip("한 자리에 오래 머물 때 시간초과 탈락 활성화 여부 (개발/테스트 중에는 끄는 것을 권장)")]
        [SerializeField] private bool _enableInactivityTimeout = false;

        [Tooltip("한 자리에 오래 머물 경우 시간초과 탈락까지의 제한시간(초)")]
        [SerializeField] private float _inactivityTimeout = 10f;

        // 내부 그리드 좌표 상태
        private int _currentGridX = 0;
        private int _currentGridZ = 0;
        private int _maxReachedGridZ = 0;

        // 상태 플래그
        private bool _isHopping = false;
        private bool _isAlive = true;
        private bool _isControlEnabled = true;
        private float _idleTimer = 0f;

        // 캐싱 변수
        private Vector3 _originalModelScale = Vector3.one;
        private Coroutine _hopCoroutine;

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
                // 사용자 요청: 스케일을 0.9 값으로 고정
                _visualModelTransform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                _originalModelScale = _visualModelTransform.localScale;
                _jumpStretchScale = new Vector3(_originalModelScale.x * 0.85f, _originalModelScale.y * 1.25f, _originalModelScale.z * 0.85f);
                _landSquashScale = new Vector3(_originalModelScale.x * 1.20f, _originalModelScale.y * 0.80f, _originalModelScale.z * 1.20f);
            }

            // 시작 좌표를 그리드에 스냅
            _currentGridX = Mathf.RoundToInt(transform.position.x / _gridSize);
            _currentGridZ = Mathf.RoundToInt(transform.position.z / _gridSize);
            transform.position = new Vector3(_currentGridX * _gridSize, 0f, _currentGridZ * _gridSize);
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

            // 점프 중에는 새로운 방향 입력을 받지 않음
            if (_isHopping) return;

            // 1. 키보드 입력 처리
            ProcessKeyboardInput();

            // 2. 모바일/태블릿 터치 및 스와이프 입력 처리
            ProcessTouchInput();
        }

        #region Input Processing (키보드 & 터치 입력)

        private void ProcessKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
            {
                TryHop(Vector3Int.forward);
            }
            else if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
            {
                TryHop(Vector3Int.back);
            }
            else if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
            {
                TryHop(Vector3Int.left);
            }
            else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
            {
                TryHop(Vector3Int.right);
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
                        if (delta.x > 0) TryHop(Vector3Int.right);
                        else TryHop(Vector3Int.left);
                    }
                    else
                    {
                        // 상하 스와이프 (탭 포함 기본 위로 전진)
                        if (delta.y > 0) TryHop(Vector3Int.forward);
                        else TryHop(Vector3Int.back);
                    }
                }
            }
        }

        #endregion

        #region Hop Movement Logic (포물선 홉 이동 로직)

        /// <summary>
        /// 지정한 그리드 방향으로의 점프 이동을 시도합니다.
        /// </summary>
        public bool TryHop(Vector3Int gridDirection)
        {
            if (_isHopping || !_isAlive || !_isControlEnabled) return false;

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

            // 이동 방향으로 캐릭터 회전
            Vector3 worldDirection = new Vector3(gridDirection.x, 0f, gridDirection.z);
            if (worldDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(worldDirection, Vector3.up);
            }

            // 목표 위치 계산
            Vector3 startPos = transform.position;
            Vector3 targetPos = new Vector3(targetX * _gridSize, 0f, targetZ * _gridSize);

            // 상태 갱신
            _currentGridX = targetX;
            _currentGridZ = targetZ;
            _idleTimer = 0f; // 이동 시 활동 타이머 리셋

            // 최고 기록 전진 여부 확인 (좌우, 뒤로는 점수 변동 없고 오직 앞으로 최고 기록 갱신 시에만 +1점)
            if (_currentGridZ > _maxReachedGridZ)
            {
                _maxReachedGridZ = _currentGridZ;
                GameEvents.TriggerPlayerHopped(_maxReachedGridZ);
                GameEvents.TriggerScoreChanged(_maxReachedGridZ); // 앞으로 전진 1칸당 1점
            }

            GameEvents.TriggerPlayerGridMoved(new Vector3Int(_currentGridX, 0, _currentGridZ));

            // 점프 코루틴 실행
            if (_hopCoroutine != null) StopCoroutine(_hopCoroutine);
            _hopCoroutine = StartCoroutine(HopRoutine(startPos, targetPos));

            return true;
        }

        private IEnumerator HopRoutine(Vector3 startPos, Vector3 targetPos)
        {
            _isHopping = true;
            float elapsed = 0f;

            while (elapsed < _hopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _hopDuration);

                // 수평 위치 선형 보간
                Vector3 currentHorizontal = Vector3.Lerp(startPos, targetPos, t);

                // Y축 포물선 계산 (Sin 곡선: 0 -> 1 -> 0)
                float currentY = Mathf.Sin(t * Mathf.PI) * _jumpPeakHeight;
                transform.position = new Vector3(currentHorizontal.x, currentY, currentHorizontal.z);

                // 스쿼시 & 스트레치 애니메이션 보간
                if (_visualModelTransform != null)
                {
                    if (t < 0.5f)
                    {
                        // 상승 중: 세로로 늘어남 (Stretch)
                        _visualModelTransform.localScale = Vector3.Lerp(_originalModelScale, _jumpStretchScale, t * 2f);
                    }
                    else
                    {
                        // 하강 및 착지 중: 원래 크기로 복귀
                        _visualModelTransform.localScale = Vector3.Lerp(_jumpStretchScale, _originalModelScale, (t - 0.5f) * 2f);
                    }
                }

                yield return null;
            }

            // 착지 완료
            transform.position = targetPos;
            if (_visualModelTransform != null)
            {
                _visualModelTransform.localScale = _originalModelScale;
            }

            _isHopping = false;
        }

        #endregion

        #region Inactivity & Lifecycle Management

        private void CheckInactivityTimer()
        {
            if (!_enableInactivityTimeout) return;

            _idleTimer += Time.deltaTime;

            if (_idleTimer >= _inactivityTimeout)
            {
                // 시간 초과 탈락 (길건너 친구들의 독수리 낚아채기 대응)
                Debug.LogWarning("[CampusRun] 시간 초과! 등굣길에서 너무 오래 망설여 탈락했습니다.");
                Die("시간 초과! 등굣길에서 너무 오래 망설였습니다.");
            }
        }

        /// <summary>
        /// 장애물 충돌 또는 시간초과로 인한 플레이어 사망 처리
        /// </summary>
        public void Die(string deathReason)
        {
            if (!_isAlive) return;

            _isAlive = false;
            _isControlEnabled = false;

            Debug.LogWarning($"[CampusRun] 게임오버! {deathReason} (최종 점수: {_maxReachedGridZ}점)");

            if (_hopCoroutine != null)
            {
                StopCoroutine(_hopCoroutine);
            }

            // 사망 이벤트 발생 (앞으로 전진한 1칸당 1점과 동일하게 전달)
            GameEvents.TriggerPlayerDied(deathReason, _maxReachedGridZ);
            GameEvents.TriggerGameStateChanged(GameState.GameOver);
        }

        private void HandleGameStateChanged(GameState state)
        {
            _isControlEnabled = (state == GameState.Playing);
        }

        private void ResetPlayer()
        {
            if (_hopCoroutine != null) StopCoroutine(_hopCoroutine);

            _currentGridX = 0;
            _currentGridZ = 0;
            _maxReachedGridZ = 0;
            _idleTimer = 0f;
            _isHopping = false;
            _isAlive = true;
            _isControlEnabled = true;

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            if (_visualModelTransform != null)
            {
                _visualModelTransform.localScale = _originalModelScale;
            }
        }

        #endregion
    }
}
