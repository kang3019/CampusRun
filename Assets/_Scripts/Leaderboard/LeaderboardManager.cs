using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusRun.Leaderboard
{
    /// <summary>
    /// 인게임 로컬 TOP 5 랭킹 데이터의 저장, 조회 및 정렬을 관리하는 매니저 클래스입니다.
    /// PlayerPrefs를 통해 영구 저장되며, 부스 전시 환경에서 재시작 시에도 유지됩니다.
    /// </summary>
    public class LeaderboardManager : MonoBehaviour
    {
        public static LeaderboardManager Instance { get; private set; }

        private const string PrefsKey = "CampusRun_Leaderboard_JSON";
        private const int MaxEntries = 5;

        private LeaderboardData _data;

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

            LoadLeaderboard();
        }

        /// <summary>
        /// PlayerPrefs에서 랭킹 데이터를 로드합니다. 데이터가 없으면 재미있는 기본 더미 랭킹을 생성합니다.
        /// </summary>
        public void LoadLeaderboard()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
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
                SaveLeaderboard();
            }
        }

        /// <summary>
        /// 현재 랭킹 데이터를 PlayerPrefs에 저장합니다.
        /// </summary>
        public void SaveLeaderboard()
        {
            if (_data == null) return;
            string json = JsonUtility.ToJson(_data);
            PlayerPrefs.SetString(PrefsKey, json);
            PlayerPrefs.Save();
            OnLeaderboardUpdated?.Invoke();
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
        /// 새로운 랭킹 기록을 추가하고 내림차순 정렬 후 상위 5개를 보존합니다.
        /// </summary>
        public void AddEntry(string playerName, int score, string grade)
        {
            if (_data == null)
            {
                _data = new LeaderboardData();
            }

            LeaderboardEntry newEntry = new LeaderboardEntry(playerName, score, grade);
            _data.entries.Add(newEntry);

            // 점수 내림차순 정렬
            _data.entries.Sort((a, b) => b.score.CompareTo(a.score));

            // 최대 5개 초과분 제거
            if (_data.entries.Count > MaxEntries)
            {
                _data.entries.RemoveRange(MaxEntries, _data.entries.Count - MaxEntries);
            }

            SaveLeaderboard();
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
