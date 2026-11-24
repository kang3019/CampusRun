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
        /// 5종 캐릭터 프리셋(민우, 수아, 준호, 지민, 펭귄) 데이터를 초기화합니다.
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
                    customScale: 0.95f
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
                EnsureURPMaterials(newVisual);
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

        private void EnsureURPMaterials(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r.sharedMaterials == null) continue;

                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && (mats[m].shader == null || mats[m].shader.name == "Standard" || mats[m].shader.name.StartsWith("Legacy")))
                    {
                        if (urpLit != null)
                        {
                            mats[m].shader = urpLit;
                        }
                    }
                }
                r.sharedMaterials = mats;
            }
        }

        public CharacterInfo GetCharacterAt(int index)
        {
            if (_characters == null || index < 0 || index >= _characters.Length) return null;
            return _characters[index];
        }
    }
}
