using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using CampusRun.Core;
using CampusRun.Leaderboard;

namespace CampusRun.Auth
{
    /// <summary>
    /// 회원가입/로그인 모달, 최초 닉네임 등록 모달, 홈 화면 좌측 상단 프로필 설정 탭(유저 이름 변경 & 테두리 꾸미기),
    /// 그리고 1~3위 달성 테두리 장식 획득 알림 창을 전담하는 UI 컨트롤러입니다.
    /// 씬 파일(Sandbox_PlayTest.unity) 및 InGameUIManager.cs 수정 없이 런타임에 100% 자동 생성됩니다.
    /// </summary>
    public class AuthUI : MonoBehaviour
    {
        public static AuthUI Instance { get; private set; }

        private GameObject _authModal;
        private GameObject _nicknameModal;
        private GameObject _profileBadgeObj;
        private GameObject _profileModal;
        private GameObject _borderUnlockModal;

        // 로그인/회원가입 모달 컴포넌트
        private bool _isRegisterMode = false;
        private Text _authTitleText;
        private Text _authSubTitleText;
        private Button _loginTabButton;
        private Image _loginTabBg;
        private Button _registerTabButton;
        private Image _registerTabBg;
        private InputField _userIdInput;
        private InputField _passwordInput;
        private Text _authFeedbackText;
        private Button _authSubmitButton;
        private Text _authSubmitBtnText;
        private Button _modeSwitchLinkButton;
        private Text _modeSwitchLinkText;

        // 최초 1회 닉네임 입력 모달 컴포넌트
        private InputField _nicknameInput;
        private Outline _nicknameInputOutline;
        private Text _nicknameFeedbackText;
        private Button _nicknameSubmitButton;

        // 좌측 상단 프로필 설정 탭(배지 버튼) 컴포넌트
        private Image _profileBadgeBg;
        private Image _profileBadgeBorderFrame;
        private Image _profileBadgeIcon;
        private Text _profileBadgeText;
        private Text _profileBadgeSubText;
        private Button _profileTabButton;

        // 프로필 설정 & 꾸미기 모달 컴포넌트
        private Image _previewCardBg;
        private Image _previewBorderFrame;
        private Image _previewBorderIcon;
        private Text _previewNicknameText;
        private Text _previewEquippedText;
        private Text _previewStatsText;
        private InputField _profileNicknameInput;
        private Outline _profileNicknameOutline;
        private Text _profileNicknameFeedbackText;
        private Button _profileNicknameChangeBtn;
        private Text _borderActionFeedbackText;

        private readonly BorderOptionCard[] _borderCards = new BorderOptionCard[4];

        private class BorderOptionCard
        {
            public BorderTier Tier;
            public Button Button;
            public Image CardBg;
            public Image FrameOverlay;
            public Image IconImage;
            public Text TitleText;
            public Text ConditionText;
            public Image StatusBadgeBg;
            public Text StatusBadgeText;
        }

        // 신규 테두리 장식 획득 축하 팝업 컴포넌트
        private readonly Queue<BorderTier> _pendingUnlockQueue = new Queue<BorderTier>();
        private BorderTier _currentUnlockedTier = BorderTier.None;
        private Image _unlockModalBorderFrame;
        private Image _unlockModalIcon;
        private Text _unlockModalTierNameText;
        private Text _unlockModalDescText;
        private Image _unlockPreviewBg;
        private Image _unlockPreviewFrame;
        private Text _unlockPreviewNameText;

        private Font _pixelFont;
        private bool _isInitialized = false;
        private bool _isGamePlaying = false;
        private bool _isGameOver = false;
        private GameObject _cachedSplashPanel;
        private bool _hasSearchedSplash = false;
        private int _lastSubmitFrame = -1;

        public bool IsAnyAuthModalActive =>
            (_authModal != null && _authModal.activeSelf) ||
            (_nicknameModal != null && _nicknameModal.activeSelf) ||
            (_profileModal != null && _profileModal.activeSelf) ||
            (_borderUnlockModal != null && _borderUnlockModal.activeSelf);

        private static bool _sceneLoadedRegistered = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRegistration()
        {
            _sceneLoadedRegistered = false;
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeInScene()
        {
            if (!_sceneLoadedRegistered)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                SceneManager.sceneLoaded += HandleSceneLoaded;
                _sceneLoadedRegistered = true;
            }

            EnsureInstanceInCurrentScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstanceInCurrentScene();
        }

        private static void EnsureInstanceInCurrentScene()
        {
            if (FindAnyObjectByType<AuthUI>() == null)
            {
                if (FindAnyObjectByType<CampusRun.Player.GridPlayerController>() != null)
                {
                    GameObject obj = new GameObject("[AuthUI]");
                    obj.AddComponent<AuthUI>();
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnGameStarted += HandleGameStarted;
            GameEvents.OnGameRestarted += HandleGameRestarted;
            GameEvents.OnPlayerDied += HandlePlayerDied;

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnAuthStateChanged -= RefreshUIState;
                AuthManager.Instance.OnAuthStateChanged += RefreshUIState;
                AuthManager.Instance.OnNewBorderUnlocked -= HandleNewBorderUnlocked;
                AuthManager.Instance.OnNewBorderUnlocked += HandleNewBorderUnlocked;
            }
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            GameEvents.OnPlayerDied -= HandlePlayerDied;

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnAuthStateChanged -= RefreshUIState;
                AuthManager.Instance.OnNewBorderUnlocked -= HandleNewBorderUnlocked;
            }
        }

        private void Start()
        {
            EnsureUI();

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.OnAuthStateChanged -= RefreshUIState;
                AuthManager.Instance.OnAuthStateChanged += RefreshUIState;
                AuthManager.Instance.OnNewBorderUnlocked -= HandleNewBorderUnlocked;
                AuthManager.Instance.OnNewBorderUnlocked += HandleNewBorderUnlocked;
            }

            RefreshUIState();
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // 스플래시 화면이 아직 재생 중이면 모달 표시를 보류하고 스플래시 종료 직후 자동으로 띄움
            if (IsSplashScreenActive())
            {
                if (_authModal != null && _authModal.activeSelf) _authModal.SetActive(false);
                if (_nicknameModal != null && _nicknameModal.activeSelf) _nicknameModal.SetActive(false);
                if (_profileBadgeObj != null && _profileBadgeObj.activeSelf) _profileBadgeObj.SetActive(false);
                if (_profileModal != null && _profileModal.activeSelf) _profileModal.SetActive(false);
                if (_borderUnlockModal != null && _borderUnlockModal.activeSelf) _borderUnlockModal.SetActive(false);
                return;
            }

            // 아직 로그인되지 않았거나 닉네임이 없다면 모달 강제 활성화 및 플레이어 조작 차단 유지
            if (AuthManager.Instance != null)
            {
                if (!AuthManager.Instance.IsLoggedIn)
                {
                    if (_profileModal != null && _profileModal.activeSelf) _profileModal.SetActive(false);
                    if (_authModal != null && !_authModal.activeSelf)
                    {
                        OpenAuthModal(false);
                    }
                    EnsurePlayerPausedWhileModalOpen();
                }
                else if (!AuthManager.Instance.HasNickname)
                {
                    if (_authModal != null && _authModal.activeSelf) _authModal.SetActive(false);
                    if (_profileModal != null && _profileModal.activeSelf) _profileModal.SetActive(false);
                    if (_nicknameModal != null && !_nicknameModal.activeSelf)
                    {
                        OpenNicknameModal();
                    }
                    EnsurePlayerPausedWhileModalOpen();
                }
                else
                {
                    // 대기 중인 장식 획득 알림이 있으면 팝업 표시
                    if ((_borderUnlockModal == null || !_borderUnlockModal.activeSelf) && _pendingUnlockQueue.Count > 0)
                    {
                        ShowNextBorderUnlockPopup();
                    }

                    // 홈 화면에서 프로필 설정 모달이나 장식 획득 창이 열려 있는 동안 이동 입력 차단
                    if (!_isGamePlaying && !_isGameOver &&
                        ((_profileModal != null && _profileModal.activeSelf) ||
                         (_borderUnlockModal != null && _borderUnlockModal.activeSelf)))
                    {
                        EnsurePlayerPausedWhileModalOpen();
                    }
                }
            }

            // 모달 내 키보드 단축키 처리
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_authModal != null && _authModal.activeSelf)
            {
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    if (_userIdInput != null && _userIdInput.isFocused && _passwordInput != null)
                    {
                        _passwordInput.Select();
                        _passwordInput.ActivateInputField();
                    }
                    else if (_userIdInput != null)
                    {
                        _userIdInput.Select();
                        _userIdInput.ActivateInputField();
                    }
                }

                if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) &&
                    (AuthManager.Instance == null || !AuthManager.Instance.IsBusy))
                {
                    OnAuthSubmitClicked();
                }
            }
            else if (_nicknameModal != null && _nicknameModal.activeSelf)
            {
                if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) &&
                    (AuthManager.Instance == null || !AuthManager.Instance.IsBusy))
                {
                    OnNicknameSubmitClicked();
                }
            }
            else if (_profileModal != null && _profileModal.activeSelf)
            {
                if (_profileNicknameInput != null && _profileNicknameInput.isFocused &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) &&
                    (AuthManager.Instance == null || !AuthManager.Instance.IsBusy))
                {
                    OnProfileNicknameChangeClicked();
                }
            }
        }

        private bool IsSplashScreenActive()
        {
            if (!_hasSearchedSplash || _cachedSplashPanel == null)
            {
                _cachedSplashPanel = GameObject.Find("SplashScreen_Panel");
                _hasSearchedSplash = true;
            }
            return _cachedSplashPanel != null && _cachedSplashPanel.activeInHierarchy;
        }

        private void EnsurePlayerPausedWhileModalOpen()
        {
            var player = CampusRun.Player.GridPlayerController.Instance;
            if (player != null)
            {
                GameEvents.TriggerGameStateChanged(GameState.Paused);
            }
        }

        private void HandleGameStarted()
        {
            _isGamePlaying = true;
            _isGameOver = false;
            if (_profileBadgeObj != null) _profileBadgeObj.SetActive(false);
            if (_profileModal != null) _profileModal.SetActive(false);
        }

        private void HandleGameRestarted()
        {
            _isGamePlaying = false;
            _isGameOver = false;
            RefreshUIState();
        }

        private void HandlePlayerDied(string deathReason, int finalScore)
        {
            _isGamePlaying = false;
            _isGameOver = true;
        }

        public void EnsureUI()
        {
            if (_isInitialized) return;

            _pixelFont = LoadPixelFont();

            GameObject canvasObj = new GameObject("Auth_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // 인게임(100) 및 리더보드(150)보다 상위에 표시

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            CreateProfileBadge(canvas.transform);
            CreateAuthModal(canvas.transform);
            CreateNicknameModal(canvas.transform);
            CreateProfileModal(canvas.transform);
            CreateBorderUnlockModal(canvas.transform);

            _isInitialized = true;
        }

        private Font LoadPixelFont()
        {
            Font font = Resources.Load<Font>("Fonts/neodgm");
#if UNITY_EDITOR
            if (font == null)
            {
                font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/neodgm.ttf");
            }
#endif
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            if (font != null && font.material != null && font.material.mainTexture != null)
            {
                font.material.mainTexture.filterMode = FilterMode.Point;
            }
            return font;
        }

        #region --- 1. 좌측 상단 프로필 설정 탭 (배지 버튼) ---

        private void CreateProfileBadge(Transform canvasTransform)
        {
            _profileBadgeObj = new GameObject("Auth_Profile_Tab_Button");
            _profileBadgeObj.transform.SetParent(canvasTransform, false);

            RectTransform rect = _profileBadgeObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(45f, -42f);
            rect.sizeDelta = new Vector2(360f, 66f);

            _profileBadgeBg = _profileBadgeObj.AddComponent<Image>();
            _profileBadgeBg.color = new Color(0.08f, 0.11f, 0.18f, 0.94f);

            _profileTabButton = _profileBadgeObj.AddComponent<Button>();
            ColorBlock cb = _profileTabButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            _profileTabButton.colors = cb;
            _profileTabButton.onClick.AddListener(OpenProfileModal);

            // 좌측 등급 도트 아이콘
            GameObject iconObj = new GameObject("Border_Icon");
            iconObj.transform.SetParent(_profileBadgeObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(34f, 0f);
            iconRect.sizeDelta = new Vector2(36f, 36f);

            _profileBadgeIcon = iconObj.AddComponent<Image>();
            _profileBadgeIcon.sprite = BorderDecorationUtility.GetBorderIconSprite(BorderTier.None);
            _profileBadgeIcon.raycastTarget = false;

            // 상단 닉네임 텍스트
            GameObject txtObj = new GameObject("Profile_Name_Text");
            txtObj.transform.SetParent(_profileBadgeObj.transform, false);
            RectTransform txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0f, 0.5f);
            txtRect.anchorMax = new Vector2(1f, 1f);
            txtRect.offsetMin = new Vector2(62f, -2f);
            txtRect.offsetMax = new Vector2(-108f, -6f);

            _profileBadgeText = txtObj.AddComponent<Text>();
            _profileBadgeText.font = _pixelFont;
            _profileBadgeText.fontSize = 20;
            _profileBadgeText.fontStyle = FontStyle.Bold;
            _profileBadgeText.alignment = TextAnchor.MiddleLeft;
            _profileBadgeText.resizeTextForBestFit = true;
            _profileBadgeText.resizeTextMinSize = 13;
            _profileBadgeText.resizeTextMaxSize = 20;
            _profileBadgeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _profileBadgeText.verticalOverflow = VerticalWrapMode.Truncate;
            _profileBadgeText.color = Color.white;
            _profileBadgeText.raycastTarget = false;
            _profileBadgeText.text = "러너";

            // 하단 장착 테두리 상태 소제목
            GameObject subObj = new GameObject("Profile_Sub_Text");
            subObj.transform.SetParent(_profileBadgeObj.transform, false);
            RectTransform subRect = subObj.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 0f);
            subRect.anchorMax = new Vector2(1f, 0.5f);
            subRect.offsetMin = new Vector2(62f, 6f);
            subRect.offsetMax = new Vector2(-108f, 2f);

            _profileBadgeSubText = subObj.AddComponent<Text>();
            _profileBadgeSubText.font = _pixelFont;
            _profileBadgeSubText.fontSize = 14;
            _profileBadgeSubText.alignment = TextAnchor.MiddleLeft;
            _profileBadgeSubText.resizeTextForBestFit = true;
            _profileBadgeSubText.resizeTextMinSize = 11;
            _profileBadgeSubText.resizeTextMaxSize = 14;
            _profileBadgeSubText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _profileBadgeSubText.verticalOverflow = VerticalWrapMode.Truncate;
            _profileBadgeSubText.color = new Color(0.75f, 0.85f, 0.95f);
            _profileBadgeSubText.raycastTarget = false;
            _profileBadgeSubText.text = "기본 테두리";

            // 우측 [프로필 설정] 뱃지 태그
            GameObject tagObj = new GameObject("Profile_Action_Tag");
            tagObj.transform.SetParent(_profileBadgeObj.transform, false);
            RectTransform tagRect = tagObj.AddComponent<RectTransform>();
            tagRect.anchorMin = new Vector2(1f, 0.5f);
            tagRect.anchorMax = new Vector2(1f, 0.5f);
            tagRect.pivot = new Vector2(1f, 0.5f);
            tagRect.anchoredPosition = new Vector2(-14f, 0f);
            tagRect.sizeDelta = new Vector2(88f, 34f);

            Image tagBg = tagObj.AddComponent<Image>();
            tagBg.color = new Color(0.18f, 0.26f, 0.42f, 0.95f);
            tagBg.raycastTarget = false;

            GameObject tagTxtObj = new GameObject("Text");
            tagTxtObj.transform.SetParent(tagObj.transform, false);
            RectTransform tagTxtRect = tagTxtObj.AddComponent<RectTransform>();
            tagTxtRect.sizeDelta = tagRect.sizeDelta;

            Text tagTxt = tagTxtObj.AddComponent<Text>();
            tagTxt.font = _pixelFont;
            tagTxt.fontSize = 15;
            tagTxt.fontStyle = FontStyle.Bold;
            tagTxt.alignment = TextAnchor.MiddleCenter;
            tagTxt.color = new Color(1f, 0.92f, 0.55f);
            tagTxt.raycastTarget = false;
            tagTxt.text = "프로필";

            // 최상위 도트 9-Slice 테두리 프레임 오버레이 (금/은/동/기본 실시간 적용)
            GameObject frameObj = new GameObject("Border_Frame_Overlay");
            frameObj.transform.SetParent(_profileBadgeObj.transform, false);
            RectTransform frameRect = frameObj.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            _profileBadgeBorderFrame = frameObj.AddComponent<Image>();
            _profileBadgeBorderFrame.type = Image.Type.Sliced;
            _profileBadgeBorderFrame.sprite = BorderDecorationUtility.GetBorderFrameSprite(BorderTier.None);
            _profileBadgeBorderFrame.raycastTarget = false;

            _profileBadgeObj.SetActive(false);
        }

        #endregion

        #region --- 2. 회원가입 / 로그인 모달 생성 ---

        private void CreateAuthModal(Transform canvasTransform)
        {
            GameObject dimObj = new GameObject("Auth_Modal_Dim");
            dimObj.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = dimObj.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            Image dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.82f);

            GameObject boxObj = new GameObject("Auth_Box");
            boxObj.transform.SetParent(dimObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(680f, 540f);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.07f, 0.09f, 0.15f, 0.98f);

            Outline boxOutline = boxObj.AddComponent<Outline>();
            boxOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.9f);
            boxOutline.effectDistance = new Vector2(3f, -3f);

            // 상단 타이틀
            _authTitleText = CreateText(boxObj.transform, "Auth_Title", new Vector2(0f, 215f), new Vector2(620f, 50f),
                "캠퍼스 런 학사포털 로그인", 34, FontStyle.Bold, new Color(1f, 0.86f, 0.2f));

            _authSubTitleText = CreateText(boxObj.transform, "Auth_SubTitle", new Vector2(0f, 170f), new Vector2(620f, 34f),
                "로그인 후 기록한 최고 점수는 전체 유저 랭킹에 실시간 반영됩니다!", 18, FontStyle.Normal, new Color(0.8f, 0.88f, 0.98f));

            // 탭 버튼 (로그인 탭 / 회원가입 탭)
            _loginTabButton = CreateButton(boxObj.transform, "Tab_Login", new Vector2(-135f, 112f), new Vector2(250f, 48f),
                "로그인", 22, new Color(0.18f, 0.68f, 0.38f), () => SwitchAuthMode(false), out _loginTabBg, out _);

            _registerTabButton = CreateButton(boxObj.transform, "Tab_Register", new Vector2(135f, 112f), new Vector2(250f, 48f),
                "회원가입", 22, new Color(0.16f, 0.20f, 0.30f), () => SwitchAuthMode(true), out _registerTabBg, out _);

            // 아이디 입력 행
            CreateText(boxObj.transform, "Label_UserId", new Vector2(-195f, 35f), new Vector2(130f, 45f),
                "아이디", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            _userIdInput = CreateInputField(boxObj.transform, "InputField_UserId", new Vector2(55f, 35f), new Vector2(390f, 54f),
                "아이디 입력 (2~16자)", false, out _);

            // 비밀번호 입력 행
            CreateText(boxObj.transform, "Label_Password", new Vector2(-195f, -40f), new Vector2(130f, 45f),
                "비밀번호", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            _passwordInput = CreateInputField(boxObj.transform, "InputField_Password", new Vector2(55f, -40f), new Vector2(390f, 54f),
                "비밀번호 입력 (4자 이상)", true, out _);

            // 피드백/경고 메시지
            _authFeedbackText = CreateText(boxObj.transform, "Auth_Feedback", new Vector2(0f, -102f), new Vector2(620f, 36f),
                string.Empty, 18, FontStyle.Bold, new Color(1f, 0.38f, 0.38f));

            // 제출 버튼
            _authSubmitButton = CreateButton(boxObj.transform, "Auth_Submit_Btn", new Vector2(0f, -165f), new Vector2(380f, 60f),
                "로그인 (Enter)", 24, new Color(0.18f, 0.78f, 0.38f), OnAuthSubmitClicked, out _, out _authSubmitBtnText);

            // 하단 모드 전환 보조 버튼
            _modeSwitchLinkButton = CreateButton(boxObj.transform, "Auth_Switch_Link", new Vector2(0f, -228f), new Vector2(460f, 36f),
                "계정이 없으신가요? [회원가입 탭으로 이동]", 17, new Color(0f, 0f, 0f, 0f),
                () => SwitchAuthMode(!_isRegisterMode), out _, out _modeSwitchLinkText);
            _modeSwitchLinkText.color = new Color(0.65f, 0.85f, 1f);

            _authModal = dimObj;
            _authModal.SetActive(false);
        }

        #endregion

        #region --- 3. 최초 1회 영구 닉네임 입력 모달 생성 ---

        private void CreateNicknameModal(Transform canvasTransform)
        {
            GameObject dimObj = new GameObject("Nickname_Modal_Dim");
            dimObj.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = dimObj.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            Image dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.85f);

            GameObject boxObj = new GameObject("Nickname_Box");
            boxObj.transform.SetParent(dimObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(780f, 500f);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.08f, 0.10f, 0.16f, 0.98f);

            Outline boxOutline = boxObj.AddComponent<Outline>();
            boxOutline.effectColor = new Color(0.2f, 0.9f, 0.45f, 0.95f);
            boxOutline.effectDistance = new Vector2(3f, -3f);

            CreateText(boxObj.transform, "Nick_Title", new Vector2(0f, 180f), new Vector2(720f, 56f),
                "★ 신입생 학생증 발급 (닉네임 등록) ★", 28, FontStyle.Bold, new Color(1f, 0.88f, 0.2f));

            Text descText = CreateText(boxObj.transform, "Nick_Desc", new Vector2(0f, 105f), new Vector2(720f, 80f),
                "환영합니다! 랭킹에 표시될 나만의 닉네임을 설정해 주세요.\n(중복 닉네임은 사용할 수 없으며, 이후 프로필 설정에서 변경할 수 있습니다)",
                18, FontStyle.Normal, new Color(0.88f, 0.94f, 1f));
            descText.lineSpacing = 1.3f;

            _nicknameInput = CreateInputField(boxObj.transform, "InputField_Nickname", new Vector2(0f, 10f), new Vector2(480f, 64f),
                "닉네임 입력 (2~8자, 중복 불가)", false, out _nicknameInputOutline);

            _nicknameFeedbackText = CreateText(boxObj.transform, "Nick_Feedback", new Vector2(0f, -62f), new Vector2(720f, 44f),
                string.Empty, 18, FontStyle.Bold, new Color(1f, 0.38f, 0.38f));

            _nicknameSubmitButton = CreateButton(boxObj.transform, "Nick_Submit_Btn", new Vector2(0f, -145f), new Vector2(420f, 66f),
                "닉네임 확정 및 출발! (Enter)", 22, new Color(0.18f, 0.78f, 0.38f), OnNicknameSubmitClicked, out _, out _);

            _nicknameModal = dimObj;
            _nicknameModal.SetActive(false);
        }

        #endregion

        #region --- 4. 프로필 설정 & 테두리 꾸미기 모달 생성 ---

        private void CreateProfileModal(Transform canvasTransform)
        {
            GameObject dimObj = new GameObject("Profile_Modal_Dim");
            dimObj.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = dimObj.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            Image dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.80f);

            // 바깥 어두운 영역 클릭 시 모달 닫기 (boxObj와 형제 배치하여 내부 클릭 버블링 완전 차단)
            GameObject backdropBtnObj = new GameObject("Dim_Close_Backdrop");
            backdropBtnObj.transform.SetParent(dimObj.transform, false);
            RectTransform backdropRect = backdropBtnObj.AddComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.sizeDelta = Vector2.zero;
            Image backdropImg = backdropBtnObj.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0f);
            Button dimCloseBtn = backdropBtnObj.AddComponent<Button>();
            dimCloseBtn.transition = Selectable.Transition.None;
            dimCloseBtn.onClick.AddListener(CloseProfileModal);

            // 중앙 다이얼로그 박스 (840 x 690)
            GameObject boxObj = new GameObject("Profile_Box");
            boxObj.transform.SetParent(dimObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(840f, 690f);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.07f, 0.09f, 0.15f, 0.98f);

            Outline boxOutline = boxObj.AddComponent<Outline>();
            boxOutline.effectColor = new Color(1f, 0.82f, 0.2f, 0.92f);
            boxOutline.effectDistance = new Vector2(3f, -3f);

            // 상단 타이틀 & [X] 닫기 버튼
            CreateText(boxObj.transform, "Profile_Title", new Vector2(0f, 298f), new Vector2(640f, 48f),
                "★ 프로필 설정 ★", 34, FontStyle.Bold, new Color(1f, 0.86f, 0.2f));

            CreateButton(boxObj.transform, "Profile_Close_X", new Vector2(378f, 298f), new Vector2(44f, 44f),
                "X", 22, new Color(0.75f, 0.22f, 0.25f, 0.95f), CloseProfileModal, out _, out _);

            // 1) 내 프로필 실시간 미리보기 카드 (Y = 200, 좌우 -370 ~ +370)
            GameObject previewObj = new GameObject("Profile_Preview_Card");
            previewObj.transform.SetParent(boxObj.transform, false);
            RectTransform previewRect = previewObj.AddComponent<RectTransform>();
            previewRect.anchoredPosition = new Vector2(0f, 200f);
            previewRect.sizeDelta = new Vector2(740f, 112f);

            _previewCardBg = previewObj.AddComponent<Image>();
            _previewCardBg.color = new Color(0.10f, 0.13f, 0.22f, 0.96f);

            GameObject pIconObj = new GameObject("Preview_Icon");
            pIconObj.transform.SetParent(previewObj.transform, false);
            RectTransform pIconRect = pIconObj.AddComponent<RectTransform>();
            pIconRect.anchorMin = new Vector2(0f, 0.5f);
            pIconRect.anchorMax = new Vector2(0f, 0.5f);
            pIconRect.anchoredPosition = new Vector2(54f, 0f);
            pIconRect.sizeDelta = new Vector2(56f, 56f);
            _previewBorderIcon = pIconObj.AddComponent<Image>();
            _previewBorderIcon.raycastTarget = false;

            // 미리보기 상단 좌측: 유저 닉네임
            _previewNicknameText = CreateText(previewObj.transform, "Preview_Nickname", new Vector2(-52f, 20f), new Vector2(396f, 38f),
                "러너 닉네임", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft);
            ConfigureBoundedText(_previewNicknameText, 18, 28, false);

            // 미리보기 상단 우측: 현재 장착 중인 테두리명
            _previewEquippedText = CreateText(previewObj.transform, "Preview_Equipped", new Vector2(246f, 20f), new Vector2(198f, 32f),
                "장착: 기본 테두리", 16, FontStyle.Bold, new Color(0.82f, 0.92f, 1f), TextAnchor.MiddleRight);
            ConfigureBoundedText(_previewEquippedText, 12, 16, false);

            // 미리보기 하단: ID | 현재 순위 | 최고 기록 (박스 내부 안전 여백 준수)
            _previewStatsText = CreateText(previewObj.transform, "Preview_Stats", new Vector2(46f, -22f), new Vector2(598f, 32f),
                "ID: -  |  현재 순위: -  |  최고 기록: 0m", 17, FontStyle.Normal, new Color(0.80f, 0.88f, 0.98f), TextAnchor.MiddleLeft);
            ConfigureBoundedText(_previewStatsText, 12, 17, false);

            GameObject pFrameObj = new GameObject("Preview_Border_Frame");
            pFrameObj.transform.SetParent(previewObj.transform, false);
            RectTransform pFrameRect = pFrameObj.AddComponent<RectTransform>();
            pFrameRect.anchorMin = Vector2.zero;
            pFrameRect.anchorMax = Vector2.one;
            pFrameRect.offsetMin = Vector2.zero;
            pFrameRect.offsetMax = Vector2.zero;
            _previewBorderFrame = pFrameObj.AddComponent<Image>();
            _previewBorderFrame.type = Image.Type.Sliced;
            _previewBorderFrame.raycastTarget = false;

            // 2) 유저 이름(닉네임) 변경 섹션 (좌우 -370 ~ +370 정밀 정렬)
            CreateText(boxObj.transform, "Section_Name_Label", new Vector2(-230f, 114f), new Vector2(280f, 28f),
                "유저 이름 설정 (2~8자)", 18, FontStyle.Bold, new Color(0.76f, 0.88f, 1f), TextAnchor.MiddleLeft);

            _profileNicknameInput = CreateInputField(boxObj.transform, "Profile_Nickname_Input", new Vector2(-113f, 70f), new Vector2(514f, 48f),
                "새 닉네임 입력 (2~8자)", false, out _profileNicknameOutline);

            _profileNicknameChangeBtn = CreateButton(boxObj.transform, "Profile_Nickname_SaveBtn", new Vector2(264f, 70f), new Vector2(212f, 48f),
                "이름 변경", 20, new Color(0.20f, 0.58f, 0.92f), OnProfileNicknameChangeClicked, out _, out _);

            _profileNicknameFeedbackText = CreateText(boxObj.transform, "Profile_Nickname_Feedback", new Vector2(0f, 30f), new Vector2(740f, 26f),
                string.Empty, 16, FontStyle.Bold, new Color(0.35f, 0.95f, 0.55f));
            ConfigureBoundedText(_profileNicknameFeedbackText, 12, 16, false);

            // 구분선
            GameObject dividerObj = new GameObject("Section_Divider");
            dividerObj.transform.SetParent(boxObj.transform, false);
            RectTransform divRect = dividerObj.AddComponent<RectTransform>();
            divRect.anchoredPosition = new Vector2(0f, 8f);
            divRect.sizeDelta = new Vector2(740f, 2f);
            Image divImg = dividerObj.AddComponent<Image>();
            divImg.color = new Color(0.30f, 0.36f, 0.48f, 0.7f);

            // 3) 테두리 장식 선택 (꾸미기) 섹션 (좌우 -370 ~ +370 정밀 정렬)
            Text borderSectionTitle = CreateText(boxObj.transform, "Section_Border_Title", new Vector2(-180f, -18f), new Vector2(380f, 30f),
                "테두리 장식 (클릭하여 장착/해제)", 19, FontStyle.Bold, new Color(1f, 0.88f, 0.30f), TextAnchor.MiddleLeft);
            ConfigureBoundedText(borderSectionTitle, 14, 19, false);

            Text borderSectionHint = CreateText(boxObj.transform, "Section_Border_Hint", new Vector2(195f, -18f), new Vector2(350f, 28f),
                "※ 획득 장식은 순위 변동 시에도 영구 소유", 13, FontStyle.Normal, new Color(0.72f, 0.82f, 0.92f), TextAnchor.MiddleRight);
            ConfigureBoundedText(borderSectionHint, 11, 13, false);

            // 4개 테두리 선택 카드 (None, Gold, Silver, Bronze)
            BorderTier[] tiers = new BorderTier[] { BorderTier.None, BorderTier.Gold, BorderTier.Silver, BorderTier.Bronze };
            float cardWidth = 176f;
            float cardHeight = 192f;
            float spacing = 12f;
            float startX = -((cardWidth * 4f + spacing * 3f) * 0.5f) + cardWidth * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                float posX = startX + i * (cardWidth + spacing);
                _borderCards[i] = CreateBorderOptionCard(boxObj.transform, tiers[i], new Vector2(posX, -138f), new Vector2(cardWidth, cardHeight));
            }

            _borderActionFeedbackText = CreateText(boxObj.transform, "Border_Action_Feedback", new Vector2(0f, -252f), new Vector2(740f, 26f),
                string.Empty, 16, FontStyle.Bold, new Color(1f, 0.90f, 0.40f));
            ConfigureBoundedText(_borderActionFeedbackText, 12, 16, false);

            // 4) 하단 버튼 바 (Y = -302, 좌우 -370 ~ +370 균형 배치)
            CreateButton(boxObj.transform, "Profile_Logout_Btn", new Vector2(-260f, -302f), new Vector2(220f, 50f),
                "로그아웃", 20, new Color(0.75f, 0.25f, 0.28f, 0.95f), OnLogoutClicked, out _, out _);

            CreateButton(boxObj.transform, "Profile_Close_Btn", new Vector2(115f, -302f), new Vector2(510f, 50f),
                "닫기", 22, new Color(0.18f, 0.75f, 0.40f, 0.98f), CloseProfileModal, out _, out _);

            _profileModal = dimObj;
            _profileModal.SetActive(false);
        }

        private BorderOptionCard CreateBorderOptionCard(Transform parent, BorderTier tier, Vector2 pos, Vector2 size)
        {
            BorderOptionCard card = new BorderOptionCard { Tier = tier };

            GameObject cardObj = new GameObject($"BorderCard_{tier}");
            cardObj.transform.SetParent(parent, false);
            RectTransform rect = cardObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            card.CardBg = cardObj.AddComponent<Image>();
            card.CardBg.color = new Color(0.11f, 0.14f, 0.22f, 0.95f);

            card.Button = cardObj.AddComponent<Button>();
            ColorBlock cb = card.Button.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            cb.disabledColor = new Color(0.55f, 0.55f, 0.58f, 0.85f);
            card.Button.colors = cb;
            BorderTier capturedTier = tier;
            card.Button.onClick.AddListener(() => OnBorderCardClicked(capturedTier));

            // 아이콘
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(cardObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(0f, 44f);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            card.IconImage = iconObj.AddComponent<Image>();
            card.IconImage.sprite = BorderDecorationUtility.GetBorderIconSprite(tier);
            card.IconImage.raycastTarget = false;

            // 이름
            card.TitleText = CreateText(cardObj.transform, "Title", new Vector2(0f, -2f), new Vector2(154f, 28f),
                BorderDecorationUtility.GetShortBadgeName(tier), 16, FontStyle.Bold, BorderDecorationUtility.GetPrimaryColor(tier));
            ConfigureBoundedText(card.TitleText, 12, 16, false);
            card.TitleText.raycastTarget = false;

            // 획득 조건 설명
            string condLabel = tier == BorderTier.None ? "기본 스타일" : $"{BorderDecorationUtility.GetRequiredRank(tier)}위 달성 보상";
            card.ConditionText = CreateText(cardObj.transform, "Condition", new Vector2(0f, -28f), new Vector2(154f, 24f),
                condLabel, 13, FontStyle.Normal, new Color(0.75f, 0.80f, 0.88f));
            ConfigureBoundedText(card.ConditionText, 11, 13, false);
            card.ConditionText.raycastTarget = false;

            // 하단 상태 뱃지
            GameObject badgeObj = new GameObject("Status_Badge");
            badgeObj.transform.SetParent(cardObj.transform, false);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchoredPosition = new Vector2(0f, -64f);
            badgeRect.sizeDelta = new Vector2(148f, 34f);
            card.StatusBadgeBg = badgeObj.AddComponent<Image>();
            card.StatusBadgeBg.color = new Color(0.20f, 0.24f, 0.34f, 1f);
            card.StatusBadgeBg.raycastTarget = false;

            card.StatusBadgeText = CreateText(badgeObj.transform, "Status_Text", Vector2.zero, new Vector2(138f, 30f),
                "장착 가능", 14, FontStyle.Bold, Color.white);
            ConfigureBoundedText(card.StatusBadgeText, 11, 14, false);
            card.StatusBadgeText.raycastTarget = false;

            // 외곽 도트 테두리 프레임
            GameObject frameObj = new GameObject("Frame_Overlay");
            frameObj.transform.SetParent(cardObj.transform, false);
            RectTransform frameRect = frameObj.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            card.FrameOverlay = frameObj.AddComponent<Image>();
            card.FrameOverlay.type = Image.Type.Sliced;
            card.FrameOverlay.sprite = BorderDecorationUtility.GetBorderFrameSprite(tier);
            card.FrameOverlay.raycastTarget = false;

            return card;
        }

        #endregion

        #region --- 5. 신규 테두리 장식 획득 축하 알림 창 생성 ---

        private void CreateBorderUnlockModal(Transform canvasTransform)
        {
            GameObject dimObj = new GameObject("BorderUnlock_Modal_Dim");
            dimObj.transform.SetParent(canvasTransform, false);
            RectTransform dimRect = dimObj.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.sizeDelta = Vector2.zero;

            Image dimImg = dimObj.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.84f);

            GameObject boxObj = new GameObject("BorderUnlock_Box");
            boxObj.transform.SetParent(dimObj.transform, false);
            RectTransform boxRect = boxObj.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(680f, 490f);
            boxRect.anchoredPosition = Vector2.zero;

            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.color = new Color(0.08f, 0.09f, 0.15f, 0.98f);

            // 상단 타이틀
            CreateText(boxObj.transform, "Unlock_Title", new Vector2(0f, 192f), new Vector2(620f, 48f),
                "★ 신규 테두리 장식 획득! ★", 34, FontStyle.Bold, new Color(1f, 0.88f, 0.22f));

            // 중앙 대형 도트 아이콘
            GameObject iconObj = new GameObject("Unlock_Icon");
            iconObj.transform.SetParent(boxObj.transform, false);
            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(0f, 108f);
            iconRect.sizeDelta = new Vector2(76f, 76f);
            _unlockModalIcon = iconObj.AddComponent<Image>();
            _unlockModalIcon.raycastTarget = false;

            // 획득 장식 이름
            _unlockModalTierNameText = CreateText(boxObj.transform, "Unlock_TierName", new Vector2(0f, 44f), new Vector2(600f, 40f),
                "황금 왕관 테두리", 28, FontStyle.Bold, new Color(1f, 0.84f, 0.16f));

            // 미니 프로필 테두리 적용 미리보기 박스
            GameObject miniPreviewObj = new GameObject("Unlock_MiniPreview");
            miniPreviewObj.transform.SetParent(boxObj.transform, false);
            RectTransform miniRect = miniPreviewObj.AddComponent<RectTransform>();
            miniRect.anchoredPosition = new Vector2(0f, -18f);
            miniRect.sizeDelta = new Vector2(360f, 58f);

            _unlockPreviewBg = miniPreviewObj.AddComponent<Image>();
            _unlockPreviewBg.color = new Color(0.16f, 0.12f, 0.04f, 0.96f);

            _unlockPreviewNameText = CreateText(miniPreviewObj.transform, "Preview_Name", Vector2.zero, new Vector2(330f, 44f),
                "러너 닉네임", 22, FontStyle.Bold, Color.white);

            GameObject miniFrameObj = new GameObject("Preview_Frame");
            miniFrameObj.transform.SetParent(miniPreviewObj.transform, false);
            RectTransform miniFrameRect = miniFrameObj.AddComponent<RectTransform>();
            miniFrameRect.anchorMin = Vector2.zero;
            miniFrameRect.anchorMax = Vector2.one;
            miniFrameRect.offsetMin = Vector2.zero;
            miniFrameRect.offsetMax = Vector2.zero;
            _unlockPreviewFrame = miniFrameObj.AddComponent<Image>();
            _unlockPreviewFrame.type = Image.Type.Sliced;
            _unlockPreviewFrame.raycastTarget = false;

            // 설명 문구
            _unlockModalDescText = CreateText(boxObj.transform, "Unlock_Desc", new Vector2(0f, -96f), new Vector2(600f, 84f),
                "축하합니다! 전체 랭킹 1위 달성으로 테두리 장식을 획득했습니다!\n순위가 바뀌어도 영구 소유되며 프로필 설정에서 장착/해제할 수 있습니다.",
                16, FontStyle.Normal, new Color(0.88f, 0.93f, 1f));
            _unlockModalDescText.lineSpacing = 1.3f;
            ConfigureBoundedText(_unlockModalDescText, 12, 16, true);

            // 하단 버튼 (좌: 바로 장착하기 / 우: 확인)
            CreateButton(boxObj.transform, "Unlock_EquipNow_Btn", new Vector2(-135f, -184f), new Vector2(240f, 56f),
                "바로 장착하기", 22, new Color(0.18f, 0.78f, 0.40f), OnUnlockEquipNowClicked, out _, out _);

            CreateButton(boxObj.transform, "Unlock_Confirm_Btn", new Vector2(135f, -184f), new Vector2(240f, 56f),
                "확인", 22, new Color(0.22f, 0.56f, 0.90f), CloseCurrentBorderUnlockPopup, out _, out _);

            // 다이얼로그 외곽 도트 테두리 프레임 오버레이
            GameObject boxFrameObj = new GameObject("Unlock_Box_Frame");
            boxFrameObj.transform.SetParent(boxObj.transform, false);
            RectTransform boxFrameRect = boxFrameObj.AddComponent<RectTransform>();
            boxFrameRect.anchorMin = Vector2.zero;
            boxFrameRect.anchorMax = Vector2.one;
            boxFrameRect.offsetMin = Vector2.zero;
            boxFrameRect.offsetMax = Vector2.zero;
            _unlockModalBorderFrame = boxFrameObj.AddComponent<Image>();
            _unlockModalBorderFrame.type = Image.Type.Sliced;
            _unlockModalBorderFrame.raycastTarget = false;

            _borderUnlockModal = dimObj;
            _borderUnlockModal.SetActive(false);
        }

        #endregion

        #region --- UI 헬퍼 메서드 ---

        private Text CreateText(Transform parent, string name, Vector2 pos, Vector2 size, string content,
            int fontSize, FontStyle style, Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.font = _pixelFont;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = content;
            return txt;
        }

        private static void ConfigureBoundedText(Text txt, int minSize, int maxSize, bool multiLine)
        {
            if (txt == null) return;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = minSize;
            txt.resizeTextMaxSize = maxSize;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = multiLine ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        }

        private Button CreateButton(Transform parent, string name, Vector2 pos, Vector2 size, string label,
            int fontSize, Color bgColor, UnityEngine.Events.UnityAction onClick, out Image outBg, out Text outText)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rect = btnObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            outBg = btnObj.AddComponent<Image>();
            outBg.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.sizeDelta = size;

            outText = txtObj.AddComponent<Text>();
            outText.font = _pixelFont;
            outText.fontSize = fontSize;
            outText.fontStyle = FontStyle.Bold;
            outText.alignment = TextAnchor.MiddleCenter;
            outText.horizontalOverflow = HorizontalWrapMode.Overflow;
            outText.verticalOverflow = VerticalWrapMode.Overflow;
            outText.color = Color.white;
            outText.text = label;

            return btn;
        }

        private InputField CreateInputField(Transform parent, string name, Vector2 pos, Vector2 size,
            string placeholder, bool isPassword, out Outline outOutline)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent, false);
            RectTransform rect = inputObj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Image bg = inputObj.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.17f, 0.26f, 1f);

            outOutline = inputObj.AddComponent<Outline>();
            outOutline.effectColor = new Color(0.38f, 0.68f, 1f, 0.9f);
            outOutline.effectDistance = new Vector2(2f, -2f);

            InputField input = inputObj.AddComponent<InputField>();
            input.characterLimit = 0; // 한글 IME 조합 중 잘림 방지
            if (isPassword)
            {
                input.contentType = InputField.ContentType.Password;
            }

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 2f);
            textRect.offsetMax = new Vector2(-12f, -2f);

            Text txt = textObj.AddComponent<Text>();
            txt.font = _pixelFont;
            txt.fontSize = 22;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            input.textComponent = txt;

            GameObject placeObj = new GameObject("Placeholder");
            placeObj.transform.SetParent(inputObj.transform, false);
            RectTransform placeRect = placeObj.AddComponent<RectTransform>();
            placeRect.anchorMin = Vector2.zero;
            placeRect.anchorMax = Vector2.one;
            placeRect.offsetMin = new Vector2(12f, 2f);
            placeRect.offsetMax = new Vector2(-12f, -2f);

            Text placeTxt = placeObj.AddComponent<Text>();
            placeTxt.font = _pixelFont;
            placeTxt.fontSize = 19;
            placeTxt.fontStyle = FontStyle.Italic;
            placeTxt.color = new Color(0.58f, 0.62f, 0.72f, 0.8f);
            placeTxt.alignment = TextAnchor.MiddleCenter;
            placeTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            placeTxt.verticalOverflow = VerticalWrapMode.Overflow;
            placeTxt.text = placeholder;
            input.placeholder = placeTxt;

            return input;
        }

        #endregion

        #region --- 모달 제어 및 이벤트 핸들러 ---

        public void RefreshUIState()
        {
            EnsureUI();

            if (IsSplashScreenActive()) return;

            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsLoggedIn)
            {
                if (_nicknameModal != null) _nicknameModal.SetActive(false);
                if (_profileBadgeObj != null) _profileBadgeObj.SetActive(false);
                if (_profileModal != null) _profileModal.SetActive(false);
                OpenAuthModal(_isRegisterMode);
                EnsurePlayerPausedWhileModalOpen();
            }
            else if (!auth.HasNickname)
            {
                if (_authModal != null) _authModal.SetActive(false);
                if (_profileBadgeObj != null) _profileBadgeObj.SetActive(false);
                if (_profileModal != null) _profileModal.SetActive(false);
                OpenNicknameModal();
                EnsurePlayerPausedWhileModalOpen();
            }
            else
            {
                if (_authModal != null) _authModal.SetActive(false);
                if (_nicknameModal != null) _nicknameModal.SetActive(false);

                bool showProfileTab = !_isGamePlaying && !_isGameOver;
                if (_profileBadgeObj != null)
                {
                    _profileBadgeObj.SetActive(showProfileTab);
                }

                RefreshProfileBadgeVisual(auth);

                if (_profileModal != null && _profileModal.activeSelf)
                {
                    RefreshProfileModalContent();
                }

                if (!_isGamePlaying && !_isGameOver && !IsAnyAuthModalActive)
                {
                    GameEvents.TriggerGameStateChanged(GameState.Ready);
                }
            }
        }

        private void RefreshProfileBadgeVisual(AuthManager auth)
        {
            if (auth == null) return;

            BorderTier equipped = auth.CurrentEquippedBorder;
            string hex = BorderDecorationUtility.GetHexColor(equipped);

            if (_profileBadgeBg != null)
            {
                _profileBadgeBg.color = BorderDecorationUtility.GetAccentBgColor(equipped);
            }
            if (_profileBadgeBorderFrame != null)
            {
                _profileBadgeBorderFrame.sprite = BorderDecorationUtility.GetBorderFrameSprite(equipped);
            }
            if (_profileBadgeIcon != null)
            {
                _profileBadgeIcon.sprite = BorderDecorationUtility.GetBorderIconSprite(equipped);
            }
            if (_profileBadgeText != null)
            {
                _profileBadgeText.text = $"<color={hex}>{auth.CurrentNickname}</color>";
            }
            if (_profileBadgeSubText != null)
            {
                _profileBadgeSubText.text = BorderDecorationUtility.GetShortBadgeName(equipped);
                _profileBadgeSubText.color = equipped == BorderTier.None
                    ? new Color(0.74f, 0.84f, 0.94f)
                    : BorderDecorationUtility.GetPrimaryColor(equipped);
            }
        }

        public void OpenProfileModal()
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsLoggedIn || !auth.HasNickname) return;
            if (_isGamePlaying) return;

            EnsureUI();

            if (_profileNicknameInput != null)
            {
                _profileNicknameInput.text = auth.CurrentNickname;
            }
            if (_profileNicknameFeedbackText != null)
            {
                _profileNicknameFeedbackText.text = string.Empty;
            }
            if (_profileNicknameOutline != null)
            {
                _profileNicknameOutline.effectColor = new Color(0.38f, 0.68f, 1f, 0.9f);
            }
            if (_borderActionFeedbackText != null)
            {
                _borderActionFeedbackText.text = string.Empty;
            }

            RefreshProfileModalContent();

            if (_profileModal != null)
            {
                _profileModal.SetActive(true);
            }

            EnsurePlayerPausedWhileModalOpen();
        }

        public void CloseProfileModal()
        {
            if (_profileModal != null)
            {
                _profileModal.SetActive(false);
            }

            if (!_isGamePlaying && !_isGameOver && !IsAnyAuthModalActive)
            {
                GameEvents.TriggerGameStateChanged(GameState.Ready);
            }
        }

        private void RefreshProfileModalContent()
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsLoggedIn) return;

            BorderTier equipped = auth.CurrentEquippedBorder;
            string hex = BorderDecorationUtility.GetHexColor(equipped);

            // 상단 실시간 미리보기 카드 갱신
            if (_previewCardBg != null)
            {
                _previewCardBg.color = BorderDecorationUtility.GetAccentBgColor(equipped);
            }
            if (_previewBorderFrame != null)
            {
                _previewBorderFrame.sprite = BorderDecorationUtility.GetBorderFrameSprite(equipped);
            }
            if (_previewBorderIcon != null)
            {
                _previewBorderIcon.sprite = BorderDecorationUtility.GetBorderIconSprite(equipped);
            }
            if (_previewNicknameText != null)
            {
                _previewNicknameText.text = $"<color={hex}>{auth.CurrentNickname}</color>";
            }
            if (_previewEquippedText != null)
            {
                _previewEquippedText.text = $"장착: <color={hex}>{BorderDecorationUtility.GetShortBadgeName(equipped)}</color>";
            }
            if (_previewStatsText != null)
            {
                int rank = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.GetPlayerRank(auth.CurrentNickname) : -1;
                int bestScore = auth.CurrentAccount != null ? auth.CurrentAccount.bestScore : 0;
                if (LeaderboardManager.Instance != null)
                {
                    var entry = LeaderboardManager.Instance.GetPlayerEntry(auth.CurrentNickname);
                    if (entry != null && entry.score > bestScore) bestScore = entry.score;
                }
                string rankText = rank > 0 ? $"{rank}위" : "미등록";
                _previewStatsText.text = $"ID: {auth.CurrentUserId}  |  현재 순위: <color=#FFD700>{rankText}</color>  |  최고 기록: <color=#4ADE80>{bestScore}m</color>";
            }

            // 4개 테두리 선택 카드 갱신
            for (int i = 0; i < _borderCards.Length; i++)
            {
                BorderOptionCard card = _borderCards[i];
                if (card == null) continue;

                bool isUnlocked = auth.IsBorderUnlocked(card.Tier);
                bool isEquipped = equipped == card.Tier;

                if (card.Button != null)
                {
                    card.Button.interactable = isUnlocked;
                }

                if (!isUnlocked)
                {
                    if (card.CardBg != null) card.CardBg.color = new Color(0.08f, 0.09f, 0.13f, 0.90f);
                    if (card.FrameOverlay != null) card.FrameOverlay.color = new Color(0.45f, 0.45f, 0.48f, 0.55f);
                    if (card.IconImage != null) card.IconImage.color = new Color(0.35f, 0.35f, 0.38f, 0.7f);
                    if (card.TitleText != null) card.TitleText.color = new Color(0.52f, 0.55f, 0.62f);
                    if (card.StatusBadgeBg != null) card.StatusBadgeBg.color = new Color(0.22f, 0.16f, 0.18f, 0.95f);
                    if (card.StatusBadgeText != null)
                    {
                        card.StatusBadgeText.text = $"잠김 ({BorderDecorationUtility.GetRequiredRank(card.Tier)}위)";
                        card.StatusBadgeText.color = new Color(0.88f, 0.55f, 0.55f);
                    }
                }
                else if (isEquipped)
                {
                    if (card.CardBg != null) card.CardBg.color = BorderDecorationUtility.GetAccentBgColor(card.Tier);
                    if (card.FrameOverlay != null) card.FrameOverlay.color = Color.white;
                    if (card.IconImage != null) card.IconImage.color = Color.white;
                    if (card.TitleText != null) card.TitleText.color = BorderDecorationUtility.GetPrimaryColor(card.Tier);
                    if (card.StatusBadgeBg != null) card.StatusBadgeBg.color = new Color(0.16f, 0.72f, 0.38f, 1f);
                    if (card.StatusBadgeText != null)
                    {
                        card.StatusBadgeText.text = card.Tier == BorderTier.None ? "★ 적용 중" : "★ 장착 중 (해제)";
                        card.StatusBadgeText.color = Color.white;
                    }
                }
                else
                {
                    if (card.CardBg != null) card.CardBg.color = new Color(0.12f, 0.15f, 0.24f, 0.95f);
                    if (card.FrameOverlay != null) card.FrameOverlay.color = new Color(0.92f, 0.92f, 0.92f, 0.9f);
                    if (card.IconImage != null) card.IconImage.color = Color.white;
                    if (card.TitleText != null) card.TitleText.color = BorderDecorationUtility.GetPrimaryColor(card.Tier);
                    if (card.StatusBadgeBg != null) card.StatusBadgeBg.color = new Color(0.20f, 0.42f, 0.72f, 1f);
                    if (card.StatusBadgeText != null)
                    {
                        card.StatusBadgeText.text = card.Tier == BorderTier.None ? "장착 해제" : "장착하기";
                        card.StatusBadgeText.color = Color.white;
                    }
                }
            }
        }

        private void OnBorderCardClicked(BorderTier tier)
        {
            var auth = AuthManager.Instance;
            if (auth == null || !auth.IsLoggedIn) return;
            if (tier != BorderTier.None && !auth.IsBorderUnlocked(tier)) return;

            // 이미 장착 중인 장식을 다시 클릭하면 해제(None)로 토글
            BorderTier nextTier = (auth.CurrentEquippedBorder == tier && tier != BorderTier.None)
                ? BorderTier.None
                : tier;

            auth.SetEquippedBorder(nextTier);

            if (_borderActionFeedbackText != null)
            {
                if (nextTier == BorderTier.None)
                {
                    _borderActionFeedbackText.color = new Color(0.75f, 0.88f, 1f);
                    _borderActionFeedbackText.text = "테두리 장식이 해제되어 기본 스타일로 변경되었습니다.";
                }
                else
                {
                    _borderActionFeedbackText.color = BorderDecorationUtility.GetPrimaryColor(nextTier);
                    _borderActionFeedbackText.text = $"[{BorderDecorationUtility.GetDisplayName(nextTier)}] 장착 완료!";
                }
            }

            RefreshProfileBadgeVisual(auth);
            RefreshProfileModalContent();
        }

        private void OnProfileNicknameChangeClicked()
        {
            if (_lastSubmitFrame == Time.frameCount) return;
            _lastSubmitFrame = Time.frameCount;

            if (AuthManager.Instance == null || AuthManager.Instance.IsBusy) return;

            string rawNewName = FlushInputField(_profileNicknameInput);

            if (_profileNicknameFeedbackText != null)
            {
                _profileNicknameFeedbackText.color = new Color(0.8f, 0.9f, 1f);
                _profileNicknameFeedbackText.text = "닉네임 중복 확인 및 변경 중...";
            }

            AuthManager.Instance.ChangeNickname(rawNewName, (success, msg) =>
            {
                if (_profileNicknameFeedbackText != null)
                {
                    _profileNicknameFeedbackText.color = success
                        ? new Color(0.35f, 0.95f, 0.55f)
                        : new Color(1f, 0.38f, 0.38f);
                    _profileNicknameFeedbackText.text = msg;
                }

                if (_profileNicknameOutline != null)
                {
                    _profileNicknameOutline.effectColor = success
                        ? new Color(0.28f, 0.88f, 0.48f, 0.95f)
                        : new Color(1f, 0.32f, 0.32f, 0.95f);
                }

                if (success)
                {
                    RefreshProfileBadgeVisual(AuthManager.Instance);
                    RefreshProfileModalContent();
                }
            });
        }

        private void HandleNewBorderUnlocked(BorderTier tier)
        {
            if (tier == BorderTier.None) return;

            EnsureUI();
            if (!_pendingUnlockQueue.Contains(tier))
            {
                _pendingUnlockQueue.Enqueue(tier);
            }

            if (!IsSplashScreenActive() && (_borderUnlockModal == null || !_borderUnlockModal.activeSelf))
            {
                ShowNextBorderUnlockPopup();
            }
        }

        private void ShowNextBorderUnlockPopup()
        {
            if (_pendingUnlockQueue.Count == 0) return;

            _currentUnlockedTier = _pendingUnlockQueue.Dequeue();
            EnsureUI();

            Color primaryColor = BorderDecorationUtility.GetPrimaryColor(_currentUnlockedTier);
            string hex = BorderDecorationUtility.GetHexColor(_currentUnlockedTier);
            int requiredRank = BorderDecorationUtility.GetRequiredRank(_currentUnlockedTier);
            string displayName = BorderDecorationUtility.GetDisplayName(_currentUnlockedTier);
            string myName = (AuthManager.Instance != null && AuthManager.Instance.HasNickname)
                ? AuthManager.Instance.CurrentNickname
                : "캠퍼스 러너";

            if (_unlockModalBorderFrame != null)
            {
                _unlockModalBorderFrame.sprite = BorderDecorationUtility.GetBorderFrameSprite(_currentUnlockedTier);
            }
            if (_unlockModalIcon != null)
            {
                _unlockModalIcon.sprite = BorderDecorationUtility.GetBorderIconSprite(_currentUnlockedTier);
            }
            if (_unlockModalTierNameText != null)
            {
                _unlockModalTierNameText.text = displayName;
                _unlockModalTierNameText.color = primaryColor;
            }
            if (_unlockPreviewBg != null)
            {
                _unlockPreviewBg.color = BorderDecorationUtility.GetAccentBgColor(_currentUnlockedTier);
            }
            if (_unlockPreviewFrame != null)
            {
                _unlockPreviewFrame.sprite = BorderDecorationUtility.GetBorderFrameSprite(_currentUnlockedTier);
            }
            if (_unlockPreviewNameText != null)
            {
                _unlockPreviewNameText.text = $"<color={hex}>{myName}</color>";
            }
            if (_unlockModalDescText != null)
            {
                _unlockModalDescText.text =
                    $"축하합니다! 전체 랭킹 <color={hex}>{requiredRank}위</color> 달성 보상으로\n<color={hex}>[{displayName}]</color> 장식을 획득했습니다!\n(순위가 바뀌어도 영구 소유되며, 프로필 설정에서 언제든 장착/해제 가능)";
            }

            if (_borderUnlockModal != null)
            {
                _borderUnlockModal.transform.SetAsLastSibling();
                _borderUnlockModal.SetActive(true);
            }

            if (!_isGamePlaying && !_isGameOver)
            {
                EnsurePlayerPausedWhileModalOpen();
            }
        }

        private void OnUnlockEquipNowClicked()
        {
            if (AuthManager.Instance != null && _currentUnlockedTier != BorderTier.None)
            {
                AuthManager.Instance.SetEquippedBorder(_currentUnlockedTier);
            }
            CloseCurrentBorderUnlockPopup();
        }

        private void CloseCurrentBorderUnlockPopup()
        {
            if (_borderUnlockModal != null)
            {
                _borderUnlockModal.SetActive(false);
            }
            _currentUnlockedTier = BorderTier.None;

            if (_pendingUnlockQueue.Count > 0)
            {
                ShowNextBorderUnlockPopup();
                return;
            }

            if (!_isGamePlaying && !_isGameOver && !IsAnyAuthModalActive)
            {
                GameEvents.TriggerGameStateChanged(GameState.Ready);
            }
        }

        public void OpenAuthModal(bool registerMode)
        {
            EnsureUI();
            if (_authModal != null)
            {
                _authModal.SetActive(true);
            }

            SwitchAuthMode(registerMode);
            EnsurePlayerPausedWhileModalOpen();
        }

        private void SwitchAuthMode(bool registerMode)
        {
            _isRegisterMode = registerMode;

            // 탭(로그인 <-> 회원가입) 전환 시 입력되어 있던 아이디/비밀번호를 즉시 초기화하여 잔류 방지
            if (_userIdInput != null)
            {
                _userIdInput.text = string.Empty;
            }
            if (_passwordInput != null)
            {
                _passwordInput.text = string.Empty;
            }

            if (_authTitleText != null)
            {
                _authTitleText.text = _isRegisterMode
                    ? "캠퍼스 런 신입생 회원가입"
                    : "캠퍼스 런 학사포털 로그인";
            }

            if (_loginTabBg != null)
            {
                _loginTabBg.color = !_isRegisterMode
                    ? new Color(0.18f, 0.68f, 0.38f)
                    : new Color(0.16f, 0.20f, 0.30f);
            }

            if (_registerTabBg != null)
            {
                _registerTabBg.color = _isRegisterMode
                    ? new Color(0.22f, 0.56f, 0.92f)
                    : new Color(0.16f, 0.20f, 0.30f);
            }

            if (_authSubmitBtnText != null)
            {
                _authSubmitBtnText.text = _isRegisterMode
                    ? "회원가입 완료 (Enter)"
                    : "로그인 (Enter)";
            }

            if (_modeSwitchLinkText != null)
            {
                _modeSwitchLinkText.text = _isRegisterMode
                    ? "이미 계정이 있으신가요? [로그인 탭으로 이동]"
                    : "계정이 없으신가요? [회원가입 탭으로 이동]";
            }

            if (_authFeedbackText != null)
            {
                _authFeedbackText.text = string.Empty;
            }

            if (_userIdInput != null)
            {
                _userIdInput.Select();
                _userIdInput.ActivateInputField();
            }
        }

        private void OpenNicknameModal()
        {
            EnsureUI();
            if (_nicknameModal != null)
            {
                _nicknameModal.SetActive(true);
            }

            if (_nicknameFeedbackText != null)
            {
                _nicknameFeedbackText.text = string.Empty;
            }

            if (_nicknameInput != null)
            {
                _nicknameInput.text = string.Empty;
                _nicknameInput.Select();
                _nicknameInput.ActivateInputField();
            }

            EnsurePlayerPausedWhileModalOpen();
        }

        private void OnAuthSubmitClicked()
        {
            if (_lastSubmitFrame == Time.frameCount) return;
            _lastSubmitFrame = Time.frameCount;

            if (AuthManager.Instance == null || AuthManager.Instance.IsBusy) return;

            string userId = FlushInputField(_userIdInput);
            string password = FlushInputField(_passwordInput);

            if (_authFeedbackText != null)
            {
                _authFeedbackText.color = new Color(0.8f, 0.9f, 1f);
                _authFeedbackText.text = "서버와 통신 중입니다...";
            }

            if (_isRegisterMode)
            {
                AuthManager.Instance.Register(userId, password, (success, msg) =>
                {
                    if (success)
                    {
                        SwitchAuthMode(false);
                        if (_userIdInput != null) _userIdInput.text = userId.Trim();
                        if (_passwordInput != null)
                        {
                            _passwordInput.text = string.Empty;
                            _passwordInput.Select();
                            _passwordInput.ActivateInputField();
                        }
                        if (_authFeedbackText != null)
                        {
                            _authFeedbackText.color = new Color(0.35f, 0.95f, 0.55f);
                            _authFeedbackText.text = "✔ " + msg;
                        }
                    }
                    else
                    {
                        if (_authFeedbackText != null)
                        {
                            _authFeedbackText.color = new Color(1f, 0.38f, 0.38f);
                            _authFeedbackText.text = msg;
                        }
                    }
                });
            }
            else
            {
                AuthManager.Instance.Login(userId, password, (success, msg) =>
                {
                    if (success)
                    {
                        if (_passwordInput != null) _passwordInput.text = string.Empty;
                        RefreshUIState();
                    }
                    else
                    {
                        if (_authFeedbackText != null)
                        {
                            _authFeedbackText.color = new Color(1f, 0.38f, 0.38f);
                            _authFeedbackText.text = msg;
                        }
                    }
                });
            }
        }

        private void OnNicknameSubmitClicked()
        {
            if (_lastSubmitFrame == Time.frameCount) return;
            _lastSubmitFrame = Time.frameCount;

            if (AuthManager.Instance == null || AuthManager.Instance.IsBusy) return;

            string rawNickname = FlushInputField(_nicknameInput);

            if (_nicknameFeedbackText != null)
            {
                _nicknameFeedbackText.color = new Color(0.8f, 0.9f, 1f);
                _nicknameFeedbackText.text = "닉네임 중복 확인 및 등록 중...";
            }

            AuthManager.Instance.SetInitialNickname(rawNickname, (success, msg) =>
            {
                if (success)
                {
                    RefreshUIState();
                }
                else
                {
                    if (_nicknameFeedbackText != null)
                    {
                        _nicknameFeedbackText.color = new Color(1f, 0.38f, 0.38f);
                        _nicknameFeedbackText.text = msg;
                    }
                    if (_nicknameInputOutline != null)
                    {
                        _nicknameInputOutline.effectColor = new Color(1f, 0.32f, 0.32f);
                    }
                    if (_nicknameInput != null)
                    {
                        _nicknameInput.Select();
                        _nicknameInput.ActivateInputField();
                    }
                }
            });
        }

        private void OnLogoutClicked()
        {
            if (_profileModal != null)
            {
                _profileModal.SetActive(false);
            }
            if (_borderUnlockModal != null)
            {
                _borderUnlockModal.SetActive(false);
            }
            _pendingUnlockQueue.Clear();
            _currentUnlockedTier = BorderTier.None;

            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.Logout();
            }
        }

        private static string FlushInputField(InputField field)
        {
            if (field == null) return string.Empty;

            field.DeactivateInputField();
            string text = field.text ?? string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(Input.compositionString))
                {
                    text += Input.compositionString;
                }
            }
            catch { }
            return text;
        }

        #endregion
    }
}
