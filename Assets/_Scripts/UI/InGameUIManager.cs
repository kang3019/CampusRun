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

        [Header("--- 폰트 설정 ---")]
        [Tooltip("레트로 도트 폰트 (미지정 시 Resources/Fonts/neodgm 자동 로드)")]
        [SerializeField] private Font _pixelFont;

        private const string HighScoreKey = "CampusRun_HighScore";
        private int _currentScore = 0;
        private bool _isGameOver = false;
        private bool _isGameStarted = false;
        private Coroutine _warningBlinkRoutine;
        private Coroutine _promptPulseRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeInScene()
        {
            if (FindAnyObjectByType<InGameUIManager>() == null)
            {
                // 씬에 플레이어 컨트롤러가 있는 실제 게임 씬에서만 자동 생성 (빈 씬에서의 오작동 방지)
                if (FindAnyObjectByType<CampusRun.Player.GridPlayerController>() != null)
                {
                    GameObject uiManagerObj = new GameObject("[InGameUIManager]");
                    uiManagerObj.AddComponent<InGameUIManager>();
                }
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

            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(OnRetryButtonClicked);
            }

            // 시작 화면 활성화 및 점수 HUD 대기 상태 초기화
            _isGameStarted = false;
            if (_titleScreenPanel != null)
            {
                _titleScreenPanel.SetActive(true);
            }
            if (_scoreText != null)
            {
                _scoreText.gameObject.SetActive(false);
            }
            StartPromptPulse();
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarted += HandleGameStarted;
            GameEvents.OnGameRestarted += HandleGameRestarted;
            GameEvents.OnScoreChanged += UpdateScoreDisplay;
            GameEvents.OnPlayerDied += ShowGameOverPanel;
            GameEvents.OnInactivityWarning += HandleInactivityWarning;
            GameEvents.OnInactivityTimerUpdated += HandleInactivityTimerUpdated;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            GameEvents.OnScoreChanged -= UpdateScoreDisplay;
            GameEvents.OnPlayerDied -= ShowGameOverPanel;
            GameEvents.OnInactivityWarning -= HandleInactivityWarning;
            GameEvents.OnInactivityTimerUpdated -= HandleInactivityTimerUpdated;
        }

        private void Start()
        {
            UpdateScoreDisplay(0);
        }

        private void Update()
        {
            // 게임 시작 전: 마우스 좌클릭, 스페이스바, 엔터키로도 즉시 첫 도약하며 게임 시작 가능
            if (!_isGameStarted && !_isGameOver)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                var mouse = UnityEngine.InputSystem.Mouse.current;

                bool shouldStart = false;
                if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                {
                    shouldStart = true;
                }
                else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    shouldStart = true;
                }

                if (shouldStart)
                {
                    var player = FindAnyObjectByType<CampusRun.Player.GridPlayerController>();
                    if (player != null && player.IsAlive)
                    {
                        player.TryHop(Vector3Int.forward);
                    }
                    else
                    {
                        HandleGameStarted();
                    }
                }
            }

            // 게임오버 시 키보드 R키, 스페이스바, 엔터키로도 즉시 다시하기 가능
            if (_isGameOver)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && (keyboard.rKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
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
        /// 점수 HUD, 게임오버 대화상자 박스, 다시하기 버튼의 스케일을 0.9로 정밀하게 고정합니다.
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

        /// <summary> 플레이어 사망 시 게임오버 패널 활성화 및 성적표 발급 </summary>
        private void ShowGameOverPanel(string deathReason, int finalScore)
        {
            _isGameOver = true;

            // 경고 배너 끄기
            HandleInactivityWarning(false);

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
    }
}
