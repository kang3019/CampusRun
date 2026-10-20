using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CampusRun.EditorTools
{
    /// <summary>
    /// 10월 16일 소프트웨어학과 축제 부스 전시를 위한 단독 실행 파일(.exe) 원클릭 빌드 자동화 스크립트입니다.
    /// 메인 플레이 씬 등록, 1920x1080 전체화면 보더리스 설정 및 StandaloneWindows64 빌드를 총괄합니다.
    /// </summary>
    public static class BuildScript
    {
        private const string BuildOutputDir = "Builds/Windows";
        private const string ExeFileName = "CampusRun.exe";
        private const string MainScenePath = "Assets/_Scenes/Sandboxes/Sandbox_PlayTest.unity";

        [MenuItem("CampusRun/전시용 Windows 빌드 생성 (.exe)")]
        public static void BuildWindows()
        {
            Debug.Log("[BuildScript] === 축제 부스 전시용 Windows 64비트 빌드 시작 ===");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 1. 빌드 씬 확인 및 설정
            if (!File.Exists(MainScenePath))
            {
                Debug.LogError($"[BuildScript] 메인 씬 파일을 찾을 수 없습니다: {MainScenePath}");
                return;
            }

            // EditorBuildSettings 씬 목록 갱신 (메인 씬을 Index 0으로 등록)
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;

            // 2. 출력 디렉토리 준비
            string fullOutputDir = Path.GetFullPath(BuildOutputDir);
            if (!Directory.Exists(fullOutputDir))
            {
                Directory.CreateDirectory(fullOutputDir);
            }
            string buildPath = Path.Combine(fullOutputDir, ExeFileName);

            // 3. 플레이어 설정 최적화 (부스 전시 최적화)
            PlayerSettings.productName = "지각 5분전 (CampusRun)";
            PlayerSettings.companyName = "CampusRun_Team";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.visibleInBackground = true;

            // 4. 빌드 옵션 구성
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            // 5. 빌드 실행
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            sw.Stop();

            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] ✅ 전시용 Windows 빌드 성공! (소요 시간: {sw.ElapsedMilliseconds / 1000f:F1}초, 총 용량: {summary.totalSize / (1024f * 1024f):F1} MB)");
                Debug.Log($"[BuildScript] 📁 실행 파일 위치: {buildPath}");

                if (!Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(buildPath);
                }
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"[BuildScript] ❌ 빌드 실패! (총 에러 수: {summary.totalErrors})");
            }
            else
            {
                Debug.LogWarning($"[BuildScript] ⚠️ 빌드 상태: {summary.result}");
            }
        }
    }
}
