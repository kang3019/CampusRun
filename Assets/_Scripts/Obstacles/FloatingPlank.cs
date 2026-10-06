using System;
using UnityEngine;
using CampusRun.Player;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 거대 물웅덩이 레인 위를 일정한 방향과 속도로 떠다니는 널판지/나무판자 컴포넌트입니다.
    /// 플레이어가 널판지 위에 올라타면 함께 X축으로 표류(Drift)하며, 화면 밖으로 벗어나면 풀로 반환됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloatingPlank : MonoBehaviour
    {
        [Header("--- 이동 및 경계 설정 ---")]
        [Tooltip("경계를 벗어나면 풀로 반환될 X축 한계 좌표")]
        [SerializeField] private float _despawnBoundaryX = 14f;

        [Tooltip("널판지의 길이(X축 크기)")]
        [SerializeField] private float _plankLength = 2.2f;

        private float _direction = 1f; // +1: 오른쪽, -1: 왼쪽
        private float _speed = 3.5f;
        private Action<FloatingPlank> _onDespawn;
        private bool _isInitialized = false;

        // 현재 널판지 위에 탑승한 플레이어
        private GridPlayerController _mountedPlayer;

        public float Speed => _speed;
        public float Direction => _direction;
        public float PlankLength => _plankLength;

        /// <summary>
        /// 레인에서 풀을 통해 꺼낼 때 방향과 속도를 지정하여 초기화합니다.
        /// </summary>
        public void Initialize(float direction, float speed, Action<FloatingPlank> onDespawn)
        {
            _direction = Mathf.Sign(direction);
            _speed = speed;
            _onDespawn = onDespawn;
            _mountedPlayer = null;
            _isInitialized = true;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // X축으로 지속 이동
            float moveDelta = _direction * _speed * Time.deltaTime;
            transform.position += new Vector3(moveDelta, 0f, 0f);

            // 탑승 중인 플레이어가 있다면 동일한 이동량만큼 함께 이동(표류)
            if (_mountedPlayer != null && _mountedPlayer.IsAlive)
            {
                _mountedPlayer.ApplyPlankDrift(moveDelta);
            }

            // 소멸 경계 도달 검사
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        /// <summary>
        /// 플레이어가 이 널판지에 착지했을 때 탑승 등록합니다.
        /// </summary>
        public void MountPlayer(GridPlayerController player)
        {
            _mountedPlayer = player;
        }

        /// <summary>
        /// 플레이어가 다른 칸으로 점프하여 이탈했을 때 탑승 해제합니다.
        /// </summary>
        public void UnmountPlayer(GridPlayerController player)
        {
            if (_mountedPlayer == player)
            {
                _mountedPlayer = null;
            }
        }

        public void Despawn()
        {
            if (_mountedPlayer != null)
            {
                // 플레이어가 타고 있는 채로 화면 밖으로 나가면 화면 밖 표류 탈락
                if (_mountedPlayer.IsAlive)
                {
                    _mountedPlayer.Die("🚧 공사 안전 발판을 타고 공사장 밖으로 밀려나 1교시 지각했습니다!");
                }
                _mountedPlayer = null;
            }

            _isInitialized = false;
            _onDespawn?.Invoke(this);
        }
    }
}
