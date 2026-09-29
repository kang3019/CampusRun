using System;
using UnityEngine;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 원작 길건너 친구들의 '기차'에 해당하는 대학 캠퍼스 초고속 배달 오토바이 장애물입니다.
    /// 바닥 붉은색 경고선 점멸 후 시속 25~35m/s의 초고속으로 도로를 질주합니다.
    /// </summary>
    [SelectionBase]
    public class FastMotorcycle : MonoBehaviour
    {
        [Header("--- 이동 및 경계 설정 ---")]
        [Tooltip("기본 초고속 이동 속도 (m/s)")]
        [SerializeField] private float _rushSpeed = 30f;

        [Tooltip("레인 중심 기준 비활성화되는 X축 경계선")]
        [SerializeField] private float _despawnBoundaryX = 18f;

        // 런타임 상태 변수 (GC 할당 방지)
        private float _direction = 1f;
        private float _currentSpeed = 30f;
        private bool _isActive = false;
        private Action<FastMotorcycle> _onDespawnCallback;

        public bool IsActive => _isActive;
        public float Speed => _currentSpeed;

        /// <summary>
        /// 오브젝트 풀에서 꺼내질 때 오토바이의 진행 방향과 속도를 초기화합니다.
        /// </summary>
        public void Initialize(float direction, float speed, Action<FastMotorcycle> onDespawn)
        {
            _direction = Mathf.Sign(direction);
            _currentSpeed = speed > 0 ? speed : _rushSpeed;
            _onDespawnCallback = onDespawn;
            _isActive = true;

            // 이동 방향에 맞춰 전면 회전
            Vector3 lookDir = (_direction > 0) ? Vector3.right : Vector3.left;
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_isActive) return;

            // X축 방향으로 초고속 직선 이동 (GC Free)
            transform.position += new Vector3(_direction * _currentSpeed * Time.deltaTime, 0f, 0f);

            // 경계선 도달 시 풀에 안전하게 반환
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
            _onDespawnCallback?.Invoke(this);
        }
    }
}
