using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    /// <summary>Opt-in Play Mode review of all active animated runners, including pause and 2x playback.</summary>
    public static class RaceMotionValidation
    {
        private static Sys_Race race;
        private static Sys_RaceCamera director;
        private static RaceRunnerView[] views;
        private static Vector3[] headMin = new Vector3[4];
        private static Vector3[] headMax = new Vector3[4];
        private static Vector3[] pausedHead = new Vector3[4];
        private static Quaternion[] pausedRotation = new Quaternion[4];
        private static float[] minRate = new float[4];
        private static float[] maxRate = new float[4];
        private static readonly StringBuilder samples = new StringBuilder();
        private static double pauseStarted;
        private static bool pauseDone;
        private static bool doubled;
        private static float pauseDrift;
        private static float pauseTurn;
        private static int lastFrame;
        private static int frameCount;
        private static int capture;
        private static float nextCapture;
        private static string directory;
        public static string Status { get; private set; } = "Not started";

        public static string StartCapture(string outputDirectory = "DataBundles/Scene_All3D/RaceValidation/Polish/Motion")
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode.");
            race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            views = UnityEngine.Object.FindObjectsByType<RaceRunnerView>(FindObjectsSortMode.None).OrderBy(v => v.RunnerId).ToArray();
            if (views.Length != race.Entrants.Length) throw new InvalidOperationException("Runner count mismatch.");
            headMin = new Vector3[views.Length]; headMax = new Vector3[views.Length]; pausedHead = new Vector3[views.Length]; pausedRotation = new Quaternion[views.Length]; minRate = new float[views.Length]; maxRate = new float[views.Length];
            directory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(directory);
            race.PlaybackSpeed = 1; race.RestartRace(); director.ManualShot = 2;
            for (int i = 0; i < views.Length; i++)
            {
                headMin[i] = Vector3.one * float.MaxValue; headMax[i] = Vector3.one * float.MinValue;
                minRate[i] = float.MaxValue; maxRate[i] = 0;
            }
            pauseStarted = 0; pauseDone = false; doubled = false; pauseDrift = 0; pauseTurn = 0;
            frameCount = 0; capture = 0; nextCapture = 2; lastFrame = -1;
            samples.Clear(); samples.AppendLine("time,frame,runner,clipRate,headX,headY,headZ");
            Status = "Running";
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return directory;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || race == null)
            {
                EditorApplication.update -= Tick; Status = "Stopped before completion"; return;
            }
            if (lastFrame == Time.frameCount) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            lastFrame = Time.frameCount; frameCount++;
            double time = race.Simulation.Time;
            if (time >= 2 && time < 16)
            {
                for (int i = 0; i < views.Length; i++)
                {
                    var v = views[i];
                    var relative = v.transform.InverseTransformVector(v.FaceTarget.position - v.MountRoot.position);
                    if (float.IsNaN(relative.y)) throw new InvalidOperationException("Non-finite runner pose.");
                    headMin[i] = Vector3.Min(headMin[i], relative); headMax[i] = Vector3.Max(headMax[i], relative);
                    minRate[i] = Mathf.Min(minRate[i], v.CurrentAnimationRate); maxRate[i] = Mathf.Max(maxRate[i], v.CurrentAnimationRate);
                    samples.AppendLine(time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + Time.frameCount + "," + i + "," + v.CurrentAnimationRate.ToString("F3") + "," + relative.x.ToString("F3") + "," + relative.y.ToString("F3") + "," + relative.z.ToString("F3"));
                }
            }
            if (time >= nextCapture && capture < 48 && !race.Paused)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "frame_" + capture.ToString("000") + ".png"));
                capture++; nextCapture += 0.125f;
            }
            if (time >= 8.2 && !pauseDone)
            {
                if (pauseStarted == 0)
                {
                    race.Paused = true; pauseStarted = EditorApplication.timeSinceStartup;
                    for (int i = 0; i < views.Length; i++) { pausedHead[i] = views[i].FaceTarget.position; pausedRotation[i] = views[i].FaceTarget.rotation; }
                }
                else if (EditorApplication.timeSinceStartup - pauseStarted > 0.1)
                {
                    for (int i = 0; i < views.Length; i++)
                    {
                        pauseDrift = Mathf.Max(pauseDrift, Vector3.Distance(pausedHead[i], views[i].FaceTarget.position));
                        pauseTurn = Mathf.Max(pauseTurn, Quaternion.Angle(pausedRotation[i], views[i].FaceTarget.rotation));
                    }
                    if (EditorApplication.timeSinceStartup - pauseStarted > 0.5)
                    { race.Paused = false; pauseDone = true; }
                }
            }
            if (time >= 9 && !doubled) { race.PlaybackSpeed = 2; doubled = true; }
            if (time >= 16)
            {
                var report = new StringBuilder();
                report.AppendLine("Actual Play Mode frames: " + frameCount);
                report.AppendLine("Pause head drift (m): " + pauseDrift.ToString("F6") + "; rotation drift (deg): " + pauseTurn.ToString("F4"));
                bool passed = pauseDone && doubled && pauseDrift < 0.002f && pauseTurn < 0.1f;
                for (int i = 0; i < views.Length; i++)
                {
                    Vector3 range = headMax[i] - headMin[i];
                    passed &= range.magnitude > 0.08f && minRate[i] > 0.1f && maxRate[i] <= 1.85f;
                    report.AppendLine(views[i].name + " face motion relative to monster root (m)=" + range.ToString("F3") + " clip rate=" + minRate[i].ToString("F3") + ".." + maxRate[i].ToString("F3") + " gallops/clip=" + views[i].GallopsPerClip);
                }
                report.AppendLine("Paused pose / monster animation / 2x execution: " + (passed ? "PASS" : "FAIL"));
                File.WriteAllText(Path.Combine(directory, "motion-validation.txt"), report.ToString());
                File.WriteAllText(Path.Combine(directory, "motion-telemetry.csv"), samples.ToString());
                race.PlaybackSpeed = 1; race.Paused = false; director.ManualShot = -1;
                EditorApplication.update -= Tick; Status = report.ToString();
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}

