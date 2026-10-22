using System;
using System.IO;
using System.Linq;
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

        public static void QueueFast() => Enqueue(true);

        // Synchronous entry point for unity run -executeMethod.
        public static void BuildBatch()
        {
            RequireIdleScenes();
            Directory.CreateDirectory(Records);
            Build();
        }

        private static void Enqueue(bool fast = false)
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
                try { Build(fast); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
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

        private static void Build(bool fast = false)
        {
            string runId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string output = Records + "/새빌드/" + runId + "/심사데모_WebGL";
            string recordDirectory = Records + "/빌드기록/" + runId;
            string[] scenes = DemoScenes;
            try
            {
                foreach (string scene in scenes)
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) throw new InvalidOperationException("Missing build scene: " + scene);
                File.WriteAllText(StatusPath,"Building " + DateTime.Now.ToString("O") + "\n" + string.Join("\n",scenes));
                PlayerSettings.WebGL.compressionFormat = fast ? WebGLCompressionFormat.Disabled : WebGLCompressionFormat.Gzip;
                PlayerSettings.WebGL.decompressionFallback = false;
                UnityEditor.WebGL.UserBuildSettings.codeOptimization = fast
                    ? UnityEditor.WebGL.WasmCodeOptimization.BuildTimes : UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
                PlayerSettings.WebGL.template = "PROJECT:ProjectRunLandscape";
                PlayerSettings.defaultWebScreenWidth = 1280;
                PlayerSettings.defaultWebScreenHeight = 720;
                AssetDatabase.SaveAssets();
                BuildReport report;
                // Keep the previous playable build intact until its replacement succeeds.
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                Directory.CreateDirectory(recordDirectory);
                File.WriteAllText(recordDirectory + "/빌드옵션.txt", "웹 압축: " + PlayerSettings.WebGL.compressionFormat
                    + " / 코드 최적화: " + UnityEditor.WebGL.UserBuildSettings.codeOptimization);
                using (DemoWebBuildResources.Begin(scenes))
                {
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                        scenes = scenes, target = BuildTarget.WebGL, locationPathName = output, options = BuildOptions.DetailedBuildReport
                    });
                }
                CompleteBuild(report, output, runId);
            }
            catch (Exception exception) { File.WriteAllText(StatusPath,"Failed\n" + exception); throw; }
        }

        // Retry packaging after a Windows file lock without repeating a successful compilation.
        [MenuItem("Tools/Project Run/Demo/Publish completed web build")]
        public static void PublishCompletedBuild()
        {
            RequireIdleScenes();
            var report = BuildReport.GetLatestReport();
            if (report == null || report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("There is no successful build to publish.");
            string output = Path.GetFullPath(report.summary.outputPath);
            string staging = Path.GetFullPath(Records + "/새빌드") + Path.DirectorySeparatorChar;
            if (!output.StartsWith(staging, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(output))
                throw new InvalidOperationException("The latest build is not waiting in the game build staging folder.");
            try { CompleteBuild(report, output, Directory.GetParent(output).Name); }
            catch (Exception exception) { File.WriteAllText(StatusPath,"Failed\n" + exception); throw; }
        }

        private static void CompleteBuild(BuildReport report, string output, string runId)
        {
            string[] scenes = DemoScenes;
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Build result: " + report.summary.result);
            foreach (string launcherFile in new[] { "Play.cmd", "Launcher/StartDemo.ps1", "Launcher/Serve.ps1", "Game/index.html" })
                if (!File.Exists(Path.Combine(output, launcherFile))) throw new InvalidOperationException(launcherFile + " was not packaged.");
            var packed = report.packedAssets.SelectMany(a => a.contents).ToArray();
            if (packed.Any(a => a.sourceAssetPath != null && a.sourceAssetPath.StartsWith("Assets/WebBuildExcluded/", StringComparison.Ordinal)))
                throw new InvalidOperationException("An excluded resource was unexpectedly included in the build.");
            File.WriteAllLines(Records + "/포함에셋_크기.tsv",
                new[] { "Bytes\tAsset" }.Concat(packed.OrderByDescending(a => a.packedSize)
                    .Select(a => a.packedSize + "\t" + a.sourceAssetPath)));
            string recordDirectory = Records + "/빌드기록/" + runId;
            Directory.CreateDirectory(recordDirectory);
            string manifest = "심사 데모 웹 빌드\n" + DateTime.Now.ToString("O")
                + "\n진입 씬: " + scenes[0] + "\n포함 씬:\n" + string.Join("\n",scenes)
                + "\nUI 기준: 1280×720 / 기존 PC 품질 유지\n"
                + (File.Exists(recordDirectory + "/빌드옵션.txt") ? File.ReadAllText(recordDirectory + "/빌드옵션.txt") : "빌드 옵션 기록 없음")
                + "\n미사용 리소스: 빌드 중 제외 후 원래 경로로 복원\n실행: Play.cmd\n";
            File.WriteAllText(recordDirectory + "/빌드구성.txt",manifest);
            Publish(output, runId);
            long folderBytes = Directory.GetFiles(DemoOutput, "*", SearchOption.AllDirectories)
                .Sum(path => new FileInfo(path).Length);
            string status = "Succeeded\n" + DateTime.Now.ToString("O") + "\nBytes: " + report.summary.totalSize
                + "\nFolderBytes: " + folderBytes
                + "\nSeconds: " + report.summary.totalTime.TotalSeconds + "\n" + Path.GetFullPath(DemoOutput) + "\n" + string.Join("\n",scenes);
            File.WriteAllText(StatusPath,status);
            UnityEngine.Debug.Log("DEMO_WEB_BUILD_SUCCEEDED " + DemoOutput);
        }

        private static void Publish(string source, string runId)
        {
            string root = Path.GetFullPath("DataBundles/빌드") + Path.DirectorySeparatorChar;
            string destination = Path.GetFullPath(DemoOutput);
            string previous = Path.GetFullPath(Records + "/이전빌드/" + runId + "_" + DateTime.Now.ToString("HHmmssfff"));
            source = Path.GetFullPath(source);
            foreach (string path in new[] { source, destination, previous })
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Build output must stay inside DataBundles/빌드.");
            string[] oldFiles = Directory.Exists(destination) ? RelativeFiles(destination) : Array.Empty<string>();
            string[] newFiles = RelativeFiles(source);
            // Explorer or an open terminal may hold the parent directory on Windows.
            // Back up first, then replace its contents without renaming that directory.
            CopyFiles(destination, previous, oldFiles);
            try
            {
                CopyFiles(source, destination, newFiles);
                foreach (string path in oldFiles.Except(newFiles))
                    File.Delete(Path.Combine(destination, path));
            }
            catch
            {
                CopyFiles(previous, destination, oldFiles);
                foreach (string path in newFiles.Except(oldFiles))
                    File.Delete(Path.Combine(destination, path));
                throw;
            }
            string stagingRoot = Path.GetFullPath(Records + "/새빌드") + Path.DirectorySeparatorChar;
            if (source.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase))
            {
                try { Directory.Delete(source, true); }
                catch (IOException exception) { UnityEngine.Debug.LogWarning("Published build; staging cleanup deferred: " + exception.Message); }
                catch (UnauthorizedAccessException exception) { UnityEngine.Debug.LogWarning("Published build; staging cleanup deferred: " + exception.Message); }
            }
        }

        private static string[] RelativeFiles(string directory)
        {
            string prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Directory.GetFiles(prefix, "*", SearchOption.AllDirectories).Select(p => p.Substring(prefix.Length)).ToArray();
        }

        private static void CopyFiles(string source, string destination, string[] files)
        {
            foreach (string path in files)
            {
                string target = Path.Combine(destination, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(Path.Combine(source, path), target, true);
            }
        }
    }
}
