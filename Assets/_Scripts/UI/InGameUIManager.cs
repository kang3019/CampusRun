using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using CampusRun.Core;

namespace CampusRun.UI
{
    /// <summary>
    /// 인게임 실시간 거리 점수 HUD 및 사망 시 '다시 하기' 팝업 패널을 총괄하는 UI 매니저입니다.
    /// 씬에 UI가 없더라도 런타임에 100% 자동 생성하여 시각적 점수와 재도전 버튼을 안정적으로 제공합니다.
    /// </summary>
    public class InGameUIManager : MonoBehaviour
    {
        [Header("--- 인게임 실시간 점수 HUD ---")]
        [Tooltip("실시간 전진 거리 텍스트 (예: 150m)")]
        [SerializeField] private Text _scoreText;

        [Tooltip("제자리 지체 시 깜빡이는 지각 경고 배너 텍스트")]
        [SerializeField] private Text _warningBannerText;

        [Header("--- 게임오버 성적표 패널 ---")]
        [Tooltip("사망 시 활성화될 게임오버 팝업 패널")]
        [SerializeField] private GameObject _gameOverPanel;

        [Tooltip("탈락 사유 텍스트")]
        [SerializeField] private Text _deathReasonText;

        [Tooltip("학점 등급 뱃지 텍스트 (A+, B0, F 등)")]
        [SerializeField] private Text _gradeBadgeText;

        [Tooltip("최종 거리 텍스트")]
        [SerializeField] private Text _finalScoreText;

        [Tooltip("최고 기록 텍스트")]
        [SerializeField] private Text _highScoreText;

        [Tooltip("다시하기 버튼")]
        [SerializeField] private Button _retryButton;

        [Header("--- 게임 시작 화면 (지각 5분전) ---")]
        [Tooltip("게임 시작 전 표시될 타이틀 화면 패널")]
        [SerializeField] private GameObject _titleScreenPanel;

        [Tooltip("타이틀 텍스트 (지각 5분전)")]
        [SerializeField] private Text _titleText;

        [Tooltip("조작 안내 프롬프트 텍스트 (방향키를 눌러 시작)")]
        [SerializeField] private Text _startPromptText;

        [Tooltip("조작 키 가이드 텍스트")]
        [SerializeField] private Text _keyGuideText;

        [Header("--- 홈 화면 설정 (Settings) ---")]
        [Tooltip("홈 화면에 표시될 설정 열기 버튼")]
        [SerializeField] private Button _settingsButton;

        [Tooltip("설정 팝업 다이얼로그 패널")]
        [SerializeField] private GameObject _settingsPanel;

        [Header("--- 스플래시 인트로 화면 (Splash Screen) ---")]
        [Tooltip("스플래시 화면 패널")]
        [SerializeField] private GameObject _splashScreenPanel;

        [Tooltip("스플래시 화면 활성화 여부 (게임 최초 기동 시 1회 재생)")]
        [SerializeField] private bool _enableSplashScreen = true;

        [Tooltip("스플래시 화면 진행 지속 시간(초)")]
        [SerializeField] private float _splashDuration = 2.6f;

        [Header("--- 폰트 설정 ---")]
        [Tooltip("레트로 도트 폰트 (미지정 시 Resources/Fonts/neodgm 자동 로드)")]
        [SerializeField] private Font _pixelFont;

        private const string HighScoreKey = "CampusRun_HighScore";
        private const string PrefKeyMasterVolume = "CampusRun_MasterVolume";
        private const string PrefKeyBGMVolume = "CampusRun_BGMVolume";
        private const string PrefKeySFXVolume = "CampusRun_SFXVolume";
        private const string PrefKeyScreenShake = "CampusRun_ScreenShake";

        private static bool _hasShownSplashThisSession = false; // 앱 실행 중 최초 1회만 재생 (다시하기 시 즉시 홈 화면)
        private bool _isSplashActive = false;
        private int _currentScore = 0;
        private bool _isGameOver = false;
        private bool _isGameStarted = false;
        private bool _isSettingsOpen = false;
        private Coroutine _warningBlinkRoutine;
        private Coroutine _promptPulseRoutine;
        private Coroutine _splashSequenceRoutine;

        // 스플래시 화면 UI 요소 캐싱
        private RectTransform _splashProgressFillRect;
        private Text _splashProgressPercentText;
        private Text _splashStatusText;

        // 힙 할당(GC Alloc) 방지를 위한 정적 캐싱 문자열
        private static readonly string[] SplashStatusMessages = new string[]
        {
            "1교시 강의계획서 확인 중...",
            "캠퍼스 셔틀버스 실시간 위치 추적 중...",
            "학생 식당 학식 메뉴 및 잔여 출석 일수 조회 중...",
            "교수님 출석부 및 과제 제출함 동기화 중...",
            "출석체크 준비 완료! 지금 출발하세요! 🏃‍♂️💨"
        };

        private static readonly string[] CachedPercentStrings = GeneratePercentStrings();
        private static string[] GeneratePercentStrings()
        {
            string[] arr = new string[101];
            for (int i = 0; i <= 100; i++) arr[i] = $"{i}%";
            return arr;
        }

        // 설정 패널 UI 동적 캐싱 변수
        private Slider _masterVolumeSlider;
        private Text _masterVolumeValueText;
        private Slider _bgmVolumeSlider;
        private Text _bgmVolumeValueText;
        private Slider _sfxVolumeSlider;
        private Text _sfxVolumeValueText;
        private Button _screenShakeToggleButton;
        private Text _screenShakeToggleText;
        private Image _screenShakeToggleBg;
        private Button _displayModeToggleButton;
        private Text _displayModeToggleText;
        private Text _resetFeedbackText;
        private Coroutine _resetFeedbackRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeInScene()
        {
            if (FindAnyObjectByType<InGameUIManager>() == null)
            {
                GameObject uiManagerObj = new GameObject("[InGameUIManager]");
                uiManagerObj.AddComponent<InGameUIManager>();
            }
        }

        private void Awake()
        {
            // 프레임 타임 안정화 및 GPU 발열/스터터링 방지 (60 FPS 고정)
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;

            EnsureEventSystem();
            EnsureHUDAndGameOverUI();
            SetupScoreHUDPosition();
            ApplyFixedScale();

            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(false);
            }

            if (_warningBannerText != null)
            {
                _warningBannerText.gameObject.SetActive(false);
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(OnRetryButtonClicked);
            }

            if (_settingsButton != null)
            {
                _settingsButton.onClick.RemoveAllListeners();
                _settingsButton.onClick.AddListener(OpenSettings);
                _settingsButton.gameObject.SetActive(true);
            }

            LoadAndApplySettings();

            // 시작 화면 활성화 (스플래시 화면 우선 실행 또는 타이틀 즉시 대기)
            _isGameStarted = false;
            if (_enableSplashScreen && !_hasShownSplashThisSession)
            {
                _isSplashActive = true;
                _hasShownSplashThisSession = true;

                if (_titleScreenPanel != null) _titleScreenPanel.SetActive(false);
                if (_settingsButton != null) _settingsButton.gameObject.SetActive(false);
                if (_scoreText != null) _scoreText.gameObject.SetActive(false);

                if (_splashScreenPanel != null)
                {
                    _splashScreenPanel.SetActive(true);
                    _splashSequenceRoutine = StartCoroutine(SplashScreenSequenceRoutine());
                }
            }
            else
            {
                _isSplashActive = false;
                if (_splashScreenPanel != null) _splashScreenPanel.SetActive(false);
                if (_titleScreenPanel != null) _titleScreenPanel.SetActive(true);
                if (_settingsButton != null) _settingsButton.gameObject.SetActive(true);
                if (_scoreText != null) _scoreText.gameObject.SetActive(false);
                StartPromptPulse();
            }
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarted += HandleGameStarted;
            GameEvents.OnGameRestarted += HandleGameRestarted;
            GameEvents.OnScoreChanged += UpdateScoreDisplay;
            GameEvents.OnPlayerDied += ShowGameOverPanel;
            GameEvents.OnInactivityWarning += HandleInactivityWarning;
            GameEvents.OnInactivityTimerUpdated += HandleInactivityTimerUpdated;
            GameEvents.OnBoundaryWarning += HandleBoundaryWarning;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            GameEvents.OnScoreChanged -= UpdateScoreDisplay;
            GameEvents.OnPlayerDied -= ShowGameOverPanel;
            GameEvents.OnInactivityWarning -= HandleInactivityWarning;
            GameEvents.OnInactivityTimerUpdated -= HandleInactivityTimerUpdated;
            GameEvents.OnBoundaryWarning -= HandleBoundaryWarning;
        }

        private void Start()
        {
            UpdateScoreDisplay(0);
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var touch = UnityEngine.InputSystem.Touchscreen.current;

            // 0. 스플래시 화면 진행 중 스킵 입력 감지 (Space, Enter, Esc, 마우스 클릭, 화면 터치)
            if (_isSplashActive)
            {
                bool skipPressed = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                                || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                                || (touch != null && touch.primaryTouch.press.wasPressedThisFrame);

                if (skipPressed)
                {
                    SkipSplashScreen();
                }
                return;
            }

            if (keyboard == null) return;

            // 1. 설정 창이 열려 있을 때 ESC 키로 닫기
            if (_isSettingsOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    CloseSettings();
                }
                return;
            }

            // 2. 홈 화면(게임 시작 전)에서 ESC 또는 O키를 누르면 설정 열기
            if (!_isGameStarted && !_isGameOver)
            {
                if (keyboard.escapeKey.wasPressedThisFrame || keyboard.oKey.wasPressedThisFrame)
                {
                    OpenSettings();
                    return;
                }
            }

            // 3. 게임오버 시 키보드 R키, 스페이스바, 엔터키로 즉시 다시하기
            if (_isGameOver)
            {
                if (keyboard.rKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)
                {
                    OnRetryButtonClicked();
                }
            }
        }

        /// <summary>
        /// 버튼 클릭을 처리하기 위한 EventSystem이 씬에 없으면 생성하며, New Input System UI 모듈을 안전하게 연결합니다.
        /// </summary>
        private void EnsureEventSystem()
        {
            EventSystem es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                es = eventSystemObj.AddComponent<EventSystem>();
            }

            // New Input System 환경에서 오류를 발생시키는 레거시 StandaloneInputModule 제거
            StandaloneInputModule legacyModule = es.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Destroy(legacyModule);
            }

            // New Input System 전용 UI 입력 모듈 추가
            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        /// <summary>
        /// 도트/픽셀 폰트를 반환합니다. (Resources -> AssetDatabase -> 내장 폰트 순으로 폴백)
        /// </summary>
        private Font GetEffectivePixelFont()
        {
            if (_pixelFont != null) return _pixelFont;

            // 1. Resources에서 neodgm 폰트 로드 시도
            _pixelFont = Resources.Load<Font>("Fonts/neodgm");
            if (_pixelFont != null) return _pixelFont;

#if UNITY_EDITOR
            // 2. 에디터 환경에서 AssetDatabase를 통한 로드 시도
            _pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/neodgm.ttf");
            if (_pixelFont == null)
            {
                _pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/neodgm.ttf");
            }
            if (_pixelFont != null) return _pixelFont;
#endif

            // 3. 폴백: 기본 내장 폰트
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return defaultFont;
        }

        /// <summary>
        /// 도트 폰트의 글리프 텍스처를 Point 필터링으로 설정하여 픽셀의 선명한 칼각을 살립니다.
        /// </summary>
        private void ConfigurePixelFontTexture(Font font)
        {
            if (font == null) return;
            if (font.material != null && font.material.mainTexture != null)
            {
                font.material.mainTexture.filterMode = FilterMode.Point;
            }
        }

        /// <summary>
        /// 씬에 UI Canvas가 없을 경우 100% 안전하게 도트 폰트를 활용한 점수 HUD 및 게임오버 패널을 자동 생성합니다.
        /// </summary>
        private void EnsureHUDAndGameOverUI()
        {
            if (_scoreText != null && _gameOverPanel != null && _titleScreenPanel != null) return;

            Font defaultFont = GetEffectivePixelFont();
            ConfigurePixelFontTexture(defaultFont);

            // 1. Canvas 생성 또는 탐색
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("InGame_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 2. 화면 우측 상단 실시간 점수(거리) HUD 생성
            if (_scoreText == null)
            {
                GameObject scoreObj = new GameObject("Score_HUD_Text");
                scoreObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = scoreObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f); // 우상단
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-60f, -50f);
                rect.sizeDelta = new Vector2(400f, 120f);

                _scoreText = scoreObj.AddComponent<Text>();
                _scoreText.font = defaultFont;
                _scoreText.fontSize = 76;
                _scoreText.fontStyle = FontStyle.Normal;
                _scoreText.color = Color.white;
                _scoreText.alignment = TextAnchor.UpperRight;
                _scoreText.text = "0 m";

                // 또렷한 검은색 그림자
                Shadow shadow = scoreObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(3f, -3f);

                // 스케일 0.9 고정
                scoreObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            }

            // 3. 화면 상단 중앙 지각 위기 경고 배너 생성
            if (_warningBannerText == null)
            {
                GameObject bannerObj = new GameObject("Warning_Banner_Text");
                bannerObj.transform.SetParent(canvas.transform, false);

                RectTransform bannerRect = bannerObj.AddComponent<RectTransform>();
                bannerRect.anchorMin = new Vector2(0.5f, 1f); // 상단 중앙
                bannerRect.anchorMax = new Vector2(0.5f, 1f);
                bannerRect.pivot = new Vector2(0.5f, 1f);
                bannerRect.anchoredPosition = new Vector2(0f, -50f);
                bannerRect.sizeDelta = new Vector2(700f, 70f);

                _warningBannerText = bannerObj.AddComponent<Text>();
                _warningBannerText.font = defaultFont;
                _warningBannerText.fontSize = 32;
                _warningBannerText.fontStyle = FontStyle.Bold;
                _warningBannerText.color = new Color(1f, 0.35f, 0.1f);
                _warningBannerText.alignment = TextAnchor.MiddleCenter;
                _warningBannerText.text = "⚠️ 지각 위기! 교수님이 다가옵니다!";

                Shadow bannerShadow = bannerObj.AddComponent<Shadow>();
                bannerShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                bannerShadow.effectDistance = new Vector2(2f, -2f);

                bannerObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                bannerObj.SetActive(false);
            }

            // 4. 게임오버 팝업 패널 생성 (사망 시 활성화)
            if (_gameOverPanel == null)
            {
                // 반투명 어두운 전체 배경
                _gameOverPanel = new GameObject("GameOver_Panel");
                _gameOverPanel.transform.SetParent(canvas.transform, false);

                RectTransform panelRect = _gameOverPanel.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;

                Image bgImg = _gameOverPanel.AddComponent<Image>();
                bgImg.color = new Color(0f, 0f, 0f, 0.72f); // 72% 반투명 블랙

                // 중앙 다이얼로그 박스
                GameObject boxObj = new GameObject("Dialog_Box");
                boxObj.transform.SetParent(_gameOverPanel.transform, false);

                RectTransform boxRect = boxObj.AddComponent<RectTransform>();
                boxRect.anchorMin = new Vector2(0.5f, 0.5f);
                boxRect.anchorMax = new Vector2(0.5f, 0.5f);
                boxRect.pivot = new Vector2(0.5f, 0.5f);
                boxRect.sizeDelta = new Vector2(580f, 520f);

                // 스케일 0.9 고정
                boxObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

                Image boxImg = boxObj.AddComponent<Image>();
                boxImg.color = new Color(0.12f, 0.12f, 0.16f, 0.96f); // 묵직한 다크 패널

                // [GAME OVER 타이틀]
                GameObject titleObj = new GameObject("Title_Text");
                titleObj.transform.SetParent(boxObj.transform, false);
                RectTransform titleRect = titleObj.AddComponent<RectTransform>();
                titleRect.anchoredPosition = new Vector2(0f, 180f);
                titleRect.sizeDelta = new Vector2(520f, 60f);
                Text titleText = titleObj.AddComponent<Text>();
                titleText.font = defaultFont;
                titleText.fontSize = 48;
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = new Color(1f, 0.35f, 0.35f); // 강렬한 레드오렌지
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.text = "GAME OVER";

                // [사망 사유]
                GameObject reasonObj = new GameObject("Reason_Text");
                reasonObj.transform.SetParent(boxObj.transform, false);
                RectTransform reasonRect = reasonObj.AddComponent<RectTransform>();
                reasonRect.anchoredPosition = new Vector2(0f, 120f);
                reasonRect.sizeDelta = new Vector2(520f, 50f);
                _deathReasonText = reasonObj.AddComponent<Text>();
                _deathReasonText.font = defaultFont;
                _deathReasonText.fontSize = 24;
                _deathReasonText.color = new Color(0.85f, 0.85f, 0.85f);
                _deathReasonText.alignment = TextAnchor.MiddleCenter;
                _deathReasonText.text = "셔틀버스를 피하지 못했습니다!";

                // [최종 거리]
                GameObject finalObj = new GameObject("FinalScore_Text");
                finalObj.transform.SetParent(boxObj.transform, false);
                RectTransform finalRect = finalObj.AddComponent<RectTransform>();
                finalRect.anchoredPosition = new Vector2(0f, 55f);
                finalRect.sizeDelta = new Vector2(520f, 55f);
                _finalScoreText = finalObj.AddComponent<Text>();
                _finalScoreText.font = defaultFont;
                _finalScoreText.fontSize = 38;
                _finalScoreText.fontStyle = FontStyle.Bold;
                _finalScoreText.color = Color.white;
                _finalScoreText.alignment = TextAnchor.MiddleCenter;
                _finalScoreText.text = "최종 거리: 0m";

                // [학점 뱃지]
                GameObject gradeObj = new GameObject("Grade_Badge_Text");
                gradeObj.transform.SetParent(boxObj.transform, false);
                RectTransform gradeRect = gradeObj.AddComponent<RectTransform>();
                gradeRect.anchoredPosition = new Vector2(0f, -5f);
                gradeRect.sizeDelta = new Vector2(520f, 55f);
                _gradeBadgeText = gradeObj.AddComponent<Text>();
                _gradeBadgeText.font = defaultFont;
                _gradeBadgeText.fontSize = 32;
                _gradeBadgeText.fontStyle = FontStyle.Bold;
                _gradeBadgeText.color = new Color(0.95f, 0.25f, 0.25f);
                _gradeBadgeText.alignment = TextAnchor.MiddleCenter;
                _gradeBadgeText.text = "학점: F (재수강 확정)";

                // [최고 기록]
                GameObject highObj = new GameObject("HighScore_Text");
                highObj.transform.SetParent(boxObj.transform, false);
                RectTransform highRect = highObj.AddComponent<RectTransform>();
                highRect.anchoredPosition = new Vector2(0f, -65f);
                highRect.sizeDelta = new Vector2(520f, 40f);
                _highScoreText = highObj.AddComponent<Text>();
                _highScoreText.font = defaultFont;
                _highScoreText.fontSize = 24;
                _highScoreText.color = new Color(1f, 0.82f, 0.2f); // 골드
                _highScoreText.alignment = TextAnchor.MiddleCenter;
                _highScoreText.text = "최고 기록: 0m";

                // [다시 하기 버튼]
                GameObject btnObj = new GameObject("Retry_Button");
                btnObj.transform.SetParent(boxObj.transform, false);
                RectTransform btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(0f, -155f);
                btnRect.sizeDelta = new Vector2(300f, 70f);

                // 스케일 0.9 고정
                btnObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

                Image btnImg = btnObj.AddComponent<Image>();
                btnImg.color = new Color(0.18f, 0.78f, 0.38f); // 상쾌하고 또렷한 에메랄드 그린

                _retryButton = btnObj.AddComponent<Button>();
                ColorBlock colors = _retryButton.colors;
                colors.highlightedColor = new Color(0.28f, 0.88f, 0.48f);
                colors.pressedColor = new Color(0.12f, 0.62f, 0.28f);
                _retryButton.colors = colors;

                Shadow btnShadow = btnObj.AddComponent<Shadow>();
                btnShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
                btnShadow.effectDistance = new Vector2(2f, -2f);

                GameObject btnTextObj = new GameObject("Button_Text");
                btnTextObj.transform.SetParent(btnObj.transform, false);
                RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
                btnTextRect.sizeDelta = btnRect.sizeDelta;
                Text btnText = btnTextObj.AddComponent<Text>();
                btnText.font = defaultFont;
                btnText.fontSize = 30;
                btnText.fontStyle = FontStyle.Bold;
                btnText.color = Color.white;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.text = "다시 하기 (R)";

                _gameOverPanel.SetActive(false);
            }

            // 5. 게임 시작 화면 (지각 5분전 타이틀 및 방향키 조작 안내)
            EnsureTitleScreenUI(canvas, defaultFont);

            // 6. 홈 화면 설정 버튼 및 설정 팝업 패널
            EnsureSettingsUI(canvas, defaultFont);

            // 7. 스플래시 인트로 화면
            EnsureSplashScreenUI(canvas, defaultFont);
        }

        /// <summary>
        /// "지각 5분전" 타이틀 화면 및 방향키 출발 안내 UI를 생성합니다.
        /// </summary>
        private void EnsureTitleScreenUI(Canvas canvas, Font defaultFont)
        {
            if (_titleScreenPanel != null) return;

            Transform existing = canvas.transform.Find("TitleScreen_Panel");
            if (existing != null)
            {
                _titleScreenPanel = existing.gameObject;
                _titleText = _titleScreenPanel.transform.Find("MainTitle_Text")?.GetComponent<Text>();
                _startPromptText = _titleScreenPanel.transform.Find("StartPrompt_Text")?.GetComponent<Text>();
                _keyGuideText = _titleScreenPanel.transform.Find("KeyGuide_Box/KeyGuide_Text")?.GetComponent<Text>();
                return;
            }

            // [타이틀 화면 루트 패널]
            GameObject titlePanelObj = new GameObject("TitleScreen_Panel");
            titlePanelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = titlePanelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 40f);
            panelRect.sizeDelta = new Vector2(900f, 500f);
            titlePanelObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

            _titleScreenPanel = titlePanelObj;

            // 1. 캠퍼스 런 상단 뱃지 라벨
            GameObject badgeObj = new GameObject("Badge_Text");
            badgeObj.transform.SetParent(titlePanelObj.transform, false);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchoredPosition = new Vector2(0f, 155f);
            badgeRect.sizeDelta = new Vector2(600f, 40f);
            Text badgeText = badgeObj.AddComponent<Text>();
            badgeText.font = defaultFont;
            badgeText.fontSize = 22;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.color = new Color(1f, 0.72f, 0.22f); // 따뜻한 골드 오렌지
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.text = "⚡ CAMPUS SURVIVAL RUN ⚡";
            Shadow badgeShadow = badgeObj.AddComponent<Shadow>();
            badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            badgeShadow.effectDistance = new Vector2(2f, -2f);

            // 2. 메인 타이틀: "지각 5분전!!" (레트로 도트 폰트 적용)
            GameObject mainTitleObj = new GameObject("MainTitle_Text");
            mainTitleObj.transform.SetParent(titlePanelObj.transform, false);
            RectTransform mainTitleRect = mainTitleObj.AddComponent<RectTransform>();
            mainTitleRect.anchoredPosition = new Vector2(0f, 85f);
            mainTitleRect.sizeDelta = new Vector2(900f, 130f);
            _titleText = mainTitleObj.AddComponent<Text>();
            _titleText.font = defaultFont;
            _titleText.fontSize = 84;
            _titleText.fontStyle = FontStyle.Normal; // 도트 폰트 픽셀 모양을 칼같이 유지
            _titleText.color = new Color(1f, 0.88f, 0.18f); // 화려한 골드 옐로우
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.text = "지각 5분전!!";

            Outline titleOutline = mainTitleObj.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.12f, 0.05f, 0.02f, 0.95f);
            titleOutline.effectDistance = new Vector2(3f, -3f);

            Shadow titleShadow = mainTitleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            titleShadow.effectDistance = new Vector2(4f, -4f);

            // 3. 서브타이틀
            GameObject subTitleObj = new GameObject("SubTitle_Text");
            subTitleObj.transform.SetParent(titlePanelObj.transform, false);
            RectTransform subTitleRect = subTitleObj.AddComponent<RectTransform>();
            subTitleRect.anchoredPosition = new Vector2(0f, 18f);
            subTitleRect.sizeDelta = new Vector2(800f, 45f);
            Text subText = subTitleObj.AddComponent<Text>();
            subText.font = defaultFont;
            subText.fontSize = 24;
            subText.color = new Color(1f, 0.95f, 0.85f, 0.92f);
            subText.alignment = TextAnchor.MiddleCenter;
            subText.text = "1교시 출석 마감 직전! 셔틀버스를 피해 캠퍼스를 질주하라!";
            Shadow subShadow = subTitleObj.AddComponent<Shadow>();
            subShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            subShadow.effectDistance = new Vector2(2f, -2f);

            // 4. 출발 안내 프롬프트 ("▶ 방향키를 눌러 출발! ◀")
            GameObject promptObj = new GameObject("StartPrompt_Text");
            promptObj.transform.SetParent(titlePanelObj.transform, false);
            RectTransform promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchoredPosition = new Vector2(0f, -50f);
            promptRect.sizeDelta = new Vector2(800f, 60f);
            _startPromptText = promptObj.AddComponent<Text>();
            _startPromptText.font = defaultFont;
            _startPromptText.fontSize = 34;
            _startPromptText.fontStyle = FontStyle.Normal;
            _startPromptText.color = new Color(0.28f, 0.98f, 0.48f); // 에메랄드 그린
            _startPromptText.alignment = TextAnchor.MiddleCenter;
            _startPromptText.text = "▶ 방향키 (W, A, S, D / ↑ ↓ ← →)를 눌러 출발! ◀";
            Shadow promptShadow = promptObj.AddComponent<Shadow>();
            promptShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            promptShadow.effectDistance = new Vector2(2f, -2f);

            // 5. 조작 키 가이드 박스
            GameObject guideBoxObj = new GameObject("KeyGuide_Box");
            guideBoxObj.transform.SetParent(titlePanelObj.transform, false);
            RectTransform guideBoxRect = guideBoxObj.AddComponent<RectTransform>();
            guideBoxRect.anchoredPosition = new Vector2(0f, -120f);
            guideBoxRect.sizeDelta = new Vector2(620f, 46f);
            Image guideBoxImg = guideBoxObj.AddComponent<Image>();
            guideBoxImg.color = new Color(0.10f, 0.12f, 0.16f, 0.75f);

            GameObject guideTextObj = new GameObject("KeyGuide_Text");
            guideTextObj.transform.SetParent(guideBoxObj.transform, false);
            RectTransform guideTextRect = guideTextObj.AddComponent<RectTransform>();
            guideTextRect.anchoredPosition = Vector2.zero;
            guideTextRect.sizeDelta = new Vector2(600f, 40f);
            _keyGuideText = guideTextObj.AddComponent<Text>();
            _keyGuideText.font = defaultFont;
            _keyGuideText.fontSize = 20;
            _keyGuideText.color = new Color(0.85f, 0.90f, 0.95f, 0.9f);
            _keyGuideText.alignment = TextAnchor.MiddleCenter;
            _keyGuideText.text = "[ ↑ / W ] 전진     [ ← / A ] 좌측     [ → / D ] 우측     [ ↓ / S ] 후퇴";
        }

        /// <summary>
        /// 시작 안내 프롬프트 텍스트를 부드럽게 깜빡이는 펄스 코루틴을 시작합니다.
        /// </summary>
        private void StartPromptPulse()
        {
            if (_promptPulseRoutine != null)
            {
                StopCoroutine(_promptPulseRoutine);
            }
            _promptPulseRoutine = StartCoroutine(PromptPulseRoutine());
        }

        private System.Collections.IEnumerator PromptPulseRoutine()
        {
            if (_startPromptText == null) yield break;

            Color baseColor = _startPromptText.color;
            float time = 0f;

            while (!_isGameStarted)
            {
                time += Time.deltaTime * 4f;
                float alpha = Mathf.Lerp(0.35f, 1f, (Mathf.Sin(time) + 1f) * 0.5f);
                _startPromptText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
        }

        /// <summary>
        /// 플레이어가 첫 방향키를 눌러 출발했을 때 호출됩니다.
        /// 타이틀 화면을 페이드아웃하고 점수 HUD를 화면에 표시합니다.
        /// </summary>
        private void HandleGameStarted()
        {
            _isGameStarted = true;

            if (_isSettingsOpen)
            {
                CloseSettings();
            }

            if (_settingsButton != null)
            {
                _settingsButton.gameObject.SetActive(false);
            }

            if (_promptPulseRoutine != null)
            {
                StopCoroutine(_promptPulseRoutine);
                _promptPulseRoutine = null;
            }

            if (_scoreText != null)
            {
                _scoreText.gameObject.SetActive(true);
            }

            if (_titleScreenPanel != null)
            {
                StartCoroutine(FadeOutTitleScreenRoutine());
            }
        }

        private System.Collections.IEnumerator FadeOutTitleScreenRoutine()
        {
            if (_titleScreenPanel == null) yield break;

            CanvasGroup cg = _titleScreenPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = _titleScreenPanel.AddComponent<CanvasGroup>();

            RectTransform rect = _titleScreenPanel.GetComponent<RectTransform>();
            Vector2 startPos = rect.anchoredPosition;
            Vector2 targetPos = startPos + new Vector2(0f, 40f);

            float elapsed = 0f;
            float duration = 0.25f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                cg.alpha = 1f - t;
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            _titleScreenPanel.SetActive(false);
        }

        /// <summary>
        /// 게임 재시작 시 호출됩니다.
        /// </summary>
        private void HandleGameRestarted()
        {
            _isGameStarted = false;
            _isGameOver = false;

            if (_isSettingsOpen)
            {
                CloseSettings();
            }

            if (_settingsButton != null)
            {
                _settingsButton.gameObject.SetActive(true);
            }

            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_warningBannerText != null) _warningBannerText.gameObject.SetActive(false);
            if (_scoreText != null)
            {
                _scoreText.text = "0 m";
                _scoreText.gameObject.SetActive(false);
            }

            if (_titleScreenPanel != null)
            {
                CanvasGroup cg = _titleScreenPanel.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;

                RectTransform rect = _titleScreenPanel.GetComponent<RectTransform>();
                if (rect != null) rect.anchoredPosition = new Vector2(0f, 40f);

                _titleScreenPanel.SetActive(true);
                StartPromptPulse();
            }
        }

        /// <summary>
        /// 점수(거리) HUD의 앵커 및 위치를 화면 우측 상단으로 안전하게 정렬합니다.
        /// </summary>
        public void SetupScoreHUDPosition()
        {
            if (_scoreText == null) return;

            RectTransform rect = _scoreText.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f); // 우상단
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-60f, -50f);
            _scoreText.alignment = TextAnchor.UpperRight;
        }

        /// <summary>
        /// 점수 HUD, 게임오버 대화상자 박스, 다시하기 버튼 및 설정 버튼/창의 스케일을 0.9로 정밀하게 고정합니다.
        /// </summary>
        public void ApplyFixedScale()
        {
            Vector3 fixedScale = new Vector3(0.9f, 0.9f, 0.9f);

            if (_scoreText != null)
            {
                _scoreText.transform.localScale = fixedScale;
            }

            if (_warningBannerText != null)
            {
                _warningBannerText.transform.localScale = fixedScale;
            }

            if (_gameOverPanel != null)
            {
                Transform box = _gameOverPanel.transform.Find("Dialog_Box");
                if (box != null)
                {
                    box.localScale = fixedScale;
                }
            }

            if (_retryButton != null)
            {
                _retryButton.transform.localScale = fixedScale;
            }

            if (_settingsButton != null)
            {
                _settingsButton.transform.localScale = fixedScale;
            }

            if (_settingsPanel != null)
            {
                Transform box = _settingsPanel.transform.Find("Dialog_Box");
                if (box != null)
                {
                    box.localScale = fixedScale;
                }
            }
        }

        /// <summary> 실시간 전진 거리 HUD 텍스트 갱신 (1칸당 10m) </summary>
        private void UpdateScoreDisplay(int score)
        {
            _currentScore = score;
            if (_scoreText != null)
            {
                _scoreText.text = $"{score} m";
            }
        }

        /// <summary> 제자리 지체 시 지각 위기 경고 배너 점멸 처리 </summary>
        private void HandleInactivityWarning(bool isWarning)
        {
            if (_warningBannerText == null) return;

            if (isWarning)
            {
                _warningBannerText.gameObject.SetActive(true);
                if (_warningBlinkRoutine != null) StopCoroutine(_warningBlinkRoutine);
                _warningBlinkRoutine = StartCoroutine(WarningBlinkRoutine());
            }
            else
            {
                if (_warningBlinkRoutine != null)
                {
                    StopCoroutine(_warningBlinkRoutine);
                    _warningBlinkRoutine = null;
                }
                _warningBannerText.gameObject.SetActive(false);
            }
        }

        private System.Collections.IEnumerator WarningBlinkRoutine()
        {
            Color orange = new Color(1f, 0.45f, 0.1f);
            Color red = new Color(1f, 0.15f, 0.15f);

            while (true)
            {
                if (_warningBannerText != null)
                {
                    _warningBannerText.color = red;
                }
                yield return new WaitForSeconds(0.25f);

                if (_warningBannerText != null)
                {
                    _warningBannerText.color = orange;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        /// <summary> 실시간 지각 위기 남은 시간 카운트다운 텍스트 갱신 </summary>
        private void HandleInactivityTimerUpdated(float remainingSeconds, float progress)
        {
            if (_warningBannerText == null || !_warningBannerText.gameObject.activeSelf) return;

            if (remainingSeconds <= 1.0f)
            {
                _warningBannerText.text = $"🚨 출석 마감 직전! ({remainingSeconds:0.0}초)";
            }
            else
            {
                _warningBannerText.text = $"⚠️ 지각 위기! 교수님이 다가옵니다! ({remainingSeconds:0.0}초)";
            }
        }

        /// <summary> 하천/트랙 경계선(아웃 위험 구역) 근접 경고 처리 </summary>
        private void HandleBoundaryWarning(bool isWarning, float dangerProgress, bool isRight)
        {
            if (_warningBannerText == null) return;

            if (isWarning)
            {
                if (!_warningBannerText.gameObject.activeSelf)
                {
                    _warningBannerText.gameObject.SetActive(true);
                }

                string dirText = isRight ? "우측" : "좌측";
                if (dangerProgress >= 0.75f)
                {
                    _warningBannerText.text = $"🚨 {dirText} 배수구 흡입 위험! 부표 이탈 주의!";
                    _warningBannerText.color = new Color(1f, 0.2f, 0.2f);
                }
                else
                {
                    _warningBannerText.text = $"⚠️ 급류 경고! {dirText} 위험 부표로 밀려나는 중!";
                    _warningBannerText.color = new Color(1f, 0.6f, 0.15f);
                }
            }
            else
            {
                // 부표/배수구 관련 경고 텍스트일 때만 비활성화
                if (_warningBannerText.text.Contains("부표") || _warningBannerText.text.Contains("배수구"))
                {
                    _warningBannerText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary> 플레이어 사망 시 게임오버 패널 활성화 및 성적표 발급 </summary>
        private void ShowGameOverPanel(string deathReason, int finalScore)
        {
            _isGameOver = true;

            // 경고 배너 끄기
            HandleInactivityWarning(false);
            HandleBoundaryWarning(false, 0f, false);

            EnsureEventSystem();
            EnsureHUDAndGameOverUI();
            ApplyFixedScale();

            if (_gameOverPanel == null) return;

            // 1. 최고 기록 갱신 및 저장
            int bestScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            if (finalScore > bestScore)
            {
                bestScore = finalScore;
                PlayerPrefs.SetInt(HighScoreKey, bestScore);
                PlayerPrefs.Save();
            }

            // 2. 탈락 사유 및 최종 거리 표시
            if (_deathReasonText != null)
            {
                _deathReasonText.text = deathReason;
            }

            if (_finalScoreText != null)
            {
                _finalScoreText.text = $"최종 거리: {finalScore}m";
            }

            if (_highScoreText != null)
            {
                _highScoreText.text = $"최고 기록: {bestScore}m";
            }

            // 3. 대학생 학점 등급 판정 및 컬러링 연출
            if (_gradeBadgeText != null)
            {
                string grade = EvaluateGrade(finalScore, out Color gradeColor);
                _gradeBadgeText.text = grade;
                _gradeBadgeText.color = gradeColor;
            }

            _gameOverPanel.SetActive(true);
        }

        /// <summary>
        /// 도달 거리(m)에 따른 대학생 학점 등급 및 축제 부스 보상 판정
        /// </summary>
        private string EvaluateGrade(int score, out Color color)
        {
            if (score >= 500)
            {
                color = new Color(1f, 0.85f, 0.15f); // 화려한 골드
                return "학점: A+ (수석 졸업 - 축제 음료 쿠폰!)";
            }
            if (score >= 400)
            {
                color = new Color(0.44f, 0.88f, 0f);  // 우등생 연녹색
                return "학점: A0 (우등생 - 간식 세트)";
            }
            if (score >= 300)
            {
                color = new Color(0.22f, 0.69f, 0f);  // 안전통과 청록색
                return "학점: B+ (안전 통과 - 젤리 세트)";
            }
            if (score >= 200)
            {
                color = new Color(0f, 0.47f, 0.71f);  // 출석성공 하늘색
                return "학점: B0 (출석 성공 - 사탕 획득)";
            }
            if (score >= 100)
            {
                color = new Color(0.97f, 0.50f, 0f);  // 지각모면 주황색
                return "학점: C+ (지각 모면 - 위로 스티커)";
            }

            color = new Color(0.9f, 0.22f, 0.27f);     // 강렬한 F학점 레드
            return "학점: F (재수강 확정 - F학점 경고장)";
        }

        /// <summary> 다시 시작 버튼 클릭 시 씬 새로고침 </summary>
        private void OnRetryButtonClicked()
        {
            _isGameOver = false;
            HandleInactivityWarning(false);
            GameEvents.ClearAllSubscriptions();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #region Settings Management (홈 화면 설정 버튼 & 설정 팝업)

        /// <summary>
        /// 홈 화면 우측 상단 '⚙️ 설정' 버튼 및 설정 팝업 다이얼로그 패널을 자동 생성합니다.
        /// </summary>
        private void EnsureSettingsUI(Canvas canvas, Font defaultFont)
        {
            if (_settingsButton == null)
            {
                // 1. 홈 화면 설정 버튼 (화면 우측 상단)
                GameObject btnObj = new GameObject("Home_Settings_Button");
                btnObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = btnObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f); // 우상단
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-60f, -50f);
                rect.sizeDelta = new Vector2(140f, 54f);
                btnObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

                Image btnImg = btnObj.AddComponent<Image>();
                btnImg.color = new Color(0.12f, 0.15f, 0.22f, 0.92f);

                _settingsButton = btnObj.AddComponent<Button>();
                ColorBlock colors = _settingsButton.colors;
                colors.highlightedColor = new Color(0.22f, 0.28f, 0.40f, 1f);
                colors.pressedColor = new Color(0.10f, 0.12f, 0.18f, 1f);
                _settingsButton.colors = colors;

                Shadow shadow = btnObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
                shadow.effectDistance = new Vector2(2f, -2f);

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(btnObj.transform, false);
                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.sizeDelta = rect.sizeDelta;

                Text btnText = textObj.AddComponent<Text>();
                btnText.font = defaultFont;
                btnText.fontSize = 24;
                btnText.fontStyle = FontStyle.Bold;
                btnText.color = Color.white;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.text = "⚙️ 설정";

                _settingsButton.onClick.RemoveAllListeners();
                _settingsButton.onClick.AddListener(OpenSettings);
            }

            if (_settingsPanel == null)
            {
                // 2. 설정 팝업 다이얼로그 패널
                _settingsPanel = new GameObject("Settings_Panel");
                _settingsPanel.transform.SetParent(canvas.transform, false);

                RectTransform panelRect = _settingsPanel.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;

                Image dimImg = _settingsPanel.AddComponent<Image>();
                dimImg.color = new Color(0f, 0f, 0f, 0.78f);

                // 중앙 다이얼로그 박스
                GameObject boxObj = new GameObject("Dialog_Box");
                boxObj.transform.SetParent(_settingsPanel.transform, false);

                RectTransform boxRect = boxObj.AddComponent<RectTransform>();
                boxRect.anchorMin = new Vector2(0.5f, 0.5f);
                boxRect.anchorMax = new Vector2(0.5f, 0.5f);
                boxRect.pivot = new Vector2(0.5f, 0.5f);
                boxRect.sizeDelta = new Vector2(580f, 620f);
                boxObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

                Image boxImg = boxObj.AddComponent<Image>();
                boxImg.color = new Color(0.11f, 0.13f, 0.17f, 0.98f);

                // 상단 타이틀: ⚙️ 게임 설정
                GameObject titleObj = new GameObject("Title_Text");
                titleObj.transform.SetParent(boxObj.transform, false);
                RectTransform titleRect = titleObj.AddComponent<RectTransform>();
                titleRect.anchoredPosition = new Vector2(0f, 255f);
                titleRect.sizeDelta = new Vector2(400f, 50f);

                Text titleText = titleObj.AddComponent<Text>();
                titleText.font = defaultFont;
                titleText.fontSize = 36;
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = new Color(1f, 0.88f, 0.2f);
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.text = "⚙️ 게임 설정";

                // 상단 우측 [X] 닫기 버튼
                GameObject closeBtnObj = new GameObject("Close_X_Button");
                closeBtnObj.transform.SetParent(boxObj.transform, false);
                RectTransform closeRect = closeBtnObj.AddComponent<RectTransform>();
                closeRect.anchoredPosition = new Vector2(245f, 255f);
                closeRect.sizeDelta = new Vector2(42f, 42f);

                Image closeImg = closeBtnObj.AddComponent<Image>();
                closeImg.color = new Color(0.75f, 0.22f, 0.22f, 0.9f);
                Button closeBtn = closeBtnObj.AddComponent<Button>();
                closeBtn.onClick.AddListener(CloseSettings);

                GameObject closeTextObj = new GameObject("Text");
                closeTextObj.transform.SetParent(closeBtnObj.transform, false);
                RectTransform closeTextRect = closeTextObj.AddComponent<RectTransform>();
                closeTextRect.sizeDelta = closeRect.sizeDelta;
                Text closeText = closeTextObj.AddComponent<Text>();
                closeText.font = defaultFont;
                closeText.fontSize = 22;
                closeText.fontStyle = FontStyle.Bold;
                closeText.color = Color.white;
                closeText.alignment = TextAnchor.MiddleCenter;
                closeText.text = "X";

                // 1) 전체 음량 슬라이더 (Y = 175)
                CreateSliderRow("전체 음량", boxObj.transform, 175f, defaultFont,
                    PlayerPrefs.GetFloat(PrefKeyMasterVolume, 1.0f),
                    OnMasterVolumeChanged,
                    out _masterVolumeSlider, out _masterVolumeValueText);

                // 2) 배경 음악 슬라이더 (Y = 105)
                CreateSliderRow("배경 음악", boxObj.transform, 105f, defaultFont,
                    PlayerPrefs.GetFloat(PrefKeyBGMVolume, 0.8f),
                    OnBGMVolumeChanged,
                    out _bgmVolumeSlider, out _bgmVolumeValueText);

                // 3) 효과음 슬라이더 (Y = 35)
                CreateSliderRow("효과음", boxObj.transform, 35f, defaultFont,
                    PlayerPrefs.GetFloat(PrefKeySFXVolume, 1.0f),
                    OnSFXVolumeChanged,
                    out _sfxVolumeSlider, out _sfxVolumeValueText);

                // 4) 화면 흔들림 토글 (Y = -35)
                bool isShakeOn = PlayerPrefs.GetInt(PrefKeyScreenShake, 1) == 1;
                CreateToggleRow("화면 흔들림", boxObj.transform, -35f, defaultFont,
                    isShakeOn ? "ON (켬)" : "OFF (끔)",
                    isShakeOn ? new Color(0.18f, 0.78f, 0.38f) : new Color(0.4f, 0.45f, 0.5f),
                    OnScreenShakeToggled,
                    out _screenShakeToggleButton, out _screenShakeToggleText, out _screenShakeToggleBg);

                // 5) 화면 모드 토글 (Y = -105)
                bool isFullscreen = Screen.fullScreen;
                CreateToggleRow("화면 모드", boxObj.transform, -105f, defaultFont,
                    isFullscreen ? "[ 전체화면 ]" : "[ 창모드 ]",
                    new Color(0.24f, 0.55f, 0.9f),
                    OnDisplayModeToggled,
                    out _displayModeToggleButton, out _displayModeToggleText, out _);

                // 6) 최고 기록 초기화 (Y = -175)
                GameObject resetRowObj = new GameObject("Reset_Row");
                resetRowObj.transform.SetParent(boxObj.transform, false);

                GameObject resetLabelObj = new GameObject("Label");
                resetLabelObj.transform.SetParent(resetRowObj.transform, false);
                RectTransform resetLabelRect = resetLabelObj.AddComponent<RectTransform>();
                resetLabelRect.anchoredPosition = new Vector2(-150f, -175f);
                resetLabelRect.sizeDelta = new Vector2(160f, 40f);
                Text resetLabelText = resetLabelObj.AddComponent<Text>();
                resetLabelText.font = defaultFont;
                resetLabelText.fontSize = 22;
                resetLabelText.color = Color.white;
                resetLabelText.alignment = TextAnchor.MiddleLeft;
                resetLabelText.text = "최고 기록";

                GameObject resetBtnObj = new GameObject("Reset_Button");
                resetBtnObj.transform.SetParent(resetRowObj.transform, false);
                RectTransform resetBtnRect = resetBtnObj.AddComponent<RectTransform>();
                resetBtnRect.anchoredPosition = new Vector2(95f, -175f);
                resetBtnRect.sizeDelta = new Vector2(180f, 42f);
                Image resetBtnImg = resetBtnObj.AddComponent<Image>();
                resetBtnImg.color = new Color(0.85f, 0.45f, 0.15f);
                Button resetBtn = resetBtnObj.AddComponent<Button>();
                resetBtn.onClick.AddListener(OnResetHighScoreClicked);

                GameObject resetBtnTextObj = new GameObject("Text");
                resetBtnTextObj.transform.SetParent(resetBtnObj.transform, false);
                RectTransform resetBtnTextRect = resetBtnTextObj.AddComponent<RectTransform>();
                resetBtnTextRect.sizeDelta = resetBtnRect.sizeDelta;
                Text resetBtnText = resetBtnTextObj.AddComponent<Text>();
                resetBtnText.font = defaultFont;
                resetBtnText.fontSize = 20;
                resetBtnText.fontStyle = FontStyle.Bold;
                resetBtnText.color = Color.white;
                resetBtnText.alignment = TextAnchor.MiddleCenter;
                resetBtnText.text = "기록 초기화";

                // 초기화 완료 피드백 텍스트 (Y = -215)
                GameObject feedbackObj = new GameObject("Reset_Feedback_Text");
                feedbackObj.transform.SetParent(boxObj.transform, false);
                RectTransform fbRect = feedbackObj.AddComponent<RectTransform>();
                fbRect.anchoredPosition = new Vector2(0f, -215f);
                fbRect.sizeDelta = new Vector2(400f, 30f);
                _resetFeedbackText = feedbackObj.AddComponent<Text>();
                _resetFeedbackText.font = defaultFont;
                _resetFeedbackText.fontSize = 18;
                _resetFeedbackText.color = new Color(0.3f, 0.95f, 0.5f);
                _resetFeedbackText.alignment = TextAnchor.MiddleCenter;
                _resetFeedbackText.text = "최고 기록이 0m로 초기화되었습니다!";
                _resetFeedbackText.gameObject.SetActive(false);

                // 7) 하단 확인/닫기 버튼 (Y = -260)
                GameObject bottomCloseObj = new GameObject("Bottom_Close_Button");
                bottomCloseObj.transform.SetParent(boxObj.transform, false);
                RectTransform bcRect = bottomCloseObj.AddComponent<RectTransform>();
                bcRect.anchoredPosition = new Vector2(0f, -260f);
                bcRect.sizeDelta = new Vector2(260f, 52f);

                Image bcImg = bottomCloseObj.AddComponent<Image>();
                bcImg.color = new Color(0.18f, 0.78f, 0.38f);
                Button bcBtn = bottomCloseObj.AddComponent<Button>();
                bcBtn.onClick.AddListener(CloseSettings);

                GameObject bcTextObj = new GameObject("Text");
                bcTextObj.transform.SetParent(bottomCloseObj.transform, false);
                RectTransform bcTextRect = bcTextObj.AddComponent<RectTransform>();
                bcTextRect.sizeDelta = bcRect.sizeDelta;
                Text bcText = bcTextObj.AddComponent<Text>();
                bcText.font = defaultFont;
                bcText.fontSize = 24;
                bcText.fontStyle = FontStyle.Bold;
                bcText.color = Color.white;
                bcText.alignment = TextAnchor.MiddleCenter;
                bcText.text = "닫기 (ESC)";

                _settingsPanel.SetActive(false);
            }
        }

        private void CreateSliderRow(string label, Transform parent, float yPos, Font font, float initialVal,
            Action<float> onValueChanged, out Slider outSlider, out Text outValText)
        {
            GameObject rowObj = new GameObject($"Row_{label}");
            rowObj.transform.SetParent(parent, false);

            // 라벨 텍스트
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(-150f, yPos);
            labelRect.sizeDelta = new Vector2(160f, 40f);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 22;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.text = label;

            // 슬라이더 루트
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(rowObj.transform, false);
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchoredPosition = new Vector2(35f, yPos);
            sliderRect.sizeDelta = new Vector2(180f, 24f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialVal;

            // 슬라이더 배경
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.22f, 0.28f, 1f);

            // Fill Area & Fill
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.sizeDelta = new Vector2(-16f, 0f);

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.18f, 0.78f, 0.38f);
            slider.fillRect = fillRect;

            // Handle Slide Area & Handle
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = new Vector2(-16f, 0f);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20f, 0f);
            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = Color.white;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImg;

            // 퍼센트 수치 텍스트
            GameObject valObj = new GameObject("Value_Text");
            valObj.transform.SetParent(rowObj.transform, false);
            RectTransform valRect = valObj.AddComponent<RectTransform>();
            valRect.anchoredPosition = new Vector2(185f, yPos);
            valRect.sizeDelta = new Vector2(70f, 40f);
            Text valText = valObj.AddComponent<Text>();
            valText.font = font;
            valText.fontSize = 20;
            valText.color = new Color(0.9f, 0.9f, 0.9f);
            valText.alignment = TextAnchor.MiddleCenter;
            valText.text = $"{Mathf.RoundToInt(initialVal * 100f)}%";

            outSlider = slider;
            outValText = valText;

            slider.onValueChanged.AddListener((v) =>
            {
                valText.text = $"{Mathf.RoundToInt(v * 100f)}%";
                onValueChanged?.Invoke(v);
            });
        }

        private void CreateToggleRow(string label, Transform parent, float yPos, Font font,
            string initialText, Color initialColor, Action onClick,
            out Button outBtn, out Text outBtnText, out Image outBgImg)
        {
            GameObject rowObj = new GameObject($"Row_{label}");
            rowObj.transform.SetParent(parent, false);

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchoredPosition = new Vector2(-150f, yPos);
            labelRect.sizeDelta = new Vector2(160f, 40f);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 22;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.text = label;

            GameObject btnObj = new GameObject("Toggle_Button");
            btnObj.transform.SetParent(rowObj.transform, false);
            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(95f, yPos);
            btnRect.sizeDelta = new Vector2(180f, 42f);

            Image bgImg = btnObj.AddComponent<Image>();
            bgImg.color = initialColor;
            outBgImg = bgImg;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            outBtn = btn;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = btnRect.sizeDelta;
            Text btnText = textObj.AddComponent<Text>();
            btnText.font = font;
            btnText.fontSize = 20;
            btnText.fontStyle = FontStyle.Bold;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.text = initialText;
            outBtnText = btnText;
        }

        /// <summary>
        /// 설정 창을 화면에 열고 조작을 일시정지합니다.
        /// </summary>
        public void OpenSettings()
        {
            if (_settingsPanel == null) return;

            _isSettingsOpen = true;
            _settingsPanel.SetActive(true);

            // 게임 대기 상태에서 플레이어가 움직이지 않도록 조작 차단
            GameEvents.TriggerGameStateChanged(GameState.Paused);
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 설정 창을 닫고 설정을 저장하며 조작을 복구합니다.
        /// </summary>
        public void CloseSettings()
        {
            if (_settingsPanel == null) return;

            _isSettingsOpen = false;
            _settingsPanel.SetActive(false);

            PlayerPrefs.Save();
            Time.timeScale = 1f;

            if (!_isGameStarted)
            {
                GameEvents.TriggerGameStateChanged(GameState.Ready);
            }
            else
            {
                GameEvents.TriggerGameStateChanged(GameState.Playing);
            }
        }

        private void LoadAndApplySettings()
        {
            float masterVol = PlayerPrefs.GetFloat(PrefKeyMasterVolume, 1.0f);
            AudioListener.volume = masterVol;

            if (_masterVolumeSlider != null) _masterVolumeSlider.value = masterVol;
            if (_masterVolumeValueText != null) _masterVolumeValueText.text = $"{Mathf.RoundToInt(masterVol * 100f)}%";

            float bgmVol = PlayerPrefs.GetFloat(PrefKeyBGMVolume, 0.8f);
            if (_bgmVolumeSlider != null) _bgmVolumeSlider.value = bgmVol;
            if (_bgmVolumeValueText != null) _bgmVolumeValueText.text = $"{Mathf.RoundToInt(bgmVol * 100f)}%";

            float sfxVol = PlayerPrefs.GetFloat(PrefKeySFXVolume, 1.0f);
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.value = sfxVol;
            if (_sfxVolumeValueText != null) _sfxVolumeValueText.text = $"{Mathf.RoundToInt(sfxVol * 100f)}%";
        }

        private void OnMasterVolumeChanged(float val)
        {
            AudioListener.volume = val;
            PlayerPrefs.SetFloat(PrefKeyMasterVolume, val);
        }

        private void OnBGMVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat(PrefKeyBGMVolume, val);
        }

        private void OnSFXVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat(PrefKeySFXVolume, val);
        }

        private void OnScreenShakeToggled()
        {
            int current = PlayerPrefs.GetInt(PrefKeyScreenShake, 1);
            int next = (current == 1) ? 0 : 1;
            PlayerPrefs.SetInt(PrefKeyScreenShake, next);

            if (_screenShakeToggleText != null)
            {
                _screenShakeToggleText.text = (next == 1) ? "ON (켬)" : "OFF (끔)";
            }
            if (_screenShakeToggleBg != null)
            {
                _screenShakeToggleBg.color = (next == 1) ? new Color(0.18f, 0.78f, 0.38f) : new Color(0.4f, 0.45f, 0.5f);
            }
        }

        private void OnDisplayModeToggled()
        {
            bool nextFullscreen = !Screen.fullScreen;
            Screen.fullScreen = nextFullscreen;

            if (_displayModeToggleText != null)
            {
                _displayModeToggleText.text = nextFullscreen ? "[ 전체화면 ]" : "[ 창모드 ]";
            }
        }

        private void OnResetHighScoreClicked()
        {
            PlayerPrefs.DeleteKey(HighScoreKey);
            PlayerPrefs.Save();

            if (_highScoreText != null)
            {
                _highScoreText.text = "최고 기록: 0m";
            }

            if (_resetFeedbackText != null)
            {
                if (_resetFeedbackRoutine != null) StopCoroutine(_resetFeedbackRoutine);
                _resetFeedbackRoutine = StartCoroutine(ResetFeedbackRoutine());
            }
        }

        private System.Collections.IEnumerator ResetFeedbackRoutine()
        {
            _resetFeedbackText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(2.5f);
            _resetFeedbackText.gameObject.SetActive(false);
            _resetFeedbackRoutine = null;
        }

        #endregion

        #region Splash Screen Management (스플래시 인트로 화면)

        /// <summary>
        /// 레트로 아케이드 감성의 스플래시 인트로 화면(로고, 프로그레스 바, 대학생 컨셉 문구)을 자동 생성합니다.
        /// </summary>
        private void EnsureSplashScreenUI(Canvas canvas, Font defaultFont)
        {
            if (_splashScreenPanel != null) return;

            Transform existing = canvas.transform.Find("SplashScreen_Panel");
            if (existing != null)
            {
                _splashScreenPanel = existing.gameObject;
                _splashProgressFillRect = _splashScreenPanel.transform.Find("Content_Container/ProgressBar_Box/Fill_Area/Fill")?.GetComponent<RectTransform>();
                _splashProgressPercentText = _splashScreenPanel.transform.Find("Content_Container/Percent_Text")?.GetComponent<Text>();
                _splashStatusText = _splashScreenPanel.transform.Find("Content_Container/Status_Text")?.GetComponent<Text>();
                return;
            }

            // [스플래시 전체 화면 루트 패널]
            GameObject splashObj = new GameObject("SplashScreen_Panel");
            splashObj.transform.SetParent(canvas.transform, false);

            RectTransform splashRect = splashObj.AddComponent<RectTransform>();
            splashRect.anchorMin = Vector2.zero;
            splashRect.anchorMax = Vector2.one;
            splashRect.sizeDelta = Vector2.zero;

            Image bgImg = splashObj.AddComponent<Image>();
            bgImg.color = new Color(0.045f, 0.055f, 0.085f, 1f); // 묵직하고 고급스러운 딥 다크 네이비

            CanvasGroup cg = splashObj.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            _splashScreenPanel = splashObj;

            // [중앙 컨텐츠 컨테이너]
            GameObject contentObj = new GameObject("Content_Container");
            contentObj.transform.SetParent(splashObj.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(1000f, 700f);
            contentObj.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);

            // 1. 상단 프로젝트 배지
            GameObject badgeObj = new GameObject("Badge_Text");
            badgeObj.transform.SetParent(contentObj.transform, false);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchoredPosition = new Vector2(0f, 230f);
            badgeRect.sizeDelta = new Vector2(600f, 40f);
            Text badgeText = badgeObj.AddComponent<Text>();
            badgeText.font = defaultFont;
            badgeText.fontSize = 22;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.color = new Color(1f, 0.76f, 0.22f); // 골드 오렌지
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.text = "⚡ CAMPUS SURVIVAL ACTION ⚡";
            Shadow badgeShadow = badgeObj.AddComponent<Shadow>();
            badgeShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            badgeShadow.effectDistance = new Vector2(2f, -2f);

            // 2. 메인 영문 로고 타이틀: CAMPUS RUN
            GameObject titleObj = new GameObject("MainTitle_Text");
            titleObj.transform.SetParent(contentObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 150f);
            titleRect.sizeDelta = new Vector2(900f, 120f);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = defaultFont;
            titleText.fontSize = 92;
            titleText.fontStyle = FontStyle.Normal;
            titleText.color = new Color(1f, 0.88f, 0.2f); // 화려한 레트로 골드 옐로우
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.text = "CAMPUS RUN";

            Outline titleOutline = titleObj.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.12f, 0.05f, 0.02f, 0.95f);
            titleOutline.effectDistance = new Vector2(3f, -3f);
            Shadow titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            titleShadow.effectDistance = new Vector2(4f, -4f);

            // 3. 한글 서브 타이틀: 캠퍼스 런 : 지각 5분전!!
            GameObject subTitleObj = new GameObject("SubTitle_Text");
            subTitleObj.transform.SetParent(contentObj.transform, false);
            RectTransform subTitleRect = subTitleObj.AddComponent<RectTransform>();
            subTitleRect.anchoredPosition = new Vector2(0f, 75f);
            subTitleRect.sizeDelta = new Vector2(800f, 55f);
            Text subTitleText = subTitleObj.AddComponent<Text>();
            subTitleText.font = defaultFont;
            subTitleText.fontSize = 38;
            subTitleText.fontStyle = FontStyle.Normal;
            subTitleText.color = new Color(0.28f, 0.98f, 0.48f); // 에메랄드 그린
            subTitleText.alignment = TextAnchor.MiddleCenter;
            subTitleText.text = "캠 퍼 스 런 : 지 각  5 분 전 ! !";
            Shadow subTitleShadow = subTitleObj.AddComponent<Shadow>();
            subTitleShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            subTitleShadow.effectDistance = new Vector2(2f, -2f);

            // 4. 중앙 도트 이모지 심볼
            GameObject emojiObj = new GameObject("Emoji_Text");
            emojiObj.transform.SetParent(contentObj.transform, false);
            RectTransform emojiRect = emojiObj.AddComponent<RectTransform>();
            emojiRect.anchoredPosition = new Vector2(0f, 5f);
            emojiRect.sizeDelta = new Vector2(600f, 60f);
            Text emojiText = emojiObj.AddComponent<Text>();
            emojiText.font = defaultFont;
            emojiText.fontSize = 44;
            emojiText.alignment = TextAnchor.MiddleCenter;
            emojiText.text = "🏃‍♂️💨   ⏰   🏫";

            // 5. 스토리 인트로 안내 문구
            GameObject storyObj = new GameObject("Story_Text");
            storyObj.transform.SetParent(contentObj.transform, false);
            RectTransform storyRect = storyObj.AddComponent<RectTransform>();
            storyRect.anchoredPosition = new Vector2(0f, -65f);
            storyRect.sizeDelta = new Vector2(800f, 40f);
            Text storyText = storyObj.AddComponent<Text>();
            storyText.font = defaultFont;
            storyText.fontSize = 22;
            storyText.color = new Color(0.88f, 0.92f, 0.96f, 0.9f);
            storyText.alignment = TextAnchor.MiddleCenter;
            storyText.text = "1교시 출석 마감 직전! 셔틀버스를 피하고 강의실로 질주하라!";

            // 6. 로딩 프로그레스 바 컨테이너
            GameObject barBoxObj = new GameObject("ProgressBar_Box");
            barBoxObj.transform.SetParent(contentObj.transform, false);
            RectTransform barBoxRect = barBoxObj.AddComponent<RectTransform>();
            barBoxRect.anchoredPosition = new Vector2(0f, -145f);
            barBoxRect.sizeDelta = new Vector2(560f, 26f);

            Image barBg = barBoxObj.AddComponent<Image>();
            barBg.color = new Color(0.12f, 0.14f, 0.19f, 0.95f);

            Outline barOutline = barBoxObj.AddComponent<Outline>();
            barOutline.effectColor = new Color(0.24f, 0.28f, 0.38f, 0.85f);
            barOutline.effectDistance = new Vector2(2f, -2f);

            // Fill Area & Fill
            GameObject fillArea = new GameObject("Fill_Area");
            fillArea.transform.SetParent(barBoxObj.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = new Vector2(-6f, -6f);

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            _splashProgressFillRect = fillObj.AddComponent<RectTransform>();
            _splashProgressFillRect.anchorMin = new Vector2(0f, 0f);
            _splashProgressFillRect.anchorMax = new Vector2(0f, 1f);
            _splashProgressFillRect.pivot = new Vector2(0f, 0.5f);
            _splashProgressFillRect.sizeDelta = new Vector2(0f, 0f);

            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = new Color(0.22f, 0.88f, 0.45f, 1f); // 산뜻한 에메랄드 그린

            // 7. 퍼센트 텍스트
            GameObject percentObj = new GameObject("Percent_Text");
            percentObj.transform.SetParent(contentObj.transform, false);
            RectTransform percentRect = percentObj.AddComponent<RectTransform>();
            percentRect.anchoredPosition = new Vector2(0f, -188f);
            percentRect.sizeDelta = new Vector2(200f, 36f);
            _splashProgressPercentText = percentObj.AddComponent<Text>();
            _splashProgressPercentText.font = defaultFont;
            _splashProgressPercentText.fontSize = 24;
            _splashProgressPercentText.fontStyle = FontStyle.Bold;
            _splashProgressPercentText.color = new Color(1f, 0.88f, 0.25f);
            _splashProgressPercentText.alignment = TextAnchor.MiddleCenter;
            _splashProgressPercentText.text = "0%";

            // 8. 대학생 컨셉 상태 텍스트
            GameObject statusObj = new GameObject("Status_Text");
            statusObj.transform.SetParent(contentObj.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchoredPosition = new Vector2(0f, -225f);
            statusRect.sizeDelta = new Vector2(700f, 36f);
            _splashStatusText = statusObj.AddComponent<Text>();
            _splashStatusText.font = defaultFont;
            _splashStatusText.fontSize = 20;
            _splashStatusText.color = new Color(0.48f, 0.88f, 1f, 0.95f);
            _splashStatusText.alignment = TextAnchor.MiddleCenter;
            _splashStatusText.text = SplashStatusMessages[0];

            // 9. 스킵 안내 텍스트
            GameObject skipObj = new GameObject("Skip_Prompt_Text");
            skipObj.transform.SetParent(contentObj.transform, false);
            RectTransform skipRect = skipObj.AddComponent<RectTransform>();
            skipRect.anchoredPosition = new Vector2(0f, -280f);
            skipRect.sizeDelta = new Vector2(600f, 34f);
            Text skipText = skipObj.AddComponent<Text>();
            skipText.font = defaultFont;
            skipText.fontSize = 18;
            skipText.color = new Color(1f, 1f, 1f, 0.5f);
            skipText.alignment = TextAnchor.MiddleCenter;
            skipText.text = "[ SPACE / ENTER / 클릭 시 즉시 스킵 ]";

            _splashScreenPanel.SetActive(false);
        }

        /// <summary>
        /// 스플래시 화면 진행 시퀀스 코루틴입니다.
        /// </summary>
        private System.Collections.IEnumerator SplashScreenSequenceRoutine()
        {
            if (_splashScreenPanel == null) yield break;

            CanvasGroup cg = _splashScreenPanel.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            float maxBarWidth = 554f; // 560f - 6f padding
            float elapsed = 0f;
            float duration = Mathf.Max(1.5f, _splashDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // 부드러운 가속도 (SmoothStep 곡선)
                float smoothed = Mathf.SmoothStep(0f, 1f, progress);
                int percentInt = Mathf.Clamp(Mathf.RoundToInt(smoothed * 100f), 0, 100);

                // 1. 프로그레스 바 Fill 너비 갱신
                if (_splashProgressFillRect != null)
                {
                    _splashProgressFillRect.sizeDelta = new Vector2(maxBarWidth * smoothed, 0f);
                }

                // 2. 캐싱된 퍼센트 텍스트 갱신 (GC Alloc 0B)
                if (_splashProgressPercentText != null)
                {
                    _splashProgressPercentText.text = CachedPercentStrings[percentInt];
                }

                // 3. 진행도별 상태 메시지 전환 (GC Alloc 0B)
                if (_splashStatusText != null)
                {
                    int msgIdx = Mathf.Min(percentInt / 20, SplashStatusMessages.Length - 1);
                    _splashStatusText.text = SplashStatusMessages[msgIdx];
                }

                yield return null;
            }

            // 100% 도달 완료
            if (_splashProgressFillRect != null) _splashProgressFillRect.sizeDelta = new Vector2(maxBarWidth, 0f);
            if (_splashProgressPercentText != null) _splashProgressPercentText.text = CachedPercentStrings[100];
            if (_splashStatusText != null) _splashStatusText.text = SplashStatusMessages[4];

            yield return new WaitForSeconds(0.25f);

            // 스플래시 화면 페이드아웃 및 홈 화면으로 전환
            yield return StartCoroutine(FadeOutSplashScreenRoutine());
        }

        /// <summary>
        /// 유저가 키 입력이나 클릭으로 스플래시를 즉시 건너뛸 때 호출됩니다.
        /// </summary>
        public void SkipSplashScreen()
        {
            if (!_isSplashActive) return;

            if (_splashSequenceRoutine != null)
            {
                StopCoroutine(_splashSequenceRoutine);
                _splashSequenceRoutine = null;
            }

            StartCoroutine(FadeOutSplashScreenRoutine(fastSkip: true));
        }

        private System.Collections.IEnumerator FadeOutSplashScreenRoutine(bool fastSkip = false)
        {
            if (_splashScreenPanel == null)
            {
                EndSplashScreen();
                yield break;
            }

            CanvasGroup cg = _splashScreenPanel.GetComponent<CanvasGroup>();
            float fadeDuration = fastSkip ? 0.2f : 0.35f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                if (cg != null) cg.alpha = 1f - t;
                yield return null;
            }

            EndSplashScreen();
        }

        private void EndSplashScreen()
        {
            _isSplashActive = false;

            if (_splashScreenPanel != null)
            {
                _splashScreenPanel.SetActive(false);
            }

            // 홈 타이틀 화면 및 홈 설정 버튼 활성화
            if (_titleScreenPanel != null)
            {
                _titleScreenPanel.SetActive(true);
            }

            if (_settingsButton != null)
            {
                _settingsButton.gameObject.SetActive(true);
            }

            StartPromptPulse();
        }

        #endregion
    }
}
