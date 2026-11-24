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

            // 2. 새 캐릭터 모델 인스턴스화
            GameObject newVisual = Instantiate(charInfo.modelPrefab, playerTransform);
            newVisual.name = "VisualModel";
            newVisual.transform.localPosition = Vector3.zero;
            newVisual.transform.localRotation = Quaternion.identity;

            // 디버깅: 모든 자식 렌더러 및 머티리얼 로그 기록
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[DebugModel] Selected: {charInfo.id}, Prefab: {charInfo.modelPrefab.name}");
                Renderer[] allR = newVisual.GetComponentsInChildren<Renderer>(true);
                foreach (var r in allR)
                {
                    sb.AppendLine($" Renderer: '{r.name}' ({r.GetType().Name}), enabled={r.enabled}");
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
                Renderer[] renderers = newVisual.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterial = _characterMaterial;
                }
            }
            else
            {
                // 펭귄 등 자체 모델 머티리얼: URP 셰이더 호환성 보장
                EnsureURPMaterials(newVisual, charInfo);
            }

            // 스켈레톤의 경우 전용 3D 카툰 눈구멍 및 안광 장착
            if (charInfo.id == "char_skeleton")
            {
                AttachSkeletonEyes(newVisual);
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

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterials == null) continue;

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

                    // 눈동자 머티리얼은 양면 렌더링(Cull Off)으로 어떤 각도나 노멀 방향에서도 100% 렌더링 보장!
                    if (isEye && mat.HasProperty("_Cull"))
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
        /// 텍스처가 누락된 카툰 해골 모델의 얼굴 정면에 앙증맞고 또렷한 카툰 눈(Stylized Skull Eyes)을 3D로 장착합니다.
        /// </summary>
        private void AttachSkeletonEyes(GameObject skeletonVisual)
        {
            if (skeletonVisual == null) return;

            Transform head = FindChildRecursively(skeletonVisual.transform, "Joint_Head");
            if (head == null) head = FindChildRecursively(skeletonVisual.transform, "Head");
            if (head == null) return;

            // 기존 눈이 있다면 제거
            Transform oldEyes = skeletonVisual.transform.Find("Skeleton_Eyes_Root");
            if (oldEyes != null) Destroy(oldEyes.gameObject);

            GameObject eyesRoot = new GameObject("Skeleton_Eyes_Root");
            eyesRoot.transform.SetParent(head, false);

            // 머리 크기(높이) 계산
            Transform headEnd = FindChildRecursively(skeletonVisual.transform, "Joint_HeadEnd");
            float headHeight = 0.28f;
            if (headEnd != null)
            {
                headHeight = Vector3.Distance(head.position, headEnd.position);
                if (headHeight < 0.12f || headHeight > 0.8f) headHeight = 0.28f;
            }

            // 1. 눈구멍(검은 안와) 머티리얼 - 칠흑색 블랙
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
            Material socketMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            socketMat.color = new Color(0.02f, 0.02f, 0.03f, 1f);
            if (socketMat.HasProperty("_BaseColor")) socketMat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.03f, 1f));
            if (socketMat.HasProperty("_Smoothness")) socketMat.SetFloat("_Smoothness", 0.1f);
            if (socketMat.HasProperty("_Cull")) socketMat.SetFloat("_Cull", 0f);

            // 2. 눈 중심 붉은 안광 머티리얼 (UI 썸네일과 일치하는 카툰 포인트)
            Material pupilMat = new Material(urpLit != null ? urpLit : Shader.Find("Diffuse"));
            Color pupilColor = new Color(0.98f, 0.22f, 0.22f, 1f); // 선명한 카툰 레드
            pupilMat.color = pupilColor;
            if (pupilMat.HasProperty("_BaseColor")) pupilMat.SetColor("_BaseColor", pupilColor);
            if (pupilMat.HasProperty("_EmissionColor"))
            {
                pupilMat.EnableKeyword("_EMISSION");
                pupilMat.SetColor("_EmissionColor", pupilColor * 1.5f);
            }

            // 플레이어 정면, 위, 우측 벡터 (월드 좌표 기준 계산으로 본 회전축 왜곡 방지)
            Vector3 forward = skeletonVisual.transform.forward;
            Vector3 up = skeletonVisual.transform.up;
            Vector3 right = skeletonVisual.transform.right;

            Vector3 headCenter = head.position + up * (headHeight * 0.40f);
            float eyeSpacing = headHeight * 0.22f; // 좌우 간격
            float eyeForwardDist = headHeight * 0.33f; // 얼굴 전면 돌출

            Vector3 leftPos = headCenter - right * eyeSpacing + forward * eyeForwardDist;
            Vector3 rightPos = headCenter + right * eyeSpacing + forward * eyeForwardDist;

            Vector3 socketScale = new Vector3(headHeight * 0.22f, headHeight * 0.26f, headHeight * 0.16f);
            Vector3 pupilScale = socketScale * 0.45f;

            // 좌측 눈 (안와 + 안광)
            CreateEyePart(eyesRoot.transform, leftPos, socketScale, socketMat, "Socket_Left");
            CreateEyePart(eyesRoot.transform, leftPos + forward * (headHeight * 0.04f), pupilScale, pupilMat, "Pupil_Left");

            // 우측 눈 (안와 + 안광)
            CreateEyePart(eyesRoot.transform, rightPos, socketScale, socketMat, "Socket_Right");
            CreateEyePart(eyesRoot.transform, rightPos + forward * (headHeight * 0.04f), pupilScale, pupilMat, "Pupil_Right");
        }

        private void CreateEyePart(Transform parent, Vector3 worldPos, Vector3 scale, Material mat, string name)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            part.name = name;
            part.transform.position = worldPos;
            part.transform.localScale = scale;
            part.transform.SetParent(parent, true); // 월드 위치 보존하며 parent에 바인딩

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
    }
}
