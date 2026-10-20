using System.IO;
using UnityEngine;
using UnityEditor;
using CampusRun.Obstacles;
using CampusRun.Track;

namespace CampusRun.EditorTools
{
    /// <summary>
    /// 한국 대학가 및 도심 골목길 컨셉에 맞추어 보도블록, 볼라드, 가로등, 세로 횡단보도 및
    /// 보도블록 굴착 공사 현장(생시멘트 구덩이 & 철제 안전 발판)을 구성하는 에디터 툴입니다.
    /// </summary>
    public static class KoreanStreetSetupTool
    {
        private const string MaterialsDir = "Assets/_Materials";
        private const string PrefabsEnvDir = "Assets/_Prefabs/Environment";
        private const string PrefabsObsDir = "Assets/_Prefabs/Obstacles";

        // 머티리얼 경로
        private const string MatSidewalkPath = MaterialsDir + "/M_Sidewalk_Blocks.mat";
        private const string MatBraillePath = MaterialsDir + "/M_Braille_Block.mat";
        private const string MatCurbPath = MaterialsDir + "/M_Curb_Stone.mat";
        private const string MatBollardOrangePath = MaterialsDir + "/M_Bollard_Orange.mat";
        private const string MatBollardReflectorPath = MaterialsDir + "/M_Bollard_Reflector.mat";
        private const string MatBollardBasePath = MaterialsDir + "/M_Bollard_Base.mat";
        private const string MatRoadMarkingPath = MaterialsDir + "/M_Road_Marking.mat";
        private const string MatStreetLightPolePath = MaterialsDir + "/M_StreetLight_Pole.mat";
        private const string MatStreetLightGlowPath = MaterialsDir + "/M_StreetLight_Glow.mat";
        private const string MatCrosswalkWhitePath = MaterialsDir + "/M_Crosswalk_White.mat";
        private const string MatWetCementPath = MaterialsDir + "/M_Wet_Cement.mat";
        private const string MatScaffoldMetalPath = MaterialsDir + "/M_Scaffold_Metal.mat";
        private const string MatScaffoldDarkPath = MaterialsDir + "/M_Scaffold_Dark.mat";
        private const string MatSafetyYellowPath = MaterialsDir + "/M_Safety_Yellow.mat";
        private const string MatWaterPath = MaterialsDir + "/M_Water.mat";
        private const string MatGrassPath = MaterialsDir + "/M_Grass_Vibrant.mat";
        private const string MatBoundaryToneDownPath = MaterialsDir + "/M_Boundary_ToneDown.mat";

        private const string PrefsKey = "CampusRun_KoreanStreetSetup_Executed_v15";

        // 프리팹 경로
        private const string SafeLanePrefabPath = PrefabsEnvDir + "/PF_SafeLane.prefab";
        private const string RoadLanePrefabPath = PrefabsEnvDir + "/PF_RoadLane.prefab";
        private const string MotorcycleLanePrefabPath = PrefabsEnvDir + "/PF_MotorcycleLane.prefab";
        private const string WaterLanePrefabPath = PrefabsEnvDir + "/PF_WaterLane.prefab";
        private const string BollardPrefabPath = PrefabsObsDir + "/PF_Bollard.prefab";
        private const string KickboardPrefabPath = PrefabsObsDir + "/PF_Kickboard.prefab";
        private const string StreetLightPrefabPath = PrefabsEnvDir + "/PF_StreetLight.prefab";
        private const string FloatingPlankPrefabPath = PrefabsObsDir + "/PF_FloatingPlank.prefab";
        private const string TreePrefabPath = PrefabsObsDir + "/PF_Tree.prefab";
        private const string FastVehiclePrefabPath = PrefabsObsDir + "/PF_Motorcycle.prefab";

        // 3D 모델 원본 경로
        private const string SportCarSourcePrefabPath = "Assets/Awb-Free Low Poly Vehicles/Prefabs/Sport Car_39.prefab";
        private const string ScooterSourcePrefabPath = "Assets/MARCIN'S Assets/Electric Scooter/Prefabs/Scooter_1.prefab";
        private const string PoliceCarSourcePrefabPath = "Assets/Awb-Free Low Poly Vehicles/Prefabs/Police Car N_4.prefab";

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            if (!EditorPrefs.GetBool(PrefsKey, false))
            {
                EditorPrefs.SetBool(PrefsKey, true);
                EditorApplication.delayCall += () =>
                {
                    ImportAndApplyPixelFont();
                    SetupKoreanStreetTheme();
                    SetupFastPoliceCarPrefab();
                };
            }
        }

        [MenuItem("CampusRun/도트 폰트(Neo둥근모) 임포트 및 적용")]
        public static void ImportAndApplyPixelFont()
        {
            AssetDatabase.Refresh();

            string[] paths = new string[]
            {
                "Assets/Resources/Fonts/neodgm.ttf",
                "Assets/Fonts/neodgm.ttf"
            };

            foreach (string p in paths)
            {
                if (File.Exists(p))
                {
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                    Font font = AssetDatabase.LoadAssetAtPath<Font>(p);
                    if (font != null && font.material != null && font.material.mainTexture != null)
                    {
                        font.material.mainTexture.filterMode = FilterMode.Point;
                    }
                    Debug.Log($"[KoreanStreetSetupTool] 도트 폰트({p}) 임포트 완료: {(font != null ? font.name : "null")}");
                }
            }
        }

        [MenuItem("CampusRun/트랙 좌우 톤다운 경계 세팅 (Clean Tone-Down)")]
        public static void SetupTrackBoundariesAndWaterBuoys()
        {
            SetupKoreanStreetTheme();
        }

        [MenuItem("CampusRun/한국 거리 테마 적용 (보도블록 공사현장 & 철제 안전발판)")]
        public static void SetupKoreanStreetTheme()
        {
            Debug.Log("[KoreanStreetSetupTool] === 한국 도심 거리 및 트랙 경계 세팅 시작 ===");

            // 1. 머티리얼 로드
            Material matSidewalk = AssetDatabase.LoadAssetAtPath<Material>(MatSidewalkPath);
            Material matBraille = AssetDatabase.LoadAssetAtPath<Material>(MatBraillePath);
            Material matCurb = AssetDatabase.LoadAssetAtPath<Material>(MatCurbPath);
            Material matBollardOrange = AssetDatabase.LoadAssetAtPath<Material>(MatBollardOrangePath);
            Material matBollardReflector = AssetDatabase.LoadAssetAtPath<Material>(MatBollardReflectorPath);
            Material matBollardBase = AssetDatabase.LoadAssetAtPath<Material>(MatBollardBasePath);
            Material matRoadMarking = AssetDatabase.LoadAssetAtPath<Material>(MatRoadMarkingPath);
            Material matLightPole = AssetDatabase.LoadAssetAtPath<Material>(MatStreetLightPolePath);
            Material matLightGlow = AssetDatabase.LoadAssetAtPath<Material>(MatStreetLightGlowPath);
            Material matCrosswalk = AssetDatabase.LoadAssetAtPath<Material>(MatCrosswalkWhitePath);
            Material matWetCement = AssetDatabase.LoadAssetAtPath<Material>(MatWetCementPath);
            Material matScaffoldMetal = AssetDatabase.LoadAssetAtPath<Material>(MatScaffoldMetalPath);
            Material matScaffoldDark = AssetDatabase.LoadAssetAtPath<Material>(MatScaffoldDarkPath);
            Material matSafetyYellow = AssetDatabase.LoadAssetAtPath<Material>(MatSafetyYellowPath);
            Material matWater = AssetDatabase.LoadAssetAtPath<Material>(MatWaterPath);
            Material matGrass = AssetDatabase.LoadAssetAtPath<Material>(MatGrassPath);
            Material matToneDown = AssetDatabase.LoadAssetAtPath<Material>(MatBoundaryToneDownPath);

            // 2. 가로등 프리팹
            GameObject streetLightPrefab = RebuildStreetLightPrefab(matLightPole, matLightGlow);

            // 3. 주황색 볼라드 장애물 프리팹
            GameObject bollardPrefab = RebuildBollardPrefab(matBollardOrange, matBollardReflector, matBollardBase);

            // 4. PF_SafeLane 재구성
            RebuildSafeLanePrefab(matSidewalk, matCurb, matBraille, streetLightPrefab, bollardPrefab);

            // 5. PF_RoadLane 재구성
            RebuildRoadLanePrefab(matCrosswalk, matRoadMarking);

            // 6. 하천 레인 (PF_WaterLane) - 복잡한 3D 모델 제거 및 깔끔한 물 표면 + 톤다운 경계 재구성
            RebuildWaterLaneClean(matWater, matToneDown);

            // 7. 모든 레인 프리팹에 미니멀 톤다운 경계선(Clean Tone-Down) 적용
            ApplyToneDownBoundaryToPrefab(SafeLanePrefabPath, matToneDown);
            ApplyToneDownBoundaryToPrefab(RoadLanePrefabPath, matToneDown);
            ApplyToneDownBoundaryToPrefab(MotorcycleLanePrefabPath, matToneDown);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[KoreanStreetSetupTool] === 트랙 좌우 톤다운 경계선(미니멀 음영) 세팅 완료! ===");
        }

        #region Prefabs

        private static GameObject RebuildStreetLightPrefab(Material matPole, Material matGlow)
        {
            GameObject root = new GameObject("PF_StreetLight");

            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Base";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            baseObj.transform.localScale = new Vector3(0.25f, 0.05f, 0.25f);
            Object.DestroyImmediate(baseObj.GetComponent<Collider>());
            baseObj.GetComponent<MeshRenderer>().sharedMaterial = matPole;

            GameObject poleObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            poleObj.name = "Pole";
            poleObj.transform.SetParent(root.transform, false);
            poleObj.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            poleObj.transform.localScale = new Vector3(0.08f, 1.05f, 0.08f);
            Object.DestroyImmediate(poleObj.GetComponent<Collider>());
            poleObj.GetComponent<MeshRenderer>().sharedMaterial = matPole;

            GameObject armObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armObj.name = "Arm";
            armObj.transform.SetParent(root.transform, false);
            armObj.transform.localPosition = new Vector3(0f, 2.22f, 0.24f);
            armObj.transform.localScale = new Vector3(0.07f, 0.06f, 0.48f);
            Object.DestroyImmediate(armObj.GetComponent<Collider>());
            armObj.GetComponent<MeshRenderer>().sharedMaterial = matPole;

            GameObject shadeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadeObj.name = "Lampshade";
            shadeObj.transform.SetParent(root.transform, false);
            shadeObj.transform.localPosition = new Vector3(0f, 2.20f, 0.48f);
            shadeObj.transform.localScale = new Vector3(0.32f, 0.035f, 0.32f);
            Object.DestroyImmediate(shadeObj.GetComponent<Collider>());
            shadeObj.GetComponent<MeshRenderer>().sharedMaterial = matPole;

            GameObject bulbObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bulbObj.name = "Bulb";
            bulbObj.transform.SetParent(shadeObj.transform, false);
            bulbObj.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            bulbObj.transform.localScale = new Vector3(0.75f, 0.4f, 0.75f);
            Object.DestroyImmediate(bulbObj.GetComponent<Collider>());
            bulbObj.GetComponent<MeshRenderer>().sharedMaterial = matGlow;

            GameObject lightObj = new GameObject("LightSource");
            lightObj.transform.SetParent(shadeObj.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, -0.8f, 0f);
            lightObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Light spotLight = lightObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.color = new Color(1.0f, 0.95f, 0.82f);
            spotLight.range = 5.5f;
            spotLight.spotAngle = 65f;
            spotLight.intensity = 1.8f;
            spotLight.shadows = LightShadows.None;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, StreetLightPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        private static GameObject RebuildBollardPrefab(Material matOrange, Material matReflector, Material matBase)
        {
            GameObject root = new GameObject("PF_Bollard");
            root.tag = "Obstacle";

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.3f, 0.7f, 0.3f);
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.isTrigger = false;

            root.AddComponent<StationaryObstacle>();

            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Base";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            baseObj.transform.localScale = new Vector3(0.24f, 0.015f, 0.24f);
            Object.DestroyImmediate(baseObj.GetComponent<Collider>());
            baseObj.GetComponent<MeshRenderer>().sharedMaterial = matBase;

            GameObject bodyObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bodyObj.name = "Body";
            bodyObj.transform.SetParent(root.transform, false);
            bodyObj.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            bodyObj.transform.localScale = new Vector3(0.16f, 0.32f, 0.16f);
            Object.DestroyImmediate(bodyObj.GetComponent<Collider>());
            bodyObj.GetComponent<MeshRenderer>().sharedMaterial = matOrange;

            GameObject band1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band1.name = "Reflector_Top";
            band1.transform.SetParent(root.transform, false);
            band1.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            band1.transform.localScale = new Vector3(0.168f, 0.03f, 0.168f);
            Object.DestroyImmediate(band1.GetComponent<Collider>());
            band1.GetComponent<MeshRenderer>().sharedMaterial = matReflector;

            GameObject band2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band2.name = "Reflector_Bottom";
            band2.transform.SetParent(root.transform, false);
            band2.transform.localPosition = new Vector3(0f, 0.40f, 0f);
            band2.transform.localScale = new Vector3(0.168f, 0.03f, 0.168f);
            Object.DestroyImmediate(band2.GetComponent<Collider>());
            band2.GetComponent<MeshRenderer>().sharedMaterial = matReflector;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, BollardPrefabPath);
            Object.DestroyImmediate(root);
            return savedPrefab;
        }

        #endregion

        #region Lane Rebuilds

        private static void RebuildSafeLanePrefab(Material matSidewalk, Material matCurb, Material matBraille, GameObject streetLightPrefab, GameObject bollardPrefab)
        {
            GameObject safeRoot = PrefabUtility.LoadPrefabContents(SafeLanePrefabPath);
            if (safeRoot == null) return;

            for (int i = safeRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(safeRoot.transform.GetChild(i).gameObject);
            }

            safeRoot.transform.localScale = new Vector3(60f, 0.2f, 1f);

            MeshRenderer mr = safeRoot.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = matSidewalk;

            GameObject curbGroup = new GameObject("CurbDecorations");
            curbGroup.transform.SetParent(safeRoot.transform, false);

            GameObject curbFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curbFront.name = "Curb_Front";
            curbFront.transform.SetParent(curbGroup.transform, false);
            Object.DestroyImmediate(curbFront.GetComponent<Collider>());
            MeshRenderer mrFront = curbFront.GetComponent<MeshRenderer>();
            mrFront.sharedMaterial = matCurb;
            mrFront.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            curbFront.transform.localPosition = new Vector3(0f, 0.52f, 0.465f);
            curbFront.transform.localScale = new Vector3(1f, 0.22f, 0.07f);

            GameObject curbBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curbBack.name = "Curb_Back";
            curbBack.transform.SetParent(curbGroup.transform, false);
            Object.DestroyImmediate(curbBack.GetComponent<Collider>());
            MeshRenderer mrBack = curbBack.GetComponent<MeshRenderer>();
            mrBack.sharedMaterial = matCurb;
            mrBack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            curbBack.transform.localPosition = new Vector3(0f, 0.52f, -0.465f);
            curbBack.transform.localScale = new Vector3(1f, 0.22f, 0.07f);

            GameObject brailleGroup = new GameObject("BrailleBlockGroup");
            brailleGroup.transform.SetParent(safeRoot.transform, false);

            GameObject brailleLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brailleLine.name = "Braille_Line";
            brailleLine.transform.SetParent(brailleGroup.transform, false);
            Object.DestroyImmediate(brailleLine.GetComponent<Collider>());
            MeshRenderer mrBraille = brailleLine.GetComponent<MeshRenderer>();
            mrBraille.sharedMaterial = matBraille;
            mrBraille.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            brailleLine.transform.localPosition = new Vector3(0f, 0.515f, 0.25f);
            brailleLine.transform.localScale = new Vector3(1f, 0.06f, 0.25f);

            brailleGroup.SetActive(false);

            SafeLane laneScript = safeRoot.GetComponent<SafeLane>();
            if (laneScript != null)
            {
                SerializedObject so = new SerializedObject(laneScript);
                so.FindProperty("_sidewalkMaterial").objectReferenceValue = matSidewalk;
                so.FindProperty("_curbDecorations").objectReferenceValue = curbGroup;
                so.FindProperty("_brailleBlockGroup").objectReferenceValue = brailleGroup;
                so.FindProperty("_brailleBlockChance").floatValue = 0.45f;

                so.FindProperty("_streetLightPrefab").objectReferenceValue = streetLightPrefab;
                so.FindProperty("_streetLightSpawnChance").floatValue = 0.12f;

                GameObject kickboardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KickboardPrefabPath);
                GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
                StationaryObstacle kickObs = kickboardPrefab != null ? kickboardPrefab.GetComponent<StationaryObstacle>() : null;
                StationaryObstacle bollardObs = bollardPrefab != null ? bollardPrefab.GetComponent<StationaryObstacle>() : null;
                StationaryObstacle treeObs = treePrefab != null ? treePrefab.GetComponent<StationaryObstacle>() : null;

                SerializedProperty obsListProp = so.FindProperty("_obstaclePrefabs");
                if (kickObs != null && bollardObs != null && treeObs != null)
                {
                    obsListProp.arraySize = 3;
                    obsListProp.GetArrayElementAtIndex(0).objectReferenceValue = kickObs;
                    obsListProp.GetArrayElementAtIndex(1).objectReferenceValue = bollardObs;
                    obsListProp.GetArrayElementAtIndex(2).objectReferenceValue = treeObs;
                }

                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(safeRoot, SafeLanePrefabPath);
            PrefabUtility.UnloadPrefabContents(safeRoot);
            Debug.Log("[KoreanStreetSetupTool] PF_SafeLane.prefab 재구성 완료!");
        }

        private static GameObject CreateFloorDecorationCube(string name, Transform parent, Material mat, Vector3 localPos, Vector3 localScale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            MeshRenderer mr = cube.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
            return cube;
        }

        private static GameObject CreateScaledCube(string name, Transform parent, Material mat, Vector3 worldPos, Vector3 worldSize, Vector3 parentScale, bool keepCollider = false)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            if (!keepCollider) Object.DestroyImmediate(cube.GetComponent<Collider>());
            MeshRenderer mr = cube.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cube.transform.localPosition = new Vector3(
                worldPos.x / parentScale.x,
                worldPos.y / parentScale.y,
                worldPos.z / parentScale.z
            );
            cube.transform.localScale = new Vector3(
                worldSize.x / parentScale.x,
                worldSize.y / parentScale.y,
                worldSize.z / parentScale.z
            );
            return cube;
        }

        private static GameObject CreateScaledCylinder(string name, Transform parent, Material mat, Vector3 worldPos, Vector3 worldSize, Vector3 parentScale, bool keepCollider = false)
        {
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cyl.name = name;
            cyl.transform.SetParent(parent, false);
            if (!keepCollider) Object.DestroyImmediate(cyl.GetComponent<Collider>());
            MeshRenderer mr = cyl.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cyl.transform.localPosition = new Vector3(
                worldPos.x / parentScale.x,
                worldPos.y / parentScale.y,
                worldPos.z / parentScale.z
            );
            cyl.transform.localScale = new Vector3(
                worldSize.x / parentScale.x,
                (worldSize.y * 0.5f) / parentScale.y,
                worldSize.z / parentScale.z
            );
            return cyl;
        }

        private static void RebuildRoadLanePrefab(Material matCrosswalk, Material matRoadMarking)
        {
            GameObject roadRoot = PrefabUtility.LoadPrefabContents(RoadLanePrefabPath);
            if (roadRoot == null) return;

            for (int i = roadRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(roadRoot.transform.GetChild(i).gameObject);
            }

            roadRoot.transform.localScale = new Vector3(60f, 0.2f, 1f);

            float invX = 1f / 60f;
            float invY = 1f / 0.2f;

            // [사용자 요청] 횡단보도(사선/잔선)는 완전 제거!
            // 통일했던 그 일정한 일자 작대기 페인트(Dash Line)의 다채로운 랜덤 패턴 그룹 5종 구축

            // --- 패턴 1: 기본 일자 점선 (Standard Dashes, 3.6m 등간격) ---
            GameObject patternStandard = new GameObject("DashPattern_Standard");
            patternStandard.transform.SetParent(roadRoot.transform, false);
            float[] standardX = new float[] { -25.2f, -21.6f, -18f, -14.4f, -10.8f, -7.2f, -3.6f, 0f, 3.6f, 7.2f, 10.8f, 14.4f, 18f, 21.6f, 25.2f };
            foreach (float wx in standardX)
            {
                CreateFloorDecorationCube($"Dash_{wx:F1}", patternStandard.transform, matRoadMarking,
                    new Vector3(wx * invX, 0.515f, 0f),
                    new Vector3(1.6f * invX, 0.03f * invY, 0.15f));
            }
            patternStandard.SetActive(true);

            // --- 패턴 2: 넓은 간격 띄엄 점선 (Sparse Dashes, 7.2m 간격) ---
            GameObject patternSparse = new GameObject("DashPattern_Sparse");
            patternSparse.transform.SetParent(roadRoot.transform, false);
            float[] sparseX = new float[] { -25.2f, -18f, -10.8f, -3.6f, 3.6f, 10.8f, 18f, 25.2f };
            foreach (float wx in sparseX)
            {
                CreateFloorDecorationCube($"Dash_Sp_{wx:F1}", patternSparse.transform, matRoadMarking,
                    new Vector3(wx * invX, 0.515f, 0f),
                    new Vector3(1.6f * invX, 0.03f * invY, 0.15f));
            }
            patternSparse.SetActive(false);

            // --- 패턴 3: 긴 호흡 일자 점선 (Long Dashes, 5.4m 간격, 2.8m 길이) ---
            GameObject patternLong = new GameObject("DashPattern_Long");
            patternLong.transform.SetParent(roadRoot.transform, false);
            float[] longX = new float[] { -24.3f, -18.9f, -13.5f, -8.1f, -2.7f, 2.7f, 8.1f, 13.5f, 18.9f, 24.3f };
            foreach (float wx in longX)
            {
                CreateFloorDecorationCube($"Dash_Lg_{wx:F1}", patternLong.transform, matRoadMarking,
                    new Vector3(wx * invX, 0.515f, 0f),
                    new Vector3(2.8f * invX, 0.03f * invY, 0.15f));
            }
            patternLong.SetActive(false);

            // --- 패턴 4: 더블 일자 점선 / 이중선 (Double Dashes, Z=±0.08m) ---
            GameObject patternDouble = new GameObject("DashPattern_Double");
            patternDouble.transform.SetParent(roadRoot.transform, false);
            foreach (float wx in standardX)
            {
                CreateFloorDecorationCube($"Dash_D1_{wx:F1}", patternDouble.transform, matRoadMarking,
                    new Vector3(wx * invX, 0.515f, -0.08f),
                    new Vector3(1.6f * invX, 0.03f * invY, 0.10f));
                CreateFloorDecorationCube($"Dash_D2_{wx:F1}", patternDouble.transform, matRoadMarking,
                    new Vector3(wx * invX, 0.515f, 0.08f),
                    new Vector3(1.6f * invX, 0.03f * invY, 0.10f));
            }
            patternDouble.SetActive(false);

            // --- 패턴 5: 촘촘한 짧은 일자 점선 (Dense Dashes, 2.4m 간격, 1.1m 길이) ---
            GameObject patternDense = new GameObject("DashPattern_Dense");
            patternDense.transform.SetParent(roadRoot.transform, false);
            float[] denseX = new float[] { -26.4f, -24.0f, -21.6f, -19.2f, -16.8f, -14.4f, -12.0f, -9.6f, -7.2f, -4.8f, -2.4f, 0f, 2.4f, 4.8f, 7.2f, 9.6f, 12.0f, 14.4f, 16.8f, 19.2f, 21.6f, 24.0f, 26.4f };
            foreach (float wx in denseX)
            {
                CreateFloorDecorationCube($"Dash_Dn_{wx:F1}", patternDense.transform, matRoadMarking,
                    new Vector3(wx * invX, 0.515f, 0f),
                    new Vector3(1.1f * invX, 0.03f * invY, 0.15f));
            }
            patternDense.SetActive(false);

            RoadLane roadScript = roadRoot.GetComponent<RoadLane>();
            if (roadScript != null)
            {
                SerializedObject so = new SerializedObject(roadScript);
                so.FindProperty("_crosswalkGroup").objectReferenceValue = null;
                so.FindProperty("_standardDashesGroup").objectReferenceValue = patternStandard;

                SerializedProperty patternListProp = so.FindProperty("_dashPatternGroups");
                if (patternListProp != null)
                {
                    patternListProp.arraySize = 5;
                    patternListProp.GetArrayElementAtIndex(0).objectReferenceValue = patternStandard;
                    patternListProp.GetArrayElementAtIndex(1).objectReferenceValue = patternSparse;
                    patternListProp.GetArrayElementAtIndex(2).objectReferenceValue = patternLong;
                    patternListProp.GetArrayElementAtIndex(3).objectReferenceValue = patternDouble;
                    patternListProp.GetArrayElementAtIndex(4).objectReferenceValue = patternDense;
                }

                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(roadRoot, RoadLanePrefabPath);
            PrefabUtility.UnloadPrefabContents(roadRoot);
            Debug.Log("[KoreanStreetSetupTool] PF_RoadLane.prefab 통일된 일자 페인트 5종 패턴 재구성 완료 (횡단보도 완전 제거)!");
        }

        /// <summary>
        /// PF_WaterLane을 '보도블록 굴착 공사현장(생시멘트 구덩이)'으로,
        /// PF_FloatingPlank를 '공사현장 정밀 은빛 철제 비계(아시바) 안전 발판'으로 재구성합니다.
        /// </summary>
        private static void RebuildConstructionLaneAndScaffold(Material matWetCement, Material matScaffoldMetal, Material matScaffoldDark, Material matSafetyYellow, Material matCurb)
        {
            // 1. PF_WaterLane (생시멘트/콘크리트 굴착 구덩이)
            GameObject waterRoot = PrefabUtility.LoadPrefabContents(WaterLanePrefabPath);
            if (waterRoot != null)
            {
                waterRoot.transform.localScale = new Vector3(60f, 0.12f, 1f);

                MeshRenderer mr = waterRoot.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = matWetCement;

                // 파헤쳐진 콘크리트 굴착 단면 연석
                Transform oldSubmerged = waterRoot.transform.Find("SubmergedCurbs");
                if (oldSubmerged != null) Object.DestroyImmediate(oldSubmerged.gameObject);

                Transform oldCurb = waterRoot.transform.Find("ConstructionCurbs");
                if (oldCurb != null) Object.DestroyImmediate(oldCurb.gameObject);

                GameObject curbContainer = new GameObject("ConstructionCurbs");
                curbContainer.transform.SetParent(waterRoot.transform, false);

                float invY = 1f / 0.12f;
                GameObject cFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cFront.name = "Curb_Front";
                cFront.transform.SetParent(curbContainer.transform, false);
                Object.DestroyImmediate(cFront.GetComponent<Collider>());
                cFront.GetComponent<MeshRenderer>().sharedMaterial = matCurb;
                cFront.transform.localPosition = new Vector3(0f, 0.54f, 0.475f);
                cFront.transform.localScale = new Vector3(1f, 0.16f * invY, 0.05f);

                GameObject cBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cBack.name = "Curb_Back";
                cBack.transform.SetParent(curbContainer.transform, false);
                Object.DestroyImmediate(cBack.GetComponent<Collider>());
                cBack.GetComponent<MeshRenderer>().sharedMaterial = matCurb;
                cBack.transform.localPosition = new Vector3(0f, 0.54f, -0.475f);
                cBack.transform.localScale = new Vector3(1f, 0.16f * invY, 0.05f);

                PrefabUtility.SaveAsPrefabAsset(waterRoot, WaterLanePrefabPath);
                PrefabUtility.UnloadPrefabContents(waterRoot);
                Debug.Log("[KoreanStreetSetupTool] PF_WaterLane ➡️ 보도블록 굴착 공사현장 리디자인 완료!");
            }

            // 2. PF_FloatingPlank (정밀 은빛 철제 비계/안전 발판)
            GameObject plankRoot = PrefabUtility.LoadPrefabContents(FloatingPlankPrefabPath);
            if (plankRoot != null)
            {
                for (int i = plankRoot.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(plankRoot.transform.GetChild(i).gameObject);
                }

                // 루트는 직관적인 1:1 월드 스케일 유지 (자식 부품들의 정밀 모델링을 위해)
                plankRoot.transform.localScale = Vector3.one;

                // 루트 자체의 MeshFilter/MeshRenderer는 제거하여 깔끔한 컨테이너 구조로 전환
                MeshFilter rootMf = plankRoot.GetComponent<MeshFilter>();
                if (rootMf != null) Object.DestroyImmediate(rootMf);
                MeshRenderer rootMr = plankRoot.GetComponent<MeshRenderer>();
                if (rootMr != null) Object.DestroyImmediate(rootMr);

                // 콜라이더 설정 (실제 발판 크기 2.2m x 0.2m x 0.8m)
                BoxCollider boxCol = plankRoot.GetComponent<BoxCollider>();
                if (boxCol == null) boxCol = plankRoot.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(2.2f, 0.2f, 0.8f);
                boxCol.center = new Vector3(0f, 0.08f, 0f);
                boxCol.isTrigger = true;

                // FloatingPlank 컴포넌트 설정
                FloatingPlank plankComp = plankRoot.GetComponent<FloatingPlank>();
                if (plankComp != null)
                {
                    SerializedObject so = new SerializedObject(plankComp);
                    so.FindProperty("_plankLength").floatValue = 2.2f;
                    so.FindProperty("_despawnBoundaryX").floatValue = 14f;
                    so.ApplyModifiedProperties();
                }

                // --- 비주얼 모델링 ---
                GameObject visualRoot = new GameObject("VisualModel");
                visualRoot.transform.SetParent(plankRoot.transform, false);

                // 1. 메인 발판 상판 (밝은 아연도금 은색 플레이트)
                GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deck.name = "Deck_Main";
                deck.transform.SetParent(visualRoot.transform, false);
                Object.DestroyImmediate(deck.GetComponent<Collider>());
                deck.GetComponent<MeshRenderer>().sharedMaterial = matScaffoldMetal;
                deck.transform.localPosition = new Vector3(0f, 0.04f, 0f);
                deck.transform.localScale = new Vector3(2.14f, 0.08f, 0.72f);

                // 2. 앞/뒤 사이드 안전 절곡 턱 (미끄럼 방지 및 강성 보강용 상향 턱)
                GameObject ribFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ribFront.name = "SideRib_Front";
                ribFront.transform.SetParent(visualRoot.transform, false);
                Object.DestroyImmediate(ribFront.GetComponent<Collider>());
                ribFront.GetComponent<MeshRenderer>().sharedMaterial = matScaffoldMetal;
                ribFront.transform.localPosition = new Vector3(0f, 0.095f, 0.365f);
                ribFront.transform.localScale = new Vector3(2.14f, 0.06f, 0.03f);

                GameObject ribBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ribBack.name = "SideRib_Back";
                ribBack.transform.SetParent(visualRoot.transform, false);
                Object.DestroyImmediate(ribBack.GetComponent<Collider>());
                ribBack.GetComponent<MeshRenderer>().sharedMaterial = matScaffoldMetal;
                ribBack.transform.localPosition = new Vector3(0f, 0.095f, -0.365f);
                ribBack.transform.localScale = new Vector3(2.14f, 0.06f, 0.03f);

                // 3. 미끄럼 방지 타공 구멍 격자 패턴 (Perforated Holes)
                GameObject holeContainer = new GameObject("PerforatedHoles");
                holeContainer.transform.SetParent(visualRoot.transform, false);

                float[] holeColsX = new float[] { -0.75f, -0.50f, -0.25f, 0.0f, 0.25f, 0.50f, 0.75f };
                float[] holeRowsZ = new float[] { -0.22f, 0.0f, 0.22f };

                foreach (float hx in holeColsX)
                {
                    foreach (float hz in holeRowsZ)
                    {
                        GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        hole.name = $"Hole_{hx:F2}_{hz:F2}";
                        hole.transform.SetParent(holeContainer.transform, false);
                        Object.DestroyImmediate(hole.GetComponent<Collider>());
                        hole.GetComponent<MeshRenderer>().sharedMaterial = matScaffoldDark;
                        hole.transform.localPosition = new Vector3(hx, 0.082f, hz);
                        hole.transform.localScale = new Vector3(0.12f, 0.005f, 0.12f);
                    }
                }

                // 4. 가로 보강 리브 빔 (Cross Stiffeners, 3등분 지점 강도 보강)
                float[] stiffenerColsX = new float[] { -0.375f, 0.375f };
                foreach (float sx in stiffenerColsX)
                {
                    GameObject stiff = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stiff.name = $"Stiffener_{sx:F2}";
                    stiff.transform.SetParent(visualRoot.transform, false);
                    Object.DestroyImmediate(stiff.GetComponent<Collider>());
                    stiff.GetComponent<MeshRenderer>().sharedMaterial = matScaffoldMetal;
                    stiff.transform.localPosition = new Vector3(sx, 0.083f, 0f);
                    stiff.transform.localScale = new Vector3(0.04f, 0.012f, 0.73f);
                }

                // 5. 양쪽 끝 안전 옐로우 포인트 (Safety Warning Bands)
                float[] yellowBandsX = new float[] { -0.98f, 0.98f };
                foreach (float yx in yellowBandsX)
                {
                    GameObject yBand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    yBand.name = $"SafetyBand_{yx:F2}";
                    yBand.transform.SetParent(visualRoot.transform, false);
                    Object.DestroyImmediate(yBand.GetComponent<Collider>());
                    yBand.GetComponent<MeshRenderer>().sharedMaterial = matSafetyYellow;
                    yBand.transform.localPosition = new Vector3(yx, 0.084f, 0f);
                    yBand.transform.localScale = new Vector3(0.05f, 0.014f, 0.73f);
                }

                // 6. 비계 파이프 체결용 걸쇠 후크 (End Pipe Hooks / Clamps, 좌우 끝단에 2개씩 총 4개)
                GameObject hookContainer = new GameObject("EndPipeHooks");
                hookContainer.transform.SetParent(visualRoot.transform, false);

                float[] hookSidesX = new float[] { -1.07f, 1.07f };
                float[] hookOffsetsZ = new float[] { -0.22f, 0.22f };

                foreach (float hx in hookSidesX)
                {
                    float sign = Mathf.Sign(hx);
                    foreach (float hz in hookOffsetsZ)
                    {
                        // 걸쇠 메인 바디
                        GameObject hookBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        hookBody.name = $"Hook_{hx:F2}_{hz:F2}_Body";
                        hookBody.transform.SetParent(hookContainer.transform, false);
                        Object.DestroyImmediate(hookBody.GetComponent<Collider>());
                        hookBody.GetComponent<MeshRenderer>().sharedMaterial = matScaffoldMetal;
                        hookBody.transform.localPosition = new Vector3(hx + sign * 0.035f, 0.065f, hz);
                        hookBody.transform.localScale = new Vector3(0.07f, 0.05f, 0.08f);

                        // 아래로 구부러진 갈고리 립 (파이프를 감싸는 턱)
                        GameObject hookLip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        hookLip.name = $"Hook_{hx:F2}_{hz:F2}_Lip";
                        hookLip.transform.SetParent(hookContainer.transform, false);
                        Object.DestroyImmediate(hookLip.GetComponent<Collider>());
                        hookLip.GetComponent<MeshRenderer>().sharedMaterial = matScaffoldMetal;
                        hookLip.transform.localPosition = new Vector3(hx + sign * 0.065f, 0.025f, hz);
                        hookLip.transform.localScale = new Vector3(0.025f, 0.06f, 0.08f);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(plankRoot, FloatingPlankPrefabPath);
                PrefabUtility.UnloadPrefabContents(plankRoot);
                Debug.Log("[KoreanStreetSetupTool] PF_FloatingPlank ➡️ 정밀 은빛 철제 비계 발판(아시바 발판) 리디자인 완료!");
            }
        }

        private static void RebuildWaterLaneClean(Material matWater, Material matToneDown)
        {
            GameObject waterRoot = PrefabUtility.LoadPrefabContents(WaterLanePrefabPath);
            if (waterRoot == null) return;

            // 하천 전체 폭 60m 설정 (와이드 화면 완벽 지원)
            waterRoot.transform.localScale = new Vector3(60f, 0.12f, 1f);

            MeshRenderer mr = waterRoot.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = matWater;

            // 물 트리거 콜라이더는 60m 전체 영역
            BoxCollider waterCol = waterRoot.GetComponent<BoxCollider>();
            if (waterCol != null)
            {
                waterCol.isTrigger = true;
                waterCol.center = Vector3.zero;
                waterCol.size = Vector3.one;
            }

            // 기존 복잡한 3D 프랍 자식 제거
            RemoveOldBoundaryDecorations(waterRoot.transform);

            // 톤다운 경계 오버레이 부착
            ApplyToneDownBoundary(waterRoot, matToneDown);

            // WaterLane 컴포넌트 설정
            WaterLane waterLane = waterRoot.GetComponent<WaterLane>();
            if (waterLane != null)
            {
                SerializedObject so = new SerializedObject(waterLane);
                so.FindProperty("_laneWidth").floatValue = 60f;
                so.FindProperty("_spawnBoundaryX").floatValue = 26f;
                so.ApplyModifiedProperties();
            }

            PrefabUtility.SaveAsPrefabAsset(waterRoot, WaterLanePrefabPath);
            PrefabUtility.UnloadPrefabContents(waterRoot);
            Debug.Log("[KoreanStreetSetupTool] PF_WaterLane.prefab 클린 톤다운 경계 재구성 완료!");
        }

        private static void ApplyToneDownBoundaryToPrefab(string prefabPath, Material matToneDown)
        {
            GameObject laneRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (laneRoot == null) return;

            RemoveOldBoundaryDecorations(laneRoot.transform);
            ApplyToneDownBoundary(laneRoot, matToneDown);

            PrefabUtility.SaveAsPrefabAsset(laneRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(laneRoot);
            Debug.Log($"[KoreanStreetSetupTool] {Path.GetFileName(prefabPath)} 톤다운 경계 적용 완료!");
        }

        private static void RemoveOldBoundaryDecorations(Transform root)
        {
            string[] targets = new string[]
            {
                "BoundaryDecorations",
                "HazardBuoys",
                "ConstructionCurbs",
                "RoadGuardrails",
                "SidewalkBorders",
                "ToneDown_Boundaries"
            };

            foreach (string t in targets)
            {
                Transform child = root.Find(t);
                if (child != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void ApplyToneDownBoundary(GameObject laneRoot, Material matToneDown)
        {
            if (matToneDown == null) return;

            Vector3 parentScale = laneRoot.transform.localScale;

            GameObject container = new GameObject("ToneDown_Boundaries");
            container.transform.SetParent(laneRoot.transform, false);

            // 좌측 톤다운 오버레이: 월드 X = -17.4f (X = -4.8m ~ -30.0m 구간, 폭 25.2m)
            CreateToneDownBlock("ToneDown_Left", container.transform, matToneDown,
                new Vector3(-17.4f / parentScale.x, 0.505f, 0f),
                new Vector3(25.2f / parentScale.x, 0.02f / parentScale.y, 1f));

            // 우측 톤다운 오버레이: 월드 X = +17.4f (X = +4.8m ~ +30.0m 구간, 폭 25.2m)
            CreateToneDownBlock("ToneDown_Right", container.transform, matToneDown,
                new Vector3(17.4f / parentScale.x, 0.505f, 0f),
                new Vector3(25.2f / parentScale.x, 0.02f / parentScale.y, 1f));
        }

        private static void CreateToneDownBlock(string name, Transform parent, Material mat, Vector3 localPos, Vector3 localScale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            MeshRenderer mr = cube.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
        }

        [MenuItem("CampusRun/초고속 질주 차량 모델 세팅 (스포츠카)")]
        public static void SetupFastSportCarPrefab()
        {
            Debug.Log("[KoreanStreetSetupTool] === 초고속 스포츠카(Sport Car_39) 프리팹 세팅 시작 ===");

            GameObject sportCarSrc = AssetDatabase.LoadAssetAtPath<GameObject>(SportCarSourcePrefabPath);
            if (sportCarSrc == null)
            {
                Debug.LogError($"[KoreanStreetSetupTool] 스포츠카 소스 프리팹을 찾을 수 없습니다: {SportCarSourcePrefabPath}");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(FastVehiclePrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[KoreanStreetSetupTool] 대상 프리팹을 찾을 수 없습니다: {FastVehiclePrefabPath}");
                return;
            }

            // 1. 기존 임시 큐브 렌더러/필터 제거
            MeshFilter oldMf = prefabRoot.GetComponent<MeshFilter>();
            if (oldMf != null) Object.DestroyImmediate(oldMf);

            MeshRenderer oldMr = prefabRoot.GetComponent<MeshRenderer>();
            if (oldMr != null) Object.DestroyImmediate(oldMr);

            // 2. 기존 모든 자식 오브젝트 정리
            for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(prefabRoot.transform.GetChild(i).gameObject);
            }

            // 3. 스포츠카 3D 모델 인스턴스화
            GameObject visual = Object.Instantiate(sportCarSrc, prefabRoot.transform);
            visual.name = "VisualModel";

            // 스포츠카 자체에 있던 원본 BoxCollider 제거 (루트 FastMotorcycle의 충돌체와 중복 방지)
            BoxCollider visualCol = visual.GetComponent<BoxCollider>();
            if (visualCol != null) Object.DestroyImmediate(visualCol);

            // 시각적 모델 좌표/회전 정규화 (전방이 로컬 +Z를 향하도록 정렬)
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // 길건너 친구들(Crossy Road) 스타일의 도톰하고 볼륨감 있는 아케이드 황금 비율 적용:
            // 원본 스포츠카 크기: 폭 2.09m, 높이 1.15m, 길이 4.74m
            // 기존 0.36 균등 스케일 적용 시 높이가 0.41m로 너무 납작해 보이는 현상 완벽 개선:
            // X: 0.40f (폭 0.84m), Y: 0.58f (높이 0.67m로 +62% 도톰하게), Z: 0.38f (길이 1.80m)
            visual.transform.localScale = new Vector3(0.40f, 0.58f, 0.38f);

            // 4. 루트 트랜스폼 및 태그 세팅
            prefabRoot.transform.localScale = Vector3.one;
            prefabRoot.transform.localRotation = Quaternion.identity;
            prefabRoot.tag = "Obstacle";

            // 5. 루트 BoxCollider 정밀 설정 (Tag: Obstacle, isTrigger: true)
            BoxCollider col = prefabRoot.GetComponent<BoxCollider>();
            if (col == null) col = prefabRoot.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.80f, 0.66f, 1.78f);
            col.center = new Vector3(0f, 0.33f, 0f);

            // 6. FastMotorcycle 컴포넌트 확인 및 연결
            FastMotorcycle fastMotorcycle = prefabRoot.GetComponent<FastMotorcycle>();
            if (fastMotorcycle == null) fastMotorcycle = prefabRoot.AddComponent<FastMotorcycle>();
            SerializedObject so = new SerializedObject(fastMotorcycle);
            so.FindProperty("_vehicleDisplayName").stringValue = "승용차";
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, FastVehiclePrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[KoreanStreetSetupTool] PF_Motorcycle ➡️ 초고속 스포츠카(Sport Car_39) 3D 모델 세팅 완료! (폭 0.75m, 길이 1.70m, 차고 0.41m)");
        }

        [MenuItem("CampusRun/초고속 질주 차량 모델 세팅 (오토바이 스쿠터)")]
        public static void SetupFastScooterPrefab()
        {
            Debug.Log("[KoreanStreetSetupTool] === 초고속 스쿠터(Scooter_1) 프리팹 세팅 시작 ===");

            GameObject scooterSrc = AssetDatabase.LoadAssetAtPath<GameObject>(ScooterSourcePrefabPath);
            if (scooterSrc == null)
            {
                Debug.LogError($"[KoreanStreetSetupTool] 스쿠터 소스 프리팹을 찾을 수 없습니다: {ScooterSourcePrefabPath}");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(FastVehiclePrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[KoreanStreetSetupTool] 대상 프리팹을 찾을 수 없습니다: {FastVehiclePrefabPath}");
                return;
            }

            // 1. 기존 임시 큐브 렌더러/필터 제거
            MeshFilter oldMf = prefabRoot.GetComponent<MeshFilter>();
            if (oldMf != null) Object.DestroyImmediate(oldMf);

            MeshRenderer oldMr = prefabRoot.GetComponent<MeshRenderer>();
            if (oldMr != null) Object.DestroyImmediate(oldMr);

            // 2. 기존 모든 자식 오브젝트 정리
            for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(prefabRoot.transform.GetChild(i).gameObject);
            }

            // 3. 스쿠터 3D 모델 인스턴스화
            GameObject visual = Object.Instantiate(scooterSrc, prefabRoot.transform);
            visual.name = "VisualModel";

            BoxCollider visualCol = visual.GetComponent<BoxCollider>();
            if (visualCol != null) Object.DestroyImmediate(visualCol);

            // 질주 방향(+Z)에 맞춘 회전 및 위치 정규화
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 1.15f;

            // 4. 루트 트랜스폼 및 태그 세팅
            prefabRoot.transform.localScale = Vector3.one;
            prefabRoot.transform.localRotation = Quaternion.identity;
            prefabRoot.tag = "Obstacle";

            // 5. 루트 BoxCollider 정밀 설정
            BoxCollider col = prefabRoot.GetComponent<BoxCollider>();
            if (col == null) col = prefabRoot.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.48f, 0.90f, 0.85f);
            col.center = new Vector3(0f, 0.45f, 0f);

            FastMotorcycle fastMotorcycle = prefabRoot.GetComponent<FastMotorcycle>();
            if (fastMotorcycle == null) fastMotorcycle = prefabRoot.AddComponent<FastMotorcycle>();
            SerializedObject so = new SerializedObject(fastMotorcycle);
            so.FindProperty("_vehicleDisplayName").stringValue = "오토바이";
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, FastVehiclePrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[KoreanStreetSetupTool] PF_Motorcycle ➡️ 초고속 전동 스쿠터(Scooter_1) 3D 모델 세팅 완료!");
        }

        [MenuItem("CampusRun/초고속 질주 차량 모델 세팅 (경찰차)")]
        public static void SetupFastPoliceCarPrefab()
        {
            Debug.Log("[KoreanStreetSetupTool] === 초고속 경찰차(Police Car N_4) 프리팹 세팅 시작 ===");

            GameObject policeSrc = AssetDatabase.LoadAssetAtPath<GameObject>(PoliceCarSourcePrefabPath);
            if (policeSrc == null)
            {
                Debug.LogError($"[KoreanStreetSetupTool] 경찰차 소스 프리팹을 찾을 수 없습니다: {PoliceCarSourcePrefabPath}");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(FastVehiclePrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[KoreanStreetSetupTool] 대상 프리팹을 찾을 수 없습니다: {FastVehiclePrefabPath}");
                return;
            }

            // 1. 기존 임시 큐브 렌더러/필터 제거
            MeshFilter oldMf = prefabRoot.GetComponent<MeshFilter>();
            if (oldMf != null) Object.DestroyImmediate(oldMf);

            MeshRenderer oldMr = prefabRoot.GetComponent<MeshRenderer>();
            if (oldMr != null) Object.DestroyImmediate(oldMr);

            // 2. 기존 모든 자식 오브젝트 정리
            for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(prefabRoot.transform.GetChild(i).gameObject);
            }

            // 3. 경찰차 3D 모델 인스턴스화
            GameObject visual = Object.Instantiate(policeSrc, prefabRoot.transform);
            visual.name = "VisualModel";

            BoxCollider visualCol = visual.GetComponent<BoxCollider>();
            if (visualCol != null) Object.DestroyImmediate(visualCol);

            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // 원본 경찰차: 폭 2.13m, 높이 1.38m(경광등 포함), 길이 4.97m
            // 0.38f 스케일 적용 -> 폭 0.81m, 높이 0.53m, 길이 1.89m
            visual.transform.localScale = Vector3.one * 0.38f;

            // 4. 루트 트랜스폼 및 태그 세팅
            prefabRoot.transform.localScale = Vector3.one;
            prefabRoot.transform.localRotation = Quaternion.identity;
            prefabRoot.tag = "Obstacle";

            // 5. 루트 BoxCollider 정밀 설정 (원본 1:1:1 비율에 완벽 일치)
            BoxCollider col = prefabRoot.GetComponent<BoxCollider>();
            if (col == null) col = prefabRoot.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(0.80f, 0.53f, 1.88f);
            col.center = new Vector3(0f, 0.37f, -0.04f);

            FastMotorcycle fastMotorcycle = prefabRoot.GetComponent<FastMotorcycle>();
            if (fastMotorcycle == null) fastMotorcycle = prefabRoot.AddComponent<FastMotorcycle>();
            SerializedObject so = new SerializedObject(fastMotorcycle);
            so.FindProperty("_vehicleDisplayName").stringValue = "경찰차";
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, FastVehiclePrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[KoreanStreetSetupTool] PF_Motorcycle ➡️ 초고속 긴급 출동 경찰차(Police Car N_4) 3D 모델 세팅 완료!");
        }

        #endregion
    }
}
