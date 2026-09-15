using System;
using UnityEngine;

namespace CampusRun.Core
{
    /// <summary>
    /// 게임 진행 상태를 나타내는 열거형입니다.
    /// </summary>
    public enum GameState
    {
        Ready,       // 게임 시작 전 (타이틀/랭킹 조회)
        Playing,     // 달리는 중 (인게임)
        Paused,      // 일시정지
        GameOver,    // 장애물 충돌 또는 시간초과 탈락
        GameClear    // 4단계 A+ 달성 및 종강 성공
    }

    /// <summary>
    /// 대학생 4단계 생존 스토리 스테이지 열거형입니다.
    /// </summary>
    public enum StageLevel
    {
        Stage1_Commute = 1,     // 1교시 등교길 (셔틀버스, 언덕길)
        Stage2_Lecture = 2,     // 전공 수업의 늪 (기습 출석 레이저, 과제 폭탄)
        Stage3_TeamProject = 3, // 조별과제 잔혹사 (프리라이더 추격)
        Stage4_Graduation = 4   // 학점 사수 & 종강 (유도 F학점 미사일)
    }

    /// <summary>
    /// 시스템 간의 직접 참조를 제거하고 느슨한 결합(Loose Coupling)을 제공하는 글로벌 이벤트 허브입니다.
    /// 플레이어, 레인 스포너, UI HUD, 사운드 매니저가 이 이벤트를 발행(Trigger)하고 구독(Subscribe)합니다.
    /// </summary>
    public static class GameEvents
    {
        #region 1. 게임 라이프사이클 이벤트 (Game Lifecycle)
        
        /// <summary> 게임 상태(Ready, Playing, Paused 등)가 변경되었을 때 발생합니다. </summary>
        public static event Action<GameState> OnGameStateChanged;

        /// <summary> 게임이 새로 시작(Ready -> Playing)되었을 때 발생합니다. </summary>
        public static event Action OnGameStarted;

        /// <summary> 게임오버 후 재시작(Retry) 버튼을 눌렀을 때 발생합니다. </summary>
        public static event Action OnGameRestarted;

        public static void TriggerGameStateChanged(GameState newState) => OnGameStateChanged?.Invoke(newState);
        public static void TriggerGameStarted() => OnGameStarted?.Invoke();
        public static void TriggerGameRestarted() => OnGameRestarted?.Invoke();

        #endregion

        #region 2. 플레이어 이동 및 생존 이벤트 (Player Domain)

        /// <summary> 플레이어가 앞으로 1칸 전진(Hop)했을 때 발생합니다. (매개변수: 전진한 Z 좌표 / 누적 미터) </summary>
        public static event Action<int> OnPlayerHopped;

        /// <summary> 플레이어의 이산 그리드 좌표가 변경되었을 때 발생합니다. (X, Y, Z) </summary>
        public static event Action<Vector3Int> OnPlayerGridMoved;

        /// <summary> 플레이어가 장애물에 충돌하거나 타임아웃으로 사망했을 때 발생합니다. (사망 원인 문자열, 최종 도달 거리) </summary>
        public static event Action<string, int> OnPlayerDied;

        public static void TriggerPlayerHopped(int forwardDistance) => OnPlayerHopped?.Invoke(forwardDistance);
        public static void TriggerPlayerGridMoved(Vector3Int newGridPos) => OnPlayerGridMoved?.Invoke(newGridPos);
        public static void TriggerPlayerDied(string deathReason, int finalDistance) => OnPlayerDied?.Invoke(deathReason, finalDistance);

        #endregion

        #region 3. 스코어 및 수집품 이벤트 (Score & Collectibles)

        /// <summary> 실시간 거리(m) 또는 점수가 갱신되었을 때 발생합니다. </summary>
        public static event Action<int> OnScoreChanged;

        /// <summary> 코인 또는 출석 도장 아이템을 획득했을 때 발생합니다. (누적 코인 수) </summary>
        public static event Action<int> OnCoinCollected;

        /// <summary> 현재 기록에 따른 학점 등급(A+, A0, B+, F 등)이 갱신되었을 때 발생합니다. </summary>
        public static event Action<string> OnGradeChanged;

        public static void TriggerScoreChanged(int currentScore) => OnScoreChanged?.Invoke(currentScore);
        public static void TriggerCoinCollected(int totalCoins) => OnCoinCollected?.Invoke(totalCoins);
        public static void TriggerGradeChanged(string grade) => OnGradeChanged?.Invoke(grade);

        #endregion

        #region 4. 월드 및 스테이지 전이 이벤트 (World & Stages)

        /// <summary> 플레이어가 다음 스토리 스테이지(1~4단계)로 진입했을 때 발생합니다. </summary>
        public static event Action<StageLevel> OnStageChanged;

        /// <summary> 플레이어가 특정 레인을 안전하게 통과했을 때 발생합니다. (통과한 레인 Z 인덱스) </summary>
        public static event Action<int> OnLanePassed;

        public static void TriggerStageChanged(StageLevel newStage) => OnStageChanged?.Invoke(newStage);
        public static void TriggerLanePassed(int laneIndex) => OnLanePassed?.Invoke(laneIndex);

        #endregion

        #region 5. 이벤트 초기화 (Scene Unload / Cleanup)

        /// <summary>
        /// 씬 전환이나 전체 초기화 시 잔여 구독자(메모리 누수 위험)를 안전하게 해제합니다.
        /// </summary>
        public static void ClearAllSubscriptions()
        {
            OnGameStateChanged = null;
            OnGameStarted = null;
            OnGameRestarted = null;

            OnPlayerHopped = null;
            OnPlayerGridMoved = null;
            OnPlayerDied = null;

            OnScoreChanged = null;
            OnCoinCollected = null;
            OnGradeChanged = null;

            OnStageChanged = null;
            OnLanePassed = null;
        }

        #endregion
    }
}
