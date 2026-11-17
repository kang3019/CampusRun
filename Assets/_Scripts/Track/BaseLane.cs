using UnityEngine;
using UnityEngine.Rendering;

namespace CampusRun.Track
{
    /// <summary>
    /// 길건너 친구들의 1개 행(Row) 단위를 구성하는 기본 레인 추상 클래스입니다.
    /// 안전 레인(보도블록), 도로 레인(셔틀버스), 하천 레인(징검다리) 등이 이를 상속받습니다.
    /// </summary>
    public abstract class BaseLane : MonoBehaviour
    {
        [Header("--- 레인 기본 속성 ---")]
        [Tooltip("레인의 Z축 격자 인덱스")]
        [SerializeField] private int _laneZIndex = 0;

        [Tooltip("레인의 좌우 가로 폭 (기본 20 World Unit)")]
        [SerializeField] protected float _laneWidth = 20f;

        [Tooltip("플레이어가 쉴 수 있는 안전지대 여부")]
        [SerializeField] protected bool _isSafeLane = false;

        private const int BoundaryVersion = 6;
        private int _preparedBoundaryVersion = 0;
        private Transform _naturalBoundaryTransform;

        private static Material _cachedCurbMat;
        private static Material _cachedGrassMat;
        private static Material _cachedWhiteFoamMat;
        private static Material _cachedInnerWaterMat;

        public int LaneZIndex => _laneZIndex;
        public bool IsSafeLane => _isSafeLane;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBoundaryMaterials()
        {
            _cachedCurbMat = null;
            _cachedGrassMat = null;
            _cachedWhiteFoamMat = null;
            _cachedInnerWaterMat = null;
        }

        /// <summary>
        /// 레인이 풀에서 꺼내져 맵에 배치될 때 호출되는 초기화 메서드입니다.
        /// </summary>
        public virtual void Initialize(int zIndex)
        {
            _laneZIndex = zIndex;
            transform.position = new Vector3(0f, 0f, zIndex);
            gameObject.SetActive(true);

            if (_preparedBoundaryVersion != BoundaryVersion)
            {
                _preparedBoundaryVersion = BoundaryVersion;
                EnsurePlanterCurbBoundary();
            }

            if (_naturalBoundaryTransform != null)
            {
                _naturalBoundaryTransform.localScale = new Vector3(1f, 1f, (zIndex & 1) == 0 ? 1f : -1f);
            }

            OnLaneSpawned();
        }

        /// <summary>
        /// 구버전 경계 오브젝트를 비활성화하고, 인도(화단/연석) 및 하천(안쪽 밝은 수면 + 양방향 대칭 도트 물거품 띠)을 보장합니다.
        /// 차도에는 경계선을 표시하지 않습니다.
        /// </summary>
        private void EnsurePlanterCurbBoundary()
        {
            string[] legacyNames =
            {
                "ToneDown_Boundaries",
                "PlanterCurb_Boundaries",
                "Natural_Boundaries_V2",
                "Natural_Boundaries_V3",
                "Natural_Boundaries_V4"
            };
            for (int i = 0; i < legacyNames.Length; i++)
            {
                Transform old = transform.Find(legacyNames[i]);
                if (old != null)
                {
                    old.gameObject.SetActive(false);
                }
            }

            EnsureBoundaryMaterials(GetComponent<MeshRenderer>());

            Transform v5 = transform.Find("Natural_Boundaries_V5");
            if (v5 != null)
            {
                _naturalBoundaryTransform = v5;
                if (GetComponent<SafeLane>() != null)
                {
                    NormalizeSafeLaneCurbsAndGrass(gameObject, v5);
                }
                else if (GetComponent<WaterLane>() != null)
                {
                    MeshRenderer[] renderers = v5.GetComponentsInChildren<MeshRenderer>(true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        MeshRenderer r = renderers[i];
                        if (r == null) continue;
                        if (r.gameObject.name == "Water_Inner_Surface")
                        {
                            if (_cachedInnerWaterMat != null) r.sharedMaterial = _cachedInnerWaterMat;
                        }
                        else if (_cachedWhiteFoamMat != null)
                        {
                            r.sharedMaterial = _cachedWhiteFoamMat;
                        }
                    }
                }
                return;
            }

            BuildPlanterCurbBoundaryForLane(gameObject, _cachedCurbMat, _cachedGrassMat, _cachedWhiteFoamMat, _cachedInnerWaterMat);
            _naturalBoundaryTransform = transform.Find("Natural_Boundaries_V5");
        }

        private static void EnsureBoundaryMaterials(MeshRenderer referenceRenderer)
        {
#if UNITY_EDITOR
            if (_cachedCurbMat == null)
            {
                _cachedCurbMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/M_Curb_Stone.mat");
            }
            if (_cachedGrassMat == null)
            {
                _cachedGrassMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/M_Grass_Vibrant.mat");
            }
            if (_cachedWhiteFoamMat == null)
            {
                _cachedWhiteFoamMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/M_Crosswalk_White.mat");
                if (_cachedWhiteFoamMat == null)
                {
                    _cachedWhiteFoamMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/M_Road_Marking.mat");
                }
            }
            if (_cachedInnerWaterMat == null)
            {
                _cachedInnerWaterMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/M_Water_Inner.mat");
            }
#endif
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null && referenceRenderer != null && referenceRenderer.sharedMaterial != null)
            {
                litShader = referenceRenderer.sharedMaterial.shader;
            }
            if (litShader == null)
            {
                litShader = Shader.Find("Standard");
            }

            if (_cachedCurbMat == null && litShader != null)
            {
                _cachedCurbMat = new Material(litShader);
                Color curbColor = new Color(0.60f, 0.60f, 0.62f, 1f);
                _cachedCurbMat.color = curbColor;
                if (_cachedCurbMat.HasProperty("_BaseColor")) _cachedCurbMat.SetColor("_BaseColor", curbColor);
                if (_cachedCurbMat.HasProperty("_Smoothness")) _cachedCurbMat.SetFloat("_Smoothness", 0.15f);
            }

            if (_cachedGrassMat == null && litShader != null)
            {
                _cachedGrassMat = new Material(litShader);
                Color grassColor = new Color(0.32f, 0.68f, 0.32f, 1f);
                _cachedGrassMat.color = grassColor;
                if (_cachedGrassMat.HasProperty("_BaseColor")) _cachedGrassMat.SetColor("_BaseColor", grassColor);
                if (_cachedGrassMat.HasProperty("_Smoothness")) _cachedGrassMat.SetFloat("_Smoothness", 0.05f);
            }

            if (_cachedWhiteFoamMat == null && litShader != null)
            {
                _cachedWhiteFoamMat = new Material(litShader);
                Color foamColor = Color.white;
                _cachedWhiteFoamMat.color = foamColor;
                if (_cachedWhiteFoamMat.HasProperty("_BaseColor")) _cachedWhiteFoamMat.SetColor("_BaseColor", foamColor);
                if (_cachedWhiteFoamMat.HasProperty("_Smoothness")) _cachedWhiteFoamMat.SetFloat("_Smoothness", 0.05f);
            }

            if (_cachedInnerWaterMat == null)
            {
                // 바깥쪽 기본 하천(RGB: 0.12, 0.52, 0.72)보다 밝기를 한 톤 높인 청량한 안쪽 수면 색상(RGB: 0.19, 0.61, 0.81)
                Shader waterShader = (referenceRenderer != null && referenceRenderer.sharedMaterial != null)
                    ? referenceRenderer.sharedMaterial.shader
                    : litShader;

                if (waterShader != null)
                {
                    if (referenceRenderer != null && referenceRenderer.sharedMaterial != null)
                    {
                        _cachedInnerWaterMat = new Material(referenceRenderer.sharedMaterial);
                    }
                    else
                    {
                        _cachedInnerWaterMat = new Material(waterShader);
                    }

                    Color innerWaterColor = new Color(0.19f, 0.61f, 0.81f, 1f);
                    _cachedInnerWaterMat.color = innerWaterColor;
                    if (_cachedInnerWaterMat.HasProperty("_BaseColor")) _cachedInnerWaterMat.SetColor("_BaseColor", innerWaterColor);
                    if (_cachedInnerWaterMat.HasProperty("_Color")) _cachedInnerWaterMat.SetColor("_Color", innerWaterColor);
                }
            }
        }

        public static void BuildPlanterCurbBoundaryForLane(
            GameObject laneRoot,
            Material matCurb,
            Material matGrass,
            Material matWhiteFoam = null,
            Material matInnerWater = null)
        {
            if (laneRoot == null || matCurb == null || matGrass == null) return;

            string[] oldNames =
            {
                "ToneDown_Boundaries",
                "PlanterCurb_Boundaries",
                "Natural_Boundaries_V2",
                "Natural_Boundaries_V3",
                "Natural_Boundaries_V4",
                "Natural_Boundaries_V5"
            };
            for (int i = 0; i < oldNames.Length; i++)
            {
                Transform existing = laneRoot.transform.Find(oldNames[i]);
                if (existing != null)
                {
                    DestroyImmediate(existing.gameObject);
                }
            }

            bool isSafe = laneRoot.GetComponent<SafeLane>() != null;
            bool isWater = laneRoot.GetComponent<WaterLane>() != null;

            // 차도(RoadLane, MotorcycleLane)에는 경계선을 일절 생성하지 않고 깔끔하게 유지합니다.
            if (!isSafe && !isWater)
            {
                return;
            }

            EnsureBoundaryMaterials(laneRoot.GetComponent<MeshRenderer>());
            if (matWhiteFoam == null) matWhiteFoam = _cachedWhiteFoamMat ?? matCurb;
            if (matInnerWater == null) matInnerWater = _cachedInnerWaterMat;

            Vector3 parentScale = laneRoot.transform.localScale;
            float invX = 1f / Mathf.Max(0.001f, parentScale.x);
            float invY = 1f / Mathf.Max(0.001f, parentScale.y);

            GameObject container = new GameObject("Natural_Boundaries_V5");
            container.transform.SetParent(laneRoot.transform, false);

            if (isSafe)
            {
                // 1. 안전 보도블록 레인:
                // 전후방 연석(Curb_Front/Back) 및 점자블록이 좌우 잔디밭까지 침범하지 않도록 중앙 인도 폭(-4.73m ~ +4.73m)으로 제한하고,
                // 좌우 경계석(Curb_Step_Left/Right)과 잔디밭(Planter_Grass_Left/Right)의 Z 길이를 1.0f(전체 폭)로 꽉 채워
                // 연속된 잔디 레인 사이에 띄엄띄엄 빈 공간(회색 틈)이 생기지 않도록 완전히 밀착시킵니다.
                TrimCentralSidewalkDecorations(laneRoot.transform, invX);

                CreateBoundaryBlock("Curb_Step_Left", container.transform, matCurb,
                    new Vector3(-4.80f * invX, 0.52f, 0f),
                    new Vector3(0.14f * invX, 0.22f, 1.0f));
                CreateBoundaryBlock("Curb_Step_Right", container.transform, matCurb,
                    new Vector3(4.80f * invX, 0.52f, 0f),
                    new Vector3(0.14f * invX, 0.22f, 1.0f));

                // 좌우 화단 잔디밭 (Z=-0.50~+0.50 전체 구간 100% 빈틈없이 연결)
                CreateBoundaryBlock("Planter_Grass_Left", container.transform, matGrass,
                    new Vector3(-17.435f * invX, 0.045f, 0f),
                    new Vector3(25.13f * invX, 1.15f, 1.0f));
                CreateBoundaryBlock("Planter_Grass_Right", container.transform, matGrass,
                    new Vector3(17.435f * invX, 0.045f, 0f),
                    new Vector3(25.13f * invX, 1.15f, 1.0f));
            }
            else if (isWater)
            {
                // 2. 하천 레인 (길건너 친구들 원작 이미지 스타일):
                float baseBoundaryX = 4.85f;

                // (1) 안쪽 플레이 구역(-4.85m ~ +4.85m, 폭 9.70m) 수면 밝기를 약간 높여
                // 물거품 띠를 경계로 안쪽(밝은 하천)과 바깥쪽(깊은 하천)이 미세하고 자연스럽게 구분되도록 합니다.
                if (matInnerWater != null)
                {
                    CreateBoundaryBlock("Water_Inner_Surface", container.transform, matInnerWater,
                        new Vector3(0f, 0.504f, 0f),
                        new Vector3((baseBoundaryX * 2f) * invX, 0.006f * invY, 1.0f),
                        receiveShadows: true);
                }

                // (2) 경계선(X = ±4.85m) 위를 덮는 안/밖 대칭형 8단 계단식 도트 순백색 물거품 띠
                float foamLocalY = 0.515f;
                float foamThicknessY = 0.015f * invY;

                // Z = -0.5 ~ +0.5 구간을 8등분하여 안쪽과 바깥쪽 모두 동일한 리듬으로 들쑥날쑥한 픽셀 포말 띠 구성
                // 양 끝(0번, 7번)의 폭(0.54m)과 중심 편차(0.00m)를 일치시켜 앞뒤 하천 레인끼리도 끊김 없이 연결됩니다.
                float[] sliceZ = { -0.4375f, -0.3125f, -0.1875f, -0.0625f, 0.0625f, 0.1875f, 0.3125f, 0.4375f };
                float[] sliceWidthX = { 0.54f, 0.68f, 0.78f, 0.62f, 0.50f, 0.64f, 0.76f, 0.54f };
                float[] sliceShiftX = { 0.00f, 0.02f, -0.02f, 0.03f, -0.02f, 0.02f, -0.02f, 0.00f };

                for (int i = 0; i < sliceZ.Length; i++)
                {
                    float sz = sliceZ[i];
                    float sw = sliceWidthX[i];
                    float sx = sliceShiftX[i];

                    CreateBoundaryBlock($"Water_PixelBand_L_{i}", container.transform, matWhiteFoam,
                        new Vector3((-baseBoundaryX + sx) * invX, foamLocalY, sz),
                        new Vector3(sw * invX, foamThicknessY, 0.128f));

                    CreateBoundaryBlock($"Water_PixelBand_R_{i}", container.transform, matWhiteFoam,
                        new Vector3((baseBoundaryX - sx) * invX, foamLocalY, -sz),
                        new Vector3(sw * invX, foamThicknessY, 0.128f));
                }

                // (3) 안쪽과 바깥쪽 양방향 모두에 대칭적인 패턴으로 흩뿌려진 도트 물방울 조각
                // 안쪽(강 중앙 방향) 도트 물방울 3개 + 바깥쪽(강 외곽 방향) 도트 물방울 3개
                float[] innerDotOffsetX = { 0.44f, 0.38f, 0.46f };
                float[] innerDotOffsetZ = { -0.30f, 0.06f, 0.33f };
                float[] innerDotSize = { 0.13f, 0.12f, 0.14f };

                float[] outerDotOffsetX = { -0.45f, -0.39f, -0.44f };
                float[] outerDotOffsetZ = { -0.16f, 0.20f, -0.38f };
                float[] outerDotSize = { 0.13f, 0.14f, 0.12f };

                for (int i = 0; i < innerDotOffsetX.Length; i++)
                {
                    // 안쪽 물방울 (좌/우)
                    float idx = innerDotOffsetX[i];
                    float idz = innerDotOffsetZ[i];
                    float ids = innerDotSize[i];

                    CreateBoundaryBlock($"Water_PixelDot_In_L_{i}", container.transform, matWhiteFoam,
                        new Vector3((-baseBoundaryX + idx) * invX, foamLocalY, idz),
                        new Vector3(ids * invX, foamThicknessY, ids));

                    CreateBoundaryBlock($"Water_PixelDot_In_R_{i}", container.transform, matWhiteFoam,
                        new Vector3((baseBoundaryX - idx) * invX, foamLocalY, -idz),
                        new Vector3(ids * invX, foamThicknessY, ids));

                    // 바깥쪽 물방울 (좌/우)
                    float odx = outerDotOffsetX[i];
                    float odz = outerDotOffsetZ[i];
                    float ods = outerDotSize[i];

                    CreateBoundaryBlock($"Water_PixelDot_Out_L_{i}", container.transform, matWhiteFoam,
                        new Vector3((-baseBoundaryX + odx) * invX, foamLocalY, odz),
                        new Vector3(ods * invX, foamThicknessY, ods));

                    CreateBoundaryBlock($"Water_PixelDot_Out_R_{i}", container.transform, matWhiteFoam,
                        new Vector3((baseBoundaryX - odx) * invX, foamLocalY, -odz),
                        new Vector3(ods * invX, foamThicknessY, ods));
                }
            }
        }

        private static void TrimCentralSidewalkDecorations(Transform laneTransform, float invX)
        {
            if (laneTransform == null) return;
            float centralWalkwayScaleX = 9.46f * invX;

            Transform curbDecor = laneTransform.Find("CurbDecorations");
            if (curbDecor != null)
            {
                Transform curbFront = curbDecor.Find("Curb_Front");
                if (curbFront != null)
                {
                    Vector3 s = curbFront.localScale;
                    curbFront.localScale = new Vector3(centralWalkwayScaleX, s.y, s.z);
                }

                Transform curbBack = curbDecor.Find("Curb_Back");
                if (curbBack != null)
                {
                    Vector3 s = curbBack.localScale;
                    curbBack.localScale = new Vector3(centralWalkwayScaleX, s.y, s.z);
                }
            }

            Transform brailleGroup = laneTransform.Find("BrailleBlockGroup");
            if (brailleGroup != null)
            {
                Transform brailleLine = brailleGroup.Find("Braille_Line");
                if (brailleLine != null)
                {
                    Vector3 s = brailleLine.localScale;
                    brailleLine.localScale = new Vector3(centralWalkwayScaleX, s.y, s.z);
                }
            }
        }

        private static void NormalizeSafeLaneCurbsAndGrass(GameObject laneRoot, Transform boundaryContainer)
        {
            if (laneRoot == null || boundaryContainer == null) return;

            float invX = 1f / Mathf.Max(0.001f, laneRoot.transform.localScale.x);
            TrimCentralSidewalkDecorations(laneRoot.transform, invX);

            Transform stepL = boundaryContainer.Find("Curb_Step_Left");
            if (stepL != null)
            {
                stepL.localPosition = new Vector3(-4.80f * invX, 0.52f, 0f);
                stepL.localScale = new Vector3(0.14f * invX, 0.22f, 1.0f);
            }

            Transform stepR = boundaryContainer.Find("Curb_Step_Right");
            if (stepR != null)
            {
                stepR.localPosition = new Vector3(4.80f * invX, 0.52f, 0f);
                stepR.localScale = new Vector3(0.14f * invX, 0.22f, 1.0f);
            }

            Transform grassL = boundaryContainer.Find("Planter_Grass_Left");
            if (grassL != null)
            {
                grassL.localPosition = new Vector3(-17.435f * invX, 0.045f, 0f);
                grassL.localScale = new Vector3(25.13f * invX, 1.15f, 1.0f);
            }

            Transform grassR = boundaryContainer.Find("Planter_Grass_Right");
            if (grassR != null)
            {
                grassR.localPosition = new Vector3(17.435f * invX, 0.045f, 0f);
                grassR.localScale = new Vector3(25.13f * invX, 1.15f, 1.0f);
            }
        }

        private static void CreateBoundaryBlock(
            string name,
            Transform parent,
            Material mat,
            Vector3 localPos,
            Vector3 localScale,
            bool receiveShadows = false)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);

            Collider col = cube.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
            }

            MeshRenderer mr = cube.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = receiveShadows;
            }

            cube.transform.localPosition = localPos;
            cube.transform.localScale = localScale;
        }

        /// <summary>
        /// 레인이 화면 뒤로 멀어져 풀(Pool)로 회수될 때 호출되는 정리 메서드입니다.
        /// </summary>
        public virtual void Recycle()
        {
            OnLaneRecycled();
            gameObject.SetActive(false);
        }

        /// <summary> 레인이 스폰될 때 자식 클래스에서 수행할 개별 로직 (장애물 스폰 시작 등) </summary>
        protected virtual void OnLaneSpawned() { }

        /// <summary> 레인이 회수될 때 자식 클래스에서 수행할 개별 로직 (장애물 정리 등) </summary>
        protected virtual void OnLaneRecycled() { }
    }
}
