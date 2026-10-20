using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using CampusRun.Core;

namespace CampusRun.Audio
{
    /// <summary>
    /// 게임 전역 사운드(BGM, SFX)를 총괄 제어하는 오디오 매니저 싱글톤입니다.
    /// 외부 오디오 클립 할당 시 최우선 사용하며, 클립이 없더라도 '둔탁한 툭/톡 도약음'을 포함한
    /// 절차적 사운드(Procedural Audio)를 런타임에 자동 생성하여 즉각적인 피드백을 제공합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoSingleton<AudioManager>
    {
        [Header("--- 1. 점프 효과음 (둔탁한 도약음) ---")]
        [Tooltip("인스펙터에서 등록할 커스텀 점프 사운드 클립 목록 (비워둘 시 내장 툭/톡 사운드 자동 생성 및 사용)")]
        [SerializeField] private AudioClip[] _customHopClips;

        [Tooltip("점프 사운드 볼륨 크기 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        [SerializeField] private float _hopVolume = 1.0f;

        [Tooltip("점프 사운드 피치 최소값 (툭 소리)")]
        [SerializeField] private float _hopPitchMin = 0.93f;

        [Tooltip("점프 사운드 피치 최대값 (톡 소리)")]
        [SerializeField] private float _hopPitchMax = 1.07f;

        [Header("--- 2. 통나무 착지 효과음 ---")]
        [Tooltip("통나무/부유판 착지 사운드 (wood1.wav)")]
        [SerializeField] private AudioClip _customWoodPlankClip;

        [Tooltip("통나무 착지 사운드 볼륨 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        [SerializeField] private float _woodPlankVolume = 1.0f;

        [Header("--- 3. 차량 및 경찰차 효과음 ---")]
        [Tooltip("차량 주행 엔진음 (car_engine.wav)")]
        [SerializeField] private AudioClip _customCarEngineClip;

        [Tooltip("차량 경적/클락션음 (car_klaxon.wav)")]
        [SerializeField] private AudioClip _customCarKlaxonClip;

        [Tooltip("초고속 경찰차 사이렌/질주음 (policecar.wav)")]
        [SerializeField] private AudioClip _customPoliceCarClip;

        [Range(0f, 1f)]
        [SerializeField] private float _vehicleSoundVolume = 0.75f;

        [Tooltip("차량 경적/클락션음 기본 볼륨 (너무 튀지 않도록 조절)")]
        [Range(0f, 1f)]
        [SerializeField] private float _carKlaxonVolume = 0.45f;

        [Range(0f, 1f)]
        [SerializeField] private float _policeCarVolume = 0.95f;

        public AudioClip CarEngineClip => _customCarEngineClip;
        public AudioClip CarKlaxonClip => _customCarKlaxonClip;
        public float CarKlaxonVolume => _carKlaxonVolume;
        public AudioClip PoliceCarClip => _customPoliceCarClip;

        [Header("--- 4. 충돌 및 사망 효과음 ---")]
        [Tooltip("차량/장애물 충돌 사운드")]
        [SerializeField] private AudioClip _customHitClip;

        [Tooltip("물에 빠짐(익사) 사운드")]
        [SerializeField] private AudioClip _customSplashClip;

        [Tooltip("지각 타임아웃 경고음")]
        [SerializeField] private AudioClip _customWarningClip;

        [Header("--- 3. BGM 및 볼륨 설정 ---")]
        [Tooltip("메인 인게임 배경음악 클립")]
        [SerializeField] private AudioClip _defaultBgmClip;

        [Tooltip("게임 시작 시 BGM 자동 재생 여부")]
        [SerializeField] private bool _autoPlayBgmOnStart = true;

        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 1.0f;

        [Range(0f, 1f)]
        [SerializeField] private float _bgmVolume = 0.45f;

        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 1.0f;

        [Header("--- 4. 내부 오디오 풀 설정 ---")]
        [Tooltip("동시 재생 가능한 SFX 오디오 소스 채널 수")]
        [SerializeField] private int _sfxChannelCount = 10;

        // 오디오 컴포넌트 캐시
        private AudioSource _bgmSource;
        private AudioSource[] _sfxSources;
        private int _currentSfxChannelIndex = 0;

        // 자체 생성 절차적 오디오 클립 캐시 (GC 방지 및 0-byte 에셋 대비)
        private AudioClip _proceduralThudHopClip;
        private AudioClip _proceduralWoodPlankClip;
        private AudioClip _proceduralHitClip;
        private AudioClip _proceduralSplashClip;
        private AudioClip _proceduralWarningClip;

        protected override void Awake()
        {
            base.Awake();

            InitializeAudioSources();
            LoadDefaultResourcesAudio();
            GenerateProceduralAudioClips();

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SubscribeEvents();

            AudioListener.pause = false;
            AudioListener.volume = 1.0f;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeEvents();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 씬 리로드/재시작 시 오디오 리스너 및 볼륨 상태 강제 정상화
            AudioListener.pause = false;
            AudioListener.volume = 1.0f;

            // 소스 풀 및 리소스 안전 검사
            InitializeAudioSources();
            LoadDefaultResourcesAudio();

            // 유실된 이벤트 구독 즉시 복구
            SubscribeEvents();
            _lastVehicleSoundTime = -10f;

            if (_autoPlayBgmOnStart && _defaultBgmClip != null && (_bgmSource == null || !_bgmSource.isPlaying))
            {
                PlayBGM(_defaultBgmClip);
            }
        }

        private void SubscribeEvents()
        {
            UnsubscribeEvents(); // 중복 구독 방지
            GameEvents.OnPlayerHopStarted += HandlePlayerHopStarted;
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnInactivityWarning += HandleInactivityWarning;
            GameEvents.OnGameStarted += HandleGameStarted;
            GameEvents.OnGameRestarted += HandleGameRestarted;
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnPlayerHopStarted -= HandlePlayerHopStarted;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnInactivityWarning -= HandleInactivityWarning;
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
        }

        /// <summary>
        /// 인스펙터에 수동 할당되지 않은 경우 Assets/Resources/SFX/ 폴더에서 jump1/wood1 및 차량 사운드를 자동 로드합니다.
        /// </summary>
        private void LoadDefaultResourcesAudio()
        {
            if (_customHopClips == null || _customHopClips.Length == 0)
            {
                AudioClip jump1Clip = Resources.Load<AudioClip>("SFX/jump1");
                AudioClip jumpClip = jump1Clip != null ? jump1Clip : Resources.Load<AudioClip>("SFX/jump");
                if (jumpClip != null)
                {
                    _customHopClips = new AudioClip[] { jumpClip };
                }
            }

            if (_customWoodPlankClip == null)
            {
                AudioClip wood1Clip = Resources.Load<AudioClip>("SFX/wood1");
                _customWoodPlankClip = wood1Clip != null ? wood1Clip : Resources.Load<AudioClip>("SFX/wood");
            }

            if (_customCarEngineClip == null)
            {
                _customCarEngineClip = Resources.Load<AudioClip>("SFX/car_engine");
            }

            if (_customCarKlaxonClip == null)
            {
                _customCarKlaxonClip = Resources.Load<AudioClip>("SFX/car_klaxon");
            }

            if (_customPoliceCarClip == null)
            {
                _customPoliceCarClip = Resources.Load<AudioClip>("SFX/policecar");
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            if (_autoPlayBgmOnStart && _defaultBgmClip != null)
            {
                PlayBGM(_defaultBgmClip);
            }
        }

        #region 초기화 및 AudioSource 풀링

        private void InitializeAudioSources()
        {
            // BGM 전용 오디오 소스 생성/복구
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
            }
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f; // 2D BGM
            _bgmSource.volume = _bgmVolume * _masterVolume;

            // SFX 오디오 소스 채널 풀링 사전 생성/복구
            if (_sfxSources == null || _sfxSources.Length != _sfxChannelCount)
            {
                _sfxSources = new AudioSource[_sfxChannelCount];
            }

            for (int i = 0; i < _sfxChannelCount; i++)
            {
                if (_sfxSources[i] == null)
                {
                    AudioSource source = gameObject.AddComponent<AudioSource>();
                    source.loop = false;
                    source.playOnAwake = false;
                    source.spatialBlend = 0f; // 2D SFX (카메라 위치에 영향받지 않고 즉시 균일하게 청취)
                    _sfxSources[i] = source;
                }
            }
        }

        /// <summary>
        /// 외부 오디오 파일이 아직 없더라도 즉시 플레이 가능한 고품질 합성 효과음을 절차적으로 생성합니다.
        /// </summary>
        private void GenerateProceduralAudioClips()
        {
            // 1. 둔탁한 도약음(Thud Hop): 고주파 뿅뿅 소리를 배제하고 지면 타격감과 중저역 킥을 조합한 툭/톡 사운드
            _proceduralThudHopClip = CreateThudFootstepClip("Procedural_ThudHop", 0.085f, 130f, 48f);

            // 2. 통나무 착지음(Wood Plank Thud)
            _proceduralWoodPlankClip = CreateWoodPlankClip("Procedural_WoodPlank", 0.12f);

            // 3. 둔탁한 충돌음(Crash Hit): 무거운 충돌
            _proceduralHitClip = CreateCrashHitClip("Procedural_Hit", 0.28f);

            // 4. 물 첨벙 소리(Water Splash): 젖은 착수음
            _proceduralSplashClip = CreateSplashClip("Procedural_Splash", 0.32f);

            // 5. 지각 경고 비프음(Warning Chime)
            _proceduralWarningClip = CreateWarningBeepClip("Procedural_Warning", 0.12f);
        }

        #endregion

        #region 재생 API

        /// <summary>
        /// 땅을 박차는 둔탁한 점프 효과음(툭/톡)을 재생합니다.
        /// 사용자가 원하는 살짝 튀는 통통 소리를 위해 적절한 고주파 피치 바리에이션과 시원한 음압을 부여합니다.
        /// </summary>
        public void PlayHopSound()
        {
            if (_customHopClips == null || _customHopClips.Length == 0)
            {
                LoadDefaultResourcesAudio();
            }

            AudioClip clipToPlay = null;
            if (_customHopClips != null && _customHopClips.Length > 0)
            {
                int randomIndex = Random.Range(0, _customHopClips.Length);
                clipToPlay = _customHopClips[randomIndex];
            }

            if (clipToPlay == null)
            {
                clipToPlay = _proceduralThudHopClip;
            }

            // 경쾌하게 튀는 통통(통통/톡) 도약 사운드
            float randomPitch = Random.Range(1.02f, 1.18f);
            PlaySFX(clipToPlay, 1.20f * _hopVolume, randomPitch);
        }

        /// <summary>
        /// 하천의 통나무/부유판에 플레이어가 안착했을 때 효과음(wood1.wav)을 귀에 확실히 꽂히도록 시원한 볼륨으로 재생합니다.
        /// </summary>
        public void PlayWoodPlankSound()
        {
            if (_customWoodPlankClip == null)
            {
                LoadDefaultResourcesAudio();
            }

            AudioClip clip = _customWoodPlankClip != null ? _customWoodPlankClip : _proceduralWoodPlankClip;
            if (clip == null) return;

            float randomPitch = Random.Range(0.96f, 1.05f);

            // 통나무 착지음이 다른 환경음에 묻히지 않도록 확실한 볼륨으로 더블 임팩트 재생
            PlaySFX(clip, 1.35f * _woodPlankVolume, randomPitch);
            PlaySFX(clip, 1.10f * _woodPlankVolume, randomPitch * 0.94f);
        }

        private float _lastVehicleSoundTime = -10f;
        private const float MinVehicleSoundInterval = 1.2f;

        /// <summary>
        /// 도로 위 차량이 지나갈 때 엔진음(car_engine.wav) 또는 경적(car_klaxon.wav)을 랜덤으로 재생합니다.
        /// </summary>
        public void PlayRandomVehicleSound()
        {
            if (Time.time - _lastVehicleSoundTime < MinVehicleSoundInterval) return;
            _lastVehicleSoundTime = Time.time;

            bool playKlaxon = Random.value < 0.45f;
            AudioClip clipToPlay = playKlaxon ? _customCarKlaxonClip : _customCarEngineClip;

            if (clipToPlay == null)
            {
                clipToPlay = _customCarEngineClip ?? _customCarKlaxonClip;
            }

            if (clipToPlay != null)
            {
                float randomPitch = Random.Range(0.95f, 1.05f);
                PlaySFX(clipToPlay, _vehicleSoundVolume, randomPitch);
            }
        }

        /// <summary>
        /// 빨간색 주의 표시가 뜬 후 경찰차가 돌진할 때 사이렌 사운드(policecar.wav)를 재생합니다.
        /// </summary>
        public void PlayPoliceCarSound()
        {
            if (_customPoliceCarClip != null)
            {
                PlaySFX(_customPoliceCarClip, _policeCarVolume, 1.0f);
            }
        }

        /// <summary>
        /// 충돌/피격 효과음을 재생합니다.
        /// </summary>
        public void PlayHitSound()
        {
            AudioClip clip = _customHitClip != null ? _customHitClip : _proceduralHitClip;
            PlaySFX(clip, 1.0f, Random.Range(0.92f, 1.04f));
        }

        /// <summary>
        /// 물에 빠지는 효과음을 재생합니다.
        /// </summary>
        public void PlaySplashSound()
        {
            AudioClip clip = _customSplashClip != null ? _customSplashClip : _proceduralSplashClip;
            PlaySFX(clip, 0.95f, Random.Range(0.95f, 1.05f));
        }

        /// <summary>
        /// 지각 경고 알람 효과음을 재생합니다.
        /// </summary>
        public void PlayWarningSound()
        {
            AudioClip clip = _customWarningClip != null ? _customWarningClip : _proceduralWarningClip;
            PlaySFX(clip, 0.75f, 1.0f);
        }

        /// <summary>
        /// 범용 단발 효과음(SFX)을 재생합니다. (오디오 소스 풀링 순환 및 자가 치유)
        /// </summary>
        public void PlaySFX(AudioClip clip, float volume = 1.0f, float pitch = 1.0f)
        {
            if (clip == null) return;

            if (_sfxSources == null || _sfxSources.Length == 0)
            {
                InitializeAudioSources();
            }

            // 순환 풀에서 소스 가져오기 (null인 경우 즉시 재생성)
            AudioSource source = _sfxSources[_currentSfxChannelIndex];
            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sfxSources[_currentSfxChannelIndex] = source;
            }
            _currentSfxChannelIndex = (_currentSfxChannelIndex + 1) % _sfxSources.Length;

            source.pitch = pitch;
            source.spatialBlend = 0f; // 2D 균일 청취 보장
            float finalVolume = Mathf.Clamp01(volume * _sfxVolume * _masterVolume);
            source.PlayOneShot(clip, finalVolume);
        }

        /// <summary>
        /// 배경음악(BGM)을 재생합니다.
        /// </summary>
        public void PlayBGM(AudioClip bgmClip, bool loop = true)
        {
            if (bgmClip == null || _bgmSource == null) return;

            _bgmSource.clip = bgmClip;
            _bgmSource.loop = loop;
            _bgmSource.volume = _bgmVolume * _masterVolume;
            _bgmSource.Play();
        }

        /// <summary>
        /// 배경음악을 정지합니다.
        /// </summary>
        public void StopBGM()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Stop();
            }
        }

        #endregion

        #region GameEvents 핸들러

        private void HandlePlayerHopStarted(Vector3Int targetGrid)
        {
            PlayHopSound();
        }

        private void HandlePlayerDied(string reason, int distance)
        {
            if (reason.Contains("물") || reason.Contains("Water") || reason.Contains("Puddle"))
            {
                PlaySplashSound();
            }
            else
            {
                PlayHitSound();
            }
        }

        private void HandleInactivityWarning(bool isWarning)
        {
            if (isWarning)
            {
                PlayWarningSound();
            }
        }

        private void HandleGameStarted()
        {
            if (_defaultBgmClip != null && !_bgmSource.isPlaying)
            {
                PlayBGM(_defaultBgmClip);
            }
        }

        private void HandleGameRestarted()
        {
            if (_defaultBgmClip != null && !_bgmSource.isPlaying)
            {
                PlayBGM(_defaultBgmClip);
            }
        }

        #endregion

        #region 절차적 사운드 합성기 (Procedural Audio Synthesizer)

        /// <summary>
        /// 땅을 딛고 튀어오르는 둔탁한 발소리/바운스 효과음 합성 (툭/톡 소리)
        /// 중저역 지수 피치 드롭과 발바닥 마찰 트랜지언트를 결합하여 제작.
        /// </summary>
        private AudioClip CreateThudFootstepClip(string clipName, float duration, float startFreq, float endFreq)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount; // 0.0 ~ 1.0

                // 1. 피치 급격 감쇠 (130Hz -> 48Hz): 묵직한 바닥 타격 주파수
                float currentFreq = Mathf.Lerp(startFreq, endFreq, Mathf.Pow(t, 0.45f));
                phase += 2.0f * Mathf.PI * currentFreq / sampleRate;

                // 2. 부드러운 저음 정현파 (Main Thud)
                float sine = Mathf.Sin(phase);

                // 3. 지면 마찰 둔탁함 부여 (초기 25% 구간에 약한 미세 노이즈 혼합)
                float noise = (Random.value * 2f - 1f) * Mathf.Max(0f, 1f - t * 4f) * 0.18f;

                // 4. 가파른 지수 감쇠 엔벨로프 (매우 짧고 옹골찬 어택 및 릴리즈)
                float envelope = Mathf.Exp(-t * 28.0f);

                samples[i] = (sine * 0.82f + noise) * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 통나무 착지 시 둔탁한 나무 울림과 물기 있는 착수음 합성
        /// </summary>
        private AudioClip CreateWoodPlankClip(string clipName, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float currentFreq = Mathf.Lerp(260f, 95f, Mathf.Pow(t, 0.5f));
                phase += 2.0f * Mathf.PI * currentFreq / sampleRate;

                float sine = Mathf.Sin(phase);
                float hollow = Mathf.Sin(phase * 1.5f) * 0.35f;
                float waterSpray = (Random.value * 2f - 1f) * Mathf.Max(0f, 1f - t * 3.5f) * 0.22f;
                float envelope = Mathf.Exp(-t * 22.0f);

                samples[i] = ((sine + hollow) * 0.7f + waterSpray) * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 무거운 충돌 타격음 합성
        /// </summary>
        private AudioClip CreateCrashHitClip(string clipName, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(220f, 35f, Mathf.Pow(t, 0.3f));
                phase += 2.0f * Mathf.PI * freq / sampleRate;

                float sine = Mathf.Sin(phase);
                float noise = (Random.value * 2f - 1f) * 0.45f;
                float envelope = Mathf.Exp(-t * 12.0f);

                samples[i] = (sine * 0.55f + noise) * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 물에 풍덩 빠지는 첨벙 사운드 합성
        /// </summary>
        private AudioClip CreateSplashClip(string clipName, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float freq = Mathf.Lerp(80f, 320f, Mathf.Sin(t * Mathf.PI));
                phase += 2.0f * Mathf.PI * freq / sampleRate;

                float bubble = Mathf.Sin(phase);
                float sprayNoise = (Random.value * 2f - 1f) * Mathf.Sin(t * Mathf.PI) * 0.4f;
                float envelope = Mathf.Exp(-t * 7.0f);

                samples[i] = (bubble * 0.6f + sprayNoise) * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>
        /// 지각 위기 경고 알람음 합성
        /// </summary>
        private AudioClip CreateWarningBeepClip(string clipName, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            float freq = 680f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                phase += 2.0f * Mathf.PI * freq / sampleRate;

                // 부드러운 사각파 / 삼각파 배음
                float square = Mathf.Sin(phase) > 0 ? 0.35f : -0.35f;
                float envelope = Mathf.Sin(t * Mathf.PI);

                samples[i] = square * envelope;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion
    }
}
