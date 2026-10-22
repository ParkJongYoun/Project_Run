using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ProjectRun.Race.Editor
{
    // Restore the original PowerShell local server through Play.cmd for WebGL builds.
    public sealed class WebLauncherPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string output = Path.GetFullPath(report.summary.outputPath);
            string content = Path.Combine(output, "Game");
            Directory.CreateDirectory(content);
            // Keep Unity's relative URLs intact by moving the web content together.
            foreach (string name in new[] { "index.html", "Build", "StreamingAssets", "TemplateData" })
            {
                string source = Path.Combine(output, name);
                string destination = Path.Combine(content, name);
                string prefix = output.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(source).StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFullPath(destination).StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Web content must stay inside the build output.");
                if (Directory.Exists(source))
                {
                    if (Directory.Exists(destination)) Directory.Delete(destination, true);
                    Directory.Move(source, destination);
                }
                else if (File.Exists(source))
                {
                    File.Copy(source, destination, true);
                    File.Delete(source);
                }
            }
            string support = Path.Combine(output, "Launcher");
            Directory.CreateDirectory(support);
            File.Copy(Path.Combine(project, "Tools/Demo/Play.cmd"), Path.Combine(output, "Play.cmd"), true);
            File.Copy(Path.Combine(project, "Tools/Demo/StartDemo.ps1"), Path.Combine(support, "StartDemo.ps1"), true);
            File.Copy(Path.GetFullPath(Path.Combine(project, "../Tools/LocalWeb/Serve.ps1")), Path.Combine(support, "Serve.ps1"), true);
            File.WriteAllText(Path.Combine(content, "demo-id.txt"), "project-run-review-demo");
            foreach (string legacy in new[] { "Play.exe", "Play.ico", "StartDemo.ps1", "Serve.ps1",
                "demo-id.txt", "실행방법.txt", "빌드옵션.txt", "빌드구성.txt" })
            {
                string path = Path.Combine(output, legacy);
                if (File.Exists(path)) File.Delete(path);
            }
            UnityEngine.Debug.Log("PLAY_CMD_PACKAGED " + output);
        }
    }
}
