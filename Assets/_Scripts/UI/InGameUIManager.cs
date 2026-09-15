using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using CampusRun.Core;

namespace CampusRun.UI
{
    /// <summary>
    /// 인게임 실시간 거리 점수 표시 및 게임오버 성적표(학점 뱃지) 팝업을 총괄하는 UI 매니저입니다.
    /// GameEvents의 점수 갱신 및 사망 이벤트를 구독하여 화면을 자동으로 갱신합니다.
    /// </summary>
    public class InGameUIManager : MonoBehaviour
    {
        [Header("--- 인게임 실시간 HUD ---")]
        [Tooltip("실시간 전진 거리 텍스트 (예: 120m)")]
        [SerializeField] private TextMeshProUGUI _scoreText;

        [Header("--- 게임오버 성적표 패널 ---")]
        [Tooltip("사망 시 활성화될 게임오버 팝업 패널")]
        [SerializeField] private GameObject _gameOverPanel;

        [Tooltip("탈락 사유 텍스트")]
        [SerializeField] private TextMeshProUGUI _deathReasonText;

        [Tooltip("최종 기록 텍스트 (예: 최종 기록: 120m)")]
        [SerializeField] private TextMeshProUGUI _finalScoreText;

        [Tooltip("학점 등급 텍스트 (A+, B0, F 등)")]
        [SerializeField] private TextMeshProUGUI _gradeText;

        [Tooltip("최고 기록 텍스트")]
        [SerializeField] private TextMeshProUGUI _highScoreText;

        [Tooltip("재도전 버튼")]
        [SerializeField] private Button _retryButton;

        private const string HighScoreKey = "CampusRun_HighScore";
        private int _currentScore = 0;

        private void Awake()
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(false);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(OnRetryButtonClicked);
            }
        }

        private void OnEnable()
        {
            GameEvents.OnScoreChanged += UpdateScoreDisplay;
            GameEvents.OnPlayerDied += ShowGameOverPanel;
        }

        private void OnDisable()
        {
            GameEvents.OnScoreChanged -= UpdateScoreDisplay;
            GameEvents.OnPlayerDied -= ShowGameOverPanel;
        }

        private void Start()
        {
            UpdateScoreDisplay(0);
        }

        /// <summary> 실시간 거리 HUD 텍스트 갱신 </summary>
        private void UpdateScoreDisplay(int score)
        {
            _currentScore = score;
            if (_scoreText != null)
            {
                _scoreText.text = $"{score}m";
            }
        }

        /// <summary> 플레이어 사망 시 게임오버 성적표 패널 활성화 </summary>
        private void ShowGameOverPanel(string deathReason, int finalScore)
        {
            if (_gameOverPanel == null) return;

            // 1. 최고 기록 갱신 및 저장
            int bestScore = PlayerPrefs.GetInt(HighScoreKey, 0);
            if (finalScore > bestScore)
            {
                bestScore = finalScore;
                PlayerPrefs.SetInt(HighScoreKey, bestScore);
                PlayerPrefs.Save();
            }

            // 2. 탈락 사유 및 최종 점수 표시
            if (_deathReasonText != null)
            {
                _deathReasonText.text = deathReason;
            }

            if (_finalScoreText != null)
            {
                _finalScoreText.text = $"최종 기록: {finalScore}m";
            }

            if (_highScoreText != null)
            {
                _highScoreText.text = $"최고 기록: {bestScore}m";
            }

            // 3. 대학생 학점 등급 판정 및 컬러링
            if (_gradeText != null)
            {
                string grade = EvaluateGrade(finalScore);
                _gradeText.text = grade;

                // 학점에 따른 시각적 색상 적용
                if (grade.Contains("A+")) _gradeText.color = new Color(0.95f, 0.75f, 0.1f); // 황금빛
                else if (grade.Contains("A")) _gradeText.color = new Color(0.2f, 0.8f, 0.4f);  // 초록
                else if (grade.Contains("B")) _gradeText.color = new Color(0.3f, 0.7f, 1.0f);  // 파랑
                else if (grade.Contains("C")) _gradeText.color = new Color(0.8f, 0.6f, 0.2f);  // 주황
                else _gradeText.color = new Color(0.95f, 0.25f, 0.25f);                         // 빨강 (F학점)
            }

            _gameOverPanel.SetActive(true);
        }

        /// <summary> 도달 거리에 따른 학점 판정 </summary>
        private string EvaluateGrade(int score)
        {
            if (score >= 500) return "A+ (수석 졸업)";
            if (score >= 400) return "A0 (장학생)";
            if (score >= 300) return "B+ (안전 통과)";
            if (score >= 200) return "B0 (출석 성공)";
            if (score >= 100) return "C+ (지각 모면)";
            return "F (재수강 확정)";
        }

        /// <summary> 다시 시작 버튼 클릭 시 씬 새로고침 </summary>
        private void OnRetryButtonClicked()
        {
            GameEvents.ClearAllSubscriptions();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
