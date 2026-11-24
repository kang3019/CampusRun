using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CampusRun.Auth;

namespace CampusRun.Leaderboard
{
    /// <summary>
    /// 인게임 전체 유저 무제한 랭킹 모달(ScrollRect 기반) 및 내 순위 요약 바를 담당하는 UI 컨트롤러입니다.
    /// 별도의 프리팹 없이도 런타임에 완전 자동 생성되어 도트 폰트로 깔끔하게 렌더링됩니다.
    /// </summary>
    public class LeaderboardUI : MonoBehaviour
    {
        public static LeaderboardUI Instance { get; private set; }

        [Header("--- 모달 패널 루트 ---")]
        [SerializeField] private GameObject _leaderboardModal;
        [SerializeField] private GameObject _nameInputModal;

        public bool IsLeaderboardActive => _leaderboardModal != null && _leaderboardModal.activeSelf;
        public bool IsInputModalActive =>
            (_nameInputModal != null && _nameInputModal.activeSelf) ||
            (AuthUI.Instance != null && AuthUI.Instance.IsAnyAuthModalActive);

        [Header("--- 동적 스크롤 랭킹 행 목록 ---")]
        private ScrollRect _scrollRect;
        private RectTransform _contentRect;
        private readonly List<GameObject> _rowObjects = new List<GameObject>();
        private readonly List<Text> _rankTexts = new List<Text>();
        private readonly List<Text> _nameTexts = new List<Text>();
        private readonly List<Text> _scoreTexts = new List<Text>();
        private readonly List<Image> _rowBgImages = new List<Image>();
        private readonly List<Image> _rowBorderFrames = new List<Image>();
        private readonly List<Image> _rowBorderIcons = new List<Image>();

        [Header("--- 내 순위 표시 요약 바 ---")]
        private Text _myRankSummaryText;

        [Header("--- 이름 입력 필드 컴포넌트 (비로그인 게스트 폴백용) ---")]
        private InputField _nameInputField;
        private Outline _inputOutline;
        private Text _inputPromptText;
        private Text _inputWarningText;
        private int _pendingScore;
        private string _pendingGrade;
        private Action _onInputCompletedCallback;

        private Font _pixelFont;
        private Text _guideText;
        private bool _isInitialized = false;
        private bool _isSubmitting = false;
        private int _lastSubmitAttemptFrame = -1;

        private GameObject _homeRankButtonObj;
        private GameObject _homeSettingsButtonObj;
        private static Sprite _cachedRetroBtnSprite;
        private static Sprite _cachedTrophyIconSprite;

        private const float RowHeight = 54f;
        private const float RowSpacing = 6f;

        private static readonly Color DefaultInputOutlineColor = new Color(0.4f, 0.7f, 1f);
        private static readonly Color WarningInputOutlineColor = new Color(1f, 0.32f, 0.32f);

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
            UpdateTitleScreenRankButtonLabel();

            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.OnLeaderboardUpdated -= HandleLeaderboardUpdated;
                LeaderboardManager.Instance.OnLeaderboardUpdated += HandleLeaderboardUpdated;
            }

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnAuthStateChanged -= HandleLeaderboardUpdated;
                AuthManager.Instance.OnAuthStateChanged += HandleLeaderboardUpdated;
            }
        }

        private void OnDestroy()
        {
            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.OnLeaderboardUpdated -= HandleLeaderboardUpdated;
            }

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnAuthStateChanged -= HandleLeaderboardUpdated;
            }
        }

        /// <summary>
        /// InGameUIManager.cs를 직접 수정하지 않고도 홈 화면의 랭킹 버튼 위치를 우측 상단(설정 버튼 옆)으로 이동시키고,
        /// 커스텀 도트 아케이드 버튼 스프라이트 및 황금 트로피 아이콘을 적용합니다.
        /// </summary>
        private void UpdateTitleScreenRankButtonLabel()
        {
            if (_homeSettingsButtonObj == null)
            {
                _homeSettingsButtonObj = GameObject.Find("Home_Settings_Button");
            }

            if (_homeRankButtonObj == null)
            {
                _homeRankButtonObj = GameObject.Find("TitleLeaderboard_Button");
            }

            if (_homeRankButtonObj == null) return;

            // 1. 우측 상단 설정 버튼(Home_Settings_Button)과 동일한 부모(InGame_Canvas)로 이동 후 바로 왼쪽에 나란히 배치
            if (_homeSettingsButtonObj != null && _homeSettingsButtonObj.transform.parent != null)
            {
                _homeRankButtonObj.transform.SetParent(_homeSettingsButtonObj.transform.parent, false);
            }

            RectTransform btnRect = _homeRankButtonObj.GetComponent<RectTransform>();
            if (btnRect != null)
            {
                btnRect.anchorMin = new Vector2(1f, 1f);
                btnRect.anchorMax = new Vector2(1f, 1f);
                btnRect.pivot = new Vector2(1f, 1f);
                btnRect.anchoredPosition = new Vector2(-212f, -50f);
                btnRect.sizeDelta = new Vector2(176f, 54f);
                _homeRankButtonObj.transform.localScale = Vector3.one;
            }

            // 2. 기존 단순 사각형 외곽선 제거 및 레트로 아케이드 9-Slice 버튼 이미지 적용
            Outline oldOutline = _homeRankButtonObj.GetComponent<Outline>();
            if (oldOutline != null)
            {
                Destroy(oldOutline);
            }

            Image btnImg = _homeRankButtonObj.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.sprite = GetOrCreateRetroButtonSprite();
                btnImg.type = Image.Type.Sliced;
                btnImg.color = Color.white;
            }

            Button btn = _homeRankButtonObj.GetComponent<Button>();
            if (btn != null)
            {
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1f, 0.96f, 0.82f, 1f);
                cb.pressedColor = new Color(0.82f, 0.72f, 0.55f, 1f);
                btn.colors = cb;
            }

            // 3. 좌측 도트 황금 트로피 아이콘 생성 및 부착
            Transform existingIcon = _homeRankButtonObj.transform.Find("Trophy_Icon");
            if (existingIcon == null)
            {
                GameObject iconObj = new GameObject("Trophy_Icon");
                iconObj.transform.SetParent(_homeRankButtonObj.transform, false);
                RectTransform iconRect = iconObj.AddComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(30f, 1f);
                iconRect.sizeDelta = new Vector2(28f, 28f);

                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = GetOrCreateTrophyIconSprite();
                iconImg.raycastTarget = false;
            }

            // 4. 버튼 텍스트 스타일 및 여백 조정
            Transform textTr = _homeRankButtonObj.transform.Find("Text");
            if (textTr != null)
            {
                RectTransform txtRect = textTr.GetComponent<RectTransform>();
                if (txtRect != null)
                {
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.offsetMin = new Vector2(46f, 2f);
                    txtRect.offsetMax = new Vector2(-8f, -2f);
                }

                Text txt = textTr.GetComponent<Text>();
                if (txt != null)
                {
                    txt.text = "랭킹 (Tab)";
                    txt.fontSize = 21;
                    txt.fontStyle = FontStyle.Bold;
                    txt.color = new Color(0.16f, 0.09f, 0.02f, 1f); // 황금 버튼 위 선명한 다크 브라운 텍스트
                    txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                    txt.verticalOverflow = VerticalWrapMode.Overflow;
                }
            }

            if (_homeSettingsButtonObj != null)
            {
                _homeRankButtonObj.SetActive(_homeSettingsButtonObj.activeSelf);
            }
        }

        /// <summary>
        /// 도트 베벨 테두리와 입체 하이라이트가 적용된 황금빛 레트로 아케이드 버튼 9-Slice 스프라이트를 생성합니다.
        /// </summary>
        private static Sprite GetOrCreateRetroButtonSprite()
        {
            if (_cachedRetroBtnSprite != null) return _cachedRetroBtnSprite;

            const int w = 32;
            const int h = 24;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color border = new Color(0.14f, 0.08f, 0.02f, 1f);       // 짙은 도트 외곽선
            Color topHighlight = new Color(1f, 0.95f, 0.55f, 1f);    // 상단/좌측 밝은 골드 하이라이트
            Color bottomShadow = new Color(0.72f, 0.42f, 0.05f, 1f); // 하단/우측 앰버 입체 음영
            Color bodyTop = new Color(1f, 0.80f, 0.20f, 1f);         // 상단 골드 바디
            Color bodyBottom = new Color(0.95f, 0.64f, 0.10f, 1f);   // 하단 오렌지 골드 바디

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 모서리 2픽셀 라운딩 컷 (레트로 픽셀 버튼 감성)
                    bool isCornerCut =
                        (x == 0 && (y <= 1 || y >= h - 2)) ||
                        (x == 1 && (y == 0 || y == h - 1)) ||
                        (x == w - 1 && (y <= 1 || y >= h - 2)) ||
                        (x == w - 2 && (y == 0 || y == h - 1));

                    if (isCornerCut)
                    {
                        tex.SetPixel(x, y, clear);
                        continue;
                    }

                    // 외곽 2픽셀 테두리
                    bool isBorder =
                        x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2 ||
                        (x == 2 && (y == 2 || y == h - 3)) ||
                        (x == w - 3 && (y == 2 || y == h - 3));

                    if (isBorder)
                    {
                        tex.SetPixel(x, y, border);
                        continue;
                    }

                    // 하단 2픽셀 입체 음영
                    if (y == 2 || y == 3 || x == w - 3)
                    {
                        tex.SetPixel(x, y, bottomShadow);
                        continue;
                    }

                    // 상단 2픽셀 및 좌측 하이라이트
                    if (y == h - 3 || y == h - 4 || x == 2)
                    {
                        tex.SetPixel(x, y, topHighlight);
                        continue;
                    }

                    // 내부 상하 그라데이션 골드 바디
                    float t = (float)y / (h - 1);
                    tex.SetPixel(x, y, Color.Lerp(bodyBottom, bodyTop, t));
                }
            }

            tex.Apply();
            _cachedRetroBtnSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            return _cachedRetroBtnSprite;
        }

        /// <summary>
        /// 16x16 도트 그래픽으로 직접 찍은 황금 트로피(🏆) 아이콘 스프라이트를 생성합니다.
        /// </summary>
        private static Sprite GetOrCreateTrophyIconSprite()
        {
            if (_cachedTrophyIconSprite != null) return _cachedTrophyIconSprite;

            string[] pattern = new string[]
            {
                "..OOOOOOOOOOOO..", // 15 (상단 림)
                ".OYYYYWWWWYYYYO.", // 14
                "OYYOYYYYYYYYOYYO", // 13 (손잡이 시작)
                "OY.OYYYYWWYYO.YO", // 12
                "OY.OYYYYYYYYO.YO", // 11
                ".OYYOYYYYYYOYYO.", // 10 (손잡이 끝)
                "..OOYYYYYYYYOO..", // 9
                "...OSSSSSSSSO...", // 8
                "....OOYYYYOO....", // 7
                "......OYYO......", // 6 (기둥)
                "......OSSO......", // 5
                ".....OOYYOO.....", // 4 (받침대 상단)
                "...OOYYYYYYOO...", // 3
                "..OSSSSSSSSSSO..", // 2 (받침대 하단)
                "..OOOOOOOOOOOO..", // 1
                "................"  // 0
            };

            const int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color outline = new Color(0.16f, 0.08f, 0.02f, 1f);
            Color gold = new Color(1f, 0.86f, 0.15f, 1f);
            Color shine = new Color(1f, 0.98f, 0.80f, 1f);
            Color shade = new Color(0.85f, 0.52f, 0.08f, 1f);

            for (int row = 0; row < size; row++)
            {
                int y = size - 1 - row;
                string line = pattern[row];
                for (int x = 0; x < size; x++)
                {
                    char c = line[x];
                    Color col = clear;
                    if (c == 'O') col = outline;
                    else if (c == 'Y') col = gold;
                    else if (c == 'W') col = shine;
                    else if (c == 'S') col = shade;

                    tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            _cachedTrophyIconSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cachedTrophyIconSprite;
        }

        private void HandleLeaderboardUpdated()
        {
            if (_isInitialized)
            {
                RefreshLeaderboardRows();
            }
        }

        private void Update()
        {
            if (_homeRankButtonObj != null && _homeSettingsButtonObj != null)
            {
                bool shouldShow = _homeSettingsButtonObj.activeSelf;
                if (_homeRankButtonObj.activeSelf != shouldShow)
                {
                    _homeRankButtonObj.SetActive(shouldShow);
                }
            }
            else if (_homeRankButtonObj == null)
            {
                UpdateTitleScreenRankButtonLabel();
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 로그인/회원가입 또는 최초 닉네임 설정 모달이 열려 있을 때는 단축키 무시
            if (AuthUI.Instance != null && AuthUI.Instance.IsAnyAuthModalActive)
            {
                return;
            }

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

            // 이름 입력 모달이 열려 있을 때 Enter 키 누르면 즉시 저장 시도
            if (_nameInputModal != null && _nameInputModal.activeSelf && !_isSubmitting)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    OnSubmitNameClicked();
                }
            }
        }

        /// <summary>
        /// 도트 폰트를 로드하고 스크롤 가능한 전체 랭킹 모달 및 이름 입력 UI 계층 구조를 동적으로 생성합니다.
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
                _pixelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
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
                canvas.sortingOrder = 150;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 3. 전체 유저 스크롤 랭킹 모달 생성
            CreateLeaderboardModal(canvas.transform);

            // 4. 이름 입력 모달 생성 (비로그인 폴백용)
            CreateNameInputModal(canvas.transform);

            _isInitialized = true;
        }

        #region --- 전체 유저 스크롤 랭킹 모달 UI 생성 ---

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
            dimImg.color = new Color(0f, 0f, 0f, 0.78f);

            // 팝업 박스 (가로 880, 세로 680)
            GameObject boxObj = new GameObject("Leaderboard_Box");
            boxObj.transform.SetParent(dimObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(880f, 680f);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.07f, 0.08f, 0.14f, 0.98f);

            Outline boxOutline = boxObj.AddComponent<Outline>();
            boxOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.9f);
            boxOutline.effectDistance = new Vector2(3f, -3f);

            // 제목 라벨
            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(boxObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchoredPosition = new Vector2(0f, 280f);
            titleRect.sizeDelta = new Vector2(820f, 56f);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = _pixelFont;
            titleText.fontSize = 36;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(1f, 0.85f, 0.2f);
            titleText.text = "★ 명예의 전당 (전체 유저 랭킹) ★";

            Shadow titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = Color.black;
            titleShadow.effectDistance = new Vector2(2f, -2f);

            // 컬럼 헤더 라벨 (학점 제거, 3컬럼 균형 배치)
            GameObject headerObj = new GameObject("Header_Row");
            headerObj.transform.SetParent(boxObj.transform, false);
            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchoredPosition = new Vector2(0f, 222f);
            headerRect.sizeDelta = new Vector2(780f, 38f);

            CreateHeaderLabel(headerObj.transform, "순위", -275f, 140f);
            CreateHeaderLabel(headerObj.transform, "러너 (닉네임)", -20f, 340f);
            CreateHeaderLabel(headerObj.transform, "최고 거리", 255f, 190f);

            // 스크롤 뷰포트 컨테이너 (가로 780, 세로 315)
            GameObject scrollViewObj = new GameObject("Rank_ScrollView");
            scrollViewObj.transform.SetParent(boxObj.transform, false);
            RectTransform scrollViewRect = scrollViewObj.AddComponent<RectTransform>();
            scrollViewRect.anchoredPosition = new Vector2(0f, 38f);
            scrollViewRect.sizeDelta = new Vector2(780f, 315f);

            Image scrollBg = scrollViewObj.AddComponent<Image>();
            scrollBg.color = new Color(0.05f, 0.06f, 0.10f, 0.9f);

            RectMask2D mask = scrollViewObj.AddComponent<RectMask2D>();

            // 스크롤 Content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollViewObj.transform, false);
            _contentRect = contentObj.AddComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0.5f, 1f);
            _contentRect.anchorMax = new Vector2(0.5f, 1f);
            _contentRect.pivot = new Vector2(0.5f, 1f);
            _contentRect.anchoredPosition = Vector2.zero;
            _contentRect.sizeDelta = new Vector2(760f, 315f);

            _scrollRect = scrollViewObj.AddComponent<ScrollRect>();
            _scrollRect.content = _contentRect;
            _scrollRect.viewport = scrollViewRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 36f;

            // 내 순위 고정 요약 바
            GameObject myRankBarObj = new GameObject("MyRank_Summary_Bar");
            myRankBarObj.transform.SetParent(boxObj.transform, false);
            RectTransform myRankBarRect = myRankBarObj.AddComponent<RectTransform>();
            myRankBarRect.anchoredPosition = new Vector2(0f, -158f);
            myRankBarRect.sizeDelta = new Vector2(780f, 52f);

            Image myRankBarBg = myRankBarObj.AddComponent<Image>();
            myRankBarBg.color = new Color(0.11f, 0.16f, 0.25f, 0.96f);

            Outline myRankOutline = myRankBarObj.AddComponent<Outline>();
            myRankOutline.effectColor = new Color(0.28f, 0.85f, 0.48f, 0.85f);
            myRankOutline.effectDistance = new Vector2(2f, -2f);

            GameObject myRankTxtObj = new GameObject("MyRank_Text");
            myRankTxtObj.transform.SetParent(myRankBarObj.transform, false);
            RectTransform myRankTxtRect = myRankTxtObj.AddComponent<RectTransform>();
            myRankTxtRect.anchorMin = Vector2.zero;
            myRankTxtRect.anchorMax = Vector2.one;
            myRankTxtRect.offsetMin = new Vector2(16f, 0f);
            myRankTxtRect.offsetMax = new Vector2(-16f, 0f);

            _myRankSummaryText = myRankTxtObj.AddComponent<Text>();
            _myRankSummaryText.font = _pixelFont;
            _myRankSummaryText.fontSize = 19;
            _myRankSummaryText.fontStyle = FontStyle.Bold;
            _myRankSummaryText.alignment = TextAnchor.MiddleCenter;
            _myRankSummaryText.resizeTextForBestFit = true;
            _myRankSummaryText.resizeTextMinSize = 12;
            _myRankSummaryText.resizeTextMaxSize = 19;
            _myRankSummaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _myRankSummaryText.verticalOverflow = VerticalWrapMode.Truncate;
            _myRankSummaryText.color = Color.white;
            _myRankSummaryText.text = "내 순위 조회 중...";

            // 하단 닫기 버튼
            GameObject closeBtnObj = new GameObject("Close_Button");
            closeBtnObj.transform.SetParent(boxObj.transform, false);
            RectTransform closeBtnRect = closeBtnObj.AddComponent<RectTransform>();
            closeBtnRect.anchoredPosition = new Vector2(0f, -242f);
            closeBtnRect.sizeDelta = new Vector2(240f, 54f);

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
            guideRect.anchoredPosition = new Vector2(0f, -298f);
            guideRect.sizeDelta = new Vector2(760f, 26f);

            Text guideText = guideObj.AddComponent<Text>();
            guideText.font = _pixelFont;
            guideText.fontSize = 15;
            guideText.alignment = TextAnchor.MiddleCenter;
            guideText.color = new Color(0.75f, 0.78f, 0.85f, 0.85f);
            guideText.text = "마우스 휠 스크롤로 전체 순위 확인 | 단축키: Tab/L 열기, Esc 닫기";
            _guideText = guideText;

            _rowObjects.Clear();
            _rankTexts.Clear();
            _nameTexts.Clear();
            _scoreTexts.Clear();
            _rowBgImages.Clear();
            _rowBorderFrames.Clear();
            _rowBorderIcons.Clear();

            // 기본 5개 행 사전 생성
            EnsureRowPoolCount(5);

            _leaderboardModal = dimObj;
            _leaderboardModal.SetActive(false);
        }

        private void EnsureRowPoolCount(int targetCount)
        {
            if (_contentRect == null) return;

            while (_rowObjects.Count < targetCount)
            {
                int index = _rowObjects.Count;
                CreateLeaderboardRow(_contentRect, index);
            }
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

        private void CreateLeaderboardRow(Transform parent, int index)
        {
            GameObject rowObj = new GameObject($"Row_{index + 1}");
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRect = rowObj.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            float posY = -6f - index * (RowHeight + RowSpacing);
            rowRect.anchoredPosition = new Vector2(0f, posY);
            rowRect.sizeDelta = new Vector2(756f, RowHeight);

            Image rowBg = rowObj.AddComponent<Image>();
            rowBg.color = (index % 2 == 0) ? new Color(0.12f, 0.14f, 0.22f, 0.85f) : new Color(0.09f, 0.11f, 0.18f, 0.85f);

            _rowObjects.Add(rowObj);
            _rowBgImages.Add(rowBg);

            // 순위 텍스트
            Text rankText = CreateRowCell(rowObj.transform, $"Rank_{index}", -275f, 140f, 22, FontStyle.Bold);
            _rankTexts.Add(rankText);

            // 장착 테두리 등급 아이콘 (닉네임 좌측)
            GameObject iconObj = new GameObject($"BorderIcon_{index}");
            iconObj.transform.SetParent(rowObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(-162f, 0f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.raycastTarget = false;
            iconObj.SetActive(false);
            _rowBorderIcons.Add(iconImg);

            // 이름 텍스트
            Text nameText = CreateRowCell(rowObj.transform, $"Name_{index}", -5f, 300f, 21, FontStyle.Bold);
            _nameTexts.Add(nameText);

            // 최고 거리(점수) 텍스트
            Text scoreText = CreateRowCell(rowObj.transform, $"Score_{index}", 255f, 190f, 23, FontStyle.Bold);
            _scoreTexts.Add(scoreText);

            // 장착 테두리 도트 프레임 오버레이 (행 외곽)
            GameObject frameObj = new GameObject($"BorderFrame_{index}");
            frameObj.transform.SetParent(rowObj.transform, false);
            RectTransform frameRect = frameObj.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            Image frameImg = frameObj.AddComponent<Image>();
            frameImg.type = Image.Type.Sliced;
            frameImg.raycastTarget = false;
            frameObj.SetActive(false);
            _rowBorderFrames.Add(frameImg);
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = Color.white;
            return text;
        }

        #endregion

        #region --- 이름 입력 모달 UI 생성 (비로그인 폴백용) ---

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
            boxOutline.effectColor = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            boxOutline.effectDistance = new Vector2(3f, -3f);

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
            titleText.text = "★ 명예의 전당 랭킹 등록! ★";

            GameObject promptObj = new GameObject("Prompt_Text");
            promptObj.transform.SetParent(boxObj.transform, false);
            RectTransform promptRect = promptObj.AddComponent<RectTransform>();
            promptRect.anchoredPosition = new Vector2(0f, 88f);
            promptRect.sizeDelta = new Vector2(620f, 55f);

            _inputPromptText = promptObj.AddComponent<Text>();
            _inputPromptText.font = _pixelFont;
            _inputPromptText.fontSize = 24;
            _inputPromptText.alignment = TextAnchor.MiddleCenter;
            _inputPromptText.color = Color.white;
            _inputPromptText.text = "최종 거리: 0m (예상 순위: 1위)";

            GameObject inputFieldObj = new GameObject("InputField");
            inputFieldObj.transform.SetParent(boxObj.transform, false);
            RectTransform inputRect = inputFieldObj.AddComponent<RectTransform>();
            inputRect.anchoredPosition = new Vector2(0f, 10f);
            inputRect.sizeDelta = new Vector2(420f, 60f);

            Image inputBg = inputFieldObj.AddComponent<Image>();
            inputBg.color = new Color(0.15f, 0.18f, 0.28f);

            _inputOutline = inputFieldObj.AddComponent<Outline>();
            _inputOutline.effectColor = DefaultInputOutlineColor;
            _inputOutline.effectDistance = new Vector2(2f, -2f);

            _nameInputField = inputFieldObj.AddComponent<InputField>();
            _nameInputField.characterLimit = 0;
            _nameInputField.onValueChanged.AddListener(OnInputFieldValueChanged);
            _nameInputField.onEndEdit.AddListener(OnInputFieldEndEdit);

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

            GameObject warningObj = new GameObject("Warning_Text");
            warningObj.transform.SetParent(boxObj.transform, false);
            RectTransform warningRect = warningObj.AddComponent<RectTransform>();
            warningRect.anchoredPosition = new Vector2(0f, -45f);
            warningRect.sizeDelta = new Vector2(640f, 36f);

            _inputWarningText = warningObj.AddComponent<Text>();
            _inputWarningText.font = _pixelFont;
            _inputWarningText.fontSize = 19;
            _inputWarningText.fontStyle = FontStyle.Bold;
            _inputWarningText.alignment = TextAnchor.MiddleCenter;
            _inputWarningText.color = new Color(1f, 0.38f, 0.38f);
            _inputWarningText.text = string.Empty;

            GameObject submitBtnObj = new GameObject("Submit_Button");
            submitBtnObj.transform.SetParent(boxObj.transform, false);
            RectTransform submitBtnRect = submitBtnObj.AddComponent<RectTransform>();
            submitBtnRect.anchoredPosition = new Vector2(0f, -115f);
            submitBtnRect.sizeDelta = new Vector2(360f, 64f);

            Image submitImg = submitBtnObj.AddComponent<Image>();
            submitImg.color = new Color(0.18f, 0.78f, 0.38f);

            Button submitBtn = submitBtnObj.AddComponent<Button>();
            submitBtn.onClick.AddListener(OnSubmitNameClicked);

            GameObject submitTextObj = new GameObject("Submit_Text");
            submitTextObj.transform.SetParent(submitBtnObj.transform, false);
            RectTransform submitTextRect = submitTextObj.AddComponent<RectTransform>();
            submitTextRect.sizeDelta = submitBtnRect.sizeDelta;

            Text submitText = submitTextObj.AddComponent<Text>();
            submitText.font = _pixelFont;
            submitText.fontSize = 22;
            submitText.fontStyle = FontStyle.Bold;
            submitText.alignment = TextAnchor.MiddleCenter;
            submitText.horizontalOverflow = HorizontalWrapMode.Overflow;
            submitText.color = Color.white;
            submitText.text = "명예의 전당 등록 (Enter)";

            _nameInputModal = dimObj;
            _nameInputModal.SetActive(false);
        }

        #endregion

        #region --- 랭킹 표시 및 제어 로직 ---

        /// <summary>
        /// 전체 유저 랭킹 모달을 화면에 열고 최신 데이터를 갱신합니다.
        /// </summary>
        public void OpenLeaderboard()
        {
            EnsureUI();
            RefreshLeaderboardRows();
            if (_leaderboardModal != null)
            {
                _leaderboardModal.SetActive(true);
            }

            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
            }

            if (LeaderboardManager.Instance != null && LeaderboardManager.Instance.IsFirebaseConfigured)
            {
                LeaderboardManager.Instance.RefreshFromFirebase();
            }
        }

        public void CloseLeaderboard()
        {
            if (_leaderboardModal != null)
            {
                _leaderboardModal.SetActive(false);
            }
        }

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
        /// 현재 전체 유저 랭킹 데이터를 읽어 스크롤 뷰의 각 행과 하단 '내 순위 요약 바'를 렌더링합니다.
        /// </summary>
        public void RefreshLeaderboardRows()
        {
            var manager = LeaderboardManager.Instance;
            var entries = manager?.GetEntries();
            if (entries == null) return;

            if (_guideText != null)
            {
                if (manager.IsFirebaseConfigured)
                {
                    string syncStatus = manager.IsFirebaseConnected
                        ? "<color=#4ADE80>● FIREBASE 실시간 연동됨</color>"
                        : "<color=#FACC15>● FIREBASE 동기화 대기/오프라인</color>";
                    _guideText.text = $"마우스 휠 스크롤 | Tab/L 열기 | Esc 닫기   ({syncStatus})";
                }
                else
                {
                    _guideText.text = "마우스 휠 스크롤로 전체 순위 확인 | 단축키: Tab/L 열기 / Esc 닫기";
                }
            }

            string currentUserNickname = (AuthManager.Instance != null && AuthManager.Instance.HasNickname)
                ? AuthManager.Instance.CurrentNickname
                : string.Empty;

            int displayCount = Mathf.Max(5, entries.Count);
            EnsureRowPoolCount(displayCount);

            // Content 높이 동적 조정
            if (_contentRect != null)
            {
                float totalHeight = 12f + displayCount * (RowHeight + RowSpacing);
                _contentRect.sizeDelta = new Vector2(760f, Mathf.Max(315f, totalHeight));
            }

            Color gold = new Color(1f, 0.84f, 0f);
            Color silver = new Color(0.85f, 0.88f, 0.95f);
            Color bronze = new Color(0.85f, 0.55f, 0.25f);
            Color normal = Color.white;
            Color myHighlightText = new Color(0.35f, 1f, 0.55f);

            for (int i = 0; i < _rowObjects.Count; i++)
            {
                if (i >= displayCount)
                {
                    _rowObjects[i].SetActive(false);
                    continue;
                }

                _rowObjects[i].SetActive(true);

                if (i < entries.Count)
                {
                    var entry = entries[i];
                    bool isMe = !string.IsNullOrWhiteSpace(currentUserNickname) &&
                                string.Equals(entry.playerName?.Trim(), currentUserNickname.Trim(), StringComparison.OrdinalIgnoreCase);

                    BorderTier equippedTier = isMe && AuthManager.Instance != null
                        ? AuthManager.Instance.CurrentEquippedBorder
                        : (BorderTier)Mathf.Clamp(entry.equippedBorder, 0, 3);

                    bool hasDecorBorder = equippedTier != BorderTier.None;

                    if (hasDecorBorder)
                    {
                        _rowBgImages[i].color = BorderDecorationUtility.GetAccentBgColor(equippedTier);
                        _rowBorderFrames[i].gameObject.SetActive(true);
                        _rowBorderFrames[i].sprite = BorderDecorationUtility.GetBorderFrameSprite(equippedTier);
                        _rowBorderIcons[i].gameObject.SetActive(true);
                        _rowBorderIcons[i].sprite = BorderDecorationUtility.GetBorderIconSprite(equippedTier);
                    }
                    else
                    {
                        _rowBorderFrames[i].gameObject.SetActive(false);
                        _rowBorderIcons[i].gameObject.SetActive(false);

                        if (isMe)
                        {
                            _rowBgImages[i].color = new Color(0.14f, 0.32f, 0.24f, 0.95f);
                        }
                        else
                        {
                            _rowBgImages[i].color = (i % 2 == 0)
                                ? new Color(0.12f, 0.14f, 0.22f, 0.85f)
                                : new Color(0.09f, 0.11f, 0.18f, 0.85f);
                        }
                    }

                    Color rankColor = (i == 0) ? gold : (i == 1) ? silver : (i == 2) ? bronze : (isMe ? myHighlightText : normal);

                    string medal = (i == 0) ? "★ 1st" : (i == 1) ? "★ 2nd" : (i == 2) ? "★ 3rd" : $"{i + 1}th";
                    _rankTexts[i].text = medal;
                    _rankTexts[i].color = rankColor;

                    _nameTexts[i].text = isMe ? $"{entry.playerName} <color=#4ADE80>[나]</color>" : entry.playerName;
                    _nameTexts[i].color = hasDecorBorder ? BorderDecorationUtility.GetPrimaryColor(equippedTier) : rankColor;

                    _scoreTexts[i].text = $"{entry.score}m";
                    _scoreTexts[i].color = rankColor;
                }
                else
                {
                    _rowBgImages[i].color = (i % 2 == 0)
                        ? new Color(0.12f, 0.14f, 0.22f, 0.85f)
                        : new Color(0.09f, 0.11f, 0.18f, 0.85f);

                    _rowBorderFrames[i].gameObject.SetActive(false);
                    _rowBorderIcons[i].gameObject.SetActive(false);

                    _rankTexts[i].text = $"{i + 1}th";
                    _rankTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                    _nameTexts[i].text = "---";
                    _nameTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                    _scoreTexts[i].text = "0m";
                    _scoreTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                }
            }

            // 하단 내 순위 요약 바 갱신
            RefreshMyRankSummary(entries.Count, currentUserNickname);
        }

        private void RefreshMyRankSummary(int totalCount, string currentUserNickname)
        {
            if (_myRankSummaryText == null) return;

            var manager = LeaderboardManager.Instance;
            if (!string.IsNullOrWhiteSpace(currentUserNickname) && manager != null)
            {
                int myRank = manager.GetPlayerRank(currentUserNickname);
                LeaderboardEntry myEntry = manager.GetPlayerEntry(currentUserNickname);

                if (myRank > 0 && myEntry != null)
                {
                    _myRankSummaryText.text =
                        $"내 순위: <color=#FFD700>{myRank}위</color> / 전체 {totalCount}명   |   닉네임: <color=#4ADE80>{currentUserNickname}</color>   |   최고 기록: <color=#FFD700>{myEntry.score}m</color>";
                }
                else
                {
                    _myRankSummaryText.text =
                        $"접속 러너: <color=#4ADE80>{currentUserNickname}</color>   |   전체 {totalCount}명 등록됨 (출발하여 첫 랭킹 기록을 세워보세요!)";
                }
            }
            else
            {
                _myRankSummaryText.text = $"전체 등록 러너: <color=#FFD700>{totalCount}명</color>   |   로그인하면 내 순위가 이곳에 자동 표시됩니다.";
            }
        }

        /// <summary>
        /// 신기록 달성 시 호출됩니다. 이미 로그인된 유저는 자동 갱신 후 즉시 완료 콜백을 호출합니다.
        /// </summary>
        public void PromptNameInput(int score, string grade, Action onCompleted = null)
        {
            if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn && AuthManager.Instance.HasNickname)
            {
                LeaderboardManager.Instance?.UpdateOrAddPlayerScore(AuthManager.Instance.CurrentNickname, score, grade);
                onCompleted?.Invoke();
                return;
            }

            EnsureUI();
            _isSubmitting = false;
            _pendingScore = score;
            _pendingGrade = grade;
            _onInputCompletedCallback = onCompleted;

            if (LeaderboardManager.Instance != null && LeaderboardManager.Instance.IsFirebaseConfigured)
            {
                LeaderboardManager.Instance.RefreshFromFirebase();
            }

            int rank = LeaderboardManager.Instance?.GetProjectedRank(score) ?? 1;
            if (_inputPromptText != null)
            {
                _inputPromptText.text = $"최종 기록: {score}m  ➔  예상 순위: <color=#FFD700>{rank}위</color>!";
            }

            ClearInputWarning();

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

        private void OnInputFieldValueChanged(string newText)
        {
            if (!_isSubmitting)
            {
                ClearInputWarning();
            }
        }

        private void ClearInputWarning()
        {
            if (_inputWarningText != null)
            {
                _inputWarningText.text = string.Empty;
            }
            if (_inputOutline != null)
            {
                _inputOutline.effectColor = DefaultInputOutlineColor;
            }
        }

        private void ShowInputWarning(string message)
        {
            if (_inputWarningText != null)
            {
                _inputWarningText.text = message;
            }
            if (_inputOutline != null)
            {
                _inputOutline.effectColor = WarningInputOutlineColor;
            }
        }

        private void OnInputFieldEndEdit(string text)
        {
            if (_isSubmitting) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                OnSubmitNameClicked();
            }
        }

        public void OnSubmitNameClicked()
        {
            if (_isSubmitting || _lastSubmitAttemptFrame == Time.frameCount) return;
            _lastSubmitAttemptFrame = Time.frameCount;
            _isSubmitting = true;

            string text = string.Empty;
            if (_nameInputField != null)
            {
                _nameInputField.DeactivateInputField();
                text = _nameInputField.text;

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

        private void SubmitName(string rawName)
        {
            string name = !string.IsNullOrWhiteSpace(rawName) ? rawName.Trim() : "익명의 러너";
            if (name.Length > 8)
            {
                name = name.Substring(0, 8);
            }

            if (LeaderboardManager.Instance != null && LeaderboardManager.Instance.IsDuplicateName(name))
            {
                _isSubmitting = false;
                ShowInputWarning($"'{name}'은(는) 이미 등록된 이름입니다! 다른 이름을 입력해 주세요.");

                if (_nameInputField != null)
                {
                    _nameInputField.Select();
                    _nameInputField.ActivateInputField();
                }
                return;
            }

            ClearInputWarning();
            LeaderboardManager.Instance?.AddEntry(name, _pendingScore, _pendingGrade);

            if (_nameInputModal != null)
            {
                _nameInputModal.SetActive(false);
            }

            _onInputCompletedCallback?.Invoke();
            _onInputCompletedCallback = null;

            OpenLeaderboard();
        }

        #endregion
    }
}
