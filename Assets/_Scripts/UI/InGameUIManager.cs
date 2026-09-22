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
        [Tooltip("실시간 전진 점수 텍스트 (0, 1, 2, 3...)")]
        [SerializeField] private Text _scoreText;

        [Header("--- 게임오버 성적표 패널 ---")]
        [Tooltip("사망 시 활성화될 게임오버 팝업 패널")]
        [SerializeField] private GameObject _gameOverPanel;

        [Tooltip("탈락 사유 텍스트")]
        [SerializeField] private Text _deathReasonText;

        [Tooltip("최종 기록 텍스트")]
        [SerializeField] private Text _finalScoreText;

        [Tooltip("최고 기록 텍스트")]
        [SerializeField] private Text _highScoreText;

        [Tooltip("다시하기 버튼")]
        [SerializeField] private Button _retryButton;

        private const string HighScoreKey = "CampusRun_HighScore";
        private int _currentScore = 0;
        private bool _isGameOver = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeInScene()
        {
            if (FindFirstObjectByType<InGameUIManager>() == null)
            {
                GameObject uiManagerObj = new GameObject("[InGameUIManager]");
                uiManagerObj.AddComponent<InGameUIManager>();
            }
        }

        private void Awake()
        {
            EnsureEventSystem();
            EnsureHUDAndGameOverUI();
            ApplyFixedScale();

            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(false);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(OnRetryButtonClicked);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnScoreChanged += UpdateScoreDisplay;
            GameEvents.OnPlayerDied += ShowGameOverPanel;
        }

        private void OnDisable()
        {
            GameEvents.OnScoreChanged -= UpdateScoreDisplay;
            GameEvents.OnPlayerDied -= ShowGameOverPanel;
        }

        private void Start()
        {
            UpdateScoreDisplay(0);
        }

        private void Update()
        {
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
            EventSystem es = FindFirstObjectByType<EventSystem>();
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
        /// 씬에 UI Canvas가 없을 경우 100% 안전하게 내장 폰트를 활용한 점수 HUD 및 게임오버 패널을 자동 생성합니다.
        /// </summary>
        private void EnsureHUDAndGameOverUI()
        {
            if (_scoreText != null && _gameOverPanel != null) return;

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            // 1. Canvas 생성 또는 탐색
            Canvas canvas = FindFirstObjectByType<Canvas>();
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

            // 2. 화면 좌측 상단 실시간 점수 HUD 생성
            if (_scoreText == null)
            {
                GameObject scoreObj = new GameObject("Score_HUD_Text");
                scoreObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = scoreObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f); // 좌상단
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(60f, -50f);
                rect.sizeDelta = new Vector2(400f, 120f);

                _scoreText = scoreObj.AddComponent<Text>();
                _scoreText.font = defaultFont;
                _scoreText.fontSize = 76;
                _scoreText.fontStyle = FontStyle.Bold;
                _scoreText.color = Color.white;
                _scoreText.alignment = TextAnchor.UpperLeft;
                _scoreText.text = "0";

                // 또렷한 검은색 그림자
                Shadow shadow = scoreObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(3f, -3f);

                // 스케일 0.9 고정
                scoreObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            }

            // 3. 게임오버 팝업 패널 생성 (사망 시 활성화)
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
                boxRect.sizeDelta = new Vector2(540f, 460f);

                // 스케일 0.9 고정
                boxObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

                Image boxImg = boxObj.AddComponent<Image>();
                boxImg.color = new Color(0.12f, 0.12f, 0.16f, 0.96f); // 묵직한 다크 패널

                // [GAME OVER 타이틀]
                GameObject titleObj = new GameObject("Title_Text");
                titleObj.transform.SetParent(boxObj.transform, false);
                RectTransform titleRect = titleObj.AddComponent<RectTransform>();
                titleRect.anchoredPosition = new Vector2(0f, 145f);
                titleRect.sizeDelta = new Vector2(500f, 60f);
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
                reasonRect.anchoredPosition = new Vector2(0f, 80f);
                reasonRect.sizeDelta = new Vector2(480f, 50f);
                _deathReasonText = reasonObj.AddComponent<Text>();
                _deathReasonText.font = defaultFont;
                _deathReasonText.fontSize = 25;
                _deathReasonText.color = new Color(0.85f, 0.85f, 0.85f);
                _deathReasonText.alignment = TextAnchor.MiddleCenter;
                _deathReasonText.text = "셔틀버스를 피하지 못했습니다!";

                // [최종 점수]
                GameObject finalObj = new GameObject("FinalScore_Text");
                finalObj.transform.SetParent(boxObj.transform, false);
                RectTransform finalRect = finalObj.AddComponent<RectTransform>();
                finalRect.anchoredPosition = new Vector2(0f, 18f);
                finalRect.sizeDelta = new Vector2(480f, 60f);
                _finalScoreText = finalObj.AddComponent<Text>();
                _finalScoreText.font = defaultFont;
                _finalScoreText.fontSize = 40;
                _finalScoreText.fontStyle = FontStyle.Bold;
                _finalScoreText.color = Color.white;
                _finalScoreText.alignment = TextAnchor.MiddleCenter;
                _finalScoreText.text = "최종 점수: 0";

                // [최고 기록]
                GameObject highObj = new GameObject("HighScore_Text");
                highObj.transform.SetParent(boxObj.transform, false);
                RectTransform highRect = highObj.AddComponent<RectTransform>();
                highRect.anchoredPosition = new Vector2(0f, -38f);
                highRect.sizeDelta = new Vector2(480f, 40f);
                _highScoreText = highObj.AddComponent<Text>();
                _highScoreText.font = defaultFont;
                _highScoreText.fontSize = 25;
                _highScoreText.color = new Color(1f, 0.82f, 0.2f); // 골드
                _highScoreText.alignment = TextAnchor.MiddleCenter;
                _highScoreText.text = "최고 기록: 0";

                // [다시 하기 버튼]
                GameObject btnObj = new GameObject("Retry_Button");
                btnObj.transform.SetParent(boxObj.transform, false);
                RectTransform btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(0f, -125f);
                btnRect.sizeDelta = new Vector2(300f, 75f);

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
                btnText.fontSize = 32;
                btnText.fontStyle = FontStyle.Bold;
                btnText.color = Color.white;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.text = "다시 하기 (R)";

                _gameOverPanel.SetActive(false);
            }
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

        /// <summary> 실시간 전진 점수 HUD 텍스트 갱신 (1칸 전진 시 1점) </summary>
        private void UpdateScoreDisplay(int score)
        {
            _currentScore = score;
            if (_scoreText != null)
            {
                _scoreText.text = score.ToString();
            }
        }

        /// <summary> 플레이어 사망 시 게임오버 패널 활성화 및 점수 집계 </summary>
        private void ShowGameOverPanel(string deathReason, int finalScore)
        {
            _isGameOver = true;

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

            // 2. 탈락 사유 및 최종 점수 표시
            if (_deathReasonText != null)
            {
                _deathReasonText.text = deathReason;
            }

            if (_finalScoreText != null)
            {
                _finalScoreText.text = $"최종 점수: {finalScore}";
            }

            if (_highScoreText != null)
            {
                _highScoreText.text = $"최고 기록: {bestScore}";
            }

            _gameOverPanel.SetActive(true);
        }

        /// <summary> 다시 시작 버튼 클릭 시 씬 새로고침 </summary>
        private void OnRetryButtonClicked()
        {
            _isGameOver = false;
            GameEvents.ClearAllSubscriptions();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
