using UnityEngine;
using CampusRun.Core;
using CampusRun.Effects;

namespace CampusRun.Player
{
    /// <summary>
    /// 길건너 친구들 스타일의 대각선 45도 쿼터뷰(아이소메트릭) 시점으로 플레이어를 부드럽게 추격하는 카메라입니다.
    /// 플레이어의 전진(Z축)에 맞춰 시야를 유지하며, 좌우(X축) 이동은 완만하게 보간합니다.
    /// </summary>
    public enum CameraViewMode
    {
        [Tooltip("길건너 친구들 표준 좌측 대각선 쿼터뷰 (우상향 전진)")]
        IsometricLeft,
        [Tooltip("우측 대각선 쿼터뷰 (좌상향 전진)")]
        IsometricRight,
        [Tooltip("정후방 직관 조감 시점")]
        Straight,
        [Tooltip("사용자 직접 지정 오프셋")]
        Custom
    }

    public class FollowCamera : MonoBehaviour
    {
        [Header("--- 시점 모드 설정 ---")]
        [Tooltip("시점 프리셋 (대각선 쿼터뷰 / 정면 뷰)")]
        [SerializeField] private CameraViewMode _viewMode = CameraViewMode.IsometricLeft;

        [Header("--- 추격 대상 및 오프셋 ---")]
        [Tooltip("추격할 플레이어의 Transform (비어있으면 자동 탐색)")]
        [SerializeField] private Transform _target;

        [Tooltip("플레이어 기준 카메라의 상대 위치 (기본: X=-3.8, Y=6.4, Z=-6.8)")]
        [SerializeField] private Vector3 _offset = new Vector3(-3.8f, 6.4f, -6.8f);

        [Header("--- 카메라 시점 각도 (눈높이 조절) ---")]
        [Tooltip("상하 내려다보는 각도 (작을수록 눈높이에 가까워짐, 기본: 36도)")]
        [Range(15f, 60f)]
        [SerializeField] private float _pitchAngle = 36f;

        [Tooltip("좌우 회전 각도 (IsometricLeft: 24도, Straight: 0도)")]
        [Range(-60f, 60f)]
        [SerializeField] private float _yawAngle = 24f;

        [Header("--- 추격 부드러움 (Damping) ---")]
        [Tooltip("카메라 추격 지연 시간(초) - 작을수록 즉각 반응")]
        [SerializeField] private float _smoothTime = 0.15f;

        [Header("--- 좌우(X축) 시야 추격 설정 ---")]
        [Tooltip("플레이어의 좌우 이동을 카메라가 얼마나 따라갈지 비율 (0.0: 트랙 중앙 고정, 0.22: 화면 가장자리 이동을 자연스럽게 보여주는 부드러운 완충 추격, 1.0: 1:1 추격)")]
        [Range(0f, 1f)]
        [SerializeField] private float _horizontalFollowFactor = 0.22f;

        [Tooltip("카메라 중심점의 좌우 최대 허용 편차 (트랙 중심 유지용)")]
        [SerializeField] private float _maxHorizontalCameraOffset = 1.2f;

        [Header("--- 카메라 이동 방식 ---")]
        [Tooltip("플레이어가 앞으로 전진할 때만 카메라가 앞으로 이동 (뒤로 물러서도 카메라는 후퇴하지 않음)")]
        [SerializeField] private bool _onlyAdvanceForward = true;

        [Header("--- 자동 전진 스크롤 (옵션) ---")]
        [Tooltip("플레이어가 가만히 있어도 카메라가 계속 자동으로 앞으로 전진하는 기능 활성화 여부")]
        [SerializeField] private bool _enableAutoScroll = false;

        [Tooltip("자동 스크롤 시 초당 전진 속도 (초당 미터)")]
        [SerializeField] private float _minScrollSpeed = 0f;

        [Tooltip("자동 스크롤 시 카메라 시야 기준 플레이어가 화면 아래로 몇 미터 이상 뒤처지면 탈락할지")]
        [SerializeField] private float _deathBehindThreshold = 2.5f;

        private Vector3 _currentVelocity;
        private float _currentBaseZ = 0f;
        private GridPlayerController _playerController;
        private bool _hasGameStarted = false;

        private void OnEnable()
        {
            GameEvents.OnPlayerHopped += HandlePlayerHopped;
            GameEvents.OnGameRestarted += HandleGameRestarted;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerHopped -= HandlePlayerHopped;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
        }

        private void HandlePlayerHopped(int playerZ)
        {
            if (!_hasGameStarted)
            {
                _hasGameStarted = true;
                if (_target != null)
                {
                    _currentBaseZ = _target.position.z;
                }
            }
        }

        private void HandleGameRestarted()
        {
            _hasGameStarted = false;
            FindTargetIfNull();
            if (_target != null)
            {
                _currentBaseZ = _target.position.z;
                SnapToTarget();
            }
            else
            {
                _currentBaseZ = 0f;
            }
            _currentVelocity = Vector3.zero;
        }

        private void OnValidate()
        {
            if (_viewMode != CameraViewMode.Custom)
            {
                ApplyViewMode();
            }
            ApplyFixedRotation();
        }

        private void ApplyViewMode()
        {
            switch (_viewMode)
            {
                case CameraViewMode.IsometricLeft:
                    _offset = new Vector3(-3.8f, 6.4f, -6.8f);
                    _pitchAngle = 36f;
                    _yawAngle = 24f;
                    break;
                case CameraViewMode.IsometricRight:
                    _offset = new Vector3(3.8f, 6.4f, -6.8f);
                    _pitchAngle = 36f;
                    _yawAngle = -24f;
                    break;
                case CameraViewMode.Straight:
                    _offset = new Vector3(0f, 6.2f, -7.0f);
                    _pitchAngle = 35f;
                    _yawAngle = 0f;
                    break;
                case CameraViewMode.Custom:
                    // 사용자 임의 설정 유지
                    break;
            }
        }

        private void ApplyFixedRotation()
        {
            transform.rotation = Quaternion.Euler(_pitchAngle, _yawAngle, 0f);
        }

        private void Awake()
        {
            if (_viewMode != CameraViewMode.Custom)
            {
                ApplyViewMode();
            }
            ApplyFixedRotation();
            FindTargetIfNull();
        }


        private void Start()
        {
            FindTargetIfNull();
            SnapToTarget();
            if (_target != null)
            {
                _currentBaseZ = _target.position.z;
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                FindTargetIfNull();
                if (_target == null) return;
                SnapToTarget();
                _currentBaseZ = _target.position.z;
            }

            // 1. Z축 기준 위치 계산
            if (_enableAutoScroll)
            {
                // 자동 전진 모드 (활성화 시 시간 경과에 따라 서서히 전진)
                _currentBaseZ += _minScrollSpeed * Time.deltaTime;
                _currentBaseZ = Mathf.Max(_currentBaseZ, _target.position.z);
            }
            else if (_onlyAdvanceForward)
            {
                // 캐릭터가 앞으로 나아갈 때만 카메라 전진 (가만히 있거나 뒤로 물러서도 카메라는 후퇴하지 않고 대기)
                _currentBaseZ = Mathf.Max(_currentBaseZ, _target.position.z);
            }
            else
            {
                // 플레이어의 현재 Z 위치를 그대로 추격
                _currentBaseZ = _target.position.z;
            }

            // 2. 부드러운 위치 추격 (SmoothDamp)
            // [중요] 플레이어의 점프(Y축 덜컹거림)를 무시하고 바닥(0f) 기준으로 고정하여 시점 튐/멀미 완전 방지
            // [개선] X축 1:1 무한 추적 대신 완충 계수를 적용하여, 플레이어가 가장자리로 이동하거나 표류 시 화면 밖(경계 부표)으로 이동함을 명확히 인지할 수 있도록 처리
            float targetX = Mathf.Clamp(_target.position.x * _horizontalFollowFactor, -_maxHorizontalCameraOffset, _maxHorizontalCameraOffset);
            Vector3 targetPosition = new Vector3(targetX, 0f, _currentBaseZ) + _offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);

            // 카메라 쉐이크 오프셋 반영 (충돌 및 물 빠짐 시 진동)
            if (CameraShake.Instance != null)
            {
                transform.position += CameraShake.Instance.ShakeOffset;
            }

            // [중요] 매 프레임 LookAt 호출 시 회전이 뒤틀리는 현상을 없애고, 안정적인 고정 각도 유지
            ApplyFixedRotation();

            // 3. 화면 아래쪽으로 밀려남 감지 (자동 스크롤 활성화 시에만 동작)
            CheckPlayerFellBehind();
        }

        private void CheckPlayerFellBehind()
        {
            if (!_enableAutoScroll || _playerController == null || !_playerController.IsAlive) return;

            // 카메라의 기준 진행도보다 플레이어가 화면 아래로 너무 많이 밀려났을 때
            if (_target.position.z < _currentBaseZ - _deathBehindThreshold)
            {
                string reason = "[!] 교수님의 매의 눈: 1교시 지각으로 F학점을 받았습니다!";
                Debug.LogWarning($"[CampusRun] {reason} (기준 진행도: {_currentBaseZ:F1}m, 플레이어 위치: {_target.position.z:F1}m)");
                _playerController.Die(reason);
            }
        }

        private void FindTargetIfNull()
        {
            if (_target == null)
            {
                _playerController = Object.FindAnyObjectByType<GridPlayerController>();
                if (_playerController != null)
                {
                    _target = _playerController.transform;
                }
            }
            else if (_playerController == null)
            {
                _playerController = _target.GetComponent<GridPlayerController>();
            }
        }

        private void SnapToTarget()
        {
            if (_target != null)
            {
                float targetX = Mathf.Clamp(_target.position.x * _horizontalFollowFactor, -_maxHorizontalCameraOffset, _maxHorizontalCameraOffset);
                transform.position = new Vector3(targetX, 0f, _target.position.z) + _offset;
                ApplyFixedRotation();
            }
        }

        public void SetTarget(Transform newTarget)
        {
            _target = newTarget;
            SnapToTarget();
        }
    }
}
