using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using CampusRun.Leaderboard;

namespace CampusRun.Editor
{
    /// <summary>
    /// Unity 에디터 상단 메뉴(CampusRun/Firebase 랭킹 연동 설정)에서
    /// Firebase Realtime Database URL을 손쉽게 저장하고 즉시 연결/업로드 테스트를 수행할 수 있는 설정 창입니다.
    /// </summary>
    public class FirebaseLeaderboardSetupWindow : EditorWindow
    {
        private const string ConfigAssetPath = "Assets/Resources/FirebaseConfig.json";
        private const string FirebaseUrlPrefsKey = "CampusRun_Firebase_URL";

        private string _databaseUrl = string.Empty;
        private bool _enableFirebase = true;
        private string _statusMessage = "Firebase Realtime Database URL을 입력하고 [설정 저장]을 눌러주세요.";
        private MessageType _statusType = MessageType.Info;

        [MenuItem("CampusRun/Firebase 랭킹 연동 설정 (URL 입력 및 테스트)")]
        public static void OpenWindow()
        {
            FirebaseLeaderboardSetupWindow window = GetWindow<FirebaseLeaderboardSetupWindow>("Firebase 랭킹 설정");
            window.minSize = new Vector2(520f, 380f);
            window.LoadCurrentConfig();
            window.Show();
        }

        private void OnEnable()
        {
            LoadCurrentConfig();
        }

        private void LoadCurrentConfig()
        {
            if (File.Exists(ConfigAssetPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigAssetPath);
                    FirebaseConfig config = JsonUtility.FromJson<FirebaseConfig>(json);
                    if (config != null)
                    {
                        _databaseUrl = config.databaseUrl ?? string.Empty;
                        _enableFirebase = config.enableFirebase;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FirebaseSetup] FirebaseConfig.json 읽기 실패: {ex.Message}");
                }
            }

            if (string.IsNullOrWhiteSpace(_databaseUrl))
            {
                _databaseUrl = PlayerPrefs.GetString(FirebaseUrlPrefsKey, string.Empty);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("CampusRun - Firebase TOP 5 랭킹 실시간 연동", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            EditorGUILayout.HelpBox(
                "1. Firebase 콘솔(console.firebase.google.com) -> 프로젝트 생성 -> Realtime Database 생성\n" +
                "2. 보안 규칙(Rules)을 테스트 모드(\".read\": true, \".write\": true)로 설정\n" +
                "3. 생성된 데이터베이스 URL(예: https://your-project-default-rtdb.firebaseio.com/)을 아래에 붙여넣으세요.",
                MessageType.None);

            EditorGUILayout.Space(10f);

            _enableFirebase = EditorGUILayout.Toggle("Firebase 연동 활성화", _enableFirebase);
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Realtime Database URL:");
            _databaseUrl = EditorGUILayout.TextField(_databaseUrl);

            string endpointPreview = LeaderboardManager.BuildLeaderboardEndpointUrl(_databaseUrl);
            if (!string.IsNullOrEmpty(endpointPreview))
            {
                EditorGUILayout.LabelField("실제 통신 주소:", endpointPreview, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(12f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1. 설정 저장 (빌드 포함)", GUILayout.Height(34f)))
            {
                SaveConfigToAsset();
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(_databaseUrl);
            if (GUILayout.Button("2. 연결 테스트 및 기본 TOP 5 업로드", GUILayout.Height(34f)))
            {
                SaveConfigToAsset();
                TestAndUploadToFirebase();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);

            GUI.enabled = !string.IsNullOrWhiteSpace(_databaseUrl);
            if (GUILayout.Button("서버(Firebase) TOP 5 기본 더미 데이터로 초기화", GUILayout.Height(28f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Firebase 랭킹 초기화",
                    "Firebase 서버의 TOP 5 랭킹을 기본 캠퍼스 더미 데이터(교수님 350m 등)로 초기화하시겠습니까?",
                    "초기화", "취소"))
                {
                    UploadDefaultDataToFirebase();
                }
            }
            GUI.enabled = true;

            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        private void SaveConfigToAsset()
        {
            _databaseUrl = string.IsNullOrWhiteSpace(_databaseUrl) ? string.Empty : _databaseUrl.Trim();

            FirebaseConfig config = new FirebaseConfig
            {
                databaseUrl = _databaseUrl,
                enableFirebase = _enableFirebase
            };

            string dir = Path.GetDirectoryName(ConfigAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(ConfigAssetPath, json, Encoding.UTF8);

            PlayerPrefs.SetString(FirebaseUrlPrefsKey, _databaseUrl);
            PlayerPrefs.Save();

            if (LeaderboardManager.Instance != null)
            {
                LeaderboardManager.Instance.SetFirebaseDatabaseUrl(_databaseUrl, _enableFirebase);
            }

            AssetDatabase.ImportAsset(ConfigAssetPath);
            AssetDatabase.SaveAssets();

            _statusMessage = string.IsNullOrEmpty(_databaseUrl)
                ? "URL이 비어 있어 오프라인 로컬 랭킹 모드로 저장되었습니다."
                : $"설정 저장 완료! ({ConfigAssetPath})\n이제 에디터 및 .exe 빌드에서 해당 Firebase DB와 자동 연동됩니다.";
            _statusType = MessageType.Info;
            Repaint();
        }

        private void TestAndUploadToFirebase()
        {
            string endpoint = LeaderboardManager.BuildLeaderboardEndpointUrl(_databaseUrl);
            if (string.IsNullOrEmpty(endpoint))
            {
                _statusMessage = "유효한 Firebase Database URL을 입력해주세요.";
                _statusType = MessageType.Error;
                return;
            }

            _statusMessage = "Firebase 서버에 연결 중입니다...";
            _statusType = MessageType.Info;
            Repaint();

            UnityWebRequest getReq = UnityWebRequest.Get(endpoint);
            getReq.timeout = 8;
            var op = getReq.SendWebRequest();

            op.completed += _ =>
            {
                if (getReq.result == UnityWebRequest.Result.Success)
                {
                    string response = getReq.downloadHandler.text;
                    getReq.Dispose();

                    if (string.IsNullOrWhiteSpace(response) || response.Trim() == "null")
                    {
                        UploadDefaultDataToFirebase();
                    }
                    else
                    {
                        _statusMessage = $"[연결 성공!] Firebase 서버에서 랭킹 데이터를 확인했습니다.\n응답 데이터: {response}";
                        _statusType = MessageType.Info;
                        Repaint();
                    }
                }
                else
                {
                    string err = getReq.error;
                    long code = getReq.responseCode;
                    getReq.Dispose();

                    _statusMessage = $"[연결 실패 (HTTP {code})] {err}\nURL 오타 여부 및 Firebase Realtime Database의 보안 규칙(.read/.write: true)을 확인해주세요.";
                    _statusType = MessageType.Error;
                    Repaint();
                }
            };
        }

        private void UploadDefaultDataToFirebase()
        {
            string endpoint = LeaderboardManager.BuildLeaderboardEndpointUrl(_databaseUrl);
            if (string.IsNullOrEmpty(endpoint)) return;

            LeaderboardData defaultData = LeaderboardData.CreateDefault();
            string json = JsonUtility.ToJson(defaultData, true);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            UnityWebRequest putReq = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPUT)
            {
                uploadHandler = new UploadHandlerRaw(bodyRaw),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 8
            };
            putReq.SetRequestHeader("Content-Type", "application/json");

            var op = putReq.SendWebRequest();
            op.completed += _ =>
            {
                if (putReq.result == UnityWebRequest.Result.Success)
                {
                    _statusMessage = "[업로드 성공!] Firebase Realtime Database에 기본 TOP 5 랭킹 데이터가 저장되었습니다.";
                    _statusType = MessageType.Info;
                }
                else
                {
                    _statusMessage = $"[업로드 실패 (HTTP {putReq.responseCode})] {putReq.error}\nFirebase Realtime Database 보안 규칙에서 \".write\": true 설정을 확인해주세요.";
                    _statusType = MessageType.Error;
                }
                putReq.Dispose();
                Repaint();
            };
        }
    }
}
