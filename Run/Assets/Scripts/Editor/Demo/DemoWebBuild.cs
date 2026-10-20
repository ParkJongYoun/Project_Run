using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ProjectRun.Race.Editor
{
    // The game supplies an explicit scene list, independent of Editor Build Settings.
    public static class DemoWebBuild
    {
        public const string DemoOutput = "DataBundles/빌드/심사데모_WebGL";
        public const string Records = "DataBundles/빌드/작업기록/심사데모_웹빌드";
        private static bool queued;
        public static string[] DemoScenes => new[] {
            Define_Scene.Path(Define_Scene.Title), Define_Scene.Path(Define_Scene.Lobby),
            Define_Scene.Path(Define_Scene.OvalToon)
        };

        [MenuItem("Tools/Project Run/Demo/Build Review Demo WebGL")]
        public static void Queue() => Enqueue();

        private static void Enqueue()
        {
            RequireIdleScenes();
            if (queued) throw new InvalidOperationException("A web build is already queued.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))
                throw new InvalidOperationException("WebGL support is unavailable.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Switch the active build platform to Web first.");
            Directory.CreateDirectory(Records);
            File.WriteAllText(StatusPath,"Queued " + DateTime.Now.ToString("O"));
            queued = true;
            EditorApplication.CallbackFunction run = null;
            run = () =>
            {
                EditorApplication.update -= run;
                try { Build(); }
                finally { queued = false; }
            };
            EditorApplication.update += run;
        }

        private static string StatusPath => Records + "/데모_빌드상태.txt";

        private static void RequireIdleScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Stop Play and wait for compilation/building.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene edits first.");
        }

        private static void Build()
        {
            string output = DemoOutput;
            string[] scenes = DemoScenes;
            try
            {
                foreach (string scene in scenes)
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new InvalidOperationException("Missing build scene: " + scene);
                File.WriteAllText(StatusPath,"Building " + DateTime.Now.ToString("O") + "\n" + string.Join("\n",scenes));
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.template = "PROJECT:ProjectRunLandscape";
                PlayerSettings.defaultWebScreenWidth = 1280;
                PlayerSettings.defaultWebScreenHeight = 720;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes, target = BuildTarget.WebGL, locationPathName = output, options = BuildOptions.DetailedBuildReport
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build result: " + report.summary.result);
                if (!File.Exists(output + "/Play.exe")) throw new InvalidOperationException("Play.exe was not packaged.");
                File.WriteAllText(output + "/demo-id.txt","project-run-review-demo");
                string manifest = "심사 데모 웹 빌드\n" + DateTime.Now.ToString("O")
                    + "\n진입 씬: " + scenes[0] + "\n포함 씬:\n" + string.Join("\n",scenes)
                    + "\nUI 기준: 1280×720 / 기존 PC 품질 유지\n실행: Play.exe\n";
                File.WriteAllText(output + "/빌드구성.txt",manifest);
                string status = "Succeeded\n" + DateTime.Now.ToString("O") + "\nBytes: " + report.summary.totalSize
                    + "\nSeconds: " + report.summary.totalTime.TotalSeconds + "\n" + Path.GetFullPath(output) + "\n" + string.Join("\n",scenes);
                File.WriteAllText(StatusPath,status);
                UnityEngine.Debug.Log("DEMO_WEB_BUILD_SUCCEEDED " + output);
            }
            catch (Exception exception) { File.WriteAllText(StatusPath,"Failed\n" + exception); UnityEngine.Debug.LogException(exception); }
        }
    }
}
