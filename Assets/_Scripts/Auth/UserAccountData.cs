using System;
using System.Collections.Generic;

namespace CampusRun.Auth
{
    /// <summary>
    /// 테두리 장식 등급 열거형입니다. (0: 미장착, 1: 1위 금테두리, 2: 2위 은테두리, 3: 3위 동테두리)
    /// </summary>
    public enum BorderTier
    {
        None = 0,
        Gold = 1,
        Silver = 2,
        Bronze = 3
    }

    /// <summary>
    /// 개별 유저의 계정 정보, 영구 귀속 닉네임, 최고 기록 및 영구 보유/장착 테두리 장식 정보를 담는 데이터 모델입니다.
    /// </summary>
    [Serializable]
    public class UserAccountData
    {
        public string userId;
        public string passwordHash;
        public string nickname;
        public int bestScore;
        public string bestGrade;
        public string createdAt;

        // 1, 2, 3위 달성 시 영구 해금되는 테두리 장식 보유 여부 (순위가 밀려나도 영구 유지)
        public bool unlockedGoldBorder;
        public bool unlockedSilverBorder;
        public bool unlockedBronzeBorder;

        // 현재 장착 중인 테두리 장식 (0: 없음, 1: 금, 2: 은, 3: 동)
        public int equippedBorder;

        public UserAccountData() { }

        public UserAccountData(string userId, string passwordHash)
        {
            this.userId = userId;
            this.passwordHash = passwordHash;
            this.nickname = string.Empty;
            this.bestScore = 0;
            this.bestGrade = "F";
            this.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            this.unlockedGoldBorder = false;
            this.unlockedSilverBorder = false;
            this.unlockedBronzeBorder = false;
            this.equippedBorder = (int)BorderTier.None;
        }
    }

    /// <summary>
    /// 전체 유저 계정 목록을 JSON 및 Firebase Realtime Database에 직렬화하기 위한 래퍼 클래스입니다.
    /// </summary>
    [Serializable]
    public class UserAccountDatabase
    {
        public List<UserAccountData> accounts = new List<UserAccountData>();
    }
}
