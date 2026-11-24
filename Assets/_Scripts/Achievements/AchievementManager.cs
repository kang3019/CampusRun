using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using CampusRun.Core;
using CampusRun.Audio;
using CampusRun.Player;

namespace CampusRun.Achievements
{
    /// <summary>
    /// 미터(m) 달성 및 다양한 인게임 플레이 미션(총 10종)을 관리하고,
    /// 미션 달성(클리어) 시 축하 연출과 함께 즉시 다른 미션으로 자동 리롤(Reroll)하는 업적 시스템 매니저입니다.
    /// 플레이어가 실패(사망/재시작)하더라도 미션 UI와 달성 진행도가 절대 사라지지 않고 유지됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }

        [Header("--- 업적/미션 시스템 설정 ---")]
        [Tooltip("화면 좌측 상단에 실시간 업적 미션 HUD 카드를 표시할지 여부")]
        [SerializeField] private bool _showMissionHUD = true;

        [Tooltip("클리어 시 알림 배너 표시 시간(초)")]
        [SerializeField] private float _clearToastDuration = 2.6f;

        private const string PrefKeyCurrentMissionId = "CampusRun_Ach_CurrentMissionId";
        private const string PrefKeyCurrentProgress = "CampusRun_Ach_CurrentProgress";
        private const string PrefKeyTotalCleared = "CampusRun_Ach_TotalCleared";

        // --- 10종의 캠퍼스 서바이벌 업적/미션 풀 (실패/사망 시에도 진행도 유지) ---
        private static readonly AchievementMissionDef[] MissionPool = new AchievementMissionDef[]
        {
            new AchievementMissionDef(
                0,
                "🏃 [워밍업 등교] 50m 돌파",
                "전진 거리 50m 달성하기",
                "m",
                AchievementMissionType.ReachDistance,
                50,
                false),

            new AchievementMissionDef(
                1,
                "🚌 [셔틀버스 추월] 100m 돌파",
                "전진 거리 100m(C+ 구간) 달성하기",
                "m",
                AchievementMissionType.ReachDistance,
                100,
                false),

            new AchievementMissionDef(
                2,
                "📚 [장학금 가시권] 200m 돌파",
                "전진 거리 200m(B0 구간) 달성하기",
                "m",
                AchievementMissionType.ReachDistance,
                200,
                false),

            new AchievementMissionDef(
                3,
                "🎓 [과탑의 위엄] 300m 돌파",
                "전진 거리 300m(B+ 구간) 달성하기",
                "m",
                AchievementMissionType.ReachDistance,
                300,
                false),

            new AchievementMissionDef(
                4,
                "🪵 [통나무 서퍼] 통나무 5회 디딤",
                "물웅덩이 통나무 위에 5번 착지하기",
                "회",
                AchievementMissionType.StepPlankCount,
                5,
                false),

            new AchievementMissionDef(
                5,
                "🌊 [하천 지배자] 통나무 12회 디딤",
                "물웅덩이 통나무 위에 12번 착지하기",
                "회",
                AchievementMissionType.StepPlankCount,
                12,
                false),

            new AchievementMissionDef(
                6,
                "🔥 [노빠꾸 직진] 무후퇴 100m 전진",
                "뒤로 후퇴(S/↓) 없이 100m 전진하기",
                "m",
                AchievementMissionType.NoBackwardDistance,
                100,
                false),

            new AchievementMissionDef(
                7,
                "⚡ [현란한 칼스텝] 좌우 이동 15회",
                "장애물을 피해 좌/우(A,D / ←,→)로 15번 이동하기",
                "회",
                AchievementMissionType.SideStepCount,
                15,
                false),

            new AchievementMissionDef(
                8,
                "🦅 [매의 눈 탈출] 지각 경고 탈출",
                "교수님 접근(지각 위기) 경고 상태에서 전진해 탈출하기",
                "회",
                AchievementMissionType.EscapeInactivityWarning,
                1,
                false),

            new AchievementMissionDef(
                9,
                "👟 [캠퍼스 마라토너] 점프 40회 달성",
                "방향에 상관없이 총 40번 점프(홉) 이동하기",
                "회",
                AchievementMissionType.TotalHopCount,
                40,
                false)
        };

        // 런타임 미션 진행 상태
        private int _currentMissionIndex = 0;
        private int _currentProgress = 0;
        private int _totalClearedCount = 0;

        // 판별 추적용 상태 변수
        private int _lastRecordedRunScore = 0;
        private int _maxZReachedInCurrentRun = 0;
        private Vector3Int _lastPlayerGridPos = Vector3Int.zero;
        private bool _wasInactivityWarningActive = false;

        // 중복 없는 순환 리롤을 위한 셔플 백(Deck)
        private readonly List<int> _rerollBag = new List<int>(10);

        // --- UI 요소 캐싱 ---
        private Font _pixelFont;
        private GameObject _hudRootObj;
        private Image _cardBgImage;
        private Outline _cardOutline;
        private Text _headerBadgeText;
        private Text _missionTitleText;
        private Text _missionDescText;
        private RectTransform _progressFillRect;
        private Image _progressFillImage;
        private Text _progressValueText;
        private Button _rerollButton;

        // 클리어 토스트 배너 UI 캐싱
        private GameObject _clearToastObj;
        private CanvasGroup _clearToastCanvasGroup;
        private RectTransform _clearToastRect;
        private Text _clearToastTitleText;
        private Text _clearToastSubText;
        private Coroutine _clearToastRoutine;
        private Coroutine _cardPulseRoutine;
        private GameObject _cachedSplashPanel;

        public AchievementMissionDef CurrentMission => MissionPool[_currentMissionIndex];
        public int CurrentProgress => _currentProgress;
        public int TotalClearedCount => _totalClearedCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFields()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitializeInScene()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 씬 최초 로드 및 실패 후 다시하기(SceneManager.LoadScene) 시에도 항상 AchievementManager가 존재하도록 보장합니다.
        /// </summary>
        public static AchievementManager EnsureInitialized()
        {
            if (Instance == null)
            {
                Instance = FindAnyObjectByType<AchievementManager>();
                if (Instance == null)
                {
                    GameObject achObj = new GameObject("[AchievementManager]");
                    Instance = achObj.AddComponent<AchievementManager>();
                    DontDestroyOnLoad(achObj);
                }
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSavedState();
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SubscribeEvents();
        }

        private void Start()
        {
            if (_showMissionHUD)
            {
                EnsureMissionHUD();
                RefreshHUDDisplay();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                UnsubscribeEvents();
                Instance = null;
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

        private void SubscribeEvents()
        {
            UnsubscribeEvents();
            GameEvents.OnGameStarted += HandleGameStarted;
            GameEvents.OnGameRestarted += HandleGameRestarted;
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnPlayerGridMoved += HandlePlayerGridMoved;
            GameEvents.OnPlayerSteppedPlank += HandlePlayerSteppedPlank;
            GameEvents.OnInactivityWarning += HandleInactivityWarning;
            GameEvents.OnPlayerDied += HandlePlayerDied;
        }

        private void UnsubscribeEvents()
        {
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnPlayerGridMoved -= HandlePlayerGridMoved;
            GameEvents.OnPlayerSteppedPlank -= HandlePlayerSteppedPlank;
            GameEvents.OnInactivityWarning -= HandleInactivityWarning;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
        }

        /// <summary>
        /// 실패 후 '다시 하기'로 씬이 재로드되었을 때, 초기화된 GameEvents를 다시 구독하고 새 Canvas에 미션 HUD를 즉시 복구합니다.
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SubscribeEvents();
            ResetPerRunCountersOnly();

            _hudRootObj = null;
            _cachedSplashPanel = null;

            if (_showMissionHUD)
            {
                EnsureMissionHUD();
                RefreshHUDDisplay();
            }
        }

        private void Update()
        {
            if (!_showMissionHUD) return;

            // 씬 리로드 직후 HUD가 아직 생성되지 않았다면 즉시 복구
            if (_hudRootObj == null)
            {
                EnsureMissionHUD();
                if (_hudRootObj != null)
                {
                    RefreshHUDDisplay();
                }
            }

            // 스플래시 인트로 화면이 떠 있는 동안에만 숨기고, 타이틀/인게임/게임오버(실패) 화면에서는 항상 표시!
            if (_hudRootObj != null)
            {
                if (_cachedSplashPanel == null)
                {
                    Canvas canvas = FindAnyObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        Transform splashTr = canvas.transform.Find("SplashScreen_Panel");
                        if (splashTr != null) _cachedSplashPanel = splashTr.gameObject;
                    }
                }

                bool shouldShow = (_cachedSplashPanel == null || !_cachedSplashPanel.activeInHierarchy);
                if (_hudRootObj.activeSelf != shouldShow)
                {
                    _hudRootObj.SetActive(shouldShow);
                }
            }
        }

        #region --- 세이브/로드 및 리롤(Reroll) 로직 ---

        private void LoadSavedState()
        {
            _totalClearedCount = PlayerPrefs.GetInt(PrefKeyTotalCleared, 0);
            int savedId = PlayerPrefs.GetInt(PrefKeyCurrentMissionId, -1);

            if (savedId >= 0 && savedId < MissionPool.Length)
            {
                _currentMissionIndex = savedId;
                _currentProgress = Mathf.Clamp(
                    PlayerPrefs.GetInt(PrefKeyCurrentProgress, 0),
                    0,
                    MissionPool[_currentMissionIndex].TargetValue);
            }
            else
            {
                // 최초 실행 시 첫 미션 배정
                RefillAndShuffleBag(-1);
                _currentMissionIndex = PopNextMissionFromBag(-1);
                _currentProgress = 0;
                SaveCurrentState();
            }
        }

        private void SaveCurrentState()
        {
            PlayerPrefs.SetInt(PrefKeyCurrentMissionId, _currentMissionIndex);
            PlayerPrefs.SetInt(PrefKeyCurrentProgress, _currentProgress);
            PlayerPrefs.SetInt(PrefKeyTotalCleared, _totalClearedCount);
            PlayerPrefs.Save();
        }

        private void RefillAndShuffleBag(int excludeIndex)
        {
            _rerollBag.Clear();
            for (int i = 0; i < MissionPool.Length; i++)
            {
                if (i != excludeIndex)
                {
                    _rerollBag.Add(i);
                }
            }

            // Fisher-Yates 셔플
            for (int i = _rerollBag.Count - 1; i > 0; i--)
            {
                int swapIdx = Random.Range(0, i + 1);
                int temp = _rerollBag[i];
                _rerollBag[i] = _rerollBag[swapIdx];
                _rerollBag[swapIdx] = temp;
            }
        }

        private int PopNextMissionFromBag(int excludeIndex)
        {
            if (_rerollBag.Count == 0)
            {
                RefillAndShuffleBag(excludeIndex);
            }

            for (int i = 0; i < _rerollBag.Count; i++)
            {
                int candidate = _rerollBag[i];
                if (candidate != excludeIndex)
                {
                    _rerollBag.RemoveAt(i);
                    return candidate;
                }
            }

            int fallback = (excludeIndex + 1) % MissionPool.Length;
            return fallback;
        }

        /// <summary>
        /// 현재 미션을 클리어했거나 수동 리롤 버튼을 눌렀을 때 다른 미션으로 즉시 교체(리롤)합니다.
        /// </summary>
        public void RerollMission(bool isManualReroll = false)
        {
            int prevIndex = _currentMissionIndex;
            int nextIndex = PopNextMissionFromBag(prevIndex);

            _currentMissionIndex = nextIndex;
            _currentProgress = 0;

            SaveCurrentState();
            RefreshHUDDisplay();

            if (isManualReroll)
            {
                AudioManager.Instance?.PlayHopSound();
                TriggerCardPulse(new Color(0.25f, 0.78f, 1.0f));
            }
        }

        #endregion

        #region --- 인게임 이벤트 핸들러 및 미션 달성 판정 ---

        private void HandleGameStarted()
        {
            ResetPerRunCountersOnly();
            RefreshHUDDisplay();
        }

        private void HandleGameRestarted()
        {
            ResetPerRunCountersOnly();
            RefreshHUDDisplay();
        }

        /// <summary>
        /// 새 판이 시작될 때 플레이어의 좌표 기준점만 0으로 초기화하며,
        /// 미션 자체나 쌓아둔 미션 진행도(_currentProgress)는 절대 초기화/삭제하지 않습니다.
        /// </summary>
        private void ResetPerRunCountersOnly()
        {
            _lastRecordedRunScore = 0;
            _maxZReachedInCurrentRun = 0;
            _lastPlayerGridPos = Vector3Int.zero;
            _wasInactivityWarningActive = false;
        }

        private void HandleScoreChanged(int newScore)
        {
            int deltaScore = newScore - _lastRecordedRunScore;
            if (deltaScore <= 0) return;

            _lastRecordedRunScore = newScore;

            AchievementMissionDef mission = CurrentMission;
            if (mission.MissionType == AchievementMissionType.ReachDistance)
            {
                // 전진한 미터(m)만큼 진행도를 누적하여, 도중에 실패(사망)하더라도 진행도가 사라지지 않도록 보장
                UpdateMissionProgress(_currentProgress + deltaScore);
            }
        }

        private void HandlePlayerGridMoved(Vector3Int newGridPos)
        {
            Vector3Int delta = newGridPos - _lastPlayerGridPos;
            _lastPlayerGridPos = newGridPos;

            AchievementMissionDef mission = CurrentMission;

            // 1. 총 점프 횟수 미션
            if (mission.MissionType == AchievementMissionType.TotalHopCount)
            {
                UpdateMissionProgress(_currentProgress + 1);
                return;
            }

            // 2. 좌/우 회피 스텝 미션
            if (mission.MissionType == AchievementMissionType.SideStepCount)
            {
                if (delta.x != 0)
                {
                    UpdateMissionProgress(_currentProgress + 1);
                }
                return;
            }

            // 3. 무후퇴 전진 미션 (앞으로 최고 Z를 경신할 때마다 +10m 누적, 실패해도 기존 진행도 유지)
            if (mission.MissionType == AchievementMissionType.NoBackwardDistance)
            {
                if (newGridPos.z > _maxZReachedInCurrentRun && delta.z > 0)
                {
                    int stepGain = (newGridPos.z - _maxZReachedInCurrentRun) * 10;
                    _maxZReachedInCurrentRun = newGridPos.z;
                    UpdateMissionProgress(_currentProgress + stepGain);
                }
            }
        }

        private void HandlePlayerSteppedPlank()
        {
            if (CurrentMission.MissionType == AchievementMissionType.StepPlankCount)
            {
                UpdateMissionProgress(_currentProgress + 1);
            }
        }

        private void HandleInactivityWarning(bool isWarning)
        {
            if (isWarning)
            {
                _wasInactivityWarningActive = true;
            }
            else
            {
                // 경고가 켜졌다가 플레이어가 살아있는 상태로 이동해서 경고가 해제된 경우 -> 탈출 성공!
                if (_wasInactivityWarningActive)
                {
                    _wasInactivityWarningActive = false;
                    if (GridPlayerController.Instance != null && GridPlayerController.Instance.IsAlive)
                    {
                        if (CurrentMission.MissionType == AchievementMissionType.EscapeInactivityWarning)
                        {
                            UpdateMissionProgress(_currentProgress + 1);
                        }
                    }
                }
            }
        }

        private void HandlePlayerDied(string reason, int finalDistance)
        {
            _wasInactivityWarningActive = false;
            // 사망(실패) 시에도 현재 진행도를 안전하게 저장하고 HUD를 계속 표시합니다.
            SaveCurrentState();
            RefreshHUDDisplay();
        }

        private void UpdateMissionProgress(int newProgress)
        {
            AchievementMissionDef mission = CurrentMission;
            int clamped = Mathf.Clamp(newProgress, 0, mission.TargetValue);

            if (clamped == _currentProgress && clamped < mission.TargetValue)
            {
                return;
            }

            _currentProgress = clamped;

            // 목표 달성(클리어) 검사 -> 클리어 시에만 다른 미션으로 리롤!
            if (_currentProgress >= mission.TargetValue)
            {
                CompleteCurrentMissionAndReroll();
            }
            else
            {
                SaveCurrentState();
                RefreshHUDDisplay();
            }
        }

        /// <summary>
        /// 현재 미션을 클리어 처리하고 축하 사운드/배너를 띄운 뒤 즉시 다음 미션으로 자동 리롤합니다.
        /// </summary>
        private void CompleteCurrentMissionAndReroll()
        {
            AchievementMissionDef clearedMission = CurrentMission;
            _totalClearedCount++;

            // 1. 축하 효과음 재생
            AudioManager.Instance?.PlayAchievementClearSound();

            // 2. 새로운 미션으로 즉시 자동 리롤
            RerollMission(false);
            AchievementMissionDef nextMission = CurrentMission;

            // 3. 카드 펄스 & 클리어 토스트 배너 연출
            TriggerCardPulse(new Color(1.0f, 0.84f, 0.18f));
            ShowClearToast(clearedMission, nextMission);
        }

        #endregion

        #region --- 레트로 도트 UI 생성 및 갱신 (자동 부착) ---

        private Font GetPixelFont()
        {
            if (_pixelFont != null) return _pixelFont;

            _pixelFont = Resources.Load<Font>("Fonts/neodgm");
            if (_pixelFont != null) return _pixelFont;

#if UNITY_EDITOR
            _pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/neodgm.ttf");
            if (_pixelFont == null)
            {
                _pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/neodgm.ttf");
            }
            if (_pixelFont != null) return _pixelFont;
#endif

            _pixelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_pixelFont == null)
            {
                _pixelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _pixelFont;
        }

        public void EnsureMissionHUD()
        {
            if (_hudRootObj != null) return;

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Transform existing = canvas.transform.Find("Achievement_HUD_Panel");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            Font font = GetPixelFont();
            if (font != null && font.material != null && font.material.mainTexture != null)
            {
                font.material.mainTexture.filterMode = FilterMode.Point;
            }

            // [1. 좌측 상단 미션 HUD 루트 컨테이너]
            _hudRootObj = new GameObject("Achievement_HUD_Panel");
            _hudRootObj.transform.SetParent(canvas.transform, false);

            // 스플래시 화면이나 설정 팝업보다는 아래, 게임오버 패널보다는 위에 그려져 실패 화면에서도 선명히 보이도록 설정
            Transform splashTr = canvas.transform.Find("SplashScreen_Panel");
            if (splashTr != null)
            {
                _cachedSplashPanel = splashTr.gameObject;
                _hudRootObj.transform.SetSiblingIndex(Mathf.Max(0, splashTr.GetSiblingIndex()));
            }
            else
            {
                _hudRootObj.transform.SetAsLastSibling();
            }

            RectTransform rootRect = _hudRootObj.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f); // 좌상단 고정
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(40f, -36f);
            rootRect.sizeDelta = new Vector2(410f, 128f);

            // 카드 배경
            _cardBgImage = _hudRootObj.AddComponent<Image>();
            _cardBgImage.color = new Color(0.09f, 0.12f, 0.18f, 0.92f);

            _cardOutline = _hudRootObj.AddComponent<Outline>();
            _cardOutline.effectColor = new Color(1.0f, 0.82f, 0.22f, 0.85f);
            _cardOutline.effectDistance = new Vector2(2f, -2f);

            Shadow cardShadow = _hudRootObj.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            cardShadow.effectDistance = new Vector2(4f, -4f);

            // [2. 상단 헤더: ★ 진행 미션 & 누적 클리어 수]
            GameObject headerObj = new GameObject("Header_Badge_Text");
            headerObj.transform.SetParent(_hudRootObj.transform, false);
            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0f, 1f);
            headerRect.anchoredPosition = new Vector2(14f, -10f);
            headerRect.sizeDelta = new Vector2(-115f, 26f);

            _headerBadgeText = headerObj.AddComponent<Text>();
            _headerBadgeText.font = font;
            _headerBadgeText.fontSize = 17;
            _headerBadgeText.fontStyle = FontStyle.Bold;
            _headerBadgeText.color = new Color(1.0f, 0.84f, 0.22f);
            _headerBadgeText.alignment = TextAnchor.MiddleLeft;
            _headerBadgeText.text = "★ 캠퍼스 미션 (클리어: 0)";

            // [3. 우측 상단 수동 리롤 버튼: ↻ 리롤]
            GameObject rerollBtnObj = new GameObject("Reroll_Button");
            rerollBtnObj.transform.SetParent(_hudRootObj.transform, false);
            RectTransform rerollRect = rerollBtnObj.AddComponent<RectTransform>();
            rerollRect.anchorMin = new Vector2(1f, 1f);
            rerollRect.anchorMax = new Vector2(1f, 1f);
            rerollRect.pivot = new Vector2(1f, 1f);
            rerollRect.anchoredPosition = new Vector2(-10f, -8f);
            rerollRect.sizeDelta = new Vector2(88f, 28f);

            Image rerollBg = rerollBtnObj.AddComponent<Image>();
            rerollBg.color = new Color(0.18f, 0.26f, 0.38f, 0.95f);

            Outline rerollOutline = rerollBtnObj.AddComponent<Outline>();
            rerollOutline.effectColor = new Color(0.4f, 0.75f, 1.0f, 0.75f);
            rerollOutline.effectDistance = new Vector2(1f, -1f);

            _rerollButton = rerollBtnObj.AddComponent<Button>();
            ColorBlock cb = _rerollButton.colors;
            cb.highlightedColor = new Color(0.28f, 0.40f, 0.58f, 1f);
            cb.pressedColor = new Color(0.12f, 0.18f, 0.28f, 1f);
            _rerollButton.colors = cb;
            _rerollButton.onClick.AddListener(() => RerollMission(true));

            // 버튼 클릭 후 포커스가 남아 스페이스바 점프 시 리롤 버튼이 눌리는 현상 방지
            Navigation nav = _rerollButton.navigation;
            nav.mode = Navigation.Mode.None;
            _rerollButton.navigation = nav;

            GameObject rerollTextObj = new GameObject("Text");
            rerollTextObj.transform.SetParent(rerollBtnObj.transform, false);
            RectTransform rerollTextRect = rerollTextObj.AddComponent<RectTransform>();
            rerollTextRect.anchorMin = Vector2.zero;
            rerollTextRect.anchorMax = Vector2.one;
            rerollTextRect.sizeDelta = Vector2.zero;

            Text rerollText = rerollTextObj.AddComponent<Text>();
            rerollText.font = font;
            rerollText.fontSize = 15;
            rerollText.fontStyle = FontStyle.Bold;
            rerollText.color = new Color(0.75f, 0.92f, 1.0f);
            rerollText.alignment = TextAnchor.MiddleCenter;
            rerollText.text = "↻ 리롤";

            // [4. 미션 타이틀 텍스트]
            GameObject titleObj = new GameObject("Mission_Title_Text");
            titleObj.transform.SetParent(_hudRootObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(14f, -38f);
            titleRect.sizeDelta = new Vector2(-28f, 28f);

            _missionTitleText = titleObj.AddComponent<Text>();
            _missionTitleText.font = font;
            _missionTitleText.fontSize = 21;
            _missionTitleText.fontStyle = FontStyle.Bold;
            _missionTitleText.color = Color.white;
            _missionTitleText.alignment = TextAnchor.MiddleLeft;

            // [5. 미션 상세 설명 텍스트]
            GameObject descObj = new GameObject("Mission_Desc_Text");
            descObj.transform.SetParent(_hudRootObj.transform, false);
            RectTransform descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 1f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0f, 1f);
            descRect.anchoredPosition = new Vector2(14f, -66f);
            descRect.sizeDelta = new Vector2(-28f, 22f);

            _missionDescText = descObj.AddComponent<Text>();
            _missionDescText.font = font;
            _missionDescText.fontSize = 15;
            _missionDescText.color = new Color(0.78f, 0.84f, 0.92f);
            _missionDescText.alignment = TextAnchor.MiddleLeft;

            // [6. 하단 진행도 바 (Progress Bar)]
            GameObject barBgObj = new GameObject("ProgressBar_Bg");
            barBgObj.transform.SetParent(_hudRootObj.transform, false);
            RectTransform barBgRect = barBgObj.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot = new Vector2(0.5f, 0f);
            barBgRect.anchoredPosition = new Vector2(0f, 12f);
            barBgRect.sizeDelta = new Vector2(-28f, 24f);

            Image barBgImg = barBgObj.AddComponent<Image>();
            barBgImg.color = new Color(0.04f, 0.06f, 0.09f, 0.95f);

            GameObject fillObj = new GameObject("ProgressBar_Fill");
            fillObj.transform.SetParent(barBgObj.transform, false);
            _progressFillRect = fillObj.AddComponent<RectTransform>();
            _progressFillRect.anchorMin = new Vector2(0f, 0f);
            _progressFillRect.anchorMax = new Vector2(0f, 1f);
            _progressFillRect.pivot = new Vector2(0f, 0.5f);
            _progressFillRect.anchoredPosition = Vector2.zero;
            _progressFillRect.sizeDelta = Vector2.zero;

            _progressFillImage = fillObj.AddComponent<Image>();
            _progressFillImage.color = new Color(0.20f, 0.85f, 0.45f, 0.95f);

            GameObject progTextObj = new GameObject("Progress_Value_Text");
            progTextObj.transform.SetParent(barBgObj.transform, false);
            RectTransform progTextRect = progTextObj.AddComponent<RectTransform>();
            progTextRect.anchorMin = Vector2.zero;
            progTextRect.anchorMax = Vector2.one;
            progTextRect.sizeDelta = Vector2.zero;

            _progressValueText = progTextObj.AddComponent<Text>();
            _progressValueText.font = font;
            _progressValueText.fontSize = 15;
            _progressValueText.fontStyle = FontStyle.Bold;
            _progressValueText.color = Color.white;
            _progressValueText.alignment = TextAnchor.MiddleCenter;

            Shadow progTextShadow = progTextObj.AddComponent<Shadow>();
            progTextShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            progTextShadow.effectDistance = new Vector2(1f, -1f);

            // [7. 미션 클리어 & 리롤 알림 토스트 배너 (카드 바로 아래)]
            _clearToastObj = new GameObject("Achievement_Clear_Toast");
            _clearToastObj.transform.SetParent(_hudRootObj.transform, false);
            _clearToastRect = _clearToastObj.AddComponent<RectTransform>();
            _clearToastRect.anchorMin = new Vector2(0f, 0f);
            _clearToastRect.anchorMax = new Vector2(1f, 0f);
            _clearToastRect.pivot = new Vector2(0.5f, 1f);
            _clearToastRect.anchoredPosition = new Vector2(0f, -10f);
            _clearToastRect.sizeDelta = new Vector2(0f, 68f);

            Image toastBg = _clearToastObj.AddComponent<Image>();
            toastBg.color = new Color(0.12f, 0.28f, 0.18f, 0.96f);

            Outline toastOutline = _clearToastObj.AddComponent<Outline>();
            toastOutline.effectColor = new Color(0.35f, 1.0f, 0.55f, 0.95f);
            toastOutline.effectDistance = new Vector2(2f, -2f);

            _clearToastCanvasGroup = _clearToastObj.AddComponent<CanvasGroup>();
            _clearToastCanvasGroup.alpha = 0f;

            GameObject toastTitleObj = new GameObject("Toast_Title");
            toastTitleObj.transform.SetParent(_clearToastObj.transform, false);
            RectTransform toastTitleRect = toastTitleObj.AddComponent<RectTransform>();
            toastTitleRect.anchorMin = new Vector2(0f, 0.5f);
            toastTitleRect.anchorMax = new Vector2(1f, 1f);
            toastTitleRect.offsetMin = new Vector2(12f, 0f);
            toastTitleRect.offsetMax = new Vector2(-12f, -6f);

            _clearToastTitleText = toastTitleObj.AddComponent<Text>();
            _clearToastTitleText.font = font;
            _clearToastTitleText.fontSize = 18;
            _clearToastTitleText.fontStyle = FontStyle.Bold;
            _clearToastTitleText.color = new Color(1.0f, 0.92f, 0.25f);
            _clearToastTitleText.alignment = TextAnchor.MiddleLeft;

            GameObject toastSubObj = new GameObject("Toast_Sub");
            toastSubObj.transform.SetParent(_clearToastObj.transform, false);
            RectTransform toastSubRect = toastSubObj.AddComponent<RectTransform>();
            toastSubRect.anchorMin = new Vector2(0f, 0f);
            toastSubRect.anchorMax = new Vector2(1f, 0.5f);
            toastSubRect.offsetMin = new Vector2(12f, 6f);
            toastSubRect.offsetMax = new Vector2(-12f, 0f);

            _clearToastSubText = toastSubObj.AddComponent<Text>();
            _clearToastSubText.font = font;
            _clearToastSubText.fontSize = 15;
            _clearToastSubText.color = new Color(0.65f, 1.0f, 0.78f);
            _clearToastSubText.alignment = TextAnchor.MiddleLeft;

            _clearToastObj.SetActive(false);
        }

        public void RefreshHUDDisplay()
        {
            if (_hudRootObj == null)
            {
                EnsureMissionHUD();
                if (_hudRootObj == null) return;
            }

            AchievementMissionDef mission = CurrentMission;
            int target = Mathf.Max(1, mission.TargetValue);
            int cur = Mathf.Clamp(_currentProgress, 0, target);
            float ratio = Mathf.Clamp01((float)cur / target);
            int percent = Mathf.RoundToInt(ratio * 100f);

            if (_headerBadgeText != null)
            {
                _headerBadgeText.text = $"★ 캠퍼스 미션 (누적 클리어: {_totalClearedCount}회)";
            }

            if (_missionTitleText != null)
            {
                _missionTitleText.text = mission.Title;
            }

            if (_missionDescText != null)
            {
                _missionDescText.text = mission.Description;
            }

            if (_progressFillRect != null)
            {
                _progressFillRect.anchorMax = new Vector2(ratio, 1f);
            }

            if (_progressValueText != null)
            {
                _progressValueText.text = $"{cur} / {target} {mission.UnitLabel} ({percent}%)";
            }
        }

        private void TriggerCardPulse(Color highlightColor)
        {
            if (_hudRootObj == null || !_hudRootObj.activeInHierarchy) return;

            if (_cardPulseRoutine != null) StopCoroutine(_cardPulseRoutine);
            _cardPulseRoutine = StartCoroutine(CardPulseRoutine(highlightColor));
        }

        private IEnumerator CardPulseRoutine(Color highlightColor)
        {
            RectTransform rect = _hudRootObj.GetComponent<RectTransform>();
            Color baseOutline = new Color(1.0f, 0.82f, 0.22f, 0.85f);

            if (_cardOutline != null) _cardOutline.effectColor = highlightColor;

            float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1.0f + Mathf.Sin(t * Mathf.PI) * 0.06f;
                if (rect != null) rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            if (rect != null) rect.localScale = Vector3.one;
            if (_cardOutline != null) _cardOutline.effectColor = baseOutline;
            _cardPulseRoutine = null;
        }

        private void ShowClearToast(AchievementMissionDef clearedMission, AchievementMissionDef nextMission)
        {
            if (_clearToastObj == null || _hudRootObj == null || !_hudRootObj.activeInHierarchy) return;

            if (_clearToastRoutine != null) StopCoroutine(_clearToastRoutine);
            _clearToastRoutine = StartCoroutine(ClearToastRoutine(clearedMission, nextMission));
        }

        private IEnumerator ClearToastRoutine(AchievementMissionDef clearedMission, AchievementMissionDef nextMission)
        {
            _clearToastObj.SetActive(true);
            _clearToastTitleText.text = $"🎉 클리어! {clearedMission.Title}";
            _clearToastSubText.text = $"➔ 새 미션 리롤: {nextMission.Title}";

            float fadeInTime = 0.18f;
            float elapsed = 0f;
            while (elapsed < fadeInTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInTime);
                _clearToastCanvasGroup.alpha = t;
                _clearToastRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(4f, -10f, t));
                yield return null;
            }

            _clearToastCanvasGroup.alpha = 1f;
            _clearToastRect.anchoredPosition = new Vector2(0f, -10f);

            yield return new WaitForSecondsRealtime(_clearToastDuration);

            float fadeOutTime = 0.25f;
            elapsed = 0f;
            while (elapsed < fadeOutTime)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutTime);
                _clearToastCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            _clearToastCanvasGroup.alpha = 0f;
            _clearToastObj.SetActive(false);
            _clearToastRoutine = null;
        }

        #endregion
    }
}
