using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace CampusRun.UI
{
    /// <summary>
    /// 독립 스플래시 씬(Scene_Splash)에서 비동기 씬 프리로드와 함께
    /// 화려한 레트로 스플래시 화면을 연출하는 컨트롤러입니다.
    /// </summary>
    public class SplashScreenController : MonoBehaviour
    {
        [Header("--- 씬 전환 설정 ---")]
        [Tooltip("스플래시 완료 후 로드할 대상 게임 씬 이름")]
        [SerializeField] private string _targetSceneName = "Sandbox_PlayTest";

        [Tooltip("최소 스플래시 연출 시간 (초)")]
        [SerializeField] private float _minDisplayDuration = 2.5f;

        [Header("--- 폰트 설정 ---")]
        [Tooltip("레트로 도트 폰트")]
        [SerializeField] private Font _pixelFont;

        private RectTransform _progressFillRect;
        private Text _progressPercentText;
        private Text _statusText;
        private CanvasGroup _canvasGroup;

        private bool _isSkipRequested = false;

        private static readonly string[] StatusMessages = new string[]
        {
            "1교시 강의계획서 확인 중...",
            "캠퍼스 셔틀버스 실시간 위치 추적 중...",
            "학생 식당 학식 메뉴 및 잔여 출석 일수 조회 중...",
            "교수님 출석부 및 과제 제출함 동기화 중...",
            "출석체크 준비 완료! 강의실로 질주하세요! 🏃‍♂️💨"
        };

        private static readonly string[] CachedPercentStrings = GeneratePercentStrings();
        private static string[] GeneratePercentStrings()
        {
            string[] arr = new string[101];
            for (int i = 0; i <= 100; i++) arr[i] = $"{i}%";
            return arr;
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            EnsureSplashUI();
        }

        private void Start()
        {
            StartCoroutine(LoadTargetSceneSequence());
        }

        private void Update()
        {
            if (_isSkipRequested) return;

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var touch = Touchscreen.current;

            bool skip = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                     || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                     || (touch != null && touch.primaryTouch.press.wasPressedThisFrame);

            if (skip)
            {
                _isSkipRequested = true;
            }
        }

        private IEnumerator LoadTargetSceneSequence()
        {
            float maxBarWidth = 554f;
            float elapsed = 0f;

            // 백그라운드 비동기 씬 로드 개시
            AsyncOperation asyncOp = null;
            if (Application.CanStreamedLevelBeLoaded(_targetSceneName))
            {
                asyncOp = SceneManager.LoadSceneAsync(_targetSceneName);
                if (asyncOp != null)
                {
                    asyncOp.allowSceneActivation = false;
                }
            }

            while (elapsed < _minDisplayDuration && !_isSkipRequested)
            {
                elapsed += Time.deltaTime;
                float progressTime = Mathf.Clamp01(elapsed / _minDisplayDuration);

                float asyncProgress = (asyncOp != null) ? Mathf.Clamp01(asyncOp.progress / 0.9f) : progressTime;
                float combinedProgress = Mathf.Min(progressTime, asyncProgress);

                float smoothed = Mathf.SmoothStep(0f, 1f, combinedProgress);
                int percentInt = Mathf.Clamp(Mathf.RoundToInt(smoothed * 100f), 0, 100);

                if (_progressFillRect != null)
                {
                    _progressFillRect.sizeDelta = new Vector2(maxBarWidth * smoothed, 0f);
                }

                if (_progressPercentText != null)
                {
                    _progressPercentText.text = CachedPercentStrings[percentInt];
                }

                if (_statusText != null)
                {
                    int msgIdx = Mathf.Min(percentInt / 20, StatusMessages.Length - 1);
                    _statusText.text = StatusMessages[msgIdx];
                }

                yield return null;
            }

            // 100% 도달
            if (_progressFillRect != null) _progressFillRect.sizeDelta = new Vector2(maxBarWidth, 0f);
            if (_progressPercentText != null) _progressPercentText.text = CachedPercentStrings[100];
            if (_statusText != null) _statusText.text = StatusMessages[4];

            yield return new WaitForSeconds(0.2f);

            // 페이드아웃
            float fadeDuration = 0.35f;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(fadeElapsed / fadeDuration);
                if (_canvasGroup != null) _canvasGroup.alpha = 1f - t;
                yield return null;
            }

            // 씬 활성화
            if (asyncOp != null)
            {
                asyncOp.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(_targetSceneName);
            }
        }

        private void EnsureSplashUI()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Splash_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            Font font = GetFont();

            GameObject root = new GameObject("SplashScreen_Root");
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            Image bgImg = root.AddComponent<Image>();
            bgImg.color = new Color(0.045f, 0.055f, 0.085f, 1f);

            _canvasGroup = root.AddComponent<CanvasGroup>();

            GameObject contentObj = new GameObject("Content_Container");
            contentObj.transform.SetParent(root.transform, false);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(1000f, 700f);
            contentObj.transform.localScale = new Vector3(0.92f, 0.92f, 0.92f);

            // 배지
            CreateText("Badge", contentObj.transform, new Vector2(0f, 230f), new Vector2(600f, 40f),
                "⚡ CAMPUS SURVIVAL ACTION ⚡", font, 22, FontStyle.Bold, new Color(1f, 0.76f, 0.22f));

            // 메인 로고
            Text title = CreateText("MainTitle", contentObj.transform, new Vector2(0f, 150f), new Vector2(900f, 120f),
                "CAMPUS RUN", font, 92, FontStyle.Normal, new Color(1f, 0.88f, 0.2f));
            Outline outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.05f, 0.02f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);

            // 한글 서브 타이틀
            CreateText("SubTitle", contentObj.transform, new Vector2(0f, 75f), new Vector2(800f, 55f),
                "캠 퍼 스 런 : 지 각  5 분 전 ! !", font, 38, FontStyle.Normal, new Color(0.28f, 0.98f, 0.48f));

            // 이모지
            CreateText("Emoji", contentObj.transform, new Vector2(0f, 5f), new Vector2(600f, 60f),
                "🏃‍♂️💨   ⏰   🏫", font, 44, FontStyle.Normal, Color.white);

            // 스토리
            CreateText("Story", contentObj.transform, new Vector2(0f, -65f), new Vector2(800f, 40f),
                "1교시 출석 마감 직전! 셔틀버스를 피하고 강의실로 질주하라!", font, 22, FontStyle.Normal, new Color(0.88f, 0.92f, 0.96f, 0.9f));

            // 프로그레스 바 박스
            GameObject barBox = new GameObject("ProgressBar_Box");
            barBox.transform.SetParent(contentObj.transform, false);
            RectTransform barRect = barBox.AddComponent<RectTransform>();
            barRect.anchoredPosition = new Vector2(0f, -145f);
            barRect.sizeDelta = new Vector2(560f, 26f);
            Image barBg = barBox.AddComponent<Image>();
            barBg.color = new Color(0.12f, 0.14f, 0.19f, 0.95f);

            GameObject fillArea = new GameObject("Fill_Area");
            fillArea.transform.SetParent(barBox.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = new Vector2(-6f, -6f);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            _progressFillRect = fill.AddComponent<RectTransform>();
            _progressFillRect.anchorMin = new Vector2(0f, 0f);
            _progressFillRect.anchorMax = new Vector2(0f, 1f);
            _progressFillRect.pivot = new Vector2(0f, 0.5f);
            _progressFillRect.sizeDelta = new Vector2(0f, 0f);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.22f, 0.88f, 0.45f, 1f);

            // 퍼센트
            _progressPercentText = CreateText("Percent", contentObj.transform, new Vector2(0f, -188f), new Vector2(200f, 36f),
                "0%", font, 24, FontStyle.Bold, new Color(1f, 0.88f, 0.25f));

            // 상태
            _statusText = CreateText("Status", contentObj.transform, new Vector2(0f, -225f), new Vector2(700f, 36f),
                StatusMessages[0], font, 20, FontStyle.Normal, new Color(0.48f, 0.88f, 1f, 0.95f));

            // 스킵 가이드
            CreateText("Skip", contentObj.transform, new Vector2(0f, -280f), new Vector2(600f, 34f),
                "[ SPACE / ENTER / 클릭 시 즉시 스킵 ]", font, 18, FontStyle.Normal, new Color(1f, 1f, 1f, 0.5f));
        }

        private Text CreateText(string name, Transform parent, Vector2 pos, Vector2 size, string text, Font font, int fontSize, FontStyle style, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = text;

            return txt;
        }

        private Font GetFont()
        {
            if (_pixelFont != null) return _pixelFont;
            Font f = Resources.Load<Font>("Fonts/neodgm");
            if (f != null) return f;
#if UNITY_EDITOR
            f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/neodgm.ttf");
            if (f != null) return f;
#endif
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
