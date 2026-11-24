// Updated with 6 characters (added skeleton)
using System;
using UnityEngine;
using CampusRun.Player;
using CampusRun.Audio;

namespace CampusRun.Character
{
    /// <summary>
    /// 캠퍼스런 캐릭터 선택 및 런타임 3D 모델 교체를 총괄하는 싱글톤 매니저입니다.
    /// PlayerPrefs를 통해 선택된 캐릭터를 영구 저장하며, 씬의 플레이어 모델을 실시간 교체합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterSelectManager : MonoBehaviour
    {
        public static CharacterSelectManager Instance { get; private set; }

        public const string PrefKeySelectedCharacter = "CampusRun_SelectedCharacterIndex";

        public static event Action<int> OnCharacterChanged;

        [Header("--- 등록된 캐릭터 프리셋 (엄선 5종) ---")]
        [SerializeField] private CharacterInfo[] _characters;

        [Header("--- 공용 캐릭터 머티리얼 ---")]
        [SerializeField] private Material _characterMaterial;

        private int _currentIndex = 0;

        public int CurrentIndex => _currentIndex;
        public int CharacterCount => _characters != null ? _characters.Length : 0;

        public CharacterInfo CurrentCharacter
        {
            get
            {
                if (_characters == null || _characters.Length == 0) return null;
                int validIndex = Mathf.Clamp(_currentIndex, 0, _characters.Length - 1);
                return _characters[validIndex];
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeCharacterPresets();
            LoadSavedCharacter();
        }

        private void Start()
        {
            // 씬 기동 시 현재 저장된 캐릭터를 플레이어에게 즉시 반영
            ApplyCurrentCharacterToPlayer(playPopEffect: false);
        }

        /// <summary>
        /// 6종 캐릭터 프리셋(민우, 수아, 준호, 지민, 펭귄, 스켈레톤) 데이터를 초기화합니다.
        /// </summary>
        private void InitializeCharacterPresets()
        {
            if (_characters != null && _characters.Length > 0) return;

            // 공용 머티리얼 로드
            if (_characterMaterial == null)
            {
                _characterMaterial = Resources.Load<Material>("M_Character_Male");
#if UNITY_EDITOR
                if (_characterMaterial == null)
                {
                    _characterMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/M_Character_Male.mat");
                }
#endif
            }

            Texture2D penguinThumb = LoadPreview("penguin6");
            if (penguinThumb == null)
            {
                penguinThumb = GeneratePenguinPixelThumbnail();
            }

            Texture2D skeletonThumb = LoadPreview("skeleton");
            if (skeletonThumb == null)
            {
                skeletonThumb = GenerateSkeletonPixelThumbnail();
            }

            Texture2D catThumb = LoadPreview("cat");
            if (catThumb == null)
            {
                catThumb = GenerateCatPixelThumbnail();
            }

            _characters = new CharacterInfo[]
            {
                new CharacterInfo(
                    id: "char_male_a",
                    displayName: "새내기 민우",
                    modelPrefab: LoadModel("character-male-a"),
                    previewTexture: LoadPreview("character-male-a"),
                    overrideMaterial: true,
                    customScale: 1.0f
                ),
                new CharacterInfo(
                    id: "char_female_a",
                    displayName: "단발 수아",
                    modelPrefab: LoadModel("character-female-a"),
                    previewTexture: LoadPreview("character-female-a"),
                    overrideMaterial: true,
                    customScale: 1.0f
                ),
                new CharacterInfo(
                    id: "char_male_b",
                    displayName: "힙스터 준호",
                    modelPrefab: LoadModel("character-male-b"),
                    previewTexture: LoadPreview("character-male-b"),
                    overrideMaterial: true,
                    customScale: 1.0f
                ),
                new CharacterInfo(
                    id: "char_female_b",
                    displayName: "체대생 지민",
                    modelPrefab: LoadModel("character-female-b"),
                    previewTexture: LoadPreview("character-female-b"),
                    overrideMaterial: true,
                    customScale: 1.0f
                ),
                new CharacterInfo(
                    id: "char_penguin",
                    displayName: "뒤뚱 펭귄",
                    modelPrefab: LoadModel("penguin6"),
                    previewTexture: penguinThumb,
                    overrideMaterial: false,
                    customScale: 0.70f // 펭귄 크기 축소 (기존 0.95f -> 0.70f)
                ),
                new CharacterInfo(
                    id: "char_skeleton",
                    displayName: "해골 선배",
                    modelPrefab: LoadModel("skeleton"),
                    previewTexture: skeletonThumb,
                    overrideMaterial: false,
                    customScale: 0.65f // 신규 스켈레톤 최적 비율
                ),
                new CharacterInfo(
                    id: "char_cat",
                    displayName: "길냥이 학생",
                    modelPrefab: LoadModel("cat") ?? CreateProceduralCatPrefab(),
                    previewTexture: catThumb,
                    overrideMaterial: false,
                    customScale: 1.0f // 일반 캐릭터(0.65m)와 1:1 완벽 일치
                )
            };
        }

        private GameObject LoadModel(string fileName)
        {
            GameObject go = Resources.Load<GameObject>($"Characters/{fileName}");
#if UNITY_EDITOR
            if (go == null)
            {
                go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/kenney_mini-characters/Models/FBX format/{fileName}.fbx");
            }
            if (go == null)
            {
                go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Characters/{fileName}.fbx");
            }
            if (go == null && (fileName == "skeleton" || fileName == "Free+Pack+-+Cartoon+Skeleton+(Rigged)"))
            {
                go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Free+Pack+-+Cartoon+Skeleton+(Rigged).fbx");
                if (go == null)
                {
                    go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Characters/skeleton.fbx");
                }
            }
#endif
            if (go == null && fileName == "cat")
            {
                go = CreateProceduralCatPrefab();
            }
            return go;
        }

        private Texture2D LoadPreview(string fileName)
        {
            Texture2D tex = Resources.Load<Texture2D>($"Characters/Previews/{fileName}");
#if UNITY_EDITOR
            if (tex == null)
            {
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Characters/kenney_mini-characters/Previews/{fileName}.png");
            }
            if (tex == null)
            {
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Characters/{fileName}.png");
            }
#endif
            return tex;
        }

        /// <summary>
        /// 펭귄 전용 64x64 도트 픽셀아트 썸네일 텍스처를 런타임에 자동 생성합니다.
        /// </summary>
        private Texture2D GeneratePenguinPixelThumbnail()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color empty = new Color(0f, 0f, 0f, 0f);
            Color body = new Color(0.12f, 0.16f, 0.24f, 1f); // 남색 깃털
            Color belly = new Color(0.95f, 0.96f, 0.98f, 1f); // 흰 배
            Color beak = new Color(1.0f, 0.65f, 0.05f, 1f); // 주황 부리
            Color eye = new Color(0.1f, 0.1f, 0.1f, 1f);
            Color eyeWhite = Color.white;
            Color blush = new Color(1f, 0.5f, 0.6f, 0.85f); // 뺩

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = empty;

            // 원형 몸체 그리기 (x: 16~47, y: 10~54)
            for (int y = 10; y <= 54; y++)
            {
                for (int x = 16; x <= 47; x++)
                {
                    float dx = (x - 31.5f) / 15.5f;
                    float dy = (y - 30f) / 22f;
                    if (dx * dx + dy * dy <= 1.0f)
                    {
                        pixels[y * size + x] = body;
                    }
                }
            }

            // 흰색 배 (x: 23~40, y: 12~40)
            for (int y = 12; y <= 40; y++)
            {
                for (int x = 23; x <= 40; x++)
                {
                    float dx = (x - 31.5f) / 8.5f;
                    float dy = (y - 25f) / 13f;
                    if (dx * dx + dy * dy <= 0.85f)
                    {
                        pixels[y * size + x] = belly;
                    }
                }
            }

            // 앙증맞은 눈 (좌: 25, 우: 38, y: 44)
            DrawPixelBox(pixels, size, 24, 43, 3, 4, eyeWhite);
            DrawPixelBox(pixels, size, 25, 44, 2, 2, eye);
            DrawPixelBox(pixels, size, 37, 43, 3, 4, eyeWhite);
            DrawPixelBox(pixels, size, 37, 44, 2, 2, eye);

            // 볼터치
            DrawPixelBox(pixels, size, 21, 38, 3, 2, blush);
            DrawPixelBox(pixels, size, 40, 38, 3, 2, blush);

            // 주황색 삼각형 부리 (x: 29~34, y: 36~40)
            DrawPixelBox(pixels, size, 29, 38, 6, 3, beak);
            DrawPixelBox(pixels, size, 30, 36, 4, 2, beak);

            // 주황색 발 (y: 6~9)
            DrawPixelBox(pixels, size, 22, 7, 6, 3, beak);
            DrawPixelBox(pixels, size, 36, 7, 6, 3, beak);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DrawPixelBox(Color[] pixels, int size, int startX, int startY, int w, int h, Color color)
        {
            for (int y = startY; y < startY + h && y < size; y++)
            {
                for (int x = startX; x < startX + w && x < size; x++)
                {
                    pixels[y * size + x] = color;
                }
            }
        }

        /// <summary>
        /// 스켈레톤 전용 64x64 도트 픽셀아트 썸네일 텍스처를 런타임에 자동 생성합니다.
        /// </summary>
        private Texture2D GenerateSkeletonPixelThumbnail()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color empty = new Color(0f, 0f, 0f, 0f);
            Color boneWhite = new Color(0.92f, 0.93f, 0.95f, 1f); // 뼈 흰색
            Color boneShadow = new Color(0.72f, 0.74f, 0.78f, 1f); // 뼈 음영
            Color darkHole = new Color(0.12f, 0.14f, 0.18f, 1f);  // 눈/코 구멍
            Color eyePupil = new Color(0.98f, 0.25f, 0.25f, 1f);  // 붉은 안광 포인트

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = empty;

            // 1. 두개골 둥근 외형 (중심: 31.5, 38, 반경: 가로 18, 세로 17)
            for (int y = 20; y <= 56; y++)
            {
                for (int x = 14; x <= 49; x++)
                {
                    float dx = (x - 31.5f) / 17.5f;
                    float dy = (y - 38f) / 17f;
                    if (dx * dx + dy * dy <= 1.0f)
                    {
                        pixels[y * size + x] = boneWhite;
                    }
                }
            }

            // 2. 턱뼈 (y: 11~22, x: 22~41)
            for (int y = 11; y <= 22; y++)
            {
                for (int x = 22; x <= 41; x++)
                {
                    pixels[y * size + x] = boneWhite;
                }
            }

            // 턱뼈 하단 음영
            DrawPixelBox(pixels, size, 22, 11, 20, 2, boneShadow);

            // 3. 큼직한 눈구멍 (좌: 20~28, y: 31~41 / 우: 34~42, y: 31~41)
            DrawPixelBox(pixels, size, 20, 31, 9, 11, darkHole);
            DrawPixelBox(pixels, size, 34, 31, 9, 11, darkHole);

            // 앙증맞은 붉은 안광 도트 (좌: 24, 36 / 우: 38, 36)
            DrawPixelBox(pixels, size, 24, 36, 2, 2, eyePupil);
            DrawPixelBox(pixels, size, 38, 36, 2, 2, eyePupil);

            // 4. 삼각형 코 구멍 (x: 30~33, y: 26~29)
            DrawPixelBox(pixels, size, 30, 26, 4, 3, darkHole);
            DrawPixelBox(pixels, size, 31, 28, 2, 2, darkHole);

            // 5. 치아 표현 (y: 14~19, x: 25~38)
            DrawPixelBox(pixels, size, 28, 14, 1, 6, darkHole);
            DrawPixelBox(pixels, size, 31, 14, 2, 6, darkHole);
            DrawPixelBox(pixels, size, 35, 14, 1, 6, darkHole);
            DrawPixelBox(pixels, size, 25, 17, 14, 1, darkHole);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// 고양이 전용 64x64 도트 픽셀아트 썸네일 텍스처를 런타임에 자동 생성합니다.
        /// 귀여운 고양이 귀, 큰 눈, 코/입, 수염이 포함된 사랑스러운 도트 그래픽입니다.
        /// </summary>
        private Texture2D GenerateCatPixelThumbnail()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;

            Color empty = new Color(0f, 0f, 0f, 0f);
            Color catOrange = new Color(0.96f, 0.62f, 0.22f, 1f); // 치즈 태비 주황색
            Color catWhite = new Color(0.98f, 0.98f, 0.99f, 1f);  // 주둥이/가슴 흰 털
            Color earInner = new Color(0.98f, 0.70f, 0.75f, 1f);  // 분홍 귓속
            Color eyeOuter = new Color(0.18f, 0.68f, 0.45f, 1f);  // 에메랄드 그린 눈
            Color eyePupil = new Color(0.08f, 0.10f, 0.12f, 1f);  // 눈동자
            Color eyeLight = Color.white;                         // 눈망울 하이라이트
            Color nosePink = new Color(0.95f, 0.45f, 0.55f, 1f);  // 분홍 코
            Color whiskerColor = new Color(0.25f, 0.25f, 0.28f, 0.9f); // 수염

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = empty;

            // 1. 뾰족한 고양이 귀 (좌: x 14~25, y 44~58 / 우: x 38~49, y 44~58)
            // 좌측 귀 삼각형
            for (int y = 44; y <= 58; y++)
            {
                int w = (58 - y); // 상단으로 갈수록 좁아짐
                int left = 20 - w;
                int right = 20 + w;
                for (int x = left; x <= right; x++)
                {
                    if (x >= 0 && x < size) pixels[y * size + x] = catOrange;
                }
            }
            // 좌측 귓속 분홍
            for (int y = 45; y <= 52; y++)
            {
                int w = (52 - y) / 2;
                for (int x = 20 - w; x <= 20 + w; x++)
                {
                    pixels[y * size + x] = earInner;
                }
            }

            // 우측 귀 삼각형
            for (int y = 44; y <= 58; y++)
            {
                int w = (58 - y);
                int left = 43 - w;
                int right = 43 + w;
                for (int x = left; x <= right; x++)
                {
                    if (x >= 0 && x < size) pixels[y * size + x] = catOrange;
                }
            }
            // 우측 귓속 분홍
            for (int y = 45; y <= 52; y++)
            {
                int w = (52 - y) / 2;
                for (int x = 43 - w; x <= 43 + w; x++)
                {
                    pixels[y * size + x] = earInner;
                }
            }

            // 2. 둥글넓적한 고양이 머리 (중심: 31.5, 34, 반경 가로 18, 세로 16)
            for (int y = 18; y <= 48; y++)
            {
                for (int x = 13; x <= 50; x++)
                {
                    float dx = (x - 31.5f) / 18.5f;
                    float dy = (y - 33f) / 15f;
                    if (dx * dx + dy * dy <= 1.0f)
                    {
                        pixels[y * size + x] = catOrange;
                    }
                }
            }

            // 3. 하단 주둥이/볼 흰색 영역
            for (int y = 18; y <= 32; y++)
            {
                for (int x = 21; x <= 42; x++)
                {
                    float dx = (x - 31.5f) / 10.5f;
                    float dy = (y - 25f) / 7f;
                    if (dx * dx + dy * dy <= 1.0f)
                    {
                        pixels[y * size + x] = catWhite;
                    }
                }
            }

            // 4. 에메랄드빛 크고 맑은 눈 (좌: 21~27, y: 33~39 / 우: 36~42, y: 33~39)
            DrawPixelBox(pixels, size, 22, 33, 6, 6, eyeOuter);
            DrawPixelBox(pixels, size, 24, 33, 3, 6, eyePupil);
            DrawPixelBox(pixels, size, 23, 36, 2, 2, eyeLight);

            DrawPixelBox(pixels, size, 36, 33, 6, 6, eyeOuter);
            DrawPixelBox(pixels, size, 37, 33, 3, 6, eyePupil);
            DrawPixelBox(pixels, size, 37, 36, 2, 2, eyeLight);

            // 5. 앙증맞은 분홍 삼각 코 (x: 30~33, y: 27~29)
            DrawPixelBox(pixels, size, 30, 28, 4, 2, nosePink);
            DrawPixelBox(pixels, size, 31, 27, 2, 1, nosePink);

            // 입 (ㅅ 모양)
            pixels[25 * size + 31] = eyePupil;
            pixels[25 * size + 32] = eyePupil;
            pixels[24 * size + 29] = eyePupil;
            pixels[24 * size + 30] = eyePupil;
            pixels[24 * size + 33] = eyePupil;
            pixels[24 * size + 34] = eyePupil;

            // 6. 고양이 수염 (좌/우 각 2줄)
            // 좌측 수염
            DrawPixelBox(pixels, size, 12, 28, 7, 1, whiskerColor);
            DrawPixelBox(pixels, size, 13, 24, 7, 1, whiskerColor);
            // 우측 수염
            DrawPixelBox(pixels, size, 45, 28, 7, 1, whiskerColor);
            DrawPixelBox(pixels, size, 44, 24, 7, 1, whiskerColor);

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void LoadSavedCharacter()
        {
            _currentIndex = PlayerPrefs.GetInt(PrefKeySelectedCharacter, 0);
            if (_characters != null && _characters.Length > 0)
            {
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _characters.Length - 1);
            }
        }

        /// <summary>
        /// 다음 캐릭터로 변경합니다.
        /// </summary>
        public void SelectNext()
        {
            if (CharacterCount == 0) return;
            int nextIndex = (_currentIndex + 1) % CharacterCount;
            SelectCharacter(nextIndex);
        }

        /// <summary>
        /// 이전 캐릭터로 변경합니다.
        /// </summary>
        public void SelectPrevious()
        {
            if (CharacterCount == 0) return;
            int prevIndex = (_currentIndex - 1 + CharacterCount) % CharacterCount;
            SelectCharacter(prevIndex);
        }

        /// <summary>
        /// 특정 인덱스의 캐릭터를 선택하고 저장 및 씬에 적용합니다.
        /// </summary>
        public void SelectCharacter(int index)
        {
            if (CharacterCount == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, CharacterCount - 1);

            PlayerPrefs.SetInt(PrefKeySelectedCharacter, _currentIndex);
            PlayerPrefs.Save();

            // 효과음 재생
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayHopSound();
            }

            // 씬의 3D 플레이어 모델 즉시 교체
            ApplyCurrentCharacterToPlayer(playPopEffect: true);

            // UI 갱신 이벤트 전파
            OnCharacterChanged?.Invoke(_currentIndex);
        }

        /// <summary>
        /// 씬에 존재하는 GridPlayerController의 외형 모델을 현재 선택된 캐릭터로 실시간 교체합니다.
        /// </summary>
        public void ApplyCurrentCharacterToPlayer(bool playPopEffect = true)
        {
            GridPlayerController player = GridPlayerController.Instance;
            if (player == null)
            {
                player = FindAnyObjectByType<GridPlayerController>();
            }

            if (player == null) return;

            CharacterInfo charInfo = CurrentCharacter;
            if (charInfo == null || charInfo.modelPrefab == null) return;

            Transform playerTransform = player.transform;

            // 1. 기존의 VisualModel 찾아서 제거
            Transform oldVisual = playerTransform.Find("VisualModel");
            if (oldVisual != null)
            {
                Destroy(oldVisual.gameObject);
            }

            // 2. 새 캐릭터 모델 인스턴스화 (고양이는 컨트롤러 간섭 방지 전용 래퍼 컨테이너 피벗 적용)
            GameObject newVisual;
            GameObject targetForMaterials;

            if (charInfo.id == "char_cat")
            {
                GameObject catPrefab = charInfo.modelPrefab != null ? charInfo.modelPrefab : CreateProceduralCatPrefab();

                // 프로시저럴 카툰 고양이 프리팹인 경우: 이미 완벽한 비율(0.65m)과 피벗을 갖추었으므로 바로 VisualModel로 적용
                if (catPrefab.name.Contains("Procedural") || catPrefab.transform.Find("Body") != null)
                {
                    newVisual = Instantiate(catPrefab, playerTransform);
                    newVisual.name = "VisualModel";
                    newVisual.transform.localPosition = Vector3.zero;
                    newVisual.transform.localRotation = Quaternion.identity;
                    newVisual.SetActive(true);
                    targetForMaterials = newVisual;
                }
                else
                {
                    // 외부 FBX 고양이인 경우: 피벗 래퍼 생성 및 직립/스케일 보정
                    newVisual = new GameObject("VisualModel");
                    newVisual.transform.SetParent(playerTransform, false);
                    newVisual.transform.localPosition = Vector3.zero;
                    newVisual.transform.localRotation = Quaternion.identity;

                    GameObject catModel = Instantiate(catPrefab, newVisual.transform);
                    catModel.name = "Cat_Mesh_Root";
                    catModel.transform.localPosition = Vector3.zero;
                    catModel.transform.localRotation = catPrefab.transform.localRotation;

                    SetupCatModelInWrapper(catModel);
                    AttachCatFace(catModel);

                    targetForMaterials = newVisual;
                }
            }
            else
            {
                newVisual = Instantiate(charInfo.modelPrefab, playerTransform);
                newVisual.name = "VisualModel";
                newVisual.transform.localPosition = Vector3.zero;
                newVisual.transform.localRotation = Quaternion.identity;

                // 스켈레톤의 경우 전용 3D 카툰 눈구멍 및 안광 장착 (두개골 표면에 자연스럽게 밀착)
                if (charInfo.id == "char_skeleton")
                {
                    AttachSkeletonEyes(newVisual);
                }

                targetForMaterials = newVisual;
            }

            // 디버깅: 모든 자식 렌더러 및 머티리얼 로그 기록
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[DebugModel] Selected: {charInfo.id}, Prefab: {charInfo.modelPrefab.name}");
                sb.AppendLine($" VisualModel scale: {newVisual.transform.localScale}, lossyScale: {newVisual.transform.lossyScale}, localPos: {newVisual.transform.localPosition}");
                Renderer[] allR = newVisual.GetComponentsInChildren<Renderer>(true);
                foreach (var r in allR)
                {
                    sb.AppendLine($" Renderer: '{r.name}' ({r.GetType().Name}), enabled={r.enabled}, bounds.size={r.bounds.size}, bounds.center={r.bounds.center}");
                    if (r is MeshRenderer mr)
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        sb.AppendLine($"   MeshFilter: {(mf != null ? (mf.sharedMesh != null ? $"mesh='{mf.sharedMesh.name}', verts={mf.sharedMesh.vertexCount}, meshBounds={mf.sharedMesh.bounds.size}" : "mesh=NULL") : "NO MF")}");
                    }
                    else if (r is SkinnedMeshRenderer smr)
                    {
                        sb.AppendLine($"   SkinnedMeshRenderer: {(smr.sharedMesh != null ? $"mesh='{smr.sharedMesh.name}', verts={smr.sharedMesh.vertexCount}, meshBounds={smr.sharedMesh.bounds.size}" : "mesh=NULL")}");
                    }
                    for (int mi = 0; mi < r.sharedMaterials.Length; mi++)
                    {
                        var m = r.sharedMaterials[mi];
                        sb.AppendLine($"   Mat[{mi}]: '{(m != null ? m.name : "NULL")}', shader='{(m != null ? m.shader.name : "NULL")}'");
                    }
                }
                System.IO.File.WriteAllText("Temp/penguin_debug.txt", sb.ToString());
            }
            catch { }

            // 3. 머티리얼 적용
            if (charInfo.overrideMaterial && _characterMaterial != null)
            {
                Renderer[] renderers = targetForMaterials.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = _characterMaterial;
                }
            }
            else
            {
                // 펭귄, 스켈레톤, 고양이 등 자체 모델 머티리얼: URP 셰이더 호환성 보장
                EnsureURPMaterials(targetForMaterials, charInfo);
            }

            // 4. 애니메이터 비활성화 (Crossy Road 물리 바운스/스쿼시 보존)
            Animator anim = newVisual.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
            }

            // 5. GridPlayerController에 새 VisualModel 연결 및 팝 이펙트
            player.SetVisualModel(newVisual.transform, playPopEffect, charInfo.customScale);
        }

        private void EnsureURPMaterials(GameObject target, CharacterInfo charInfo)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");

            bool isPenguin = (charInfo != null && charInfo.id == "char_penguin") || target.name.ToLower().Contains("penguin");
            bool isSkeleton = (charInfo != null && charInfo.id == "char_skeleton") || target.name.ToLower().Contains("skeleton");
            bool isCat = (charInfo != null && charInfo.id == "char_cat") || target.name.ToLower().Contains("cat");

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterials == null) continue;

                // 이미 고유 카툰 컬러가 지정된 이목구비 및 신체 부위는 머티리얼 보존!
                if (r.name.StartsWith("Eye") || r.name.StartsWith("Light") || r.name.StartsWith("Nose") ||
                    r.name.StartsWith("Whisker") || r.name.StartsWith("Socket") || r.name.StartsWith("Pupil") ||
                    r.name.StartsWith("EarInner") || r.name.StartsWith("Paw") || r.name.StartsWith("Belly"))
                {
                    continue;
                }

                Material[] origMats = r.sharedMaterials;
                Material[] newMats = new Material[origMats.Length];

                for (int m = 0; m < origMats.Length; m++)
                {
                    Material origMat = origMats[m];
                    if (origMat == null)
                    {
                        newMats[m] = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
                        continue;
                    }

                    // 런타임 인스턴스 머티리얼 복제 (원본 FBX 에셋 오염 방지)
                    Material mat = new Material(origMat);
                    string matName = origMat.name.ToLower();

                    // 1. 기존 텍스처 추출
                    Texture mainTex = mat.mainTexture;
                    if (mainTex == null && mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                    if (mainTex == null && mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

                    Color baseColor = Color.white;
                    float smoothness = 0.10f; // 기본 무광 매트 재질
                    bool isEye = false;

                    // [펭귄 전용 4색 멀티 머티리얼 완벽 복원]
                    if (isPenguin)
                    {
                        // 1. siyah: 펭귄의 검은 깃털/날개/머리 (다크 네이비 블랙)
                        if (matName.Contains("siyah") || matName.Contains("black") || matName.Contains("body"))
                        {
                            baseColor = new Color(0.12f, 0.15f, 0.22f, 1f);
                            smoothness = 0.10f;
                        }
                        // 2. beyaz: 펭귄의 흰 배 (깨끗한 순백색)
                        else if (matName.Contains("beyaz") || matName.Contains("white") || matName.Contains("belly"))
                        {
                            baseColor = new Color(0.96f, 0.97f, 0.99f, 1f);
                            smoothness = 0.10f;
                        }
                        // 3. turuncu: 펭귄의 주황 부리/발 (선명한 당근 오렌지)
                        else if (matName.Contains("turuncu") || matName.Contains("orange") || matName.Contains("beak"))
                        {
                            baseColor = new Color(1.0f, 0.58f, 0.04f, 1f);
                            smoothness = 0.20f;
                        }
                        // 4. göz (터키어로 '눈', Eye): 펭귄의 까만 눈동자! (초롱초롱한 칠흑색 블랙)
                        //    - FBX 머티리얼 이름: "göz"
                        //    - UTF-8 바이트 깨짐 대응: göz, goz, eye, g..z, 또는 4번째 머티리얼(인덱스 3)
                        else if (matName.Contains("göz") || matName.Contains("goz") || matName.Contains("eye") || (matName.StartsWith("g") && matName.EndsWith("z")) || m == 3 || origMats.Length == 4)
                        {
                            baseColor = new Color(0.015f, 0.015f, 0.02f, 1f); // 선명한 칠흑색 눈동자!
                            smoothness = 0.85f; // 반짝이는 유리구슬 같은 눈동자 하이라이트!
                            isEye = true;
                        }
                        else
                        {
                            // 알 수 없는 머티리얼이더라도 펭귄에서 몸체/배/부리가 아니면 눈동자일 확률 100%
                            baseColor = new Color(0.015f, 0.015f, 0.02f, 1f);
                            smoothness = 0.85f;
                            isEye = true;
                        }
                    }
                    // [해골 전용 카툰 뼈 컬러]
                    else if (isSkeleton)
                    {
                        if (mainTex == null)
                        {
                            mainTex = Resources.Load<Texture2D>("Characters/Texture_BaseColor_v02");
#if UNITY_EDITOR
                            if (mainTex == null)
                            {
                                mainTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Texture_BaseColor_v02.png");
                            }
#endif
                        }
                        baseColor = new Color(0.93f, 0.91f, 0.85f, 1f); // 아이보리 뼈 색상
                        smoothness = 0.10f;
                    }
                    // [고양이 전용 포근한 웜그레이 푸신 컬러]
                    else if (isCat)
                    {
                        if (mainTex == null)
                        {
                            mainTex = Resources.Load<Texture2D>("Characters/Pusheen_BaseColor");
#if UNITY_EDITOR
                            if (mainTex == null)
                            {
                                mainTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Characters/Pusheen_BaseColor.png");
                            }
#endif
                        }
                        baseColor = mainTex != null ? Color.white : new Color(0.82f, 0.79f, 0.76f, 1f); // 텍스처 있으면 100% 원색 발색, 없으면 부드럽고 따뜻한 회색 털
                        smoothness = 0.15f;
                    }

                    // 3. URP Lit 셰이더 적용
                    if (urpLit != null)
                    {
                        mat.shader = urpLit;
                    }

                    // 4. 프로퍼티 재바인딩
                    if (mainTex != null && mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", mainTex);
                    }
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", baseColor);
                    }
                    if (mat.HasProperty("_Smoothness"))
                    {
                        mat.SetFloat("_Smoothness", smoothness);
                    }

                    // 눈동자, 고양이, 해골 머티리얼은 양면 렌더링(Cull Off)으로 어떤 노멀 방향이나 각도에서도 100% 가시성 보장!
                    if ((isEye || isCat || isSkeleton) && mat.HasProperty("_Cull"))
                    {
                        mat.SetFloat("_Cull", 0f); // 0 = Off (양면 렌더링)
                    }

                    newMats[m] = mat;
                }

                r.materials = newMats;
            }
        }

        public CharacterInfo GetCharacterAt(int index)
        {
            if (_characters == null || index < 0 || index >= _characters.Length) return null;
            return _characters[index];
        }

        /// <summary>
        /// Blender에서 임포트된 고양이 모델의 회전(누워있음 방지), 스케일(2cm 축소 방지 -> 0.78m 로컬 높이 -> 0.67m 인게임 높이), 바닥 착지를 완벽 자동 보정합니다.
        /// </summary>
        private void SetupCatModelInWrapper(GameObject catModel)
        {
            if (catModel == null) return;

            Renderer[] rends = catModel.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;

            // 1. 현재 바운드 계산
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            // 모델이 앞으로 엎어져 누워있는 경우 (Z 길이가 Y 높이보다 긴 경우) 직립 회전 보정
            if (b.size.z > b.size.y * 1.15f)
            {
                catModel.transform.localRotation *= Quaternion.Euler(90f, 0f, 0f);
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.z > b.size.y * 1.15f)
                {
                    catModel.transform.localRotation *= Quaternion.Euler(-180f, 0f, 0f);
                    b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                }
            }

            // 2. 목표 높이: 부모의 baseScale(0.86) 적용 후 최종 인게임 높이가 0.67m(Kenney 캐릭터)가 되도록 로컬 높이를 0.78m로 설정
            float currentHeight = b.size.y;
            float targetLocalHeight = 0.78f;
            if (currentHeight > 0.0001f)
            {
                float multiplier = targetLocalHeight / currentHeight;
                catModel.transform.localScale = Vector3.Scale(catModel.transform.localScale, new Vector3(multiplier, multiplier, multiplier));
            }

            // 3. 지면(발바닥) 착지 보정: 모델 최하단(min.y)이 부모 피벗의 Y=0(지면)에 일치하도록 이동
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float bottomOffset = catModel.transform.parent.position.y - b.min.y;
            catModel.transform.position += Vector3.up * bottomOffset;
        }

        /// <summary>
        /// 텍스처가 누락된 카툰 해골 모델의 얼굴에 인게임 쿼터뷰 카메라에서도 100% 뚜렷하게 보이는
        /// 강렬한 카툰 눈(Stylized Skull Eyes & Glowing Pupil)을 3D로 장착합니다.
        /// 두개골 뼈 표면 안쪽 안와에 쏙 들어가도록 깊이를 정밀하게 맞춰 돌출을 방지합니다.
        /// </summary>
        private void AttachSkeletonEyes(GameObject skeletonVisual)
        {
            if (skeletonVisual == null) return;

            // 기존 눈 루트가 있다면 제거
            Transform oldEyes = skeletonVisual.transform.Find("Skeleton_Eyes_Root");
            if (oldEyes != null) Destroy(oldEyes.gameObject);

            GameObject eyesRoot = new GameObject("Skeleton_Eyes_Root");
            eyesRoot.transform.SetParent(skeletonVisual.transform, false); // 본 회전 왜곡 원천 차단!

            // 머리 본 위치 탐색 (로컬 Y/Z 기준점)
            Transform head = FindChildRecursively(skeletonVisual.transform, "Joint_Head");
            float headCenterY = 2.45f; // 기본 두개골 중심 높이
            float headCenterZ = 0f;
            if (head != null)
            {
                Vector3 headLocalPos = skeletonVisual.transform.InverseTransformPoint(head.position);
                headCenterY = headLocalPos.y + 0.30f; // 목(Head) 위쪽 두개골 중심
                headCenterZ = headLocalPos.z;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");

            // 1. 깊은 눈구멍 (검은 안와) 머티리얼 - 칠흑색 블랙 (무광)
            Material socketMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            Color socketColor = new Color(0.02f, 0.02f, 0.03f, 1f);
            socketMat.color = socketColor;
            if (socketMat.HasProperty("_BaseColor")) socketMat.SetColor("_BaseColor", socketColor);
            if (socketMat.HasProperty("_Smoothness")) socketMat.SetFloat("_Smoothness", 0.05f);
            if (socketMat.HasProperty("_Cull")) socketMat.SetFloat("_Cull", 0f);

            // 2. 강렬하고 눈부신 카툰 붉은 안광 머티리얼 (자체 발광 Emission 적용)
            Material pupilMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            Color pupilColor = new Color(1.0f, 0.12f, 0.12f, 1f); // 선명한 진홍색
            pupilMat.color = pupilColor;
            if (pupilMat.HasProperty("_BaseColor")) pupilMat.SetColor("_BaseColor", pupilColor);
            if (pupilMat.HasProperty("_Smoothness")) pupilMat.SetFloat("_Smoothness", 0.90f);
            pupilMat.EnableKeyword("_EMISSION");
            if (pupilMat.HasProperty("_EmissionColor"))
            {
                pupilMat.SetColor("_EmissionColor", pupilColor * 3.0f); // 어두운 곳에서도 환하게 빛남!
            }
            if (pupilMat.HasProperty("_Cull")) pupilMat.SetFloat("_Cull", 0f);

            // 두개골 앞면 안와 속에 쏙 들어간 자연스러운 카툰 눈 배치 (돌출 방지: 머리 본 중심 기준 +0.11m로 대폭 매립)
            float eyeSpacing = 0.16f; // 좌우 간격
            float eyeForward = headCenterZ + 0.11f; // 두개골 뼈 안쪽 안와 구멍 속에 쏙 들어가는 완벽 매립 깊이!
            float eyeY = headCenterY - 0.02f;

            // Z축 두께를 0.04m -> 0.012m로 초박형 밀착하여 튀어나오는 이질감 100% 제거
            Vector3 socketScale = new Vector3(0.18f, 0.22f, 0.012f); // 얇고 평평하게 밀착된 검은 안와
            Vector3 pupilScale = new Vector3(0.085f, 0.10f, 0.012f);  // 안와 속에 쏙 박힌 붉은 안광

            // 좌측 눈 (안와 + 붉은 안광)
            Vector3 leftSocket = new Vector3(-eyeSpacing, eyeY, eyeForward);
            Vector3 leftPupil = new Vector3(-eyeSpacing, eyeY, eyeForward + 0.003f);
            CreateLocalPart(eyesRoot.transform, leftSocket, socketScale, socketMat, "Socket_Left");
            CreateLocalPart(eyesRoot.transform, leftPupil, pupilScale, pupilMat, "Pupil_Left");

            // 우측 눈 (안와 + 붉은 안광)
            Vector3 rightSocket = new Vector3(eyeSpacing, eyeY, eyeForward);
            Vector3 rightPupil = new Vector3(eyeSpacing, eyeY, eyeForward + 0.003f);
            CreateLocalPart(eyesRoot.transform, rightSocket, socketScale, socketMat, "Socket_Right");
            CreateLocalPart(eyesRoot.transform, rightPupil, pupilScale, pupilMat, "Pupil_Right");
        }

        /// <summary>
        /// 고양이 모델에 인게임 쿼터뷰에서 앙증맞고 사랑스러운 고양이 카툰 얼굴(큰 눈망울, 하이라이트, 분홍 코)을 3D로 장착합니다.
        /// 고양이 모델의 실제 바운드를 기준으로 앞면 표면에 정밀 밀착 배치합니다.
        /// </summary>
        private void AttachCatFace(GameObject catVisual)
        {
            if (catVisual == null) return;

            Transform oldFace = catVisual.transform.Find("Cat_Face_Root");
            if (oldFace != null) Destroy(oldFace.gameObject);

            Renderer[] rends = catVisual.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            GameObject faceRoot = new GameObject("Cat_Face_Root");
            faceRoot.transform.SetParent(catVisual.transform, true);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");

            // 1. 초롱초롱한 까만 눈망울 머티리얼 (유리구슬 광택)
            Material eyeMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            Color eyeColor = new Color(0.05f, 0.05f, 0.06f, 1f);
            eyeMat.color = eyeColor;
            if (eyeMat.HasProperty("_BaseColor")) eyeMat.SetColor("_BaseColor", eyeColor);
            if (eyeMat.HasProperty("_Smoothness")) eyeMat.SetFloat("_Smoothness", 0.95f);
            if (eyeMat.HasProperty("_Cull")) eyeMat.SetFloat("_Cull", 0f);

            // 2. 반짝이는 눈망울 흰색 하이라이트 머티리얼
            Material highlightMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            highlightMat.color = Color.white;
            if (highlightMat.HasProperty("_BaseColor")) highlightMat.SetColor("_BaseColor", Color.white);
            if (highlightMat.HasProperty("_Smoothness")) highlightMat.SetFloat("_Smoothness", 0.95f);
            if (highlightMat.HasProperty("_Cull")) highlightMat.SetFloat("_Cull", 0f);

            // 3. 앙증맞은 베이비 핑크 삼각 코 머티리얼
            Material noseMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            Color pinkColor = new Color(0.98f, 0.52f, 0.62f, 1f);
            noseMat.color = pinkColor;
            if (noseMat.HasProperty("_BaseColor")) noseMat.SetColor("_BaseColor", pinkColor);
            if (noseMat.HasProperty("_Smoothness")) noseMat.SetFloat("_Smoothness", 0.30f);
            if (noseMat.HasProperty("_Cull")) noseMat.SetFloat("_Cull", 0f);

            // 4. 고양이 수염 머티리얼 (다크 그레이)
            Material whiskerMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            Color whiskerColor = new Color(0.20f, 0.20f, 0.22f, 1f);
            whiskerMat.color = whiskerColor;
            if (whiskerMat.HasProperty("_BaseColor")) whiskerMat.SetColor("_BaseColor", whiskerColor);

            // 고양이 바운드 중심 및 정면 표면 기준 계산
            Vector3 center = b.center;
            float eyeSpacing = b.size.x * 0.18f;
            float eyeY = center.y + b.size.y * 0.08f;
            float faceZ = b.max.z - 0.005f;

            float eyeRadius = b.size.y * 0.08f;
            Vector3 eyeScale = new Vector3(eyeRadius, eyeRadius * 1.15f, eyeRadius * 0.6f);
            Vector3 lightScale = eyeScale * 0.35f;

            // 좌측 눈 & 하이라이트
            CreateWorldPart(faceRoot.transform, new Vector3(center.x - eyeSpacing, eyeY, faceZ), eyeScale, eyeMat, "Eye_Left");
            CreateWorldPart(faceRoot.transform, new Vector3(center.x - eyeSpacing + eyeRadius * 0.2f, eyeY + eyeRadius * 0.25f, faceZ + 0.015f), lightScale, highlightMat, "Light_Left");

            // 우측 눈 & 하이라이트
            CreateWorldPart(faceRoot.transform, new Vector3(center.x + eyeSpacing, eyeY, faceZ), eyeScale, eyeMat, "Eye_Right");
            CreateWorldPart(faceRoot.transform, new Vector3(center.x + eyeSpacing - eyeRadius * 0.2f, eyeY + eyeRadius * 0.25f, faceZ + 0.015f), lightScale, highlightMat, "Light_Right");

            // 앙증맞은 분홍 코
            Vector3 noseScale = new Vector3(eyeRadius * 0.8f, eyeRadius * 0.6f, eyeRadius * 0.5f);
            float noseY = eyeY - eyeRadius * 0.9f;
            CreateWorldPart(faceRoot.transform, new Vector3(center.x, noseY, faceZ + 0.02f), noseScale, noseMat, "Nose_Pink");

            // 수염 4줄
            Vector3 whiskerScale = new Vector3(eyeSpacing * 1.1f, 0.012f, 0.012f);
            CreateWorldPart(faceRoot.transform, new Vector3(center.x - eyeSpacing * 1.5f, noseY + 0.015f, faceZ - 0.01f), whiskerScale, whiskerMat, "Whisker_L1");
            CreateWorldPart(faceRoot.transform, new Vector3(center.x - eyeSpacing * 1.45f, noseY - 0.025f, faceZ - 0.01f), whiskerScale, whiskerMat, "Whisker_L2");
            CreateWorldPart(faceRoot.transform, new Vector3(center.x + eyeSpacing * 1.5f, noseY + 0.015f, faceZ - 0.01f), whiskerScale, whiskerMat, "Whisker_R1");
            CreateWorldPart(faceRoot.transform, new Vector3(center.x + eyeSpacing * 1.45f, noseY - 0.025f, faceZ - 0.01f), whiskerScale, whiskerMat, "Whisker_R2");
        }

        private void CreateWorldPart(Transform parent, Vector3 worldPos, Vector3 scale, Material mat, string name)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.position = worldPos;
            part.transform.localScale = scale;
            part.transform.SetParent(parent, true); // 월드 위치 보존하며 parent 바인딩

            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void CreateLocalPart(Transform parent, Vector3 localPos, Vector3 localScale, Material mat, string name)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.SetParent(parent, false); // 먼저 부모 바인딩하여 스케일 왜곡 방지!
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;

            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private static Transform FindChildRecursively(Transform parent, string childName)
        {
            if (parent == null) return null;
            if (parent.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursively(parent.GetChild(i), childName);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject _cachedProceduralCatPrefab;

        /// <summary>
        /// CampusRun 캐주얼 러너 스타일에 최적화된 고품질 3D 카툰 고양이 프리팹을 빌드합니다.
        /// 외부 FBX 임포트 오류나 크래시에 면역이며, 일반 캐릭터(0.65m)와 1:1 완벽 호환되는 표준 크기와 발바닥 착지를 보장합니다.
        /// </summary>
        public static GameObject CreateProceduralCatPrefab()
        {
            if (_cachedProceduralCatPrefab != null) return _cachedProceduralCatPrefab;

            GameObject root = new GameObject("Cat_Procedural_Prefab");
            root.SetActive(false); // 템플릿용 비활성화
            DontDestroyOnLoad(root);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (urpLit == null) urpLit = Shader.Find("Diffuse");

            // 머티리얼 1: 포근하고 따뜻한 웜그레이 털 (푸신 캣 스타일)
            Material furMat = new Material(urpLit);
            Color furColor = new Color(0.82f, 0.79f, 0.76f, 1f);
            furMat.color = furColor;
            if (furMat.HasProperty("_BaseColor")) furMat.SetColor("_BaseColor", furColor);
            if (furMat.HasProperty("_Smoothness")) furMat.SetFloat("_Smoothness", 0.15f);

            // 머티리얼 2: 하얀 찹쌀떡 양말 발 & 하얀 배
            Material whiteMat = new Material(urpLit);
            Color whiteColor = new Color(0.96f, 0.96f, 0.98f, 1f);
            whiteMat.color = whiteColor;
            if (whiteMat.HasProperty("_BaseColor")) whiteMat.SetColor("_BaseColor", whiteColor);
            if (whiteMat.HasProperty("_Smoothness")) whiteMat.SetFloat("_Smoothness", 0.20f);

            // 머티리얼 3: 귓속 & 앙증맞은 코 (베이비 핑크)
            Material pinkMat = new Material(urpLit);
            Color pinkColor = new Color(0.98f, 0.58f, 0.65f, 1f);
            pinkMat.color = pinkColor;
            if (pinkMat.HasProperty("_BaseColor")) pinkMat.SetColor("_BaseColor", pinkColor);
            if (pinkMat.HasProperty("_Smoothness")) pinkMat.SetFloat("_Smoothness", 0.30f);

            // 머티리얼 4: 초롱초롱한 까만 눈망울 (유리구슬 광택)
            Material eyeMat = new Material(urpLit);
            Color eyeColor = new Color(0.04f, 0.04f, 0.05f, 1f);
            eyeMat.color = eyeColor;
            if (eyeMat.HasProperty("_BaseColor")) eyeMat.SetColor("_BaseColor", eyeColor);
            if (eyeMat.HasProperty("_Smoothness")) eyeMat.SetFloat("_Smoothness", 0.95f);

            // 머티리얼 5: 반짝이는 흰색 하이라이트
            Material highlightMat = new Material(urpLit);
            highlightMat.color = Color.white;
            if (highlightMat.HasProperty("_BaseColor")) highlightMat.SetColor("_BaseColor", Color.white);
            if (highlightMat.HasProperty("_Smoothness")) highlightMat.SetFloat("_Smoothness", 0.95f);

            // 머티리얼 6: 앙증맞은 수염 (다크 그레이)
            Material whiskerMat = new Material(urpLit);
            Color whiskerColor = new Color(0.20f, 0.20f, 0.22f, 1f);
            whiskerMat.color = whiskerColor;
            if (whiskerMat.HasProperty("_BaseColor")) whiskerMat.SetColor("_BaseColor", whiskerColor);

            // 1. 몸통 (통통하고 둥글넙적한 몸체)
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Body", new Vector3(0f, 0.28f, -0.02f), new Vector3(0.44f, 0.38f, 0.52f), Quaternion.identity, furMat);

            // 2. 하얀 배 (보들보들한 화이트 패치)
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Belly", new Vector3(0f, 0.20f, 0.02f), new Vector3(0.36f, 0.24f, 0.44f), Quaternion.identity, whiteMat);

            // 3. 머리 (귀여운 동글이 두상)
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.46f, 0.16f), new Vector3(0.42f, 0.36f, 0.36f), Quaternion.identity, furMat);

            // 4. 귀 (좌/우 쫑긋한 삼각 귀)
            CreateCatPart(root.transform, PrimitiveType.Cube, "Ear_Left", new Vector3(-0.13f, 0.62f, 0.16f), new Vector3(0.10f, 0.14f, 0.08f), Quaternion.Euler(0f, 0f, 18f), furMat);
            CreateCatPart(root.transform, PrimitiveType.Cube, "Ear_Right", new Vector3(0.13f, 0.62f, 0.16f), new Vector3(0.10f, 0.14f, 0.08f), Quaternion.Euler(0f, 0f, -18f), furMat);
            CreateCatPart(root.transform, PrimitiveType.Cube, "EarInner_Left", new Vector3(-0.13f, 0.61f, 0.20f), new Vector3(0.06f, 0.09f, 0.02f), Quaternion.Euler(0f, 0f, 18f), pinkMat);
            CreateCatPart(root.transform, PrimitiveType.Cube, "EarInner_Right", new Vector3(0.13f, 0.61f, 0.20f), new Vector3(0.06f, 0.09f, 0.02f), Quaternion.Euler(0f, 0f, -18f), pinkMat);

            // 5. 하얀 찹쌀떡 발 4개 (지면 Y=0 완벽 착지)
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Paw_FL", new Vector3(-0.14f, 0.07f, 0.16f), new Vector3(0.13f, 0.14f, 0.14f), Quaternion.identity, whiteMat);
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Paw_FR", new Vector3(0.14f, 0.07f, 0.16f), new Vector3(0.13f, 0.14f, 0.14f), Quaternion.identity, whiteMat);
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Paw_BL", new Vector3(-0.14f, 0.07f, -0.20f), new Vector3(0.13f, 0.14f, 0.14f), Quaternion.identity, whiteMat);
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Paw_BR", new Vector3(0.14f, 0.07f, -0.20f), new Vector3(0.13f, 0.14f, 0.14f), Quaternion.identity, whiteMat);

            // 6. 꼬리 (위로 앙증맞게 말린 꼬리)
            CreateCatPart(root.transform, PrimitiveType.Cylinder, "Tail", new Vector3(0f, 0.32f, -0.28f), new Vector3(0.07f, 0.11f, 0.07f), Quaternion.Euler(40f, 0f, 0f), furMat);

            // 7. 얼굴 이목구비
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Eye_Left", new Vector3(-0.10f, 0.46f, 0.33f), new Vector3(0.065f, 0.065f, 0.02f), Quaternion.identity, eyeMat);
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Light_Left", new Vector3(-0.085f, 0.48f, 0.34f), new Vector3(0.022f, 0.022f, 0.01f), Quaternion.identity, highlightMat);
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Eye_Right", new Vector3(0.10f, 0.46f, 0.33f), new Vector3(0.065f, 0.065f, 0.02f), Quaternion.identity, eyeMat);
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Light_Right", new Vector3(0.085f, 0.48f, 0.34f), new Vector3(0.022f, 0.022f, 0.01f), Quaternion.identity, highlightMat);
            CreateCatPart(root.transform, PrimitiveType.Sphere, "Nose_Pink", new Vector3(0f, 0.41f, 0.34f), new Vector3(0.05f, 0.035f, 0.02f), Quaternion.identity, pinkMat);

            // 8. 수염 4가닥
            CreateCatPart(root.transform, PrimitiveType.Cube, "Whisker_L1", new Vector3(-0.18f, 0.42f, 0.32f), new Vector3(0.11f, 0.012f, 0.012f), Quaternion.identity, whiskerMat);
            CreateCatPart(root.transform, PrimitiveType.Cube, "Whisker_L2", new Vector3(-0.17f, 0.38f, 0.32f), new Vector3(0.11f, 0.012f, 0.012f), Quaternion.identity, whiskerMat);
            CreateCatPart(root.transform, PrimitiveType.Cube, "Whisker_R1", new Vector3(0.18f, 0.42f, 0.32f), new Vector3(0.11f, 0.012f, 0.012f), Quaternion.identity, whiskerMat);
            CreateCatPart(root.transform, PrimitiveType.Cube, "Whisker_R2", new Vector3(0.17f, 0.38f, 0.32f), new Vector3(0.11f, 0.012f, 0.012f), Quaternion.identity, whiskerMat);

            _cachedProceduralCatPrefab = root;
            return root;
        }

        private static GameObject CreateCatPart(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = localRot;
            part.transform.localScale = localScale;

            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Renderer rend = part.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
            }
            return part;
        }
    }
}
