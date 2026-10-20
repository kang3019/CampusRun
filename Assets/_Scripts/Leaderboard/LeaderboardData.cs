using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusRun.Leaderboard
{
    /// <summary>
    /// 개별 랭킹 기록 항목 데이터 모델입니다.
    /// </summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public string playerName;
        public int score;
        public string grade;
        public string date;

        public LeaderboardEntry() { }

        public LeaderboardEntry(string name, int score, string grade, string date = null)
        {
            this.playerName = string.IsNullOrEmpty(name) ? "익명의 러너" : name;
            this.score = score;
            this.grade = string.IsNullOrEmpty(grade) ? "F" : grade;
            this.date = string.IsNullOrEmpty(date) ? DateTime.Now.ToString("MM/dd HH:mm") : date;
        }
    }

    /// <summary>
    /// TOP 5 랭킹 목록 전체를 직렬화하기 위한 래퍼 클래스입니다.
    /// </summary>
    [Serializable]
    public class LeaderboardData
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

        /// <summary>
        /// 부스 전시 시작 시 기본으로 채워둘 재미있는 캠퍼스 테마 더미 랭킹 데이터입니다.
        /// </summary>
        public static LeaderboardData CreateDefault()
        {
            LeaderboardData data = new LeaderboardData();
            data.entries.Add(new LeaderboardEntry("교수님", 350, "B+", "10/16 09:00"));
            data.entries.Add(new LeaderboardEntry("수석졸업생", 280, "B0", "10/16 09:05"));
            data.entries.Add(new LeaderboardEntry("대학원조교", 210, "B0", "10/16 09:10"));
            data.entries.Add(new LeaderboardEntry("과대표", 150, "C+", "10/16 09:15"));
            data.entries.Add(new LeaderboardEntry("익명의 복학생", 100, "C+", "10/16 09:20"));
            return data;
        }
    }
}
