using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    /// <summary>Opt-in visual and lifecycle checks using the real race in Play Mode.</summary>
    public static class FoxSpriteValidation
    {
        private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly StringBuilder report = new StringBuilder();
        private static RaceFoxSpriteView sprite;
        private static double started, simStarted;
        private static float phase;
        private static int stage, frame, changes, lastFrame;
        public static string Status { get; private set; } = "Not started";
        private static void Tick(object target) => target.GetType().GetMethod("LateUpdate", flags).Invoke(target, null);
        private static void Check(bool condition, string message)
        {
            report.AppendLine((condition ? "PASS " : "FAIL ") + message);
            if (!condition) throw new InvalidOperationException(message);
        }

        public static string Begin()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
            sprite = UnityEngine.Object.FindFirstObjectByType<RaceFoxSpriteView>();
            if (sprite == null) throw new InvalidOperationException("Install first.");
            report.Clear(); FoxSpriteCapture.Prepare();
            var race = sprite.Race; var director = sprite.Director;
            Directory.CreateDirectory(FoxSpriteCapture.Output + "/Review/Comparisons");
            double time = race.Simulation.Time;
            Check(sprite.SpriteSet.Shots.Length == 12, "12 independently illustrated camera pairs registered");
            Check(sprite.SpriteSet.Shots.SelectMany(s => new[] { s.Left, s.Right }).Distinct().Count() == 24, "24 unique imported sprites");
            foreach (var shot in sprite.SpriteSet.Shots)
            {
                director.CaptureShot = (int)shot.Shot; director.CaptureRunner = 2;
                typeof(Sys_RaceCamera).GetField("previousShot", flags).SetValue(director, (E_RaceShot)(-1));
                Texture2D original = null;
                for (int foot = 0; foot < 2; foot++)
                {
                    typeof(RaceRunnerView).GetField("gaitClock", flags).SetValue(sprite.Runner, (double)((foot == 0 ? shot.LeftPhase : shot.RightPhase) * sprite.Runner.RunClip.length / sprite.Runner.GallopsPerClip));
                    Tick(sprite.Runner); Tick(director);
                    UnityEngine.Object.FindFirstObjectByType<Sys_RaceLighting>().AlignToCamera();
                    if (foot == 0)
                    {
                        sprite.UseSprites = false; Tick(sprite);
                        original = FoxSpriteCapture.Render(director.RaceCamera, 720, 1280);
                    }
                    sprite.UseSprites = true; Tick(sprite);
                    Check(sprite.DisplayedShot == shot.Shot && sprite.DisplayedFrame == foot, shot.Shot + " " + (foot == 0 ? "left" : "right") + " selected at captured gait phase");
                    var result = FoxSpriteCapture.Render(director.RaceCamera, 720, 1280);
                    File.WriteAllBytes(FoxSpriteCapture.Output + "/Review/Comparisons/" + shot.Shot + "_" + foot + ".png", result.EncodeToPNG());
                    if (foot == 0)
                    {
                        var pair = new Texture2D(1440, 1280, TextureFormat.RGBA32, false);
                        pair.SetPixels(0, 0, 720, 1280, original.GetPixels()); pair.SetPixels(720, 0, 720, 1280, result.GetPixels()); pair.Apply();
                        File.WriteAllBytes(FoxSpriteCapture.Output + "/Review/Comparisons/" + shot.Shot + "_3D_vs_2D.png", pair.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(pair); UnityEngine.Object.DestroyImmediate(original);
                    }
                    UnityEngine.Object.DestroyImmediate(result);
                }
            }
            Check(race.Simulation.Time == time, "Captures and visual toggles did not advance simulation");
            Check(sprite.Runner.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), "Original surfaces hidden; original shadows preserved");
            sprite.enabled = false;
            Check(sprite.Runner.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.enabled && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly), "Disabling component restores original renderers");
            sprite.enabled = true; Tick(sprite);
            director.CaptureShot = director.CaptureRunner = -1; director.ManualShot = 1;
            started = EditorApplication.timeSinceStartup; simStarted = race.Simulation.Time;
            phase = sprite.Runner.GaitPhase; frame = sprite.DisplayedFrame; stage = 0; changes = 0; lastFrame = -1;
            Status = "Checking pause and playback";
            EditorApplication.update -= Update; EditorApplication.update += Update;
            return Status;
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying || sprite == null) { EditorApplication.update -= Update; Status = "Interrupted"; return; }
            if (lastFrame == Time.frameCount) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            lastFrame = Time.frameCount;
            try
            {
                var race = sprite.Race;
                if (stage == 0 && EditorApplication.timeSinceStartup - started > .7)
                {
                    Check(race.Simulation.Time == simStarted && Mathf.Abs(sprite.Runner.GaitPhase - phase) < .00001f, "Pause holds simulation and sprite gait");
                    stage = 1; race.PlaybackSpeed = 1; race.Paused = false; simStarted = race.Simulation.Time; changes = 0; frame = sprite.DisplayedFrame;
                }
                else if (stage == 1 || stage == 2)
                {
                    if (sprite.DisplayedFrame != frame) { changes++; frame = sprite.DisplayedFrame; }
                    if (race.Simulation.Time - simStarted >= 2)
                    {
                        Check(changes >= 4, (stage == 1 ? "1x" : "2x") + " alternates both feet during two simulation seconds (changes=" + changes + ")");
                        if (stage == 1) { stage = 2; race.PlaybackSpeed = 2; simStarted = race.Simulation.Time; changes = 0; }
                        else
                        {
                            var baseline = new Data_RaceSimulation(race.Entrants); baseline.Begin();
                            for (int i = 0; i < 18000 && baseline.Phase != E_RacePhase.Result; i++) baseline.Step();
                            race.SkipToResult();
                            Check(race.Simulation.Order.SequenceEqual(baseline.Order), "Skip result order matches unchanged simulation");
                            Check(race.Simulation.Runners.Zip(baseline.Runners, (a,b) => Math.Abs(a.FinishTime - b.FinishTime) < .00001).All(x=>x), "All finish times match unchanged simulation");
                            race.RestartRace(); race.PlaybackSpeed = 1; sprite.Director.ManualShot = -1;
                            stage = 3; started = EditorApplication.timeSinceStartup;
                        }
                    }
                }
                else if (stage == 3 && EditorApplication.timeSinceStartup - started > .3)
                {
                    Check(sprite.IsShowingSprites && sprite.DisplayedShot == E_RaceShot.Opening, "Restart restores Opening sprite pair");
                    Status = "PASS"; report.AppendLine("Play Mode checks complete. WebGL/mobile performance not measured.");
                    File.WriteAllText(FoxSpriteCapture.Output + "/Review/validation.txt", report.ToString());
                    EditorApplication.update -= Update;
                    race.Paused = true;
                }
            }
            catch (Exception exception)
            {
                Status = "FAIL: " + exception.Message; report.AppendLine(Status);
                File.WriteAllText(FoxSpriteCapture.Output + "/Review/validation.txt", report.ToString());
                EditorApplication.update -= Update; Debug.LogException(exception);
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
