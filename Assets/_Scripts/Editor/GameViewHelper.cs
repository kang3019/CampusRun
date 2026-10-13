using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CampusRun.EditorTools
{
    /// <summary>
    /// 에디터 Game View를 16:9 비율 및 Scale 1배(1x)로 맞추는 유틸리티 도구입니다.
    /// </summary>
    public static class GameViewHelper
    {
        [MenuItem("CampusRun/🎮 게임 뷰 16:9 (Scale 1배)로 맞추기")]
        public static void SetGameView169AndScale1X()
        {
            SetGameViewAspectRatio169();
            SetGameViewScale1X();
        }

        /// <summary>
        /// Game View의 종횡비 프리셋 목록 중 16:9 또는 1920x1080을 찾아 선택합니다.
        /// </summary>
        public static void SetGameViewAspectRatio169()
        {
            try
            {
                Assembly editorAssembly = typeof(Editor).Assembly;
                Type gameViewSizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
                if (gameViewSizesType == null) return;

                Type singleType = typeof(ScriptableSingleton<>).MakeGenericType(gameViewSizesType);
                PropertyInfo instanceProp = singleType.GetProperty("instance");
                if (instanceProp == null) return;

                object instance = instanceProp.GetValue(null, null);
                if (instance == null) return;

                MethodInfo getGroupMethod = gameViewSizesType.GetMethod("GetGroup");
                if (getGroupMethod == null) return;

                object group = getGroupMethod.Invoke(instance, new object[] { GameViewSizeGroupType.Standalone });
                if (group == null) return;

                MethodInfo getTotalCountMethod = group.GetType().GetMethod("GetTotalCount");
                MethodInfo getGameViewSizeMethod = group.GetType().GetMethod("GetGameViewSize");
                if (getTotalCountMethod == null || getGameViewSizeMethod == null) return;

                int targetIndex = -1;
                int totalCount = (int)getTotalCountMethod.Invoke(group, null);

                for (int i = 0; i < totalCount; i++)
                {
                    object gameViewSize = getGameViewSizeMethod.Invoke(group, new object[] { i });
                    if (gameViewSize == null) continue;

                    PropertyInfo textProp = gameViewSize.GetType().GetProperty("baseText");
                    string text = textProp?.GetValue(gameViewSize, null) as string;

                    if (!string.IsNullOrEmpty(text) && (text.Contains("16:9") || text.Contains("1920x1080") || text.Contains("1920 x 1080")))
                    {
                        targetIndex = i;
                        break;
                    }
                }

                Type gameViewType = editorAssembly.GetType("UnityEditor.GameView") ?? editorAssembly.GetType("UnityEditor.PlayModeView");
                if (gameViewType == null) return;

                EditorWindow gameViewWindow = EditorWindow.GetWindow(gameViewType);
                if (targetIndex != -1 && gameViewWindow != null)
                {
                    MethodInfo sizeSelectionCallback = gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (sizeSelectionCallback != null)
                    {
                        sizeSelectionCallback.Invoke(gameViewWindow, new object[] { targetIndex, null });
                        Debug.Log($"[CampusRun] ✅ Game View 비율을 16:9 (Index: {targetIndex})로 설정했습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampusRun] Game View 16:9 자동 설정 안내: 상단 툴바에서 16:9를 선택해주세요. (상세: {ex.Message})");
            }
        }

        /// <summary>
        /// Game View의 줌/스케일 슬라이더를 1배(1.0x)로 초기화합니다.
        /// </summary>
        public static void SetGameViewScale1X()
        {
            try
            {
                Assembly editorAssembly = typeof(EditorWindow).Assembly;
                Type gameViewType = editorAssembly.GetType("UnityEditor.GameView") ?? editorAssembly.GetType("UnityEditor.PlayModeView");
                if (gameViewType == null) return;

                EditorWindow gameViewWindow = EditorWindow.GetWindow(gameViewType);
                if (gameViewWindow == null) return;

                FieldInfo zoomAreaField = gameViewType.GetField("m_ZoomArea", BindingFlags.Instance | BindingFlags.NonPublic);
                if (zoomAreaField != null)
                {
                    object zoomAreaObj = zoomAreaField.GetValue(gameViewWindow);
                    if (zoomAreaObj != null)
                    {
                        FieldInfo scaleField = zoomAreaObj.GetType().GetField("m_Scale", BindingFlags.Instance | BindingFlags.NonPublic);
                        MethodInfo setScaleFocusedMethod = zoomAreaObj.GetType().GetMethod("SetScaleFocused", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        Vector2 targetScale = new Vector2(1f, 1f);
                        if (setScaleFocusedMethod != null)
                        {
                            Rect pos = gameViewWindow.position;
                            Vector2 focalPoint = new Vector2(pos.width * 0.5f, pos.height * 0.5f);
                            setScaleFocusedMethod.Invoke(zoomAreaObj, new object[] { focalPoint, targetScale });
                        }
                        else if (scaleField != null)
                        {
                            scaleField.SetValue(zoomAreaObj, targetScale);
                        }

                        gameViewWindow.Repaint();
                        Debug.Log("[CampusRun] ✅ Game View Scale을 1배(1x)로 설정했습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampusRun] Game View Scale 자동 설정 안내: 상단 툴바 Scale 슬라이더를 1x로 맞춰주세요. (상세: {ex.Message})");
            }
        }
    }
}
