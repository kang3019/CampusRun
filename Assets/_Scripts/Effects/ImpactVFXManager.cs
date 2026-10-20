using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using CampusRun.Core;

namespace CampusRun.Effects
{
    /// <summary>
    /// 차량 충돌 파편, 물웅덩이 풍덩 물보라, 점프 착지 모래먼지 VFX를 총괄하는 매니저입니다.
    /// UnityEngine.Pool.ObjectPool을 사용하여 GC 할당 없이 최적의 성능으로 파티클을 재생하며,
    /// 재시작 시 활성화된 모든 파티클을 즉시 회수 및 Clear하여 화면 잔상을 방지합니다.
    /// </summary>
    public class ImpactVFXManager : MonoBehaviour
    {
        private static ImpactVFXManager _instance;
        private static Texture2D _particleTexture;

        public static ImpactVFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<ImpactVFXManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject("[ImpactVFXManager]");
                        _instance = obj.AddComponent<ImpactVFXManager>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        private ObjectPool<ParticleSystem> _crashDebrisPool;
        private ObjectPool<ParticleSystem> _waterSplashPool;
        private ObjectPool<ParticleSystem> _dustPuffPool;

        private Material _particleMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFields()
        {
            _instance = null;
            _particleTexture = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (_instance == null)
            {
                var inst = Instance; // 프로퍼티 호출로 안전하게 초기화
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeMaterials();
            InitializePools();
        }

        private void OnEnable()
        {
            GameEvents.OnGameRestarted += ClearAllActiveVFX;
            GameEvents.OnGameStarted += ClearAllActiveVFX;
        }

        private void OnDisable()
        {
            GameEvents.OnGameRestarted -= ClearAllActiveVFX;
            GameEvents.OnGameStarted -= ClearAllActiveVFX;
        }

        /// <summary>
        /// 게임 재시작 또는 시작 시 재생 중이던 모든 파티클을 즉시 정지 및 소멸시킵니다.
        /// </summary>
        public void ClearAllActiveVFX()
        {
            ParticleSystem[] allParticles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < allParticles.Length; i++)
            {
                ParticleSystem ps = allParticles[i];
                if (ps != null && ps.gameObject.activeSelf)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.Clear(true);
                    ps.gameObject.SetActive(false);
                }
            }
        }

        private void InitializeMaterials()
        {
            if (_particleMaterial != null) return;

            // URP Particles/Unlit 또는 기본 스프라이트 셰이더 기반 머티리얼 준비
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Mobile/Particles/Alpha Blended");

            _particleMaterial = new Material(shader);

            // 16x16 원형 소프트 도트 텍스처 절차적 생성 (가장자리가 깔끔하고 선명한 도트 파티클)
            if (_particleTexture == null)
            {
                _particleTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                _particleTexture.filterMode = FilterMode.Bilinear;
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        float dx = (x - 7.5f) / 7.5f;
                        float dy = (y - 7.5f) / 7.5f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((1f - d) * 3f);
                        _particleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                _particleTexture.Apply();
            }

            _particleMaterial.mainTexture = _particleTexture;
            if (_particleMaterial.HasProperty("_BaseMap"))
            {
                _particleMaterial.SetTexture("_BaseMap", _particleTexture);
            }
        }

        private void InitializePools()
        {
            _crashDebrisPool = new ObjectPool<ParticleSystem>(
                createFunc: () => CreateParticleSystemInstance("CrashDebris_VFX", SetupCrashDebrisSettings, _crashDebrisPool),
                actionOnGet: ps => { ps.Clear(true); ps.gameObject.SetActive(true); },
                actionOnRelease: ps => { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(true); ps.gameObject.SetActive(false); },
                actionOnDestroy: ps => Destroy(ps.gameObject),
                collectionCheck: false,
                defaultCapacity: 5,
                maxSize: 15
            );

            _waterSplashPool = new ObjectPool<ParticleSystem>(
                createFunc: () => CreateParticleSystemInstance("WaterSplash_VFX", SetupWaterSplashSettings, _waterSplashPool),
                actionOnGet: ps => { ps.Clear(true); ps.gameObject.SetActive(true); },
                actionOnRelease: ps => { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(true); ps.gameObject.SetActive(false); },
                actionOnDestroy: ps => Destroy(ps.gameObject),
                collectionCheck: false,
                defaultCapacity: 5,
                maxSize: 15
            );

            _dustPuffPool = new ObjectPool<ParticleSystem>(
                createFunc: () => CreateParticleSystemInstance("DustPuff_VFX", SetupDustPuffSettings, _dustPuffPool),
                actionOnGet: ps => { ps.Clear(true); ps.gameObject.SetActive(true); },
                actionOnRelease: ps => { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Clear(true); ps.gameObject.SetActive(false); },
                actionOnDestroy: ps => Destroy(ps.gameObject),
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 25
            );
        }

        private ParticleSystem CreateParticleSystemInstance(string name, System.Action<ParticleSystem> setupAction, ObjectPool<ParticleSystem> pool)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform);

            ParticleSystem ps = obj.AddComponent<ParticleSystem>();
            // [중요] 생성 즉시 정지하여 설정 변경 중 Unity 재생 오류 및 설정 씹힘 방지
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            setupAction(ps);

            // 풀 반환 자동화 도우미 부착
            VFXPoolReturner returner = obj.AddComponent<VFXPoolReturner>();
            returner.Initialize(ps, pool);

            return ps;
        }

        #region --- 파티클 설정 (Settings) ---

        /// <summary> 차량 충돌 시 터져 나오는 픽셀 파편 및 스파크 </summary>
        private void SetupCrashDebrisSettings(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false; // 1회성 버스트 후 반드시 자동 종료
            main.playOnAwake = false;
            main.duration = 0.35f;
            main.startLifetime = 0.35f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f); // 크기 대폭 축소
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.3f, 0.1f));
            main.gravityModifier = 2.5f;
            main.stopAction = ParticleSystemStopAction.Callback;

            // 알파 페이드아웃 (끝날 때 자연스럽게 투명해져 소멸)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary> 강물/물웅덩이에 빠졌을 때 솟구치는 물보라 </summary>
        private void SetupWaterSplashSettings(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false; // 1회성 버스트 후 반드시 자동 종료
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.startLifetime = 0.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f); // 크기 대폭 축소
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.9f, 1f, 0.95f), Color.white);
            main.gravityModifier = 2.8f;
            main.stopAction = ParticleSystemStopAction.Callback;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 위로 솟구침

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        /// <summary> 점프 착지 시 발밑에 튀는 미니 도트 모래먼지 (시인성 확보된 아담한 크기) </summary>
        private void SetupDustPuffSettings(ParticleSystem ps)
        {
            var main = ps.main;
            main.loop = false; // 1회성 단발 방출
            main.playOnAwake = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.20f); // 12~20cm: 쿼터뷰 조감도에서 깔끔하게 보이는 크기
            main.startColor = new Color(0.96f, 0.94f, 0.88f, 0.9f); // 은은한 도트 모래색
            main.gravityModifier = 0.35f;
            main.stopAction = ParticleSystemStopAction.Callback;

            // 알파 페이드아웃 (끝날 때 자연스럽게 투명 소멸)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            // 크기 변화 커브
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.7f);
            curve.AddKey(0.25f, 1f);
            curve.AddKey(1f, 0.25f);
            sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 6) }); // 6알 도트 방출

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 10;
        }

        #endregion

        #region --- 외부 공개 재생 API ---

        /// <summary>
        /// 차량 충돌 위치에 파편 폭발 이펙트를 재생합니다.
        /// </summary>
        public static void PlayCrashDebris(Vector3 position)
        {
            if (Instance == null || Instance._crashDebrisPool == null) return;
            ParticleSystem ps = Instance._crashDebrisPool.Get();
            ps.transform.position = position + new Vector3(0f, 0.4f, 0f);
            ps.Play();
        }

        /// <summary>
        /// 물에 빠진 위치에 하얀 물보라 이펙트를 재생합니다.
        /// </summary>
        public static void PlayWaterSplash(Vector3 position)
        {
            if (Instance == null || Instance._waterSplashPool == null) return;
            ParticleSystem ps = Instance._waterSplashPool.Get();
            ps.transform.position = position + new Vector3(0f, 0.15f, 0f);
            ps.Play();
        }

        /// <summary>
        /// 점프 착지 위치에 작고 은은한 미니 모래먼지 이펙트를 재생합니다.
        /// 지면 표면(Y=0.10) 위로 확실히 띄워(Y=0.20) 파묻힘 없이 선명하게 연출합니다.
        /// </summary>
        public static void PlayDustPuff(Vector3 position)
        {
            if (Instance == null || Instance._dustPuffPool == null) return;
            ParticleSystem ps = Instance._dustPuffPool.Get();
            ps.transform.position = position + new Vector3(0f, 0.20f, -0.1f);
            ps.Play();
        }

        #endregion
    }

    /// <summary>
    /// 파티클 재생 완료 시 자동으로 ObjectPool에 안전하게 반환하는 헬퍼 컴포넌트입니다.
    /// 타이머 안전장치를 내장하여 0.45초 후 100% 무조건 회수됩니다.
    /// </summary>
    public class VFXPoolReturner : MonoBehaviour
    {
        private ParticleSystem _ps;
        private ObjectPool<ParticleSystem> _pool;
        private Coroutine _autoReleaseRoutine;

        public void Initialize(ParticleSystem ps, ObjectPool<ParticleSystem> pool)
        {
            _ps = ps;
            _pool = pool;
        }

        private void OnEnable()
        {
            if (_autoReleaseRoutine != null) StopCoroutine(_autoReleaseRoutine);
            _autoReleaseRoutine = StartCoroutine(AutoReleaseTimerRoutine());
        }

        private void OnDisable()
        {
            if (_autoReleaseRoutine != null)
            {
                StopCoroutine(_autoReleaseRoutine);
                _autoReleaseRoutine = null;
            }
        }

        private System.Collections.IEnumerator AutoReleaseTimerRoutine()
        {
            // 최대 지속시간 후 잔상 없이 강제 풀 회수
            yield return new WaitForSeconds(0.45f);
            ReleaseToPool();
        }

        private void OnParticleSystemStopped()
        {
            ReleaseToPool();
        }

        private void ReleaseToPool()
        {
            if (_pool != null && _ps != null && gameObject.activeSelf)
            {
                try
                {
                    _pool.Release(_ps);
                }
                catch { }
            }
        }
    }
}
