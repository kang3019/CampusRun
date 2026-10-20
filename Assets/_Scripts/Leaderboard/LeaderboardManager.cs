using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using CampusRun.Core;

namespace CampusRun.Leaderboard
{
    /// <summary>
    /// 인게임 TOP 5 랭킹 데이터의 저장, 조회, 정렬 및 Firebase Realtime Database 실시간 연동을 관리하는 매니저 클래스입니다.
    /// Firebase REST API(UnityWebRequest)와 로컬 JSON 파일(Application.persistentDataPath) + PlayerPrefs 이중 저장을 함께 지원하여
    /// PC 단독 빌드(.exe) 및 오프라인 환경에서도 100% 안전하게 동작합니다.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        private const string PrefsKey = "CampusRun_Leaderboard_JSON";
        private const string FirebaseUrlPrefsKey = "CampusRun_Firebase_URL";
        private const string FirebaseConfigResourcePath = "FirebaseConfig";
        private const string FileName = "CampusRun_Leaderboard.json";
        private const int MaxEntries = 5;

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
            GameEvents.OnGameStarted += HandleGameStarted;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStarted -= HandleGameStarted;
        }

        private void Start()
        {
            // 게임 시작 시 Firebase 서버에서 최신 TOP 5 랭킹 비동기 조회
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
            // 플레이 도중 최신 TOP 5 기준점을 유지하기 위해 백그라운드 갱신
            if (IsFirebaseConfigured && !_isSyncing)
            {
                RefreshFromFirebase();
            }
        }

        /// <summary>
        /// Resources/FirebaseConfig.json 또는 PlayerPrefs에서 Firebase Database URL 설정을 로드합니다.
        /// </summary>
        public void LoadFirebaseConfig()
        {
            // 1순위: Resources/FirebaseConfig.json 로드 (빌드 포함용)
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

            // 2순위: PlayerPrefs에 저장된 런타임/에디터 URL 확인
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

            // 1순위: 로컬 JSON 파일에서 로드
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

            // 2순위: 파일이 없으면 PlayerPrefs에서 로드
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
                SortAndTrimEntries(_data.entries);
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

            // 1. PlayerPrefs 저장
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();

            // 2. 로컬 JSON 파일 영구 저장
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
        /// Firebase Realtime Database에서 최신 TOP 5 랭킹 데이터를 비동기로 불러옵니다.
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

                    // Firebase DB가 비어 있는 경우("null") 현재 로컬 기본 TOP 5 데이터를 최초 업로드
                    if (string.IsNullOrWhiteSpace(responseText) || responseText.Trim() == "null")
                    {
                        Debug.Log("[LeaderboardManager] Firebase DB가 비어 있어 현재 TOP 5 데이터로 초기화합니다.");
                        yield return StartCoroutine(UploadToFirebaseCoroutine(_data, onCompleted));
                        _isSyncing = false;
                        yield break;
                    }

                    try
                    {
                        LeaderboardData remoteData = JsonUtility.FromJson<LeaderboardData>(responseText);
                        if (remoteData != null && remoteData.entries != null && remoteData.entries.Count > 0)
                        {
                            SortAndTrimEntries(remoteData.entries);
                            _data = remoteData;
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
                    // 만약 기본 미국(firebaseio.com) 리전이 404이고 아시아(싱가포르) 리전으로 생성된 경우 자동 폴백 시도
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
                                        SortAndTrimEntries(remoteData.entries);
                                        _data = remoteData;
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

        /// <summary>
        /// 새로운 TOP 5 기록 달성 시 서버 최신 목록과 병합한 뒤 상위 5개를 Firebase에 덮어쓰기(PUT) 업로드합니다.
        /// </summary>
        private IEnumerator SyncAndUploadNewEntryCoroutine(LeaderboardEntry newEntry)
        {
            string endpoint = BuildLeaderboardEndpointUrl(_firebaseDatabaseUrl);
            if (string.IsNullOrEmpty(endpoint)) yield break;

            _isSyncing = true;

            // 1단계: 다른 기기에서 등록된 최신 TOP 5가 있는지 먼저 조회
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
                            if (remoteData != null && remoteData.entries != null && remoteData.entries.Count > 0)
                            {
                                // 원격 데이터에 방금 등록한 기록을 합쳐서 다시 상위 5개 정렬
                                remoteData.entries.Add(newEntry);
                                SortAndTrimEntries(remoteData.entries);
                                _data = remoteData;
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

            // 2단계: 정렬된 최신 TOP 5 데이터를 Firebase에 PUT 저장
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
                    Debug.Log("[LeaderboardManager] Firebase Realtime Database에 TOP 5 랭킹 저장 완료!");
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

        private static void SortAndTrimEntries(List<LeaderboardEntry> entries)
        {
            if (entries == null) return;

            // 점수 내림차순 정렬
            entries.Sort((a, b) => b.score.CompareTo(a.score));

            // 최대 5개 초과분 제거
            if (entries.Count > MaxEntries)
            {
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            }
        }

        /// <summary>
        /// 현재 점수가 TOP 5 안에 진입할 수 있는 자격이 있는지 확인합니다.
        /// </summary>
        public bool IsTop5(int score)
        {
            if (score <= 0) return false;
            if (_data == null || _data.entries == null) return true;

            if (_data.entries.Count < MaxEntries)
            {
                return true;
            }

            // 5위 점수보다 높은 경우 진입 가능
            return score > _data.entries[_data.entries.Count - 1].score;
        }

        /// <summary>
        /// 특정 점수가 현재 등록된다면 몇 위가 될지 예측합니다 (1~5위 반환, 미진입 시 -1).
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

            if (_data.entries.Count < MaxEntries)
            {
                return _data.entries.Count + 1;
            }

            return -1;
        }

        /// <summary>
        /// 새로운 랭킹 기록을 추가하고 내림차순 정렬 후 상위 5개를 로컬 및 Firebase에 저장합니다.
        /// </summary>
        public void AddEntry(string playerName, int score, string grade)
        {
            if (_data == null)
            {
                _data = new LeaderboardData();
            }

            LeaderboardEntry newEntry = new LeaderboardEntry(playerName, score, grade);
            _data.entries.Add(newEntry);

            SortAndTrimEntries(_data.entries);
            SaveLeaderboardLocalOnly();

            if (IsFirebaseConfigured && Application.isPlaying)
            {
                StartCoroutine(SyncAndUploadNewEntryCoroutine(newEntry));
            }
        }

        /// <summary>
        /// 현재 TOP 5 랭킹 목록을 반환합니다.
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
        /// 부스 운영자 편의를 위한 랭킹 기본값 리셋 기능입니다. (Firebase 연동 시 서버 데이터도 함께 초기화)
        /// </summary>
        public void ResetToDefault()
        {
            _data = LeaderboardData.CreateDefault();
            SaveLeaderboard();
            Debug.Log("[LeaderboardManager] 랭킹이 기본 더미 데이터로 리셋되었습니다.");
        }
    }
}
