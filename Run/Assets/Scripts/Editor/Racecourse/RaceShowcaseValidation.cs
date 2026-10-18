using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    /// <summary>Explicitly started editor verification; does not ship in player builds.</summary>
    public static class RaceShowcaseValidation
    {
        private static readonly float[] captureTimes = { 0.5f, 6, 9.4f, 10.2f, 11.1f, 14.5f, 17.4f, 18.2f, 19.4f, 23.4f, 24.2f, 25.1f, 29.4f, 30.2f, 31.1f, 35, 40.4f, 41.2f, 42.1f, 46, 49.4f, 50.2f, 51.1f, 53, 57, 59, 63.3f };
        private static int captureIndex;
        private static Sys_Race activeRace;
        private static readonly List<string> samples = new List<string>();
        private static string directory;
        private static float nextProbe;
        private static int frameStart;
        private static double wallStart;

        public static string StartCapture(string outputDirectory = "DataBundles/Scene_Oval/RaceValidation/Sequence")
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            activeRace = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            activeRace.PlaybackSpeed = 1;
            activeRace.RestartRace();
            var director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>(); director.ManualShot = -1;
            UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>().Cutscenes = true;
            captureIndex = 0; nextProbe = 0; frameStart = Time.frameCount; wallStart = EditorApplication.timeSinceStartup;
            directory = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(directory);
            samples.Clear(); samples.Add("time,frame,shot,focus," + string.Join(",", Enumerable.Range(0, activeRace.Entrants.Length).Select(i => "rank" + i)) + "," + string.Join(",", Enumerable.Range(0, activeRace.Entrants.Length).Select(i => "distance" + i)));
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return directory;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || activeRace == null) { EditorApplication.update -= Tick; return; }
            var sim = activeRace.Simulation;
            var director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            if (sim.Time >= nextProbe)
            {
                samples.Add(sim.Time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," + Time.frameCount + "," + director.CurrentShot + "," + director.FocusRunner + "," + string.Join(",", sim.Runners.Select(r => r.Rank.ToString()).ToArray()) + "," + string.Join(",", sim.Runners.Select(r => r.Distance.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).ToArray()));
                nextProbe += 0.5f;
            }
            if (captureIndex < captureTimes.Length && (sim.Time >= captureTimes[captureIndex] || sim.Phase == E_RacePhase.Result))
            {
                string name = captureIndex.ToString("00") + "_" + captureTimes[captureIndex].ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "_" + director.CurrentShot + ".png";
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, name)); captureIndex++;
            }
            if (captureIndex >= captureTimes.Length)
            {
                File.WriteAllLines(Path.Combine(directory, "telemetry.csv"), samples);
                File.WriteAllText(Path.Combine(directory, "completion.txt"), "Advanced frames: " + (Time.frameCount - frameStart) + "\nWall seconds: " + (EditorApplication.timeSinceStartup - wallStart).ToString("F2") + "\nPhase: " + sim.Phase + "\nOvertakes: " + sim.Overtakes + "\nFinish order: " + string.Join(",", sim.Order.Select(i => activeRace.Entrants[i].Name).ToArray()));
                EditorApplication.update -= Tick;
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public static string ValidateSimulation()
        {
            var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            var baseline = Run(race.Entrants, 60, 1);
            var results = new List<string>();
            foreach (int fps in new[] { 30, 60, 120 }) foreach (int speed in new[] { 1, 2 })
            {
                var sim = Run(race.Entrants, fps, speed);
                bool equal = sim.Order.SequenceEqual(baseline.Order) && sim.Runners.Select(r => r.FinishTime).SequenceEqual(baseline.Runners.Select(r => r.FinishTime));
                if (!equal) throw new InvalidOperationException("Frame-rate / playback invariance failed.");
                results.Add(fps + " fps x" + speed + ": PASS");
            }
            var skip = new Data_RaceSimulation(race.Entrants); skip.Begin();
            for (int i = 0; i < 18000 && skip.Phase != E_RacePhase.Result; i++) skip.Step();
            if (!skip.Runners.Select(r => r.FinishTime).SequenceEqual(baseline.Runners.Select(r => r.FinishTime))) throw new InvalidOperationException("Skip result mismatch");
            results.Add("Skip-to-result parity: PASS");
            results.Add("All runners finish: " + baseline.Runners.All(r => r.Finished));
            results.Add("Overtakes: " + baseline.Overtakes);
            for (int i = 0; i < race.Entrants.Length; i++) results.Add(race.Entrants[i].Name + ": " + baseline.Runners[i].FinishTime.ToString("F6") + " s");
            var views = UnityEngine.Object.FindObjectsByType<RaceRunnerView>(FindObjectsSortMode.None);
            int missingBones = views.Sum(v => v.GetComponentsInChildren<SkinnedMeshRenderer>().Sum(s => s.bones.Count(b => b == null)));
            int missingMaterials = views.Sum(v => v.GetComponentsInChildren<Renderer>().Sum(r => r.sharedMaterials.Count(m => m == null || m.shader == null || !m.shader.isSupported)));
            if (missingBones != 0 || missingMaterials != 0) throw new InvalidOperationException("Runner asset validation failed");
            results.Add("Missing bones: " + missingBones + "; missing/unsupported materials: " + missingMaterials);
            results.Add("Instance count matches entries: " + (views.Length == race.Entrants.Length));
            string report = string.Join("\n", results);
            Directory.CreateDirectory("DataBundles/Scene_Oval/RaceValidation");
            File.WriteAllText("DataBundles/Scene_Oval/RaceValidation/simulation-validation.txt", report);
            return report;
        }
        private static Data_RaceSimulation Run(Data_RunnerSetup[] entries, int fps, int speed)
        {
            var sim = new Data_RaceSimulation(entries); sim.Begin(); double accumulator = 0;
            for (int frame = 0; frame < fps * 200 && sim.Phase != E_RacePhase.Result; frame++)
            {
                accumulator += speed / (double)fps;
                while (accumulator >= Data_RaceSimulation.StepSeconds)
                {
                    sim.Step(); accumulator -= Data_RaceSimulation.StepSeconds;
                    if (sim.Runners.Any(r => double.IsNaN(r.Distance) || r.Distance < r.PreviousDistance || r.Lane < -8.3 || r.Lane > 8.3)) throw new InvalidOperationException("Invalid race state");
                    if (sim.Runners.Select(r => r.Rank).Distinct().Count() != entries.Length) throw new InvalidOperationException("Invalid ranks");
                }
            }
            return sim;
        }
    }
}

