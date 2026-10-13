using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CampusRun.UI;

namespace CampusRun.Leaderboard
{
    /// <summary>
    /// 인게임 TOP 5 랭킹 모달 및 신기록 달성 시 이름 입력 팝업을 담당하는 UI 컨트롤러입니다.
    /// 별도의 프리팹 없이도 런타임에 완전 자동 생성되어 도트 폰트로 깔끔하게 렌더링됩니다.
    /// </summary>
    public class LeaderboardUI : MonoBehaviour
    {
        public static LeaderboardUI Instance { get; private set; }

        [Header("--- 모달 패널 루트 ---")]
        [SerializeField] private GameObject _leaderboardModal;
        [SerializeField] private GameObject _nameInputModal;

        public bool IsLeaderboardActive => _leaderboardModal != null && _leaderboardModal.activeSelf;
        public bool IsInputModalActive => _nameInputModal != null && _nameInputModal.activeSelf;

        [Header("--- 랭킹 행 텍스트 컴포넌트 목록 ---")]
        private readonly List<Text> _rankTexts = new List<Text>();
        private readonly List<Text> _nameTexts = new List<Text>();
        private readonly List<Text> _scoreTexts = new List<Text>();
        private readonly List<Text> _gradeTexts = new List<Text>();
        private readonly List<Image> _rowBgImages = new List<Image>();

        [Header("--- 이름 입력 필드 컴포넌트 ---")]
        private InputField _nameInputField;
        private Text _inputPromptText;
        private int _pendingScore;
        private string _pendingGrade;
        private Action _onInputCompletedCallback;

        private Font _pixelFont;
        private bool _isInitialized = false;
        private bool _isSubmitting = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureUI();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Esc 키로 열려 있는 모달 닫기
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_leaderboardModal != null && _leaderboardModal.activeSelf)
                {
                    CloseLeaderboard();
                }
            }

            // Tab 키 또는 L 키로 랭킹 모달 토글
            if ((keyboard.tabKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame) && 
                (_nameInputModal == null || !_nameInputModal.activeSelf))
            {
                ToggleLeaderboard();
            }

            // 이름 입력 모달이 열려 있을 때 Enter 키 누르면 즉시 저장
            if (_nameInputModal != null && _nameInputModal.activeSelf && !_isSubmitting)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    OnSubmitNameClicked();
                }
            }
        }

        /// <summary>
        /// 도트 폰트를 로드하고 랭킹 모달 및 이름 입력 UI 계층 구조를 동적으로 생성합니다.
        /// </summary>
        public void EnsureUI()
        {
            if (_isInitialized) return;

            // 1. 도트 폰트 로드
            _pixelFont = Resources.Load<Font>("Fonts/neodgm");
            if (_pixelFont == null)
            {
#if UNITY_EDITOR
                _pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/neodgm.ttf");
#endif
            }
            if (_pixelFont == null)
            {
                _pixelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            // 2. 부모 Canvas 탐색 또는 생성
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }

            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Leaderboard_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 150; // InGame UI(100)보다 위에 렌더링

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 3. TOP 5 랭킹 모달 생성
            CreateLeaderboardModal(canvas.transform);

            // 4. 이름 입력 모달 생성
            CreateNameInputModal(canvas.transform);

            _isInitialized = true;
        }

        #region --- 랭킹 모달 UI 생성 ---

        private void CreateLeaderboardModal(Transform canvasTransform)
        {
            if (_leaderboardModal != null) return;

            // 딤 배경
            GameObject dimObj = new GameObject("Leaderboard_Dim");
            dimObj.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = dimObj.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            Image dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);

            // 팝업 박스 (가로 840, 세로 620)
            GameObject boxObj = new GameObject("Leaderboard_Box");
            boxObj.transform.SetParent(dimObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(840f, 620f);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.07f, 0.08f, 0.14f, 0.98f); // 짙은 네이비

            Outline boxOutline = boxObj.AddComponent<Outline>();
            boxOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.9f); // 골드 테두리
            boxOutline.effectDistance = new Vector2(3f, -3f);

            // 제목 라벨
            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(boxObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 245f);
            titleRect.sizeDelta = new Vector2(800f, 60f);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = _pixelFont;
            titleText.fontSize = 38;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.85f, 0.2f);
            titleText.text = "🏆 명예의 전당 (TOP 5 랭킹) 🏆";

            Shadow titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = Color.black;
            titleShadow.effectDistance = new Vector2(2f, -2f);

            // 헤더 라벨
            GameObject headerObj = new GameObject("Header_Row");
            headerObj.transform.SetParent(boxObj.transform, false);
            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchoredPosition = new Vector2(0f, 180f);
            headerRect.sizeDelta = new Vector2(760f, 40f);

            CreateHeaderLabel(headerObj.transform, "순위", -290f, 100f);
            CreateHeaderLabel(headerObj.transform, "러너 (학번/이름)", -90f, 280f);
            CreateHeaderLabel(headerObj.transform, "생존 거리", 140f, 160f);
            CreateHeaderLabel(headerObj.transform, "학점", 290f, 120f);

            // 5개 랭킹 행 생성
            _rankTexts.Clear();
            _nameTexts.Clear();
            _scoreTexts.Clear();
            _gradeTexts.Clear();
            _rowBgImages.Clear();

            float startY = 115f;
            float rowHeight = 62f;

            for (int i = 0; i < 5; i++)
            {
                CreateLeaderboardRow(boxObj.transform, i, startY - (i * rowHeight));
            }

            // 하단 닫기 버튼
            GameObject closeBtnObj = new GameObject("Close_Button");
            closeBtnObj.transform.SetParent(boxObj.transform, false);
            RectTransform closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
            closeBtnRect.anchoredPosition = new Vector2(0f, -245f);
            closeBtnRect.sizeDelta = new Vector2(240f, 55f);

            Image closeBtnImg = closeBtnObj.AddComponent<Image>();
            closeBtnImg.color = new Color(0.2f, 0.6f, 0.9f);

            Button closeBtn = closeBtnObj.AddComponent<Button>();
            closeBtn.onClick.AddListener(CloseLeaderboard);

            GameObject closeTextObj = new GameObject("Close_Text");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            RectTransform closeTextRect = closeTextObj.AddComponent<RectTransform>();
            closeTextRect.sizeDelta = closeBtnRect.sizeDelta;

            Text closeText = closeTextObj.AddComponent<Text>();
            closeText.font = _pixelFont;
            closeText.fontSize = 24;
            closeText.fontStyle = FontStyle.Bold;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = Color.white;
            closeText.text = "닫기 (Esc)";

            // 하단 조작 가이드 안내 텍스트
            GameObject guideObj = new GameObject("Guide_Text");
            guideObj.transform.SetParent(boxObj.transform, false);
            RectTransform guideRect = guideObj.AddComponent<RectTransform>();
            guideRect.anchoredPosition = new Vector2(0f, -285f);
            guideRect.sizeDelta = new Vector2(700f, 25f);
            Text guideText = guideObj.AddComponent<Text>();
            guideText.font = _pixelFont;
            guideText.fontSize = 15;
            guideText.alignment = TextAnchor.MiddleCenter;
            guideText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            guideText.text = "단축키: Tab 또는 L 키로 랭킹 열기 / Esc 키로 닫기";

            _leaderboardModal = dimObj;
            _leaderboardModal.SetActive(false);
        }

        private void CreateHeaderLabel(Transform parent, string text, float posX, float width)
        {
            GameObject obj = new GameObject($"Header_{text}");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(posX, 0f);
            rect.sizeDelta = new Vector2(width, 35f);

            Text label = obj.AddComponent<Text>();
            label.font = _pixelFont;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.75f, 0.85f, 1f);
            label.text = text;
        }

        private void CreateLeaderboardRow(Transform parent, int index, float posY)
        {
            GameObject rowObj = new GameObject($"Row_{index + 1}");
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.anchoredPosition = new Vector2(0f, posY);
            rowRect.sizeDelta = new Vector2(760f, 52f);

            Image rowBg = rowObj.AddComponent<Image>();
            rowBg.color = (index % 2 == 0) ? new Color(0.12f, 0.14f, 0.22f, 0.85f) : new Color(0.09f, 0.11f, 0.18f, 0.85f);
            _rowBgImages.Add(rowBg);

            // 순위 텍스트
            Text rankText = CreateRowCell(rowObj.transform, $"Rank_{index}", -290f, 100f, 22, FontStyle.Bold);
            _rankTexts.Add(rankText);

            // 이름 텍스트
            Text nameText = CreateRowCell(rowObj.transform, $"Name_{index}", -90f, 280f, 20, FontStyle.Normal);
            _nameTexts.Add(nameText);

            // 점수 텍스트
            Text scoreText = CreateRowCell(rowObj.transform, $"Score_{index}", 140f, 160f, 22, FontStyle.Bold);
            _scoreTexts.Add(scoreText);

            // 학점 텍스트
            Text gradeText = CreateRowCell(rowObj.transform, $"Grade_{index}", 290f, 120f, 22, FontStyle.Bold);
            _gradeTexts.Add(gradeText);
        }

        private Text CreateRowCell(Transform parent, string name, float posX, float width, int fontSize, FontStyle style)
        {
            GameObject cellObj = new GameObject(name);
            cellObj.transform.SetParent(parent, false);
            RectTransform rect = cellObj.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(posX, 0f);
            rect.sizeDelta = new Vector2(width, 45f);

            Text text = cellObj.AddComponent<Text>();
            text.font = _pixelFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        #endregion

        #region --- 이름 입력 모달 UI 생성 ---

        private void CreateNameInputModal(Transform canvasTransform)
        {
            if (_nameInputModal != null) return;

            GameObject dimObj = new GameObject("NameInput_Dim");
            dimObj.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = dimObj.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            Image dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.8f);

            // 박스 (가로 680, 세로 420)
            GameObject boxObj = new GameObject("NameInput_Box");
            boxObj.transform.SetParent(dimObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(680f, 420f);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.08f, 0.1f, 0.16f, 0.98f);

            Outline boxOutline = boxObj.AddComponent<Outline>();
            boxOutline.effectColor = new Color(0.2f, 0.9f, 0.4f, 0.9f); // 에메랄드 테두리
            boxOutline.effectDistance = new Vector2(3f, -3f);

            // 축하 타이틀
            GameObject titleObj = new GameObject("InputTitle_Text");
            titleObj.transform.SetParent(boxObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 150f);
            titleRect.sizeDelta = new Vector2(640f, 50f);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = _pixelFont;
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.88f, 0.2f);
            titleText.text = "🎉 명예의 전당 TOP 5 진입! 🎉";

            // 기록 안내 텍스트
            GameObject promptObj = new GameObject("Prompt_Text");
            promptObj.transform.SetParent(boxObj.transform, false);
            RectTransform promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchoredPosition = new Vector2(0f, 85f);
            promptRect.sizeDelta = new Vector2(620f, 55f);

            _inputPromptText = promptObj.AddComponent<Text>();
            _inputPromptText.font = _pixelFont;
            _inputPromptText.fontSize = 24;
            _inputPromptText.alignment = TextAnchor.MiddleCenter;
            _inputPromptText.color = Color.white;
            _inputPromptText.text = "최종 거리: 0m (예상 순위: 1위)";

            // 입력 필드 (InputField)
            GameObject inputFieldObj = new GameObject("InputField");
            inputFieldObj.transform.SetParent(boxObj.transform, false);
            RectTransform inputRect = inputFieldObj.AddComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(0f, 0f);
            inputRect.sizeDelta = new Vector2(420f, 60f);

            Image inputBg = inputFieldObj.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.18f, 0.28f);

            Outline inputOutline = inputFieldObj.AddComponent<Outline>();
            inputOutline.effectColor = new Color(0.4f, 0.7f, 1f);
            inputOutline.effectDistance = new Vector2(2f, -2f);

            _nameInputField = inputFieldObj.AddComponent<InputField>();
            _nameInputField.characterLimit = 0; // 한글 조합 중 자음/모음 잘림 방지 (등록 시 최대 8자로 정제)
            _nameInputField.onEndEdit.AddListener(OnInputFieldEndEdit);

            // 텍스트 컴포넌트
            GameObject inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(inputFieldObj.transform, false);
            RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = new Vector2(15f, 5f);
            inputTextRect.offsetMax = new Vector2(-15f, -5f);

            Text inputText = inputTextObj.AddComponent<Text>();
            inputText.font = _pixelFont;
            inputText.fontSize = 26;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleCenter;
            _nameInputField.textComponent = inputText;

            // 플레이스홀더
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputFieldObj.transform, false);
            RectTransform placeRect = placeholderObj.AddComponent<RectTransform>();
            placeRect.anchorMin = Vector2.zero;
            placeRect.anchorMax = Vector2.one;
            placeRect.offsetMin = new Vector2(15f, 5f);
            placeRect.offsetMax = new Vector2(-15f, -5f);

            Text placeholderText = placeholderObj.AddComponent<Text>();
            placeholderText.font = _pixelFont;
            placeholderText.fontSize = 22;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            placeholderText.alignment = TextAnchor.MiddleCenter;
            placeholderText.text = "닉네임 입력 (최대 8자)";
            _nameInputField.placeholder = placeholderText;

            // 등록 버튼
            GameObject submitBtnObj = new GameObject("Submit_Button");
            submitBtnObj.transform.SetParent(boxObj.transform, false);
            RectTransform submitBtnRect = submitBtnObj.AddComponent<RectTransform>();
            submitBtnRect.anchoredPosition = new Vector2(0f, -110f);
            submitBtnRect.sizeDelta = new Vector2(280f, 60f);

            Image submitImg = submitBtnObj.AddComponent<Image>();
            submitImg.color = new Color(0.18f, 0.78f, 0.38f); // 에메랄드 그린

            Button submitBtn = submitBtnObj.AddComponent<Button>();
            submitBtn.onClick.AddListener(OnSubmitNameClicked);

            GameObject submitTextObj = new GameObject("Submit_Text");
            submitTextObj.transform.SetParent(submitBtnObj.transform, false);
            RectTransform submitTextRect = submitTextObj.AddComponent<RectTransform>();
            submitTextRect.sizeDelta = submitBtnRect.sizeDelta;

            Text submitText = submitTextObj.AddComponent<Text>();
            submitText.font = _pixelFont;
            submitText.fontSize = 24;
            submitText.fontStyle = FontStyle.Bold;
            submitText.alignment = TextAnchor.MiddleCenter;
            submitText.color = Color.white;
            submitText.text = "명예의 전당 등록 (Enter)";

            _nameInputModal = dimObj;
            _nameInputModal.SetActive(false);
        }

        #endregion

        #region --- 랭킹 표시 및 제어 로직 ---

        /// <summary>
        /// TOP 5 랭킹 모달을 화면에 열고 최신 데이터를 갱신합니다.
        /// </summary>
        public void OpenLeaderboard()
        {
            EnsureUI();
            RefreshLeaderboardRows();
            if (_leaderboardModal != null)
            {
                _leaderboardModal.SetActive(true);
            }
        }

        /// <summary>
        /// 랭킹 모달을 닫습니다.
        /// </summary>
        public void CloseLeaderboard()
        {
            if (_leaderboardModal != null)
            {
                _leaderboardModal.SetActive(false);
            }
        }

        /// <summary>
        /// 랭킹 모달을 토글(열기/닫기)합니다.
        /// </summary>
        public void ToggleLeaderboard()
        {
            EnsureUI();
            if (_leaderboardModal != null && _leaderboardModal.activeSelf)
            {
                CloseLeaderboard();
            }
            else
            {
                OpenLeaderboard();
            }
        }

        /// <summary>
        /// 현재 TOP 5 데이터를 읽어 각 행에 색상과 함께 렌더링합니다.
        /// </summary>
        public void RefreshLeaderboardRows()
        {
            var entries = LeaderboardManager.Instance?.GetEntries();
            if (entries == null) return;

            Color gold = new Color(1f, 0.84f, 0f);
            Color silver = new Color(0.85f, 0.88f, 0.95f);
            Color bronze = new Color(0.85f, 0.55f, 0.25f);
            Color normal = Color.white;

            for (int i = 0; i < 5; i++)
            {
                if (i < entries.Count)
                {
                    var entry = entries[i];
                    Color rankColor = (i == 0) ? gold : (i == 1) ? silver : (i == 2) ? bronze : normal;

                    string medal = (i == 0) ? "🥇 1st" : (i == 1) ? "🥈 2nd" : (i == 2) ? "🥉 3rd" : $"{i + 1}th";
                    _rankTexts[i].text = medal;
                    _rankTexts[i].color = rankColor;

                    _nameTexts[i].text = entry.playerName;
                    _nameTexts[i].color = rankColor;

                    _scoreTexts[i].text = $"{entry.score}m";
                    _scoreTexts[i].color = rankColor;

                    _gradeTexts[i].text = entry.grade;
                    _gradeTexts[i].color = GetGradeColor(entry.grade);
                }
                else
                {
                    _rankTexts[i].text = $"{i + 1}th";
                    _rankTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                    _nameTexts[i].text = "---";
                    _nameTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                    _scoreTexts[i].text = "0m";
                    _scoreTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                    _gradeTexts[i].text = "-";
                    _gradeTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                }
            }
        }

        private Color GetGradeColor(string grade)
        {
            if (string.IsNullOrEmpty(grade)) return Color.white;
            if (grade.Contains("A+")) return new Color(1f, 0.85f, 0.15f);
            if (grade.Contains("A0") || grade.Contains("A")) return new Color(0.44f, 0.88f, 0f);
            if (grade.Contains("B+")) return new Color(0.22f, 0.69f, 0f);
            if (grade.Contains("B0") || grade.Contains("B")) return new Color(0f, 0.47f, 0.71f);
            if (grade.Contains("C+")) return new Color(0.97f, 0.50f, 0f);
            return new Color(0.9f, 0.22f, 0.27f);
        }

        /// <summary>
        /// 신기록 달성 시 닉네임 입력 팝업을 엽니다.
        /// </summary>
        public void PromptNameInput(int score, string grade, Action onCompleted = null)
        {
            EnsureUI();
            _isSubmitting = false;
            _pendingScore = score;
            _pendingGrade = grade;
            _onInputCompletedCallback = onCompleted;

            int rank = LeaderboardManager.Instance?.GetProjectedRank(score) ?? 1;
            if (_inputPromptText != null)
            {
                _inputPromptText.text = $"최종 기록: {score}m ({grade})  ➔  예상 순위: <color=#FFD700>{rank}위</color>!";
            }

            if (_nameInputField != null)
            {
                _nameInputField.text = string.Empty;
                _nameInputField.Select();
                _nameInputField.ActivateInputField();
            }

            if (_nameInputModal != null)
            {
                _nameInputModal.SetActive(true);
            }
        }

        /// <summary>
        /// InputField에서 편집 완료 이벤트 발생 시 호출됩니다.
        /// </summary>
        private void OnInputFieldEndEdit(string text)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                OnSubmitNameClicked();
            }
        }

        /// <summary>
        /// 엔터 키 또는 '명예의 전당 등록' 버튼 클릭 시 최종 저장 처리
        /// </summary>
        public void OnSubmitNameClicked()
        {
            if (_isSubmitting) return;

            string text = string.Empty;
            if (_nameInputField != null)
            {
                // 포커스 비활성화로 현재 조합 중인 한글(IME)을 text에 즉시 강제 확정(Flush)
                _nameInputField.DeactivateInputField();
                text = _nameInputField.text;

                // 혹시 남아있는 잔여 조합 문자 결합
                try
                {
                    if (!string.IsNullOrEmpty(Input.compositionString))
                    {
                        text += Input.compositionString;
                    }
                }
                catch { }
            }

            SubmitName(text);
        }

        /// <summary>
        /// 입력된 닉네임을 최대 8자로 안전하게 정제 후 랭킹에 등록합니다.
        /// </summary>
        private void SubmitName(string rawName)
        {
            if (_isSubmitting) return;
            _isSubmitting = true;

            string name = !string.IsNullOrWhiteSpace(rawName) ? rawName.Trim() : "익명의 러너";
            if (name.Length > 8)
            {
                name = name.Substring(0, 8);
            }

            LeaderboardManager.Instance?.AddEntry(name, _pendingScore, _pendingGrade);

            if (_nameInputModal != null)
            {
                _nameInputModal.SetActive(false);
            }

            _onInputCompletedCallback?.Invoke();
            _onInputCompletedCallback = null;

            // 등록 직후 TOP 5 랭킹 화면 표시
            OpenLeaderboard();
        }

        #endregion
    }
}
