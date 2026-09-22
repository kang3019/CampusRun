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
        [SerializeField] private float _speed = 8.5f;

        [Tooltip("길의 양 끝에 도달 시 사라지는 X축 경계선 (도로 폭 20m 기준 ±13.5m)")]
        [SerializeField] private float _despawnBoundaryX = 13.5f;

        private Action<MovingVehicle> _onDespawnCallback;
        private bool _isActive = false;

        public void Initialize(float direction, float speed, Action<MovingVehicle> onDespawn)
        {
            _direction = Mathf.Sign(direction);
            _speed = speed;
            _onDespawnCallback = onDespawn;
            _isActive = true;

            // 스케일 정규화 (부모 스케일 왜곡 방지)
            transform.localScale = Vector3.one;

            // 이동 방향에 맞춰 차량 머리 회전
            Vector3 lookDir = (_direction > 0) ? Vector3.right : Vector3.left;
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isActive) return;

            // X축 방향으로 등속 직선 이동 (월드 좌표 기준)
            transform.position += new Vector3(_direction * _speed * Time.deltaTime, 0f, 0f);

            // 길의 양 끝쪽 경계선에 도달 시 즉시 비활성화 및 회수
            if ((_direction > 0 && transform.position.x > _despawnBoundaryX) ||
                (_direction < 0 && transform.position.x < -_despawnBoundaryX))
            {
                Despawn();
            }
        }

        public void Despawn()
        {
            if (!_isActive) return;

            _isActive = false;
            gameObject.SetActive(false);

            var callback = _onDespawnCallback;
            _onDespawnCallback = null;
            callback?.Invoke(this);
        }
    }
}
