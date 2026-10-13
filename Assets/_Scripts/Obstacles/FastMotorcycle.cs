using System;
using UnityEngine;
using CampusRun.Audio;

namespace CampusRun.Obstacles
{
    /// <summary>
    /// 원작 길건너 친구들의 '기차'에 해당하는 대학 캠퍼스 초고속 배달 오토바이(경찰차) 장애물입니다.
    /// 3D 공간 음향이 탑재되어 멀리서 다가올 때부터 코앞을 쏜살같이 지나갈 때까지 입체적으로 사이렌 소리가 변화합니다.
    /// </summary>
    [SelectionBase]
    public class FastMotorcycle : MonoBehaviour
    {
        [Header("--- 이동 및 경계 설정 ---")]
        [Tooltip("기본 초고속 이동 속도 (m/s)")]
        [SerializeField] private float _rushSpeed = 30f;

        [Tooltip("레인 중심 기준 비활성화되는 X축 경계선 (도로 폭 60m 기준 ±30.0m)")]
        [SerializeField] private float _despawnBoundaryX = 30.0f;

        // 런타임 상태 변수 (GC 할당 방지)
        private float _direction = 1f;
        private float _currentSpeed = 30f;
        private bool _isActive = false;
        private Action<FastMotorcycle> _onDespawnCallback;
        private AudioSource _audioSource;

        private void Awake()
        {
            SetupSpatialAudioSource();
        }

        private void SetupSpatialAudioSource()
        {
            if (_audioSource != null) return;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            // 초고속 질주 및 쿼터뷰 시점에 최적화된 3D 공간 음향 설정
            _audioSource.spatialBlend = 0.88f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 9.0f;  // 카메라/플레이어 앞을 지날 때 최대 100% 음량 보장
            _audioSource.maxDistance = 60.0f; // 먼 거리에서부터 사이렌이 다가옴
            _audioSource.dopplerLevel = 1.2f; // 초고속 질주 도플러 효과 강조
            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
        }

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

            // [중요!] 먼저 오브젝트를 활성화해야 AudioSource.Play()가 안전하게 작동
            gameObject.SetActive(true);

            // 3D 경찰차 사이렌 사운드 재생
            Play3DPoliceSiren();
        }

        private void Play3DPoliceSiren()
        {
            if (_audioSource == null) return;

            var audioMgr = AudioManager.Instance;
            AudioClip policeClip = audioMgr != null ? audioMgr.PoliceCarClip : null;
            if (policeClip == null)
            {
                policeClip = Resources.Load<AudioClip>("SFX/policecar");
            }

            if (policeClip != null)
            {
                _audioSource.clip = policeClip;
                _audioSource.volume = 1.0f;
                _audioSource.Play();
            }
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

            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }

            _isActive = false;
            gameObject.SetActive(false);
            _onDespawnCallback?.Invoke(this);
        }
    }
}
