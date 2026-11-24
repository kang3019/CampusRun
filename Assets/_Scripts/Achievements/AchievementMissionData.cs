using System;

namespace CampusRun.Achievements
{
    /// <summary>
    /// 업적/미션의 달성 조건 유형을 정의하는 열거형입니다.
    /// </summary>
    public enum AchievementMissionType
    {
        /// <summary> 한 판에서 목표 거리(m) 도달 (예: 50m, 100m, 200m, 300m) </summary>
        ReachDistance,

        /// <summary> 뒤로 후퇴(S / ↓)하지 않고 연속으로 목표 거리(m) 전진 </summary>
        NoBackwardDistance,

        /// <summary> 물웅덩이의 통나무(부유판) 위에 N회 착지 성공 </summary>
        StepPlankCount,

        /// <summary> 좌/우(A, D / ←, →) 회피 스텝 N회 이동 </summary>
        SideStepCount,

        /// <summary> 방향에 상관없이 총 점프(홉) N회 이동 </summary>
        TotalHopCount,

        /// <summary> 제자리 지체로 인한 '지각 위기(교수님 접근)' 경고가 뜬 상태에서 전진하여 무사히 탈출 </summary>
        EscapeInactivityWarning
    }

    /// <summary>
    /// 개별 업적 미션의 정의 데이터 구조체입니다.
    /// </summary>
    [Serializable]
    public struct AchievementMissionDef
    {
        public int Id;
        public string Title;
        public string Description;
        public string UnitLabel;
        public AchievementMissionType MissionType;
        public int TargetValue;
        public bool ResetOnNewRun;

        public AchievementMissionDef(
            int id,
            string title,
            string description,
            string unitLabel,
            AchievementMissionType missionType,
            int targetValue,
            bool resetOnNewRun)
        {
            Id = id;
            Title = title;
            Description = description;
            UnitLabel = unitLabel;
            MissionType = missionType;
            TargetValue = targetValue;
            ResetOnNewRun = resetOnNewRun;
        }
    }
}
