using System.Collections;
using UnityEngine;

namespace CampusRun.Effects
{
    /// <summary>
    /// 차량 충돌, 물 빠짐, 강력한 피격 시 카메라를 역동적으로 진동시키는 카메라 쉐이크 컴포넌트입니다.
    /// Perlin Noise와 비선형 감쇄를 사용하여 멀미 없이 쫄깃하고 타격감 넘치는 진동을 연출합니다.
    /// 히트 스탑(순간 슬로우/정지) 상태에서도 unscaledDeltaTime으로 선명하게 진동합니다.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("--- 쉐이크 기본 파라미터 ---")]
        [Tooltip("펄린 노이즈 진동 주파수 (높을수록 빠르게 덜덜 떨림)")]
        [SerializeField] private float _noiseFrequency = 25f;

        private float _currentIntensity = 0f;
        private float _currentDuration = 0f;
        private float _elapsedTime = 0f;
        private float _seedX;
        private float _seedY;
        private Coroutine _hitStopRoutine;

        public Vector3 ShakeOffset { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _seedX = Random.Range(0f, 100f);
            _seedY = Random.Range(100f, 200f);
        }

        private void OnEnable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted += ResetShake;
        }

        private void OnDisable()
        {
            CampusRun.Core.GameEvents.OnGameRestarted -= ResetShake;
        }

        /// <summary>
        /// 쉐이크 및 히트스탑 상태를 즉시 완전히 리셋합니다.
        /// </summary>
        public void ResetShake()
        {
            _currentIntensity = 0f;
            _currentDuration = 0f;
            _elapsedTime = 0f;
            ShakeOffset = Vector3.zero;

            if (_hitStopRoutine != null)
            {
                StopCoroutine(_hitStopRoutine);
                _hitStopRoutine = null;
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (_elapsedTime < _currentDuration)
            {
                _elapsedTime += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_elapsedTime / _currentDuration);
                
                // 3차 지수 감쇄 커브로 초기 충격은 강렬하고 끝은 부드럽게 잦아듦
                float damper = 1f - progress;
                damper = damper * damper;

                float time = Time.unscaledTime * _noiseFrequency;
                float x = (Mathf.PerlinNoise(_seedX + time, 0f) * 2f - 1f) * _currentIntensity * damper;
                float y = (Mathf.PerlinNoise(0f, _seedY + time) * 2f - 1f) * _currentIntensity * damper;
                float z = (Mathf.PerlinNoise(_seedX + time, _seedY + time) * 2f - 1f) * (_currentIntensity * 0.5f) * damper;

                ShakeOffset = new Vector3(x, y, z);
            }
            else
            {
                ShakeOffset = Vector3.zero;
            }
        }

        /// <summary>
        /// 카메라를 지정한 강도와 시간 동안 흔듭니다.
        /// </summary>
        /// <param name="intensity">진동 최대 진폭 (m)</param>
        /// <param name="duration">진동 지속 시간 (초)</param>
        public static void Shake(float intensity, float duration)
        {
            if (Instance == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    Instance = mainCam.gameObject.AddComponent<CameraShake>();
                }
            }

            if (Instance != null)
            {
                Instance.TriggerShake(intensity, duration);
            }
        }

        /// <summary>
        /// 일반 차량 충돌 시 묵직한 쉐이크 프리셋
        /// </summary>
        public static void ShakeOnCarCrash()
        {
            Shake(0.45f, 0.35f);
            DoHitStop(0.06f, 0.05f); // 0.06초간 히트스탑
        }

        /// <summary>
        /// 초고속 경찰차/지상철 충돌 시 초강력 쉐이크 프리셋
        /// </summary>
        public static void ShakeOnHeavyCrash()
        {
            Shake(0.65f, 0.45f);
            DoHitStop(0.08f, 0.02f);
        }

        /// <summary>
        /// 물에 풍덩 빠졌을 때의 출렁이는 쉐이크 프리셋
        /// </summary>
        public static void ShakeOnWaterSplash()
        {
            Shake(0.28f, 0.4f);
        }

        /// <summary>
        /// 호핑 점프 착지 시의 앙증맞은 미세 쉐이크 프리셋
        /// </summary>
        public static void ShakeOnHopLanding()
        {
            Shake(0.04f, 0.08f);
        }

        /// <summary>
        /// 타격감의 핵심인 순간 히트스탑(순간 멈춤) 효과를 발동합니다.
        /// </summary>
        public static void DoHitStop(float duration, float timeScale = 0.05f)
        {
            if (Instance != null)
            {
                if (Instance._hitStopRoutine != null)
                {
                    Instance.StopCoroutine(Instance._hitStopRoutine);
                }
                Instance._hitStopRoutine = Instance.StartCoroutine(Instance.HitStopRoutine(duration, timeScale));
            }
        }

        private IEnumerator HitStopRoutine(float duration, float targetScale)
        {
            float originalScale = Time.timeScale;
            Time.timeScale = targetScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = originalScale;
            _hitStopRoutine = null;
        }

        private void TriggerShake(float intensity, float duration)
        {
            // 더 강한 쉐이크가 들어오거나 기존 쉐이크가 약해졌을 때 갱신
            if (intensity > _currentIntensity * (1f - (_elapsedTime / Mathf.Max(0.001f, _currentDuration))))
            {
                _currentIntensity = intensity;
                _currentDuration = duration;
                _elapsedTime = 0f;
            }
        }
    }
}
