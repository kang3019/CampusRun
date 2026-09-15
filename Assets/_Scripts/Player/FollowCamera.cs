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

        private Vector3 _currentVelocity;

        private void Awake()
        {
            FindTargetIfNull();
        }

        private void Start()
        {
            FindTargetIfNull();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                FindTargetIfNull();
                if (_target == null) return;
                SnapToTarget();
            }

            // 부드러운 위치 추격 (SmoothDamp)
            Vector3 targetPosition = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, _smoothTime);

            // 항상 플레이어의 중심(살짝 위)을 안정적으로 내려다보도록 회전 고정
            Vector3 lookTarget = _target.position + Vector3.up * 0.5f;
            transform.LookAt(lookTarget);
        }

        private void FindTargetIfNull()
        {
            if (_target == null)
            {
                var player = Object.FindFirstObjectByType<GridPlayerController>();
                if (player != null)
                {
                    _target = player.transform;
                }
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
