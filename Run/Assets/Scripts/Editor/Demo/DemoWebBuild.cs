using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ProjectRun.Race.Editor
{
    // Each output supplies an explicit scene list, independent of Editor Build Settings.
    public static class DemoWebBuild
    {
        public const string DemoOutput = "DataBundles/빌드/심사데모_WebGL";
        public const string TestOutput = "DataBundles/빌드/테스트씬_WebGL";
        public const string Records = "DataBundles/빌드/작업기록/웹빌드_2종";
        private static bool queued;
        public static string[] DemoScenes => new[] {
            Define_Scene.Path(Define_Scene.Title), Define_Scene.Path(Define_Scene.Lobby),
            Define_Scene.Path(Define_Scene.OvalToon), Define_Scene.Path(Define_Scene.Sprite2D)
        };

        [MenuItem("Tools/Project Run/Demo/Build Review Demo WebGL")]
        public static void Queue() => Enqueue(false, false);
        public static void QueueTests() => Enqueue(true, false);
        public static void QueueBoth() => Enqueue(false, true);

        private static void Enqueue(bool tests, bool both)
        {
            TestSceneMenuBuilder.RequireIdleScenes();
            if (queued) throw new InvalidOperationException("A web build is already queued.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))
                throw new InvalidOperationException("WebGL support is unavailable.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Switch the active build platform to Web first.");
            if (tests || both) TestSceneMenuBuilder.Build();
            Directory.CreateDirectory(Records);
            File.WriteAllText(StatusPath(tests),"Queued " + DateTime.Now.ToString("O"));
            if (both) File.WriteAllText(StatusPath(true),"Queued " + DateTime.Now.ToString("O"));
            queued = true;
            EditorApplication.CallbackFunction run = null;
            run = () =>
            {
                EditorApplication.update -= run;
                try { Build(tests); if (both) Build(true); }
                finally { queued = false; }
            };
            EditorApplication.update += run;
        }

        private static string StatusPath(bool tests) => Records + (tests ? "/테스트씬_빌드상태.txt" : "/데모_빌드상태.txt");

        private static void Build(bool tests)
        {
            string output = tests ? TestOutput : DemoOutput;
            string[] scenes = tests ? TestSceneMenuBuilder.ScenePaths() : DemoScenes;
            try
            {
                foreach (string scene in scenes)
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new InvalidOperationException("Missing build scene: " + scene);
                File.WriteAllText(StatusPath(tests),"Building " + DateTime.Now.ToString("O") + "\n" + string.Join("\n",scenes));
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.WebGL.template = "PROJECT:ProjectRunLandscape";
                PlayerSettings.defaultWebScreenWidth = 1280;
                PlayerSettings.defaultWebScreenHeight = 720;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes, target = BuildTarget.WebGL, locationPathName = output, options = BuildOptions.DetailedBuildReport
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build result: " + report.summary.result);
                File.Copy("../Tools/LocalWeb/Serve.ps1",output + "/Serve.ps1",true);
                File.Copy(tests ? "Tools/Demo/PlayTestScenes.cmd" : "Tools/Demo/Play.cmd",output + "/Play.cmd",true);
                string launcher = tests ? "StartTestScenes.ps1" : "StartDemo.ps1";
                File.Copy("Tools/Demo/" + launcher,output + "/" + launcher,true);
                File.WriteAllText(output + "/demo-id.txt",tests ? "project-run-test-scenes" : "project-run-review-demo");
                string manifest = (tests ? "테스트 씬 웹 빌드" : "심사 데모 웹 빌드") + "\n" + DateTime.Now.ToString("O")
                    + "\n진입 씬: " + scenes[0] + "\n포함 씬:\n" + string.Join("\n",scenes)
                    + "\nUI 기준: 1280×720 / 기존 PC 품질 유지\n실행: Play.cmd\n";
                File.WriteAllText(output + "/빌드구성.txt",manifest);
                string status = "Succeeded\n" + DateTime.Now.ToString("O") + "\nBytes: " + report.summary.totalSize
                    + "\nSeconds: " + report.summary.totalTime.TotalSeconds + "\n" + Path.GetFullPath(output) + "\n" + string.Join("\n",scenes);
                File.WriteAllText(StatusPath(tests),status);
                UnityEngine.Debug.Log((tests ? "TEST_SCENES" : "DEMO") + "_WEB_BUILD_SUCCEEDED " + output);
            }
            catch (Exception exception) { File.WriteAllText(StatusPath(tests),"Failed\n" + exception); UnityEngine.Debug.LogException(exception); }
        }
    }
}
