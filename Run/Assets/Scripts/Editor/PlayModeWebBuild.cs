using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ProjectRun.Editor
{
    public static class PlayModeWebBuild
    {
        [MenuItem("Tools/Project Run/Build Web - Game")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install Web Build Support for this Editor in Unity Hub first.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Select Web in File > Build Profiles and Switch Platform first.");
            if (EditorApplication.isPlaying || EditorApplication.isCompiling)
                throw new InvalidOperationException("Wait for compilation and stop Play mode first.");
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.template = "PROJECT:ProjectRunPortrait";
            PlayerSettings.defaultWebScreenWidth = 720;
            PlayerSettings.defaultWebScreenHeight = 1280;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = PlayModeMenuBuilder.ScenePaths, target = BuildTarget.WebGL,
                locationPathName = "Builds/WebGL_PlayModes", options = BuildOptions.DetailedBuildReport
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Web build failed: " + report.summary.result);
            foreach (var file in new[] { "Play.cmd", "Serve.ps1" })
                File.Copy(Path.Combine("../Tools/LocalWeb", file), Path.Combine("Builds/WebGL_PlayModes",file),true);
            UnityEngine.Debug.Log("Web build ready: Builds/WebGL_PlayModes/Play.cmd");
        }
    }
}
