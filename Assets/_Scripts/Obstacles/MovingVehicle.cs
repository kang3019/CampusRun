using System;
using UnityEngine;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 도로 레인 위를 좌우 일정한 속도로 횡이동하는 차량/장애물 컴포넌트입니다.
    /// (1단계 셔틀버스, 승용차 등)
    /// </summary>
    public class MovingVehicle : MonoBehaviour
    {
        [Header("--- 이동 속성 ---")]
        [Tooltip("이동 방향 (+1: 오른쪽, -1: 왼쪽)")]
        [SerializeField] private float _direction = 1f;

        [Tooltip("초당 이동 속도")]
        [SerializeField] private float _speed = 5f;

        [Tooltip("레인 중심 기준 도달 시 사라지는 X축 경계선")]
        [SerializeField] private float _despawnBoundaryX = 15f;

        private Action<MovingVehicle> _onDespawnCallback;
        private bool _isActive = false;

        public void Initialize(float direction, float speed, Action<MovingVehicle> onDespawn)
        {
            _direction = Mathf.Sign(direction);
            _speed = speed;
            _onDespawnCallback = onDespawn;
            _isActive = true;

            // 이동 방향에 맞춰 차량 머리 회전
            Vector3 lookDir = (_direction > 0) ? Vector3.right : Vector3.left;
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isActive) return;

            // X축 방향으로 등속 직선 이동
            transform.Translate(Vector3.forward * (_speed * Time.deltaTime), Space.Self);

            // 경계선 도달 시 비활성화 및 콜백 호출
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        public void Despawn()
        {
            _isActive = false;
            gameObject.SetActive(false);
            _onDespawnCallback?.Invoke(this);
        }
    }
}
