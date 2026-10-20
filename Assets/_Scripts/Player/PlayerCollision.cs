using UnityEngine;
using CampusRun.Obstacles;
using CampusRun.Audio;
using CampusRun.Effects;

namespace CampusRun.Player
{
    /// <summary>
    /// 플레이어와 장애물 간의 물리 충돌을 감지하여 게임오버 및 탈락 사유를 처리하는 컴포넌트입니다.
    /// 장애물 종류(트럭, 승용차, 버스, 경찰차 등)에 맞추어 "~~에 치였습니다 !!" 문구를 제공합니다.
    /// </summary>
    [RequireComponent(typeof(GridPlayerController))]
    public class PlayerCollision : MonoBehaviour
    {
        [Header("--- 사망 메시지 설정 ---")]
        [Tooltip("공통 사망 메시지 형식 ({0}에 장애물/차량 명칭 치환)")]
        [SerializeField] private string _deathMessageFormat = "{0}에 치였습니다 !!";

        [Tooltip("식별되지 않은 일반 장애물 사망 메시지")]
        [SerializeField] private string _defaultDeathReason = "장애물에 치였습니다 !!";

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
                string obstacleType = IdentifyObstacleType(hitObject);
                string reason = !string.IsNullOrEmpty(obstacleType) 
                    ? string.Format(_deathMessageFormat, obstacleType) 
                    : _defaultDeathReason;

                Debug.LogWarning($"[CampusRun] 충돌 발생! {reason} (충돌 오브젝트: {hitObject.name})");

                // 차량(승용차, 트럭, 버스, 경찰차, 오토바이 등) 충돌 시 -> 끼이익! 쾅!! (carbreak.wav) 사운드 및 강력 진동
                if (IsVehicle(hitObject, obstacleType))
                {
                    AudioManager.Instance.PlayCarBreakSound();
                    CameraShake.ShakeOnCarCrash();
                }
                else
                {
                    AudioManager.Instance.PlayHitSound();
                    CameraShake.ShakeOnHit();
                }

                // 충돌 시 파편 VFX 재생
                ImpactVFXManager.PlayCrashDebris(transform.position);

                _playerController.Die(reason);
            }
        }

        private bool IsVehicle(GameObject hitObject, string obstacleType)
        {
            if (hitObject.GetComponentInParent<MovingVehicle>() != null) return true;
            if (hitObject.GetComponentInParent<FastMotorcycle>() != null) return true;

            if (!string.IsNullOrEmpty(obstacleType))
            {
                string lower = obstacleType.ToLower();
                if (lower.Contains("차") || lower.Contains("car") || lower.Contains("bus") ||
                    lower.Contains("truck") || lower.Contains("트럭") || lower.Contains("버스") ||
                    lower.Contains("오토바이") || lower.Contains("경찰"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 충돌한 오브젝트의 컴포넌트, 계층 구조, 모델명 등을 종합 분석하여 정확한 차량/장애물 명칭을 반환합니다.
        /// (트럭, 승용차, 버스, 경찰차 등 사용자 지정 규격 완벽 대응)
        /// </summary>
        private string IdentifyObstacleType(GameObject hitObject)
        {
            // 1. MovingVehicle 컴포넌트 명시적 식별 (버스, 트럭, 승용차)
            MovingVehicle movingVehicle = hitObject.GetComponentInParent<MovingVehicle>();
            if (movingVehicle != null && !string.IsNullOrEmpty(movingVehicle.VehicleDisplayName))
            {
                return movingVehicle.VehicleDisplayName;
            }

            // 2. FastMotorcycle (초고속 질주 차량) 컴포넌트 명시적 식별
            FastMotorcycle fastVehicle = hitObject.GetComponentInParent<FastMotorcycle>();
            if (fastVehicle != null)
            {
                string fastNames = GetHierarchyNames(fastVehicle.gameObject).ToLower();
                if (fastNames.Contains("police")) return "경찰차";
                if (fastNames.Contains("scooter") || fastNames.Contains("motorcycle") || fastNames.Contains("bike")) return "오토바이";
                if (fastNames.Contains("sport") || fastNames.Contains("car")) return "승용차";

                if (!string.IsNullOrEmpty(fastVehicle.VehicleDisplayName))
                {
                    return fastVehicle.VehicleDisplayName;
                }
                return "경찰차";
            }

            // 3. StationaryObstacle (고정형 장애물) 컴포넌트 식별
            StationaryObstacle stationary = hitObject.GetComponentInParent<StationaryObstacle>();
            if (stationary != null && !string.IsNullOrEmpty(stationary.ObstacleName))
            {
                string sName = stationary.ObstacleName.ToLower();
                if (sName.Contains("킥보드") || sName.Contains("kickboard")) return "킥보드";
                if (sName.Contains("볼라드") || sName.Contains("bollard")) return "볼라드";
                return stationary.ObstacleName;
            }

            // 4. 오브젝트 이름 및 계층 구조(부모, 자식) 텍스트 정밀 분석 (대소문자 무관)
            string hierarchyNames = GetHierarchyNames(hitObject).ToLower();

            // [경찰차] 최우선 판정: Police Car 모델이 PF_Motorcycle에 배치되어 있으므로 오토바이보다 먼저 검사!
            if (hierarchyNames.Contains("police"))
            {
                return "경찰차";
            }

            // [트럭] 1톤 탑차 / 택배 트럭
            if (hierarchyNames.Contains("truck") || hierarchyNames.Contains("delivery"))
            {
                return "트럭";
            }

            // [버스] 셔틀버스
            if (hierarchyNames.Contains("bus") || hierarchyNames.Contains("shuttle"))
            {
                return "버스";
            }

            // [승용차] 세단, 택시, 일반 승용차
            if (hierarchyNames.Contains("sedan") || hierarchyNames.Contains("taxi") || hierarchyNames.Contains("car") || hierarchyNames.Contains("sport"))
            {
                return "승용차";
            }

            // [오토바이]
            if (hierarchyNames.Contains("motorcycle") || hierarchyNames.Contains("scooter") || hierarchyNames.Contains("bike"))
            {
                return "오토바이";
            }

            // [기타 고정 장애물]
            if (hierarchyNames.Contains("kickboard") || hierarchyNames.Contains("킥보드"))
            {
                return "킥보드";
            }

            if (hierarchyNames.Contains("bollard") || hierarchyNames.Contains("볼라드"))
            {
                return "볼라드";
            }

            return "장애물";
        }

        private string GetHierarchyNames(GameObject obj)
        {
            if (obj == null) return string.Empty;

            string names = obj.name;
            Transform root = obj.transform.root;
            if (root != null && root != obj.transform)
            {
                names += " " + root.name;
            }

            Transform parent = obj.transform.parent;
            if (parent != null && parent != root)
            {
                names += " " + parent.name;
            }

            // 직계 자식들 이름도 포함
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                names += " " + obj.transform.GetChild(i).name;
            }

            return names;
        }
    }
}
