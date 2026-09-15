using UnityEngine;

namespace CampusRun.Track
{
    /// <summary>
    /// 길건너 친구들의 1개 행(Row) 단위를 구성하는 기본 레인 추상 클래스입니다.
    /// 안전 레인(보도블록), 도로 레인(셔틀버스), 강의실 레인(출석 레이저) 등이 이를 상속받습니다.
    /// </summary>
    public abstract class BaseLane : MonoBehaviour
    {
        [Header("--- 레인 기본 속성 ---")]
        [Tooltip("레인의 Z축 격자 인덱스")]
        [SerializeField] private int _laneZIndex = 0;

        [Tooltip("레인의 좌우 가로 폭 (기본 20 World Unit)")]
        [SerializeField] protected float _laneWidth = 20f;

        [Tooltip("플레이어가 쉴 수 있는 안전지대 여부")]
        [SerializeField] protected bool _isSafeLane = false;

        public int LaneZIndex => _laneZIndex;
        public bool IsSafeLane => _isSafeLane;

        /// <summary>
        /// 레인이 풀에서 꺼내져 맵에 배치될 때 호출되는 초기화 메서드입니다.
        /// </summary>
        public virtual void Initialize(int zIndex)
        {
            _laneZIndex = zIndex;
            transform.position = new Vector3(0f, 0f, zIndex);
            gameObject.SetActive(true);

            OnLaneSpawned();
        }

        /// <summary>
        /// 레인이 화면 뒤로 멀어져 풀(Pool)로 회수될 때 호출되는 정리 메서드입니다.
        /// </summary>
        public virtual void Recycle()
        {
            OnLaneRecycled();
            gameObject.SetActive(false);
        }

        /// <summary> 레인이 스폰될 때 자식 클래스에서 수행할 개별 로직 (장애물 스폰 시작 등) </summary>
        protected virtual void OnLaneSpawned() { }

        /// <summary> 레인이 회수될 때 자식 클래스에서 수행할 개별 로직 (장애물 정리 등) </summary>
        protected virtual void OnLaneRecycled() { }
    }
}
