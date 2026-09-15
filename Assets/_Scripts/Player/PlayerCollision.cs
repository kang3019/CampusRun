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
            if (!_playerController.IsAlive) return;

            // 장애물 태그 검사 (CompareTag로 GC 할당 차단)
            if (other.CompareTag("Obstacle"))
            {
                // 충돌체에 붙은 별도 사유가 있다면 우선 반영
                string reason = _defaultDeathReason;
                
                // 장애물 이름 기반 맞춤형 사유 판정
                string objectName = other.gameObject.name;
                if (objectName.Contains("Bus") || objectName.Contains("Shuttle"))
                {
                    reason = "1교시 셔틀버스를 피하지 못했습니다!";
                }
                else if (objectName.Contains("Laser") || objectName.Contains("Professor"))
                {
                    reason = "교수님의 기습 출석체크 레이저에 걸렸습니다!";
                }
                else if (objectName.Contains("Bomb") || objectName.Contains("Assignment"))
                {
                    reason = "하늘에서 떨어진 과제 폭탄에 맞았습니다!";
                }
                else if (objectName.Contains("Freerider"))
                {
                    reason = "조별과제 프리라이더에게 붙잡혔습니다!";
                }
                else if (objectName.Contains("Missile") || objectName.Contains("GradeF"))
                {
                    reason = "유도 F학점 미사일에 직격당했습니다!";
                }

                _playerController.Die(reason);
            }
        }
    }
}
