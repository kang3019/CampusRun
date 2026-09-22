using UnityEngine;

namespace CampusRun.Player
{
    /// <summary>
    /// 플레이어와 장애물 간의 물리 충돌을 감지하여 게임오버 및 탈락 사유를 처리하는 컴포넌트입니다.
    /// </summary>
    [RequireComponent(typeof(GridPlayerController))]
    public class PlayerCollision : MonoBehaviour
    {
        [Tooltip("기본 사망 메시지")]
        [SerializeField] private string _defaultDeathReason = "장애물에 부딪혔습니다!";

        private GridPlayerController _playerController;

        private void Awake()
        {
            _playerController = GetComponent<GridPlayerController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleHit(other.gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleHit(collision.gameObject);
        }

        private void HandleHit(GameObject hitObject)
        {
            if (!_playerController.IsAlive) return;

            // 장애물 태그 검사 (CompareTag로 GC 할당 차단)
            if (hitObject.CompareTag("Obstacle"))
            {
                string reason = _defaultDeathReason;
                string objectName = hitObject.name;

                if (objectName.Contains("Motorcycle") || objectName.Contains("Bike"))
                {
                    reason = "🏍️ 초고속 배달 오토바이와 충돌했습니다!";
                }
                else if (objectName.Contains("Bus") || objectName.Contains("Shuttle"))
                {
                    reason = "💥 1교시 셔틀버스를 피하지 못했습니다!";
                }
                else if (objectName.Contains("Laser") || objectName.Contains("Professor"))
                {
                    reason = "⚡ 교수님의 기습 출석체크 레이저에 걸렸습니다!";
                }
                else if (objectName.Contains("Bomb") || objectName.Contains("Assignment"))
                {
                    reason = "💣 하늘에서 떨어진 과제 폭탄에 맞았습니다!";
                }
                else if (objectName.Contains("Freerider"))
                {
                    reason = "👥 조별과제 프리라이더에게 붙잡혔습니다!";
                }
                else if (objectName.Contains("Missile") || objectName.Contains("GradeF"))
                {
                    reason = "🚀 유도 F학점 미사일에 직격당했습니다!";
                }

                Debug.LogWarning($"[CampusRun] 충돌 발생! {reason}");
                _playerController.Die(reason);
            }
        }
    }
}
