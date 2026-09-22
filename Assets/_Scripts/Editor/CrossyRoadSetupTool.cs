using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CampusRun.Player;
using CampusRun.Obstacles;
using CampusRun.Track;

namespace CampusRun.EditorTools
{
    /// <summary>
    /// 길건너 친구들(Crossy Road) 스타일의 남자 복셀 캐릭터와 3D 버스 모델 및 환경 머티리얼을
    /// 프로젝트 프리팹과 테스트 씬에 자동으로 구성하고 연결해주는 에디터 유틸리티입니다.
    /// </summary>
    public static class CrossyRoadSetupTool
    {
        private const string PrefsKey = "CampusRun_CrossyRoadSetup_Executed_v5";

        private const string CharacterFbxPath = "Assets/Characters/kenney_mini-characters/Models/FBX format/character-male-a.fbx";
        private const string CharacterTexturePath = "Assets/Characters/kenney_mini-characters/Models/FBX format/Textures/colormap.png";

        private const string BusFbxPath = "Assets/polycar/lowpoly_bus/source/bus.fbx";
        private const string BusTexturePath = "Assets/polycar/lowpoly_bus/textures/ImphenziaPalette01.png";

        private const string BusPrefabPath = "Assets/_Prefabs/Obstacles/PF_Bus.prefab";
        private const string RoadLanePrefabPath = "Assets/_Prefabs/Environment/PF_RoadLane.prefab";
        private const string SafeLanePrefabPath = "Assets/_Prefabs/Environment/PF_SafeLane.prefab";
        private const string TestScenePath = "Assets/_Scenes/Sandboxes/Sandbox_PlayTest.unity";

        private const string MatCharPath = "Assets/_Materials/M_Character_Male.mat";
        private const string MatBusPath = "Assets/_Materials/M_Bus_Imphenzia.mat";
        private const string MatRoadPath = "Assets/_Materials/M_Road_Asphalt.mat";
        private const string MatGrassPath = "Assets/_Materials/M_Grass_Vibrant.mat";

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            if (!EditorPrefs.GetBool(PrefsKey, false))
            {
                EditorPrefs.SetBool(PrefsKey, true);
                EditorApplication.delayCall += () =>
                {
                    SetupCrossyRoadAssets();
                };
            }
        }

        [MenuItem("CampusRun/길건너 친구들 에셋 세팅 (캐릭터 & 버스)")]
        public static void SetupCrossyRoadAssets()
        {
            Debug.Log("[CrossyRoadSetupTool] === 길건너 친구들 에셋 세팅 시작 ===");

            // 1. 머티리얼 준비
            Material matChar = EnsureMaterial(MatCharPath, CharacterTexturePath, Color.white, 0f);
            Material matBus = EnsureMaterial(MatBusPath, BusTexturePath, Color.white, 0.1f);
            Material matRoad = EnsureMaterial(MatRoadPath, null, new Color(0.18f, 0.18f, 0.20f, 1f), 0.1f);
            Material matGrass = EnsureMaterial(MatGrassPath, null, new Color(0.32f, 0.68f, 0.32f, 1f), 0.05f);

            // 2. 버스 프리팹 (PF_Bus.prefab) 세팅
            SetupBusPrefab(matBus);

            // 3. 도로 및 안전지대 레인 프리팹 머티리얼 세팅
            SetupLanePrefabs(matRoad, matGrass);

            // 4. 테스트 씬의 플레이어 캐릭터 세팅
            SetupPlayerInScene(matChar);

            // 5. 테스트 씬의 UI 매니저 및 캔버스 세팅
            SetupUIInScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CrossyRoadSetupTool] === 길건너 친구들 에셋 세팅 완료! ===");
        }

        private static Material EnsureMaterial(string matPath, string texPath, Color baseColor, float smoothness)
        {
            string dir = Path.GetDirectoryName(matPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader == null)
            {
                urpLitShader = Shader.Find("Standard");
            }

            if (mat == null)
            {
                mat = new Material(urpLitShader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = urpLitShader;
            }

            mat.color = baseColor;
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (!string.IsNullOrEmpty(texPath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    if (mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", tex);
                    }
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetupBusPrefab(Material matBus)
        {
            GameObject busFbx = AssetDatabase.LoadAssetAtPath<GameObject>(BusFbxPath);
            if (busFbx == null)
            {
                Debug.LogError($"[CrossyRoadSetupTool] 버스 FBX를 찾을 수 없습니다: {BusFbxPath}");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(BusPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[CrossyRoadSetupTool] 버스 프리팹을 찾을 수 없습니다: {BusPrefabPath}");
                return;
            }

            // 기존 임시 큐브 메시 제거
            MeshFilter oldMf = prefabRoot.GetComponent<MeshFilter>();
            if (oldMf != null) Object.DestroyImmediate(oldMf);

            MeshRenderer oldMr = prefabRoot.GetComponent<MeshRenderer>();
            if (oldMr != null) Object.DestroyImmediate(oldMr);

            // 기존 자식 VisualModel 정리
            Transform oldVisual = prefabRoot.transform.Find("VisualModel");
            if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

            // FBX 모델을 자식으로 인스턴스화
            GameObject visual = Object.Instantiate(busFbx, prefabRoot.transform);
            visual.name = "VisualModel";

            // 머티리얼 적용
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = matBus;
            }

            // lowpoly_bus 원본 모델 방향 분석:
            // FL/FR(앞바퀴)이 -Z에 위치하므로, +Z(진행 방향)를 향하도록 Y축 180도 회전
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // 원본 메시 크기 기준: X(폭)=7.83, Y(높이)=3.44, Z(길이)=14.63
            // 길건너 친구들(Crossy Road) 스타일의 이상적인 셔틀버스 비율 설정:
            // 1) 차폭: 0.78m (도로 레인 1타일 폭 1.0m에 쏙 맞춤)
            // 2) 높이: 0.92m (기존 0.33m 지네벌레 형태 탈피 -> 사람 키 1.0m와 비슷하게 듬직한 버스 높이)
            // 3) 길이: 2.20m (2타일 정도를 커버하는 귀여운 미니 셔틀버스 길이)
            float rawWidth = 7.83f;
            float rawHeight = 3.44f;
            float rawLength = 14.63f;

            // 캐릭터(약 0.7m x 0.7m x 0.9m)보다 조금 더 큰 앙증맞은 미니 셔틀버스 비율
            float targetWidth = 0.78f;
            float targetHeight = 0.85f;
            float targetLength = 1.15f;

            float scaleX = targetWidth / rawWidth;
            float scaleY = targetHeight / rawHeight;
            float scaleZ = targetLength / rawLength;

            visual.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

            // 바퀴 바닥면(Y ≈ -2.8f)을 루트의 로컬 Y=0에 정확히 맞추기 위한 수직 오프셋
            // 바닥면이 Y=0에 닿도록 시각적 모델을 살짝 올려줌
            float wheelBottomLocalY = -2.8f * scaleY; // 약 -0.75m
            visual.transform.localPosition = new Vector3(0f, -wheelBottomLocalY, 0f);

            // 루트 콜라이더 세팅 (Tag: Obstacle, isTrigger: true)
            prefabRoot.tag = "Obstacle";
            BoxCollider col = prefabRoot.GetComponent<BoxCollider>();
            if (col == null) col = prefabRoot.AddComponent<BoxCollider>();
            col.isTrigger = true;

            // 이동 시 로컬 Z축이 앞뒤(길이), X축이 좌우(폭)
            col.size = new Vector3(targetWidth, targetHeight, targetLength);
            col.center = new Vector3(0f, targetHeight * 0.5f, 0f);

            // 루트 Transform 스케일 정규화
            prefabRoot.transform.localScale = Vector3.one;

            // MovingVehicle 컴포넌트 확인
            MovingVehicle vehicle = prefabRoot.GetComponent<MovingVehicle>();
            if (vehicle == null) prefabRoot.AddComponent<MovingVehicle>();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, BusPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log($"[CrossyRoadSetupTool] PF_Bus.prefab 설정 완료 (방향: 전방 정렬, 크기: 폭 {targetWidth:F2}m, 높이 {targetHeight:F2}m, 길이 {targetLength:F2}m)");
        }

        private static void SetupLanePrefabs(Material matRoad, Material matGrass)
        {
            // 도로 레인
            GameObject roadRoot = PrefabUtility.LoadPrefabContents(RoadLanePrefabPath);
            if (roadRoot != null)
            {
                MeshRenderer mr = roadRoot.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = matRoad;
                PrefabUtility.SaveAsPrefabAsset(roadRoot, RoadLanePrefabPath);
                PrefabUtility.UnloadPrefabContents(roadRoot);
                Debug.Log("[CrossyRoadSetupTool] PF_RoadLane.prefab 머티리얼 적용 완료");
            }

            // 안전지대 레인
            GameObject safeRoot = PrefabUtility.LoadPrefabContents(SafeLanePrefabPath);
            if (safeRoot != null)
            {
                MeshRenderer mr = safeRoot.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = matGrass;
                PrefabUtility.SaveAsPrefabAsset(safeRoot, SafeLanePrefabPath);
                PrefabUtility.UnloadPrefabContents(safeRoot);
                Debug.Log("[CrossyRoadSetupTool] PF_SafeLane.prefab 머티리얼 적용 완료");
            }
        }

        private static void SetupPlayerInScene(Material matChar)
        {
            GameObject charFbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbxPath);
            if (charFbx == null)
            {
                Debug.LogError($"[CrossyRoadSetupTool] 캐릭터 FBX를 찾을 수 없습니다: {CharacterFbxPath}");
                return;
            }

            bool sceneWasLoaded = false;
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != TestScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
                sceneWasLoaded = true;
            }

            // 플레이어 오브젝트 탐색
            GridPlayerController playerCtrl = Object.FindFirstObjectByType<GridPlayerController>();
            if (playerCtrl == null)
            {
                Debug.LogError("[CrossyRoadSetupTool] 씬에서 GridPlayerController를 찾을 수 없습니다.");
                return;
            }

            GameObject playerObj = playerCtrl.gameObject;

            // 기존 임시 캡슐 메시 비활성화 또는 제거
            MeshFilter mf = playerObj.GetComponent<MeshFilter>();
            if (mf != null) Object.DestroyImmediate(mf);

            MeshRenderer mr = playerObj.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);

            // 기존 자식 VisualModel 정리
            Transform oldVisual = playerObj.transform.Find("VisualModel");
            if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

            // 캐릭터 FBX를 자식으로 생성
            GameObject visual = Object.Instantiate(charFbx, playerObj.transform);
            visual.name = "VisualModel";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity; // 앞방향 (+Z)
            visual.transform.localScale = Vector3.one * 0.9f; // 사용자 요청: 스케일 0.9 값으로 고정

            // 머티리얼 매핑
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = matChar;
            }

            // 애니메이터가 있으면 끄기 (GridPlayerController의 귀여운 홉 코루틴/스쿼시 물리 바운스 사용)
            Animator anim = visual.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false;

            // GridPlayerController의 _visualModelTransform에 연결
            SerializedObject so = new SerializedObject(playerCtrl);
            SerializedProperty propVisual = so.FindProperty("_visualModelTransform");
            if (propVisual != null)
            {
                propVisual.objectReferenceValue = visual.transform;
                so.ApplyModifiedProperties();
            }

            // 콜라이더 크기 조정
            CapsuleCollider capsule = playerObj.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.center = new Vector3(0f, 0.5f, 0f);
                capsule.radius = 0.35f;
                capsule.height = 1.0f;
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[CrossyRoadSetupTool] 씬 플레이어 캐릭터 설정 완료 (Scale 0.9 고정)");
        }

        private static void SetupUIInScene()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != TestScenePath)
            {
                activeScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            }

            // EventSystem 확인 및 생성
            UnityEngine.EventSystems.EventSystem es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                Debug.Log("[CrossyRoadSetupTool] 씬에 EventSystem 생성 완료");
            }

            var legacyInput = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyInput != null)
            {
                Object.DestroyImmediate(legacyInput);
            }

            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Canvas 확인 및 생성
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("InGame_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                UnityEngine.UI.CanvasScaler scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            // 실시간 점수 HUD 생성 또는 탐색 (Scale 0.9 고정)
            Transform scoreTrans = canvas.transform.Find("Score_HUD_Text");
            GameObject scoreObj = scoreTrans != null ? scoreTrans.gameObject : null;
            if (scoreObj == null)
            {
                scoreObj = new GameObject("Score_HUD_Text");
                scoreObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = scoreObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(50f, -40f);
                rect.sizeDelta = new Vector2(400f, 120f);

                UnityEngine.UI.Text text = scoreObj.AddComponent<UnityEngine.UI.Text>();
                text.font = defaultFont;
                text.fontSize = 76;
                text.fontStyle = FontStyle.Bold;
                text.color = Color.white;
                text.alignment = TextAnchor.UpperLeft;
                text.text = "0";

                UnityEngine.UI.Shadow shadow = scoreObj.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(3f, -3f);
            }
            scoreObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

            // 게임오버 패널 생성 또는 탐색
            Transform panelTrans = canvas.transform.Find("GameOver_Panel");
            GameObject panelObj = panelTrans != null ? panelTrans.gameObject : null;
            GameObject boxObj = null;
            GameObject btnObj = null;
            UnityEngine.UI.Text deathReasonText = null;
            UnityEngine.UI.Text finalScoreText = null;
            UnityEngine.UI.Text highScoreText = null;
            UnityEngine.UI.Button retryBtn = null;

            if (panelObj == null)
            {
                panelObj = new GameObject("GameOver_Panel");
                panelObj.transform.SetParent(canvas.transform, false);

                RectTransform panelRect = panelObj.AddComponent<RectTransform>();
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.sizeDelta = Vector2.zero;

                UnityEngine.UI.Image bgImg = panelObj.AddComponent<UnityEngine.UI.Image>();
                bgImg.color = new Color(0f, 0f, 0f, 0.72f);

                boxObj = new GameObject("Dialog_Box");
                boxObj.transform.SetParent(panelObj.transform, false);
                RectTransform boxRect = boxObj.AddComponent<RectTransform>();
                boxRect.anchorMin = new Vector2(0.5f, 0.5f);
                boxRect.anchorMax = new Vector2(0.5f, 0.5f);
                boxRect.pivot = new Vector2(0.5f, 0.5f);
                boxRect.sizeDelta = new Vector2(540f, 460f);

                UnityEngine.UI.Image boxImg = boxObj.AddComponent<Image>();
                boxImg.color = new Color(0.12f, 0.12f, 0.16f, 0.96f);

                // 타이틀
                GameObject titleObj = new GameObject("Title_Text");
                titleObj.transform.SetParent(boxObj.transform, false);
                RectTransform titleRect = titleObj.AddComponent<RectTransform>();
                titleRect.anchoredPosition = new Vector2(0f, 145f);
                titleRect.sizeDelta = new Vector2(500f, 60f);
                UnityEngine.UI.Text titleText = titleObj.AddComponent<UnityEngine.UI.Text>();
                titleText.font = defaultFont;
                titleText.fontSize = 48;
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = new Color(1f, 0.35f, 0.35f);
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.text = "GAME OVER";

                // 사유
                GameObject reasonObj = new GameObject("Reason_Text");
                reasonObj.transform.SetParent(boxObj.transform, false);
                RectTransform reasonRect = reasonObj.AddComponent<RectTransform>();
                reasonRect.anchoredPosition = new Vector2(0f, 80f);
                reasonRect.sizeDelta = new Vector2(480f, 50f);
                deathReasonText = reasonObj.AddComponent<UnityEngine.UI.Text>();
                deathReasonText.font = defaultFont;
                deathReasonText.fontSize = 25;
                deathReasonText.color = new Color(0.85f, 0.85f, 0.85f);
                deathReasonText.alignment = TextAnchor.MiddleCenter;
                deathReasonText.text = "셔틀버스를 피하지 못했습니다!";

                // 최종 점수
                GameObject finalObj = new GameObject("FinalScore_Text");
                finalObj.transform.SetParent(boxObj.transform, false);
                RectTransform finalRect = finalObj.AddComponent<RectTransform>();
                finalRect.anchoredPosition = new Vector2(0f, 18f);
                finalRect.sizeDelta = new Vector2(480f, 60f);
                finalScoreText = finalObj.AddComponent<UnityEngine.UI.Text>();
                finalScoreText.font = defaultFont;
                finalScoreText.fontSize = 40;
                finalScoreText.fontStyle = FontStyle.Bold;
                finalScoreText.color = Color.white;
                finalScoreText.alignment = TextAnchor.MiddleCenter;
                finalScoreText.text = "최종 점수: 0";

                // 최고 기록
                GameObject highObj = new GameObject("HighScore_Text");
                highObj.transform.SetParent(boxObj.transform, false);
                RectTransform highRect = highObj.AddComponent<RectTransform>();
                highRect.anchoredPosition = new Vector2(0f, -38f);
                highRect.sizeDelta = new Vector2(480f, 40f);
                highScoreText = highObj.AddComponent<UnityEngine.UI.Text>();
                highScoreText.font = defaultFont;
                highScoreText.fontSize = 25;
                highScoreText.color = new Color(1f, 0.82f, 0.2f);
                highScoreText.alignment = TextAnchor.MiddleCenter;
                highScoreText.text = "최고 기록: 0";

                // 다시하기 버튼
                btnObj = new GameObject("Retry_Button");
                btnObj.transform.SetParent(boxObj.transform, false);
                RectTransform btnRect = btnObj.AddComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(0f, -125f);
                btnRect.sizeDelta = new Vector2(300f, 75f);

                UnityEngine.UI.Image btnImg = btnObj.AddComponent<UnityEngine.UI.Image>();
                btnImg.color = new Color(0.18f, 0.78f, 0.38f);

                retryBtn = btnObj.AddComponent<UnityEngine.UI.Button>();
                UnityEngine.UI.ColorBlock colors = retryBtn.colors;
                colors.highlightedColor = new Color(0.28f, 0.88f, 0.48f);
                colors.pressedColor = new Color(0.12f, 0.62f, 0.28f);
                retryBtn.colors = colors;

                UnityEngine.UI.Shadow btnShadow = btnObj.AddComponent<UnityEngine.UI.Shadow>();
                btnShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
                btnShadow.effectDistance = new Vector2(2f, -2f);

                GameObject btnTextObj = new GameObject("Button_Text");
                btnTextObj.transform.SetParent(btnObj.transform, false);
                RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
                btnTextRect.sizeDelta = btnRect.sizeDelta;
                UnityEngine.UI.Text btnText = btnTextObj.AddComponent<UnityEngine.UI.Text>();
                btnText.font = defaultFont;
                btnText.fontSize = 32;
                btnText.fontStyle = FontStyle.Bold;
                btnText.color = Color.white;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.text = "다시 하기 (R)";
            }
            else
            {
                Transform box = panelObj.transform.Find("Dialog_Box");
                if (box != null)
                {
                    boxObj = box.gameObject;
                    Transform btn = box.Find("Retry_Button");
                    if (btn != null) btnObj = btn.gameObject;
                    retryBtn = btnObj != null ? btnObj.GetComponent<UnityEngine.UI.Button>() : null;
                    deathReasonText = box.Find("Reason_Text")?.GetComponent<UnityEngine.UI.Text>();
                    finalScoreText = box.Find("FinalScore_Text")?.GetComponent<UnityEngine.UI.Text>();
                    highScoreText = box.Find("HighScore_Text")?.GetComponent<UnityEngine.UI.Text>();
                }
            }

            // 다이얼로그 박스와 다시하기 버튼 Scale 0.9 고정
            if (boxObj != null) boxObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            if (btnObj != null) btnObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            panelObj.SetActive(false);

            // InGameUIManager 확인 및 직렬화 바인딩
            CampusRun.UI.InGameUIManager uiMgr = Object.FindFirstObjectByType<CampusRun.UI.InGameUIManager>();
            if (uiMgr == null)
            {
                GameObject uiObj = new GameObject("InGameUIManager");
                uiMgr = uiObj.AddComponent<CampusRun.UI.InGameUIManager>();
            }

            SerializedObject uiSo = new SerializedObject(uiMgr);
            uiSo.FindProperty("_scoreText").objectReferenceValue = scoreObj.GetComponent<UnityEngine.UI.Text>();
            uiSo.FindProperty("_gameOverPanel").objectReferenceValue = panelObj;
            if (deathReasonText != null) uiSo.FindProperty("_deathReasonText").objectReferenceValue = deathReasonText;
            if (finalScoreText != null) uiSo.FindProperty("_finalScoreText").objectReferenceValue = finalScoreText;
            if (highScoreText != null) uiSo.FindProperty("_highScoreText").objectReferenceValue = highScoreText;
            if (retryBtn != null) uiSo.FindProperty("_retryButton").objectReferenceValue = retryBtn;
            uiSo.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[CrossyRoadSetupTool] 씬 UI 세팅 완료 (Scale 0.9 고정 및 바인딩 완료)");
        }
    }
}
