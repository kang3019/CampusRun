using UnityEngine;

namespace CampusRun.Track
{
    /// <summary>
    /// 플레이어가 잠시 서서 타이밍을 잴 수 있는 안전 보도블록/잔디 레인입니다.
    /// 장애물이 없으며, 시작 구간 및 도로 사이사이의 완충 지대로 활용됩니다.
    /// </summary>
    public class SafeLane : BaseLane
    {
        private void Awake()
        {
            _isSafeLane = true;
        }

        protected override void OnLaneSpawned()
        {
            base.OnLaneSpawned();
            // 안전 레인 배치 시 필요한 추가 연출(잔디 프롭 등)이 있다면 여기서 활성화
        }

        protected override void OnLaneRecycled()
        {
            base.OnLaneRecycled();
        }
    }
}
