using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using CampusRun.Core;
using CampusRun.Character;

namespace CampusRun.UI
{
    /// <summary>
    /// 메인 화면 상단 중앙에 위치하여 5종 캐릭터(민우, 수아, 준호, 지민, 펭귄)를 직관적으로 선택할 수 있는 컴팩트 UI 위젯입니다.
    /// 화살표 버튼 또는 [C] 단축키로 캐릭터를 변경하며, 씬의 3D 플레이어 외형을 실시간으로 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSelectUI : MonoBehaviour
    {
        [Header("--- UI 컴포넌트 바인딩 ---")]
        [SerializeField] private Text _headerTitleText;
        [SerializeField] private Text _indicatorText;
        [SerializeField] private RawImage _previewRawImage;
        [SerializeField] private Text _characterNameText;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;

        private CanvasGroup _canvasGroup;
        private bool _isGameRunning = false;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (_prevButton != null)
            {
                _prevButton.onClick.RemoveAllListeners();
                _prevButton.onClick.AddListener(OnPrevButtonClicked);
            }

            if (_nextButton != null)
            {
                _nextButton.onClick.RemoveAllListeners();
                _nextButton.onClick.AddListener(OnNextButtonClicked);
            }
        }

        private void OnEnable()
        {
            CharacterSelectManager.OnCharacterChanged += HandleCharacterChanged;
            GameEvents.OnGameStarted += HandleGameStarted;
            GameEvents.OnGameRestarted += HandleGameRestarted;
            GameEvents.OnPlayerDied += HandlePlayerDied;

            // 현재 선택된 캐릭터로 UI 갱신
            RefreshUI();
        }

        private void OnDisable()
        {
            CharacterSelectManager.OnCharacterChanged -= HandleCharacterChanged;
            GameEvents.OnGameStarted -= HandleGameStarted;
            GameEvents.OnGameRestarted -= HandleGameRestarted;
            GameEvents.OnPlayerDied -= HandlePlayerDied;
        }

        private void Update()
        {
            // 게임 진행 중이거나 비활성화 상태일 때는 단축키 무시
            if (_isGameRunning || !gameObject.activeInHierarchy) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // [C] 키 입력 시 다음 캐릭터로 순환 변경
            if (keyboard.cKey.wasPressedThisFrame)
            {
                OnNextButtonClicked();
            }
        }

        private void OnPrevButtonClicked()
        {
            if (CharacterSelectManager.Instance != null)
            {
                CharacterSelectManager.Instance.SelectPrevious();
            }
        }

        private void OnNextButtonClicked()
        {
            if (CharacterSelectManager.Instance != null)
            {
                CharacterSelectManager.Instance.SelectNext();
            }
        }

        private void HandleCharacterChanged(int index)
        {
            RefreshUI();
        }

        private void HandleGameStarted()
        {
            _isGameRunning = true;
            SetVisibility(false);
        }

        private void HandleGameRestarted()
        {
            _isGameRunning = false;
            SetVisibility(true);
            RefreshUI();
        }

        private void HandlePlayerDied(string reason, int score)
        {
            _isGameRunning = true;
            SetVisibility(false);
        }

        /// <summary>
        /// 위젯 표시/숨김을 제어합니다.
        /// </summary>
        public void SetVisibility(bool visible)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// 현재 선택된 캐릭터 정보를 UI 요소에 바인딩합니다.
        /// </summary>
        public void RefreshUI()
        {
            var manager = CharacterSelectManager.Instance;
            if (manager == null) return;

            var charInfo = manager.CurrentCharacter;
            if (charInfo == null) return;

            if (_characterNameText != null)
            {
                _characterNameText.text = charInfo.displayName;
            }

            if (_indicatorText != null)
            {
                _indicatorText.text = $"({manager.CurrentIndex + 1} / {manager.CharacterCount})";
            }

            if (_previewRawImage != null)
            {
                if (charInfo.previewTexture != null)
                {
                    _previewRawImage.texture = charInfo.previewTexture;
                    _previewRawImage.color = Color.white;
                }
                else
                {
                    _previewRawImage.color = new Color(1f, 1f, 1f, 0.2f);
                }
            }
        }

        /// <summary>
        /// 씬의 Canvas 상단 중앙(Top-Center)에 컴팩트 캐릭터 선택창 UI를 동적으로 생성합니다.
        /// </summary>
        public static CharacterSelectUI Create(Canvas canvas, Font pixelFont)
        {
            if (canvas == null) return null;

            if (pixelFont == null)
            {
                pixelFont = Resources.Load<Font>("Fonts/neodgm");
                if (pixelFont == null)
                {
                    pixelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }

            // 이미 존재하는지 확인 후 정리
            Transform existing = canvas.transform.Find("CharacterSelect_Widget");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            // [메인 위젯 루트 오브젝트] - 화면 상단 중앙 고정 (Top-Center)
            GameObject rootObj = new GameObject("CharacterSelect_Widget");
            rootObj.transform.SetParent(canvas.transform, false);

            RectTransform rootRect = rootObj.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f); // 상단 중앙
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -20f); // 상단에서 20px 여백
            rootRect.sizeDelta = new Vector2(400f, 114f);
            rootObj.transform.localScale = Vector3.one;

            // 세련된 다크 블루 패널 배경
            Image bgImg = rootObj.AddComponent<Image>();
            bgImg.color = new Color(0.10f, 0.12f, 0.18f, 0.94f);

            // 골드 테두리 외곽선
            Outline outline = rootObj.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.82f, 0.25f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            Shadow shadow = rootObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(3f, -3f);

            // 1. 상단 헤더 라벨: "👤 캐릭터 선택" (좌상단 정렬, 영역 내 완벽 피팅)
            GameObject titleObj = new GameObject("Title_Text");
            titleObj.transform.SetParent(rootObj.transform, false);
            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(16f, -16f);
            titleRect.sizeDelta = new Vector2(220f, 24f);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = pixelFont;
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(1f, 0.84f, 0.25f); // 골드
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = 12;
            titleText.resizeTextMaxSize = 16;
            titleText.text = "👤 캐릭터 선택";

            // 2. 인디케이터: (1 / 5) (우상단 정렬, 우측 패딩 16px)
            GameObject indObj = new GameObject("Indicator_Text");
            indObj.transform.SetParent(rootObj.transform, false);
            RectTransform indRect = indObj.AddComponent<RectTransform>();
            indRect.anchorMin = new Vector2(1f, 1f);
            indRect.anchorMax = new Vector2(1f, 1f);
            indRect.pivot = new Vector2(1f, 0.5f);
            indRect.anchoredPosition = new Vector2(-16f, -16f);
            indRect.sizeDelta = new Vector2(90f, 24f);
            Text indText = indObj.AddComponent<Text>();
            indText.font = pixelFont;
            indText.fontSize = 14;
            indText.color = new Color(0.72f, 0.78f, 0.88f);
            indText.alignment = TextAnchor.MiddleRight;
            indText.horizontalOverflow = HorizontalWrapMode.Wrap;
            indText.verticalOverflow = VerticalWrapMode.Truncate;
            indText.resizeTextForBestFit = true;
            indText.resizeTextMinSize = 10;
            indText.resizeTextMaxSize = 14;
            indText.text = "(1 / 5)";

            // 상단 구분선 (Divider Line)
            GameObject divObj = new GameObject("Divider_Line");
            divObj.transform.SetParent(rootObj.transform, false);
            RectTransform divRect = divObj.AddComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0f, 1f);
            divRect.anchorMax = new Vector2(1f, 1f);
            divRect.pivot = new Vector2(0.5f, 0.5f);
            divRect.offsetMin = new Vector2(14f, -32f);
            divRect.offsetMax = new Vector2(-14f, -31f);
            Image divImg = divObj.AddComponent<Image>();
            divImg.color = new Color(1f, 0.85f, 0.3f, 0.3f);

            // 3. 좌측 이전 버튼 (◀)
            GameObject prevBtnObj = new GameObject("Prev_Button");
            prevBtnObj.transform.SetParent(rootObj.transform, false);
            RectTransform prevBtnRect = prevBtnObj.AddComponent<RectTransform>();
            prevBtnRect.anchorMin = new Vector2(0f, 1f);
            prevBtnRect.anchorMax = new Vector2(0f, 1f);
            prevBtnRect.pivot = new Vector2(0.5f, 0.5f);
            prevBtnRect.anchoredPosition = new Vector2(36f, -73f);
            prevBtnRect.sizeDelta = new Vector2(38f, 48f);
            Image prevImg = prevBtnObj.AddComponent<Image>();
            prevImg.color = new Color(0.20f, 0.26f, 0.38f, 0.95f);
            Button prevBtn = prevBtnObj.AddComponent<Button>();
            ColorBlock pColors = prevBtn.colors;
            pColors.highlightedColor = new Color(0.32f, 0.44f, 0.65f);
            pColors.pressedColor = new Color(0.14f, 0.18f, 0.28f);
            prevBtn.colors = pColors;

            GameObject prevTxtObj = new GameObject("Text");
            prevTxtObj.transform.SetParent(prevBtnObj.transform, false);
            RectTransform prevTxtRect = prevTxtObj.AddComponent<RectTransform>();
            prevTxtRect.anchorMin = Vector2.zero;
            prevTxtRect.anchorMax = Vector2.one;
            prevTxtRect.pivot = new Vector2(0.5f, 0.5f);
            prevTxtRect.sizeDelta = Vector2.zero;
            Text prevTxt = prevTxtObj.AddComponent<Text>();
            prevTxt.font = pixelFont;
            prevTxt.fontSize = 20;
            prevTxt.fontStyle = FontStyle.Bold;
            prevTxt.color = Color.white;
            prevTxt.alignment = TextAnchor.MiddleCenter;
            prevTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            prevTxt.verticalOverflow = VerticalWrapMode.Truncate;
            prevTxt.text = "◀";

            // 4. 중앙 썸네일 박스 (56x56)
            GameObject thumbBoxObj = new GameObject("Thumbnail_Box");
            thumbBoxObj.transform.SetParent(rootObj.transform, false);
            RectTransform thumbBoxRect = thumbBoxObj.AddComponent<RectTransform>();
            thumbBoxRect.anchorMin = new Vector2(0f, 1f);
            thumbBoxRect.anchorMax = new Vector2(0f, 1f);
            thumbBoxRect.pivot = new Vector2(0.5f, 0.5f);
            thumbBoxRect.anchoredPosition = new Vector2(96f, -73f);
            thumbBoxRect.sizeDelta = new Vector2(56f, 56f);
            Image thumbBoxImg = thumbBoxObj.AddComponent<Image>();
            thumbBoxImg.color = new Color(0.18f, 0.22f, 0.32f, 0.90f);

            Outline thumbOutline = thumbBoxObj.AddComponent<Outline>();
            thumbOutline.effectColor = new Color(0.4f, 0.5f, 0.7f, 0.6f);
            thumbOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 썸네일 RawImage
            GameObject rawImgObj = new GameObject("Preview_RawImage");
            rawImgObj.transform.SetParent(thumbBoxObj.transform, false);
            RectTransform rawImgRect = rawImgObj.AddComponent<RectTransform>();
            rawImgRect.anchorMin = new Vector2(0.5f, 0.5f);
            rawImgRect.anchorMax = new Vector2(0.5f, 0.5f);
            rawImgRect.pivot = new Vector2(0.5f, 0.5f);
            rawImgRect.anchoredPosition = Vector2.zero;
            rawImgRect.sizeDelta = new Vector2(50f, 50f);
            RawImage rawImg = rawImgObj.AddComponent<RawImage>();

            // 5. 캐릭터 이름 텍스트 (넉넉한 195px 너비 + BestFit 자동 조절로 칸 완벽 준수)
            GameObject nameObj = new GameObject("CharacterName_Text");
            nameObj.transform.SetParent(rootObj.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(0f, 1f);
            nameRect.pivot = new Vector2(0f, 0.5f);
            nameRect.anchoredPosition = new Vector2(136f, -62f);
            nameRect.sizeDelta = new Vector2(195f, 28f);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = pixelFont;
            nameText.fontSize = 20;
            nameText.fontStyle = FontStyle.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 13;
            nameText.resizeTextMaxSize = 20;
            nameText.text = "새내기 민우";

            // 6. 하단 안내 텍스트: "[C] 키로 빠른 변경" (패널 바닥 안쪽 안전 여백 17px)
            GameObject tipObj = new GameObject("Tip_Text");
            tipObj.transform.SetParent(rootObj.transform, false);
            RectTransform tipRect = tipObj.AddComponent<RectTransform>();
            tipRect.anchorMin = new Vector2(0f, 1f);
            tipRect.anchorMax = new Vector2(0f, 1f);
            tipRect.pivot = new Vector2(0f, 0.5f);
            tipRect.anchoredPosition = new Vector2(136f, -87f);
            tipRect.sizeDelta = new Vector2(195f, 20f);
            Text tipText = tipObj.AddComponent<Text>();
            tipText.font = pixelFont;
            tipText.fontSize = 12;
            tipText.color = new Color(0.70f, 0.78f, 0.88f, 0.90f);
            tipText.alignment = TextAnchor.MiddleLeft;
            tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tipText.verticalOverflow = VerticalWrapMode.Truncate;
            tipText.resizeTextForBestFit = true;
            tipText.resizeTextMinSize = 10;
            tipText.resizeTextMaxSize = 12;
            tipText.text = "[C] 키로 빠른 변경";

            // 7. 우측 다음 버튼 (▶)
            GameObject nextBtnObj = new GameObject("Next_Button");
            nextBtnObj.transform.SetParent(rootObj.transform, false);
            RectTransform nextBtnRect = nextBtnObj.AddComponent<RectTransform>();
            nextBtnRect.anchorMin = new Vector2(0f, 1f);
            nextBtnRect.anchorMax = new Vector2(0f, 1f);
            nextBtnRect.pivot = new Vector2(0.5f, 0.5f);
            nextBtnRect.anchoredPosition = new Vector2(364f, -73f);
            nextBtnRect.sizeDelta = new Vector2(38f, 48f);
            Image nextImg = nextBtnObj.AddComponent<Image>();
            nextImg.color = new Color(0.20f, 0.26f, 0.38f, 0.95f);
            Button nextBtn = nextBtnObj.AddComponent<Button>();
            nextBtn.colors = pColors;

            GameObject nextTxtObj = new GameObject("Text");
            nextTxtObj.transform.SetParent(nextBtnObj.transform, false);
            RectTransform nextTxtRect = nextTxtObj.AddComponent<RectTransform>();
            nextTxtRect.anchorMin = Vector2.zero;
            nextTxtRect.anchorMax = Vector2.one;
            nextTxtRect.pivot = new Vector2(0.5f, 0.5f);
            nextTxtRect.sizeDelta = Vector2.zero;
            Text nextTxt = nextTxtObj.AddComponent<Text>();
            nextTxt.font = pixelFont;
            nextTxt.fontSize = 20;
            nextTxt.fontStyle = FontStyle.Bold;
            nextTxt.color = Color.white;
            nextTxt.alignment = TextAnchor.MiddleCenter;
            nextTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nextTxt.verticalOverflow = VerticalWrapMode.Truncate;
            nextTxt.text = "▶";

            // 컴포넌트 연결
            CharacterSelectUI ui = rootObj.AddComponent<CharacterSelectUI>();
            ui._headerTitleText = titleText;
            ui._indicatorText = indText;
            ui._previewRawImage = rawImg;
            ui._characterNameText = nameText;
            ui._prevButton = prevBtn;
            ui._nextButton = nextBtn;

            prevBtn.onClick.AddListener(ui.OnPrevButtonClicked);
            nextBtn.onClick.AddListener(ui.OnNextButtonClicked);

            ui.RefreshUI();
            return ui;
        }
    }
}
