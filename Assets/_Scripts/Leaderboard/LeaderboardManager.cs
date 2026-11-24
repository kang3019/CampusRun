using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using CampusRun.Core;
using CampusRun.Auth;

namespace CampusRun.Leaderboard
{
    /// <summary>
    /// 인게임 전체 유저 무제한 랭킹 데이터의 저장, 조회, 정렬 및 Firebase Realtime Database 실시간 연동을 관리하는 매니저 클래스입니다.
    /// 기존 TOP 5 제한을 해제하여 모든 유저의 최고 점수가 등록되며, 로그인된 유저는 게임오버 시 자신의 닉네임으로 자동 점수 갱신됩니다.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        private const string PrefsKey = "CampusRun_Leaderboard_JSON";
        private const string FirebaseUrlPrefsKey = "CampusRun_Firebase_URL";
        private const string FirebaseConfigResourcePath = "FirebaseConfig";
        private const string FileName = "CampusRun_Leaderboard.json";

        [Header("--- Firebase Realtime Database 연동 설정 ---")]
        [Tooltip("Firebase Realtime Database 주소 (예: https://campusrun-fc7ce-default-rtdb.asia-southeast1.firebasedatabase.app/)")]
        [SerializeField] private string _firebaseDatabaseUrl = "https://campusrun-fc7ce-default-rtdb.asia-southeast1.firebasedatabase.app/";

        [Tooltip("Firebase 온라인 실시간 동기화 활성화 여부")]
        [SerializeField] private bool _enableFirebaseSync = true;

        [Tooltip("HTTP 요청 타임아웃 시간(초)")]
        [SerializeField] private int _requestTimeoutSeconds = 8;

        private LeaderboardData _data;
        private string _saveFilePath;
        private bool _isSyncing;
        private bool _isFirebaseConnected;

        public bool IsFirebaseConfigured => _enableFirebaseSync && !string.IsNullOrWhiteSpace(_firebaseDatabaseUrl);
        public bool IsFirebaseConnected => _isFirebaseConnected;
        public bool IsSyncing => _isSyncing;
        public string FirebaseDatabaseUrl => _firebaseDatabaseUrl;

        public string SaveFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(_saveFilePath))
                {
                    _saveFilePath = Path.Combine(Application.persistentDataPath, FileName);
                }
                return _saveFilePath;
            }
        }

        public event Action OnLeaderboardUpdated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject obj = new GameObject("[LeaderboardManager]");
                Instance = obj.AddComponent<LeaderboardManager>();
                DontDestroyOnLoad(obj);
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
            DontDestroyOnLoad(gameObject);

            LoadFirebaseConfig();
            LoadLeaderboard();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SubscribeGameEvents();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeGameEvents();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // InGameUIManager가 씬 리로드 시 GameEvents.ClearAllSubscriptions()를 호출하므로 매 씬 로드마다 안전하게 재구독
            SubscribeGameEvents();
        }

        private void SubscribeGameEvents()
        {
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnGameStarted += HandleGameStarted;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnPlayerDied += HandlePlayerDied;
        }

        private void UnsubscribeGameEvents()
        {
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
        }

        private void Start()
        {
            if (IsFirebaseConfigured)
            {
                RefreshFromFirebase();
            }
        }

        private void OnApplicationQuit()
        {
            SaveLeaderboardLocalOnly();
        }

        private void HandleGameStarted()
        {
            if (IsFirebaseConfigured && !_isSyncing)
            {
                RefreshFromFirebase();
            }
        }

        /// <summary>
        /// 플레이어 사망(게임오버) 시 로그인된 유저의 닉네임이 있다면 자동으로 최고 점수를 랭킹에 등록/갱신합니다.
        /// </summary>
        private void HandlePlayerDied(string deathReason, int finalScore)
        {
            if (finalScore <= 0) return;

            var auth = AuthManager.Instance;
            if (auth != null && auth.IsLoggedIn && auth.HasNickname)
            {
                string grade = EvaluateSimpleGrade(finalScore);
                auth.UpdateAccountBestScore(finalScore, grade);
                UpdateOrAddPlayerScore(auth.CurrentNickname, finalScore, grade);
            }
        }

        private static string EvaluateSimpleGrade(int score)
        {
            if (score >= 500) return "A+";
            if (score >= 400) return "A0";
            if (score >= 300) return "B+";
            if (score >= 200) return "B0";
            if (score >= 100) return "C+";
            return "F";
        }

        /// <summary>
        /// Resources/FirebaseConfig.json 또는 PlayerPrefs에서 Firebase Database URL 설정을 로드합니다.
        /// </summary>
        public void LoadFirebaseConfig()
        {
            TextAsset configAsset = Resources.Load<TextAsset>(FirebaseConfigResourcePath);
            if (configAsset != null && !string.IsNullOrWhiteSpace(configAsset.text))
            {
                try
                {
                    FirebaseConfig config = JsonUtility.FromJson<FirebaseConfig>(configAsset.text);
                    if (config != null && !string.IsNullOrWhiteSpace(config.databaseUrl))
                    {
                        _firebaseDatabaseUrl = config.databaseUrl.Trim();
                        _enableFirebaseSync = config.enableFirebase;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LeaderboardManager] FirebaseConfig.json 파싱 실패: {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(_firebaseDatabaseUrl))
            {
                _firebaseDatabaseUrl = _firebaseDatabaseUrl.Trim();
                return;
            }

            if (string.IsNullOrWhiteSpace(_firebaseDatabaseUrl))
            {
                string savedUrl = PlayerPrefs.GetString(FirebaseUrlPrefsKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(savedUrl))
                {
                    _firebaseDatabaseUrl = savedUrl.Trim();
                }
            }
        }

        /// <summary>
        /// 런타임 또는 에디터 도구에서 Firebase Database URL을 설정하고 즉시 동기화를 시도합니다.
        /// </summary>
        public void SetFirebaseDatabaseUrl(string url, bool enableSync = true)
        {
            _firebaseDatabaseUrl = string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();
            _enableFirebaseSync = enableSync;

            PlayerPrefs.SetString(FirebaseUrlPrefsKey, _firebaseDatabaseUrl);
            PlayerPrefs.Save();

            if (IsFirebaseConfigured && Application.isPlaying)
            {
                RefreshFromFirebase();
            }
        }

        /// <summary>
        /// 입력된 Firebase URL을 REST API 엔드포인트({url}/leaderboard.json) 형식으로 정규화합니다.
        /// </summary>
        public static string BuildLeaderboardEndpointUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

            string trimmed = rawUrl.Trim();
            if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            trimmed = trimmed.TrimEnd('/');
            return $"{trimmed}/leaderboard.json";
        }

        /// <summary>
        /// 로컬 JSON 파일 및 PlayerPrefs에서 랭킹 데이터를 로드합니다. 데이터가 없으면 기본 더미 랭킹을 생성합니다.
        /// </summary>
        public void LoadLeaderboard()
        {
            string json = string.Empty;

            try
            {
                if (File.Exists(SaveFilePath))
                {
                    json = File.ReadAllText(SaveFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] 로컬 랭킹 파일 읽기 실패: {ex.Message}");
            }

            if (string.IsNullOrEmpty(json))
            {
                json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            }

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _data = JsonUtility.FromJson<LeaderboardData>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LeaderboardManager] 랭킹 JSON 파싱 실패, 기본값으로 복구합니다: {ex.Message}");
                    _data = null;
                }
            }

            if (_data == null || _data.entries == null || _data.entries.Count == 0)
            {
                _data = LeaderboardData.CreateDefault();
                SaveLeaderboardLocalOnly();
            }
            else
            {
                SortAndDeduplicateEntries(_data.entries);
            }
        }

        /// <summary>
        /// 현재 랭킹 데이터를 로컬 JSON 파일과 PlayerPrefs 양쪽에 영구 저장하고, Firebase가 활성화되어 있으면 서버에도 업로드합니다.
        /// </summary>
        public void SaveLeaderboard()
        {
            SaveLeaderboardLocalOnly();

            if (IsFirebaseConfigured && Application.isPlaying)
            {
                StartCoroutine(UploadToFirebaseCoroutine(_data, null));
            }
        }

        /// <summary>
        /// 로컬(PlayerPrefs + JSON 파일)에만 즉시 저장하고 UI 갱신 이벤트를 발행합니다.
        /// </summary>
        private void SaveLeaderboardLocalOnly()
        {
            if (_data == null) return;
            string json = JsonUtility.ToJson(_data, true);

            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();

            try
            {
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] 로컬 랭킹 파일 저장 실패: {ex.Message}");
            }

            OnLeaderboardUpdated?.Invoke();
        }

        /// <summary>
        /// Firebase Realtime Database에서 최신 전체 유저 랭킹 데이터를 비동기로 불러옵니다.
        /// </summary>
        public void RefreshFromFirebase(Action<bool> onCompleted = null)
        {
            if (!IsFirebaseConfigured)
            {
                _isFirebaseConnected = false;
                onCompleted?.Invoke(false);
                return;
            }

            StartCoroutine(FetchFromFirebaseCoroutine(onCompleted));
        }

        private IEnumerator FetchFromFirebaseCoroutine(Action<bool> onCompleted)
        {
            string endpoint = BuildLeaderboardEndpointUrl(_firebaseDatabaseUrl);
            if (string.IsNullOrEmpty(endpoint))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            _isSyncing = true;

            using (UnityWebRequest req = UnityWebRequest.Get(endpoint))
            {
                req.timeout = _requestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    _isFirebaseConnected = true;
                    string responseText = req.downloadHandler.text;

                    if (string.IsNullOrWhiteSpace(responseText) || responseText.Trim() == "null")
                    {
                        Debug.Log("[LeaderboardManager] Firebase DB가 비어 있어 현재 랭킹 데이터로 초기화합니다.");
                        yield return StartCoroutine(UploadToFirebaseCoroutine(_data, onCompleted));
                        _isSyncing = false;
                        yield break;
                    }

                    try
                    {
                        LeaderboardData remoteData = JsonUtility.FromJson<LeaderboardData>(responseText);
                        if (remoteData != null && remoteData.entries != null && remoteData.entries.Count > 0)
                        {
                            // 로컬 데이터와 원격 데이터를 병합하여 누구의 기록도 유실되지 않도록 처리
                            MergeEntries(remoteData.entries);
                            SaveLeaderboardLocalOnly();
                            _isSyncing = false;
                            onCompleted?.Invoke(true);
                            yield break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[LeaderboardManager] Firebase 응답 JSON 파싱 실패: {ex.Message}");
                    }
                }
                else
                {
                    if (req.responseCode == 404 && _firebaseDatabaseUrl.Contains("campusrun-fc7ce-default-rtdb.firebaseio.com"))
                    {
                        string asiaUrl = "https://campusrun-fc7ce-default-rtdb.asia-southeast1.firebasedatabase.app/";
                        string asiaEndpoint = BuildLeaderboardEndpointUrl(asiaUrl);
                        using (UnityWebRequest asiaReq = UnityWebRequest.Get(asiaEndpoint))
                        {
                            asiaReq.timeout = _requestTimeoutSeconds;
                            yield return asiaReq.SendWebRequest();
                            if (asiaReq.result == UnityWebRequest.Result.Success)
                            {
                                _firebaseDatabaseUrl = asiaUrl;
                                _isFirebaseConnected = true;
                                string asiaText = asiaReq.downloadHandler.text;
                                if (string.IsNullOrWhiteSpace(asiaText) || asiaText.Trim() == "null")
                                {
                                    yield return StartCoroutine(UploadToFirebaseCoroutine(_data, onCompleted));
                                    _isSyncing = false;
                                    yield break;
                                }

                                try
                                {
                                    LeaderboardData remoteData = JsonUtility.FromJson<LeaderboardData>(asiaText);
                                    if (remoteData != null && remoteData.entries != null && remoteData.entries.Count > 0)
                                    {
                                        MergeEntries(remoteData.entries);
                                        SaveLeaderboardLocalOnly();
                                        _isSyncing = false;
                                        onCompleted?.Invoke(true);
                                        yield break;
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    _isFirebaseConnected = false;
                    Debug.LogWarning($"[LeaderboardManager] Firebase 랭킹 조회 실패 (오프라인 로컬 모드로 동작): {req.error}");
                }
            }

            _isSyncing = false;
            onCompleted?.Invoke(false);
        }

        private void MergeEntries(List<LeaderboardEntry> remoteEntries)
        {
            if (remoteEntries == null) return;
            if (_data == null) _data = new LeaderboardData();
            if (_data.entries == null) _data.entries = new List<LeaderboardEntry>();

            // 원격 데이터를 우선 기준으로 병합하여, 다른 유저가 점수는 그대로인 채 테두리 장식(equippedBorder)만 변경해도
            // 로컬 캐시에 덮이지 않고 최신 장착 상태가 실시간 반영되도록 보장합니다.
            Dictionary<string, LeaderboardEntry> merged = new Dictionary<string, LeaderboardEntry>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < remoteEntries.Count; i++)
            {
                LeaderboardEntry remote = remoteEntries[i];
                if (remote == null || string.IsNullOrWhiteSpace(remote.playerName)) continue;
                string key = remote.playerName.Trim();
                if (!merged.TryGetValue(key, out LeaderboardEntry existing) || remote.score > existing.score)
                {
                    merged[key] = remote;
                }
            }

            for (int i = 0; i < _data.entries.Count; i++)
            {
                LeaderboardEntry local = _data.entries[i];
                if (local == null || string.IsNullOrWhiteSpace(local.playerName)) continue;
                string key = local.playerName.Trim();
                if (!merged.TryGetValue(key, out LeaderboardEntry remoteExisting))
                {
                    merged[key] = local;
                }
                else if (local.score > remoteExisting.score)
                {
                    // 로컬 점수가 원격보다 높으면 로컬 점수를 채택하되, 다른 유저의 원격 테두리 정보는 유지
                    merged[key] = local;
                }
            }

            _data.entries.Clear();
            foreach (var kvp in merged)
            {
                _data.entries.Add(kvp.Value);
            }

            SortAndDeduplicateEntries(_data.entries);

            // 현재 로그인한 유저의 장착 테두리 최신 상태 보존 및 1~3위 미해금 테두리 보장 검사
            var auth = AuthManager.Instance;
            if (auth != null && auth.IsLoggedIn && auth.HasNickname)
            {
                LeaderboardEntry myEntry = GetPlayerEntry(auth.CurrentNickname);
                if (myEntry != null)
                {
                    myEntry.equippedBorder = (int)auth.CurrentEquippedBorder;
                }

                int myRank = GetPlayerRank(auth.CurrentNickname);
                if (myRank >= 1 && myRank <= 3)
                {
                    auth.TryUnlockBorderByRank(myRank);
                }
            }
        }

        private IEnumerator SyncAndUploadNewEntryCoroutine(LeaderboardEntry newEntry)
        {
            string endpoint = BuildLeaderboardEndpointUrl(_firebaseDatabaseUrl);
            if (string.IsNullOrEmpty(endpoint)) yield break;

            _isSyncing = true;

            using (UnityWebRequest getReq = UnityWebRequest.Get(endpoint))
            {
                getReq.timeout = _requestTimeoutSeconds;
                yield return getReq.SendWebRequest();

                if (getReq.result == UnityWebRequest.Result.Success)
                {
                    string responseText = getReq.downloadHandler.text;
                    if (!string.IsNullOrWhiteSpace(responseText) && responseText.Trim() != "null")
                    {
                        try
                        {
                            LeaderboardData remoteData = JsonUtility.FromJson<LeaderboardData>(responseText);
                            if (remoteData != null && remoteData.entries != null)
                            {
                                remoteData.entries.Add(newEntry);
                                MergeEntries(remoteData.entries);
                                SaveLeaderboardLocalOnly();
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[LeaderboardManager] 병합 중 원격 JSON 파싱 경고: {ex.Message}");
                        }
                    }
                }
            }

            yield return StartCoroutine(UploadToFirebaseCoroutine(_data, null));
            _isSyncing = false;
        }

        private IEnumerator UploadToFirebaseCoroutine(LeaderboardData dataToUpload, Action<bool> onCompleted)
        {
            if (dataToUpload == null)
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            string endpoint = BuildLeaderboardEndpointUrl(_firebaseDatabaseUrl);
            if (string.IsNullOrEmpty(endpoint))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            string json = JsonUtility.ToJson(dataToUpload);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest putReq = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPUT))
            {
                putReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
                putReq.downloadHandler = new DownloadHandlerBuffer();
                putReq.SetRequestHeader("Content-Type", "application/json");
                putReq.timeout = _requestTimeoutSeconds;

                yield return putReq.SendWebRequest();

                if (putReq.result == UnityWebRequest.Result.Success)
                {
                    _isFirebaseConnected = true;
                    Debug.Log("[LeaderboardManager] Firebase Realtime Database에 전체 유저 랭킹 저장 완료!");
                    OnLeaderboardUpdated?.Invoke();
                    onCompleted?.Invoke(true);
                }
                else
                {
                    _isFirebaseConnected = false;
                    Debug.LogWarning($"[LeaderboardManager] Firebase 랭킹 업로드 실패 (로컬에는 안전하게 저장됨): {putReq.error}");
                    onCompleted?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// 닉네임별로 가장 높은 최고 점수 1개만 남기고(중복 제거), 전체 유저를 점수 내림차순으로 무제한 정렬합니다.
        /// </summary>
        private static void SortAndDeduplicateEntries(List<LeaderboardEntry> entries)
        {
            if (entries == null) return;

            // 먼저 점수 내림차순 정렬
            entries.Sort((a, b) => b.score.CompareTo(a.score));

            // 동일 닉네임 중복 제거 (내림차순 정렬 상태이므로 첫 번째 등장 기록이 최고 점수)
            HashSet<string> seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] == null || string.IsNullOrWhiteSpace(entries[i].playerName))
                {
                    entries.RemoveAt(i);
                    continue;
                }
            }

            List<LeaderboardEntry> uniqueList = new List<LeaderboardEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                string key = entries[i].playerName.Trim();
                if (seenNames.Add(key))
                {
                    uniqueList.Add(entries[i]);
                }
            }

            entries.Clear();
            entries.AddRange(uniqueList);
        }

        /// <summary>
        /// InGameUIManager 호환용 메서드입니다.
        /// 이미 로그인되어 닉네임이 있는 유저는 HandlePlayerDied에서 자동 등록되므로 수동 이름 입력 팝업을 띄우지 않도록 false를 반환합니다.
        /// 비로그인(게스트) 상태일 때만 0점 초과 시 수동 이름 입력 팝업을 허용합니다.
        /// </summary>
        public bool IsTop5(int score)
        {
            if (score <= 0) return false;

            if (AuthManager.Instance != null && AuthManager.Instance.IsLoggedIn && AuthManager.Instance.HasNickname)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 특정 점수가 현재 전체 랭킹에 등록된다면 몇 위가 될지 예측합니다.
        /// </summary>
        public int GetProjectedRank(int score)
        {
            if (score <= 0) return -1;
            if (_data == null || _data.entries == null || _data.entries.Count == 0) return 1;

            for (int i = 0; i < _data.entries.Count; i++)
            {
                if (score > _data.entries[i].score)
                {
                    return i + 1;
                }
            }

            return _data.entries.Count + 1;
        }

        /// <summary>
        /// 특정 유저 닉네임의 현재 순위(1위부터 시작, 미등록 시 -1)를 반환합니다.
        /// </summary>
        public int GetPlayerRank(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName) || _data == null || _data.entries == null) return -1;

            string normalized = playerName.Trim();
            for (int i = 0; i < _data.entries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(_data.entries[i].playerName) &&
                    string.Equals(_data.entries[i].playerName.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }
            return -1;
        }

        /// <summary>
        /// 특정 유저 닉네임의 등록된 최고 기록 항목을 반환합니다 (미등록 시 null).
        /// </summary>
        public LeaderboardEntry GetPlayerEntry(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName) || _data == null || _data.entries == null) return null;

            string normalized = playerName.Trim();
            for (int i = 0; i < _data.entries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(_data.entries[i].playerName) &&
                    string.Equals(_data.entries[i].playerName.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return _data.entries[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 입력한 닉네임이 현재 전체 랭킹에 이미 등록되어 있는지 확인합니다.
        /// </summary>
        public bool IsDuplicateName(string playerName)
        {
            return GetPlayerEntry(playerName) != null;
        }

        /// <summary>
        /// 로그인된 유저의 점수를 자동 등록하거나 기존 최고 기록보다 높을 때 갱신하며,
        /// 1~3위 달성 시 영구 테두리 장식 해금을 검사합니다.
        /// </summary>
        public void UpdateOrAddPlayerScore(string playerName, int score, string grade)
        {
            if (string.IsNullOrWhiteSpace(playerName) || score <= 0) return;

            if (_data == null)
            {
                _data = new LeaderboardData();
            }

            string normalized = playerName.Trim();
            LeaderboardEntry existing = GetPlayerEntry(normalized);

            int currentBorder = 0;
            if (AuthManager.Instance != null &&
                string.Equals(AuthManager.Instance.CurrentNickname, normalized, StringComparison.OrdinalIgnoreCase))
            {
                currentBorder = (int)AuthManager.Instance.CurrentEquippedBorder;
            }

            bool isUpdated = false;
            if (existing != null)
            {
                existing.equippedBorder = currentBorder;
                if (score > existing.score)
                {
                    existing.score = score;
                    existing.grade = string.IsNullOrEmpty(grade) ? "F" : grade;
                    existing.date = DateTime.Now.ToString("MM/dd HH:mm");
                    isUpdated = true;
                }
            }
            else
            {
                LeaderboardEntry newEntry = new LeaderboardEntry(normalized, score, grade, null, currentBorder);
                _data.entries.Add(newEntry);
                existing = newEntry;
                isUpdated = true;
            }

            SortAndDeduplicateEntries(_data.entries);
            SaveLeaderboardLocalOnly();

            // 1, 2, 3위 달성 여부 확인 및 영구 테두리 장식 지급
            int achievedRank = GetPlayerRank(normalized);
            if (achievedRank >= 1 && achievedRank <= 3 &&
                AuthManager.Instance != null &&
                string.Equals(AuthManager.Instance.CurrentNickname, normalized, StringComparison.OrdinalIgnoreCase))
            {
                AuthManager.Instance.TryUnlockBorderByRank(achievedRank);
            }

            if (isUpdated && IsFirebaseConfigured && Application.isPlaying)
            {
                StartCoroutine(SyncAndUploadNewEntryCoroutine(existing));
            }
        }

        /// <summary>
        /// 유저가 프로필 설정 탭에서 테두리 장식을 장착/해제했을 때 랭킹 데이터에도 즉시 반영하고 Firebase에 동기화합니다.
        /// </summary>
        public void UpdatePlayerEquippedBorder(string playerName, int equippedBorder)
        {
            if (string.IsNullOrWhiteSpace(playerName) || _data == null || _data.entries == null) return;

            LeaderboardEntry existing = GetPlayerEntry(playerName);
            if (existing != null && existing.equippedBorder != equippedBorder)
            {
                existing.equippedBorder = equippedBorder;
                SaveLeaderboardLocalOnly();

                if (IsFirebaseConfigured && Application.isPlaying)
                {
                    StartCoroutine(SyncAndUploadBorderCoroutine(playerName.Trim(), equippedBorder));
                }
            }
        }

        private IEnumerator SyncAndUploadBorderCoroutine(string playerName, int equippedBorder)
        {
            string endpoint = BuildLeaderboardEndpointUrl(_firebaseDatabaseUrl);
            if (string.IsNullOrEmpty(endpoint)) yield break;

            _isSyncing = true;

            using (UnityWebRequest getReq = UnityWebRequest.Get(endpoint))
            {
                getReq.timeout = _requestTimeoutSeconds;
                yield return getReq.SendWebRequest();

                if (getReq.result == UnityWebRequest.Result.Success)
                {
                    string responseText = getReq.downloadHandler.text;
                    if (!string.IsNullOrWhiteSpace(responseText) && responseText.Trim() != "null")
                    {
                        try
                        {
                            LeaderboardData remoteData = JsonUtility.FromJson<LeaderboardData>(responseText);
                            if (remoteData != null && remoteData.entries != null)
                            {
                                MergeEntries(remoteData.entries);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[LeaderboardManager] 테두리 동기화 중 원격 JSON 파싱 경고: {ex.Message}");
                        }
                    }
                }
            }

            LeaderboardEntry target = GetPlayerEntry(playerName);
            if (target != null)
            {
                target.equippedBorder = equippedBorder;
                SaveLeaderboardLocalOnly();
            }

            yield return StartCoroutine(UploadToFirebaseCoroutine(_data, null));
            _isSyncing = false;
        }

        /// <summary>
        /// 유저가 프로필 설정 탭에서 닉네임을 변경했을 때 기존 랭킹 항목의 이름도 함께 갱신합니다.
        /// </summary>
        public void RenamePlayerEntry(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName) || _data == null || _data.entries == null) return;

            LeaderboardEntry existing = GetPlayerEntry(oldName);
            if (existing != null)
            {
                existing.playerName = newName.Trim();
                SortAndDeduplicateEntries(_data.entries);
                SaveLeaderboardLocalOnly();

                if (IsFirebaseConfigured && Application.isPlaying)
                {
                    StartCoroutine(UploadToFirebaseCoroutine(_data, null));
                }
            }
        }

        /// <summary>
        /// 새로운 랭킹 기록을 추가하고 내림차순 정렬 후 로컬 및 Firebase에 저장합니다.
        /// </summary>
        public void AddEntry(string playerName, int score, string grade)
        {
            UpdateOrAddPlayerScore(playerName, score, grade);
        }

        /// <summary>
        /// 현재 전체 유저 랭킹 목록을 반환합니다.
        /// </summary>
        public IReadOnlyList<LeaderboardEntry> GetEntries()
        {
            if (_data == null || _data.entries == null)
            {
                LoadLeaderboard();
            }
            return _data.entries.AsReadOnly();
        }

        /// <summary>
        /// 부스 운영자 편의를 위한 랭킹 기본값 리셋 기능입니다.
        /// </summary>
        public void ResetToDefault()
        {
            _data = LeaderboardData.CreateDefault();
            SaveLeaderboard();
            Debug.Log("[LeaderboardManager] 랭킹이 기본 더미 데이터로 리셋되었습니다.");
        }
    }
}
