using UnityEngine;

namespace CampusRun.Player
{
    /// <summary>
    /// 길건너 친구들 스타일의 대각선 45도 쿼터뷰(아이소메트릭) 시점으로 플레이어를 부드럽게 추격하는 카메라입니다.
    /// 플레이어의 전진(Z축)에 맞춰 시야를 유지하며, 좌우(X축) 이동은 완만하게 보간합니다.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [Header("--- 추격 대상 및 오프셋 ---")]
        [Tooltip("추격할 플레이어의 Transform (비어있으면 자동 탐색)")]
        [SerializeField] private Transform _target;

        [Tooltip("플레이어 기준 카메라의 상대 위치 (기본: Y=9, Z=-7 대각선 위)")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 9f, -7f);

        [Header("--- 추격 부드러움 (Damping) ---")]
        [Tooltip("카메라 추격 지연 시간(초) - 작을수록 즉각 반응")]
        [SerializeField] private float _smoothTime = 0.15f;

        [Header("--- Crossy Road 스크롤 압박 (Trailing Threat) ---")]
        [Tooltip("플레이어가 가만히 있어도 카메라가 앞으로 전진하는 최소 속도 (초당 전진 미터)")]
        [SerializeField] private float _minScrollSpeed = 1.0f;

        [Tooltip("카메라 시야 기준 플레이어가 화면 아래로 몇 미터 이상 뒤처지면 탈락할지")]
        [SerializeField] private float _deathBehindThreshold = 2.0f;

        [Tooltip("자동 스크롤 및 화면 밖 탈락 활성화 여부")]
        [SerializeField] private bool _enableAutoScroll = true;

        private Vector3 _currentVelocity;
        private float _currentBaseZ = 0f;
        private GridPlayerController _playerController;

        private void Awake()
        {
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

            // 1. 자동 스크롤 Z축 계산 (플레이어가 가만히 있어도 카메라는 서서히 앞으로 전진)
            if (_enableAutoScroll)
            {
                _currentBaseZ += _minScrollSpeed * Time.deltaTime;
                // 플레이어가 앞서가면 카메라도 즉시 앞선 위치로 갱신
                _currentBaseZ = Mathf.Max(_currentBaseZ, _target.position.z);
            }
            else
            {
                _currentBaseZ = _target.position.z;
            }

            // 2. 부드러운 위치 추격 (SmoothDamp)
            Vector3 targetPosition = new Vector3(_target.position.x, _target.position.y, _currentBaseZ) + _offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);

            // 항상 플레이어의 중심을 안정적으로 내려다보도록 회전 고정
            Vector3 lookTarget = new Vector3(_target.position.x, _target.position.y + 0.5f, _currentBaseZ);
            transform.LookAt(lookTarget);

            // 3. 화면 아래쪽으로 밀려남 감지 (독수리 낚아채기 대체 탈락 기믹)
            CheckPlayerFellBehind();
        }

        private void CheckPlayerFellBehind()
        {
            if (!_enableAutoScroll || _playerController == null || !_playerController.IsAlive) return;

            // 카메라의 기준 진행도보다 플레이어가 화면 아래로 너무 많이 밀려났을 때
            if (_target.position.z < _currentBaseZ - _deathBehindThreshold)
            {
                string reason = "🦅 화면 밖으로 밀려나 지각 탈락했습니다!";
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
                transform.position = _target.position + _offset;
                transform.LookAt(_target.position + Vector3.up * 0.5f);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            _target = newTarget;
            SnapToTarget();
        }
    }
}
