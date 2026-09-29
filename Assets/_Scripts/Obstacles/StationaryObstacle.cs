using System;
using UnityEngine;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 원작 길건너 친구들의 '나무/바위'에 해당하는 고정형 길막 장애물입니다.
    /// (방치된 공유 킥보드, 잡담하는 동기 무리 등)
    /// 보도블록 레인 위에 배치되어 플레이어의 1자 직진을 막고 좌우 우회를 유도합니다.
    /// </summary>
    [SelectionBase]
    public class StationaryObstacle : MonoBehaviour
    {
        [Header("--- 장애물 식별 정보 ---")]
        [Tooltip("장애물 명칭 (예: 방치된 공유 킥보드)")]
        [SerializeField] private string _obstacleName = "방치된 공유 킥보드";

        private Action<StationaryObstacle> _onRecycleCallback;
        private bool _isSpawned = false;

        public string ObstacleName => _obstacleName;
        public bool IsSpawned => _isSpawned;

        /// <summary>
        /// 풀에서 꺼내져 지정한 그리드 위치에 배치될 때 초기화합니다.
        /// </summary>
        public void Initialize(Vector3 spawnPosition, Action<StationaryObstacle> onRecycle)
        {
            transform.position = spawnPosition;
            _onRecycleCallback = onRecycle;
            _isSpawned = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 레인이 후방으로 밀려 회수될 때 장애물을 풀에 반환합니다.
        /// </summary>
        public void Recycle()
        {
            if (!_isSpawned) return;

            _isSpawned = false;
            gameObject.SetActive(false);
            _onRecycleCallback?.Invoke(this);
        }
    }
}
