using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ProjectRun.Race.Editor
{
    // Covers both Project Run build menus and Unity's regular WebGL Build button.
    public sealed class WebLauncherPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsEditor)
                throw new BuildFailedException("Play.exe packaging currently requires the Windows Editor.");

            string project = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string output = Path.GetFullPath(report.summary.outputPath);
            string script = Path.Combine(project, "Tools/Demo/BuildLauncher.ps1");
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell/v1.0/powershell.exe");
            var start = new ProcessStartInfo {
                FileName = powershell,
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script
                    + "\" -OutputDirectory \"" + output.TrimEnd('\\', '/') + "\"",
                WorkingDirectory = project,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000))
                {
                    process.Kill();
                    throw new BuildFailedException("Play.exe compilation timed out.");
                }
                if (process.ExitCode != 0 || !File.Exists(Path.Combine(output, "Play.exe")))
                    throw new BuildFailedException("Play.exe packaging failed:\n" + stdout.Result + "\n" + stderr.Result);
                UnityEngine.Debug.Log(stdout.Result);
            }
            File.WriteAllText(Path.Combine(output, "실행방법.txt"),
                "Monster Race 실행 방법\n\n"
                + "1. 이 폴더 안의 Play.exe를 더블클릭합니다.\n"
                + "2. 기본 브라우저에서 게임이 열립니다.\n"
                + "3. 종료할 때는 작업 표시줄 알림 영역의 포링 아이콘을 우클릭하고 '종료'를 선택합니다.\n\n"
                + "브라우저를 닫아도 실행기는 유지됩니다. Play.exe를 다시 누르면 게임을 다시 엽니다.\n"
                + "전달하거나 이동할 때는 Play.exe뿐 아니라 빌드 폴더 전체를 함께 옮겨 주세요.\n"
                + "Windows용 실행기입니다. 별도 Python 또는 Unity 설치는 필요하지 않습니다.\n");
        }
    }
}
