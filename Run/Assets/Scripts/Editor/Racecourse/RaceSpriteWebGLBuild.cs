using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ProjectRun.Editor
{
    // Explicit QA build. Scene order here does not change the project's build settings.
    public static class RaceSpriteWebGLBuild
    {
        private const string output = "DataBundles/검증/2D레이스_20261016/webgl_build.json";

        [MenuItem("Tools/Project Run/Race/Build WebGL review")]
        public static void Start()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish the current Editor operation first.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                throw new InvalidOperationException("Switch the Editor to WebGL first.");
            AssetDatabase.SaveAssets();
            File.WriteAllText(output, JsonConvert.SerializeObject(new { Status = "queued", Started = DateTimeOffset.Now }));
            EditorApplication.update -= Build;
            EditorApplication.update += Build;
        }

        private static void Build()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Build;
            var started = DateTimeOffset.Now;
            try
            {
                File.WriteAllText(output, JsonConvert.SerializeObject(new { Status = "building", Started = started }));
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/Scene_All2D.unity", "Assets/Scenes/Scene_2D_3D.unity", "Assets/Scenes/Scene_All3D.unity" },
                    locationPathName = "Builds/WebGL_2DReview",
                    target = BuildTarget.WebGL,
                    options = BuildOptions.DetailedBuildReport
                });
                File.WriteAllText(output, JsonConvert.SerializeObject(new
                {
                    Status = report.summary.result.ToString(), Started = started, Finished = DateTimeOffset.Now,
                    Errors = report.summary.totalErrors, Warnings = report.summary.totalWarnings,
                    Bytes = report.summary.totalSize, Seconds = report.summary.totalTime.TotalSeconds,
                    Path = report.summary.outputPath,
                    Messages = report.steps.SelectMany(s => s.messages).Where(m => m.type == UnityEngine.LogType.Error || m.type == UnityEngine.LogType.Exception).Select(m => m.content).ToArray()
                }, Formatting.Indented));
            }
            catch (Exception exception)
            {
                File.WriteAllText(output, JsonConvert.SerializeObject(new { Status = "Exception", Started = started, Message = exception.ToString() }, Formatting.Indented));
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}
