using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using CampusRun.Leaderboard;

namespace CampusRun.Auth
{
    /// <summary>
    /// 회원가입, 로그인, 최초 1회 영구 닉네임 설정 및 Firebase Realtime Database 계정 동기화를 전담하는 매니저입니다.
    /// 로컬 JSON 파일 + PlayerPrefs + Firebase REST API 3중 저장을 통해 온라인/오프라인 모두 완벽히 동작합니다.
    /// </summary>
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        private const string AccountsPrefsKey = "CampusRun_Accounts_JSON";
        private const string LastLoggedInUserKey = "CampusRun_LastUserId";
        private const string HighScorePrefsKey = "CampusRun_HighScore";
        private const string FirebaseConfigResourcePath = "FirebaseConfig";
        private const string FileName = "CampusRun_Accounts.json";

        [Header("--- Firebase Realtime Database 연동 설정 ---")]
        [SerializeField] private string _firebaseDatabaseUrl = "https://campusrun-fc7ce-default-rtdb.asia-southeast1.firebasedatabase.app/";
        [SerializeField] private bool _enableFirebaseSync = true;
        [SerializeField] private int _requestTimeoutSeconds = 6;

        private UserAccountDatabase _db = new UserAccountDatabase();
        private UserAccountData _currentAccount;
        private string _saveFilePath;
        private bool _isBusy;

        public UserAccountData CurrentAccount => _currentAccount;
        public bool IsLoggedIn => _currentAccount != null;
        public bool HasNickname => _currentAccount != null && !string.IsNullOrWhiteSpace(_currentAccount.nickname);
        public string CurrentNickname => HasNickname ? _currentAccount.nickname : string.Empty;
        public string CurrentUserId => _currentAccount != null ? _currentAccount.userId : string.Empty;
        public BorderTier CurrentEquippedBorder => _currentAccount != null ? (BorderTier)_currentAccount.equippedBorder : BorderTier.None;
        public bool IsBusy => _isBusy;
        public bool IsFirebaseConfigured => _enableFirebaseSync && !string.IsNullOrWhiteSpace(_firebaseDatabaseUrl);

        public event Action OnAuthStateChanged;
        public event Action<BorderTier> OnNewBorderUnlocked;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                GameObject obj = new GameObject("[AuthManager]");
                Instance = obj.AddComponent<AuthManager>();
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
            LoadAccountsLocal();
        }

        private void Start()
        {
            if (IsFirebaseConfigured)
            {
                StartCoroutine(FetchAndMergeAccountsFromFirebaseCoroutine(null));
            }
        }

        private void LoadFirebaseConfig()
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
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AuthManager] FirebaseConfig.json 파싱 실패: {ex.Message}");
                }
            }
        }

        public static string BuildAccountsEndpointUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

            string trimmed = rawUrl.Trim();
            if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                int lastSlash = trimmed.LastIndexOf('/');
                if (lastSlash > 8)
                {
                    trimmed = trimmed.Substring(0, lastSlash);
                }
            }

            trimmed = trimmed.TrimEnd('/');
            return $"{trimmed}/accounts.json";
        }

        /// <summary>
        /// 로컬 파일 및 PlayerPrefs에서 계정 목록을 불러옵니다.
        /// </summary>
        public void LoadAccountsLocal()
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
                Debug.LogWarning($"[AuthManager] 로컬 계정 파일 읽기 실패: {ex.Message}");
            }

            if (string.IsNullOrEmpty(json))
            {
                json = PlayerPrefs.GetString(AccountsPrefsKey, string.Empty);
            }

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _db = JsonUtility.FromJson<UserAccountDatabase>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AuthManager] 계정 JSON 파싱 실패: {ex.Message}");
                    _db = new UserAccountDatabase();
                }
            }

            if (_db == null || _db.accounts == null)
            {
                _db = new UserAccountDatabase();
            }
        }

        /// <summary>
        /// 로컬 파일과 PlayerPrefs에 계정 목록을 즉시 저장합니다.
        /// </summary>
        private void SaveAccountsLocalOnly()
        {
            if (_db == null) return;

            string json = JsonUtility.ToJson(_db, true);
            PlayerPrefs.SetString(AccountsPrefsKey, json);
            PlayerPrefs.Save();

            try
            {
                File.WriteAllText(SaveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AuthManager] 로컬 계정 파일 저장 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// SHA256 해시를 사용해 비밀번호를 단방향 암호화합니다.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes("CampusRun_Salt_" + password);
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 회원가입을 수행합니다. (Firebase 동기화 후 아이디 중복 검사 및 계정 생성)
        /// </summary>
        public void Register(string rawUserId, string rawPassword, Action<bool, string> onCompleted)
        {
            if (_isBusy) return;

            string userId = rawUserId != null ? rawUserId.Trim() : string.Empty;
            string password = rawPassword != null ? rawPassword.Trim() : string.Empty;

            if (string.IsNullOrEmpty(userId))
            {
                onCompleted?.Invoke(false, "아이디를 입력해 주세요.");
                return;
            }

            if (userId.Length < 2 || userId.Length > 16)
            {
                onCompleted?.Invoke(false, "아이디는 2~16자 이내로 입력해 주세요.");
                return;
            }

            if (userId.Contains(" "))
            {
                onCompleted?.Invoke(false, "아이디에는 공백을 포함할 수 없습니다.");
                return;
            }

            if (string.IsNullOrEmpty(password) || password.Length < 4)
            {
                onCompleted?.Invoke(false, "비밀번호는 4자 이상 입력해 주세요.");
                return;
            }

            StartCoroutine(RegisterProcessCoroutine(userId, password, onCompleted));
        }

        private IEnumerator RegisterProcessCoroutine(string userId, string password, Action<bool, string> onCompleted)
        {
            _isBusy = true;

            if (IsFirebaseConfigured)
            {
                yield return StartCoroutine(FetchAndMergeAccountsFromFirebaseCoroutine(null));
            }

            if (FindAccountByUserId(userId) != null)
            {
                _isBusy = false;
                onCompleted?.Invoke(false, $"'{userId}'은(는) 이미 존재하는 아이디입니다!");
                yield break;
            }

            string passwordHash = HashPassword(password);
            UserAccountData newAccount = new UserAccountData(userId, passwordHash);
            _db.accounts.Add(newAccount);
            SaveAccountsLocalOnly();

            if (IsFirebaseConfigured)
            {
                yield return StartCoroutine(UploadAccountsToFirebaseCoroutine(null));
            }

            _isBusy = false;
            PlayerPrefs.SetString(LastLoggedInUserKey, userId);
            PlayerPrefs.Save();

            onCompleted?.Invoke(true, "회원가입이 완료되었습니다! 이제 로그인해 주세요.");
        }

        /// <summary>
        /// 로그인을 수행합니다. 성공 시 계정에 닉네임이 없으면 최초 1회 닉네임 설정 단계로 이어집니다.
        /// </summary>
        public void Login(string rawUserId, string rawPassword, Action<bool, string> onCompleted)
        {
            if (_isBusy) return;

            string userId = rawUserId != null ? rawUserId.Trim() : string.Empty;
            string password = rawPassword != null ? rawPassword.Trim() : string.Empty;

            if (string.IsNullOrEmpty(userId))
            {
                onCompleted?.Invoke(false, "아이디를 입력해 주세요.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                onCompleted?.Invoke(false, "비밀번호를 입력해 주세요.");
                return;
            }

            StartCoroutine(LoginProcessCoroutine(userId, password, onCompleted));
        }

        private IEnumerator LoginProcessCoroutine(string userId, string password, Action<bool, string> onCompleted)
        {
            _isBusy = true;

            if (IsFirebaseConfigured)
            {
                yield return StartCoroutine(FetchAndMergeAccountsFromFirebaseCoroutine(null));
            }

            UserAccountData account = FindAccountByUserId(userId);
            if (account == null)
            {
                _isBusy = false;
                onCompleted?.Invoke(false, "존재하지 않는 아이디입니다. 먼저 회원가입을 진행해 주세요.");
                yield break;
            }

            string inputHash = HashPassword(password);
            if (!string.Equals(account.passwordHash, inputHash, StringComparison.OrdinalIgnoreCase))
            {
                _isBusy = false;
                onCompleted?.Invoke(false, "비밀번호가 일치하지 않습니다.");
                yield break;
            }

            _currentAccount = account;
            PlayerPrefs.SetString(LastLoggedInUserKey, account.userId);

            // 이 계정의 최고 기록을 로컬 HighScore와 동기화
            int rankToUnlockAfterLogin = 0;
            if (!string.IsNullOrWhiteSpace(account.nickname) && LeaderboardManager.Instance != null)
            {
                LeaderboardEntry lbEntry = LeaderboardManager.Instance.GetPlayerEntry(account.nickname);
                if (lbEntry != null && lbEntry.score > account.bestScore)
                {
                    account.bestScore = lbEntry.score;
                    account.bestGrade = lbEntry.grade;
                }

                // 현재 리더보드 순위가 1, 2, 3위라면 로그인 직후 테두리 해금 검사 수행
                int currentRank = LeaderboardManager.Instance.GetPlayerRank(account.nickname);
                if (currentRank >= 1 && currentRank <= 3)
                {
                    rankToUnlockAfterLogin = currentRank;
                }

                SaveAccountsLocalOnly();
            }

            PlayerPrefs.SetInt(HighScorePrefsKey, account.bestScore);
            PlayerPrefs.Save();

            _isBusy = false;
            OnAuthStateChanged?.Invoke();
            onCompleted?.Invoke(true, "로그인 성공!");

            if (rankToUnlockAfterLogin >= 1 && rankToUnlockAfterLogin <= 3)
            {
                TryUnlockBorderByRank(rankToUnlockAfterLogin);
            }
        }

        /// <summary>
        /// 회원가입 후 첫 로그인 시 최초 1회 닉네임을 설정하고 계정에 영구 귀속시킵니다. (중복 검사 포함)
        /// </summary>
        public void SetInitialNickname(string rawNickname, Action<bool, string> onCompleted)
        {
            if (_isBusy) return;

            if (_currentAccount == null)
            {
                onCompleted?.Invoke(false, "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.");
                return;
            }

            // 이미 닉네임이 귀속된 계정이면 변경 불가 (최초 1회만 허용)
            if (!string.IsNullOrWhiteSpace(_currentAccount.nickname))
            {
                onCompleted?.Invoke(true, "이미 닉네임이 등록되어 있습니다.");
                return;
            }

            string nickname = rawNickname != null ? rawNickname.Trim() : string.Empty;
            if (string.IsNullOrEmpty(nickname))
            {
                onCompleted?.Invoke(false, "사용할 닉네임을 입력해 주세요.");
                return;
            }

            if (nickname.Length > 8)
            {
                nickname = nickname.Substring(0, 8);
            }

            if (nickname.Length < 2)
            {
                onCompleted?.Invoke(false, "닉네임은 2~8자 이내로 입력해 주세요.");
                return;
            }

            StartCoroutine(SetNicknameProcessCoroutine(nickname, onCompleted));
        }

        private IEnumerator SetNicknameProcessCoroutine(string nickname, Action<bool, string> onCompleted)
        {
            _isBusy = true;

            // 서버의 최신 계정 및 리더보드 데이터를 먼저 동기화하여 정확한 중복 검사 보장
            if (IsFirebaseConfigured)
            {
                yield return StartCoroutine(FetchAndMergeAccountsFromFirebaseCoroutine(null));
                if (LeaderboardManager.Instance != null && LeaderboardManager.Instance.IsFirebaseConfigured)
                {
                    bool lbDone = false;
                    LeaderboardManager.Instance.RefreshFromFirebase(_ => lbDone = true);
                    float wait = 0f;
                    while (!lbDone && wait < 3f)
                    {
                        wait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }

            if (IsDuplicateNickname(nickname))
            {
                _isBusy = false;
                onCompleted?.Invoke(false, $"'{nickname}'은(는) 이미 등록된 닉네임입니다! 다른 닉네임을 입력해 주세요.");
                yield break;
            }

            // 현재 계정에 영구 귀속
            _currentAccount.nickname = nickname;
            UserAccountData dbAccount = FindAccountByUserId(_currentAccount.userId);
            if (dbAccount != null)
            {
                dbAccount.nickname = nickname;
            }

            SaveAccountsLocalOnly();

            if (IsFirebaseConfigured)
            {
                yield return StartCoroutine(UploadAccountsToFirebaseCoroutine(null));
            }

            _isBusy = false;
            OnAuthStateChanged?.Invoke();
            onCompleted?.Invoke(true, $"닉네임 '{nickname}' 등록 완료!");
        }

        /// <summary>
        /// 프로필 설정 탭에서 유저 이름(닉네임)을 변경합니다. (중복 검사 및 랭킹 항목 이름 동기화 포함)
        /// </summary>
        public void ChangeNickname(string rawNewNickname, Action<bool, string> onCompleted)
        {
            if (_isBusy) return;

            if (_currentAccount == null)
            {
                onCompleted?.Invoke(false, "로그인 상태에서만 닉네임을 변경할 수 있습니다.");
                return;
            }

            string newNickname = rawNewNickname != null ? rawNewNickname.Trim() : string.Empty;
            if (string.IsNullOrEmpty(newNickname))
            {
                onCompleted?.Invoke(false, "새 닉네임을 입력해 주세요.");
                return;
            }

            if (newNickname.Length > 8)
            {
                newNickname = newNickname.Substring(0, 8);
            }

            if (newNickname.Length < 2)
            {
                onCompleted?.Invoke(false, "닉네임은 2~8자 이내로 입력해 주세요.");
                return;
            }

            string oldNickname = _currentAccount.nickname ?? string.Empty;
            if (string.Equals(oldNickname.Trim(), newNickname, StringComparison.OrdinalIgnoreCase))
            {
                onCompleted?.Invoke(false, "현재 사용 중인 닉네임과 동일합니다.");
                return;
            }

            StartCoroutine(ChangeNicknameProcessCoroutine(oldNickname, newNickname, onCompleted));
        }

        private IEnumerator ChangeNicknameProcessCoroutine(string oldNickname, string newNickname, Action<bool, string> onCompleted)
        {
            _isBusy = true;

            if (IsFirebaseConfigured)
            {
                yield return StartCoroutine(FetchAndMergeAccountsFromFirebaseCoroutine(null));
                if (LeaderboardManager.Instance != null && LeaderboardManager.Instance.IsFirebaseConfigured)
                {
                    bool lbDone = false;
                    LeaderboardManager.Instance.RefreshFromFirebase(_ => lbDone = true);
                    float wait = 0f;
                    while (!lbDone && wait < 3f)
                    {
                        wait += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
            }

            if (IsDuplicateNickname(newNickname))
            {
                _isBusy = false;
                onCompleted?.Invoke(false, $"'{newNickname}'은(는) 이미 사용 중인 닉네임입니다!");
                yield break;
            }

            _currentAccount.nickname = newNickname;
            UserAccountData dbAccount = FindAccountByUserId(_currentAccount.userId);
            if (dbAccount != null)
            {
                dbAccount.nickname = newNickname;
            }

            SaveAccountsLocalOnly();

            if (LeaderboardManager.Instance != null && !string.IsNullOrWhiteSpace(oldNickname))
            {
                LeaderboardManager.Instance.RenamePlayerEntry(oldNickname, newNickname);
            }

            if (IsFirebaseConfigured)
            {
                yield return StartCoroutine(UploadAccountsToFirebaseCoroutine(null));
            }

            _isBusy = false;
            OnAuthStateChanged?.Invoke();
            onCompleted?.Invoke(true, $"닉네임이 '{newNickname}'(으)로 변경되었습니다!");
        }

        /// <summary>
        /// 입력한 닉네임이 전체 유저 계정 또는 랭킹 보드에 이미 존재하는지 검사합니다.
        /// </summary>
        public bool IsDuplicateNickname(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname)) return false;
            string normalized = nickname.Trim();

            // 1. 전체 가입 계정의 닉네임 중복 확인
            if (_db != null && _db.accounts != null)
            {
                for (int i = 0; i < _db.accounts.Count; i++)
                {
                    string existing = _db.accounts[i].nickname;
                    if (!string.IsNullOrWhiteSpace(existing) &&
                        string.Equals(existing.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // 2. 전체 랭킹 목록의 닉네임 중복 확인
            if (LeaderboardManager.Instance != null && LeaderboardManager.Instance.IsDuplicateName(normalized))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 로그인된 유저가 특정 테두리 장식(금/은/동)을 영구 보유하고 있는지 여부를 반환합니다.
        /// </summary>
        public bool IsBorderUnlocked(BorderTier tier)
        {
            if (_currentAccount == null) return false;
            switch (tier)
            {
                case BorderTier.Gold: return _currentAccount.unlockedGoldBorder;
                case BorderTier.Silver: return _currentAccount.unlockedSilverBorder;
                case BorderTier.Bronze: return _currentAccount.unlockedBronzeBorder;
                default: return true;
            }
        }

        /// <summary>
        /// 달성한 순위(1, 2, 3위)에 해당하는 테두리 장식을 영구 해금합니다.
        /// 최초로 해금된 경우 OnNewBorderUnlocked 이벤트를 발행하여 축하 획득 팝업을 띄웁니다.
        /// </summary>
        public bool TryUnlockBorderByRank(int rank)
        {
            if (_currentAccount == null) return false;

            BorderTier unlockedTier = BorderTier.None;
            UserAccountData dbAccount = FindAccountByUserId(_currentAccount.userId);

            if (rank == 1 && !_currentAccount.unlockedGoldBorder)
            {
                _currentAccount.unlockedGoldBorder = true;
                if (dbAccount != null) dbAccount.unlockedGoldBorder = true;
                unlockedTier = BorderTier.Gold;
            }
            else if (rank == 2 && !_currentAccount.unlockedSilverBorder)
            {
                _currentAccount.unlockedSilverBorder = true;
                if (dbAccount != null) dbAccount.unlockedSilverBorder = true;
                unlockedTier = BorderTier.Silver;
            }
            else if (rank == 3 && !_currentAccount.unlockedBronzeBorder)
            {
                _currentAccount.unlockedBronzeBorder = true;
                if (dbAccount != null) dbAccount.unlockedBronzeBorder = true;
                unlockedTier = BorderTier.Bronze;
            }

            if (unlockedTier != BorderTier.None)
            {
                SaveAccountsLocalOnly();
                if (IsFirebaseConfigured && Application.isPlaying)
                {
                    StartCoroutine(UploadAccountsToFirebaseCoroutine(null));
                }

                OnAuthStateChanged?.Invoke();
                OnNewBorderUnlocked?.Invoke(unlockedTier);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 보유한 테두리 장식을 장착하거나 장착 해제(BorderTier.None)합니다.
        /// </summary>
        public void SetEquippedBorder(BorderTier tier)
        {
            if (_currentAccount == null) return;
            if (tier != BorderTier.None && !IsBorderUnlocked(tier)) return;

            _currentAccount.equippedBorder = (int)tier;
            UserAccountData dbAccount = FindAccountByUserId(_currentAccount.userId);
            if (dbAccount != null)
            {
                dbAccount.equippedBorder = (int)tier;
            }

            SaveAccountsLocalOnly();

            if (!string.IsNullOrWhiteSpace(_currentAccount.nickname) && LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.UpdatePlayerEquippedBorder(_currentAccount.nickname, (int)tier);
            }

            if (IsFirebaseConfigured && Application.isPlaying)
            {
                StartCoroutine(UploadAccountsToFirebaseCoroutine(null));
            }

            OnAuthStateChanged?.Invoke();
        }

        /// <summary>
        /// 로그인된 유저가 게임을 마쳤을 때 계정 최고 기록을 갱신합니다.
        /// </summary>
        public void UpdateAccountBestScore(int score, string grade)
        {
            if (_currentAccount == null || score <= 0) return;

            if (score > _currentAccount.bestScore)
            {
                _currentAccount.bestScore = score;
                _currentAccount.bestGrade = string.IsNullOrEmpty(grade) ? "F" : grade;

                UserAccountData dbAccount = FindAccountByUserId(_currentAccount.userId);
                if (dbAccount != null)
                {
                    dbAccount.bestScore = _currentAccount.bestScore;
                    dbAccount.bestGrade = _currentAccount.bestGrade;
                }

                PlayerPrefs.SetInt(HighScorePrefsKey, _currentAccount.bestScore);
                SaveAccountsLocalOnly();

                if (IsFirebaseConfigured && Application.isPlaying)
                {
                    StartCoroutine(UploadAccountsToFirebaseCoroutine(null));
                }
            }
        }

        /// <summary>
        /// 현재 계정에서 로그아웃합니다.
        /// </summary>
        public void Logout()
        {
            _currentAccount = null;
            OnAuthStateChanged?.Invoke();
        }

        public string GetLastLoggedInUserId()
        {
            return PlayerPrefs.GetString(LastLoggedInUserKey, string.Empty);
        }

        private UserAccountData FindAccountByUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || _db == null || _db.accounts == null) return null;

            string normalized = userId.Trim();
            for (int i = 0; i < _db.accounts.Count; i++)
            {
                if (string.Equals(_db.accounts[i].userId, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return _db.accounts[i];
                }
            }
            return null;
        }

        private IEnumerator FetchAndMergeAccountsFromFirebaseCoroutine(Action<bool> onCompleted)
        {
            string endpoint = BuildAccountsEndpointUrl(_firebaseDatabaseUrl);
            if (string.IsNullOrEmpty(endpoint))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            using (UnityWebRequest req = UnityWebRequest.Get(endpoint))
            {
                req.timeout = _requestTimeoutSeconds;
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string responseText = req.downloadHandler.text;
                    if (!string.IsNullOrWhiteSpace(responseText) && responseText.Trim() != "null")
                    {
                        try
                        {
                            UserAccountDatabase remoteDb = JsonUtility.FromJson<UserAccountDatabase>(responseText);
                            if (remoteDb != null && remoteDb.accounts != null)
                            {
                                MergeAccounts(remoteDb.accounts);
                                SaveAccountsLocalOnly();
                                onCompleted?.Invoke(true);
                                yield break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AuthManager] 원격 계정 JSON 파싱 경고: {ex.Message}");
                        }
                    }
                    else if (_db != null && _db.accounts != null && _db.accounts.Count > 0)
                    {
                        // 원격이 비어있고 로컬에 계정이 있으면 초기 업로드
                        yield return StartCoroutine(UploadAccountsToFirebaseCoroutine(null));
                    }
                    onCompleted?.Invoke(true);
                    yield break;
                }
            }

            onCompleted?.Invoke(false);
        }

        private void MergeAccounts(List<UserAccountData> remoteAccounts)
        {
            if (remoteAccounts == null) return;
            if (_db == null) _db = new UserAccountDatabase();
            if (_db.accounts == null) _db.accounts = new List<UserAccountData>();

            for (int i = 0; i < remoteAccounts.Count; i++)
            {
                UserAccountData remote = remoteAccounts[i];
                if (remote == null || string.IsNullOrWhiteSpace(remote.userId)) continue;

                UserAccountData local = FindAccountByUserId(remote.userId);
                if (local == null)
                {
                    _db.accounts.Add(remote);
                }
                else
                {
                    // 닉네임이 원격에 설정되어 있으면 보존
                    if (string.IsNullOrWhiteSpace(local.nickname) && !string.IsNullOrWhiteSpace(remote.nickname))
                    {
                        local.nickname = remote.nickname;
                    }
                    // 최고 점수 높은 쪽 반영
                    if (remote.bestScore > local.bestScore)
                    {
                        local.bestScore = remote.bestScore;
                        local.bestGrade = remote.bestGrade;
                    }
                    // 한번 해금한 테두리는 OR 연산으로 영구 보존
                    local.unlockedGoldBorder |= remote.unlockedGoldBorder;
                    local.unlockedSilverBorder |= remote.unlockedSilverBorder;
                    local.unlockedBronzeBorder |= remote.unlockedBronzeBorder;

                    if (local.equippedBorder == 0 && remote.equippedBorder != 0)
                    {
                        local.equippedBorder = remote.equippedBorder;
                    }
                }
            }

            // 현재 로그인된 계정 참조 최신화
            if (_currentAccount != null)
            {
                UserAccountData updated = FindAccountByUserId(_currentAccount.userId);
                if (updated != null)
                {
                    _currentAccount = updated;
                }
            }
        }

        private IEnumerator UploadAccountsToFirebaseCoroutine(Action<bool> onCompleted)
        {
            string endpoint = BuildAccountsEndpointUrl(_firebaseDatabaseUrl);
            if (string.IsNullOrEmpty(endpoint) || _db == null)
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            string json = JsonUtility.ToJson(_db);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest putReq = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPUT))
            {
                putReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
                putReq.downloadHandler = new DownloadHandlerBuffer();
                putReq.SetRequestHeader("Content-Type", "application/json");
                putReq.timeout = _requestTimeoutSeconds;

                yield return putReq.SendWebRequest();

                bool success = putReq.result == UnityWebRequest.Result.Success;
                onCompleted?.Invoke(success);
            }
        }
    }
}
