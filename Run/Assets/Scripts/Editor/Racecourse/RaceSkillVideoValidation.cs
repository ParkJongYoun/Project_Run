using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    // Explicit editor-only integration exercise; no gameplay or scene settings are persisted.
    public static class RaceSkillVideoValidation
    {
        private static Sys_RaceSkillVideo video;
        private static Sys_Race race;
        private static string directory;
        private static readonly List<string> report = new List<string>();
        private static readonly List<string> telemetry = new List<string>();
        private static readonly float[] captures = { .25f, 2.5f, 4.5f, 6.5f, 8.5f, 10.5f, 11.7f };
        private static int stage, capture, lastFrame;
        private static double wallStart, holdTime, pausedAt, pauseMedia, returnAt;
        private static bool seen, paused, resumed, skipping;

        public static string Begin(string outputDirectory)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play mode required.");
            video = UnityEngine.Object.FindFirstObjectByType<Sys_RaceSkillVideo>();
            if (video == null) throw new InvalidOperationException("Movie system not installed.");
            race = video.Race; directory = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(directory);
            stage = capture = 0; seen = paused = resumed = skipping = false; lastFrame = -1;
            holdTime = -1; returnAt = 0; wallStart = EditorApplication.timeSinceStartup;
            report.Clear(); telemetry.Clear(); telemetry.Add("wall,stage,sim,held,active,frame,media,alpha,paused");
            video.Presentation.Cutscenes = true; race.PlaybackSpeed = 2; race.RestartRace();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return "Started video lifecycle capture: " + directory;
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.Add("PASS " + message);
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying || video == null) { EditorApplication.update -= Tick; return; }
            if (Time.frameCount == lastFrame) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            lastFrame = Time.frameCount;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - wallStart > 200) throw new TimeoutException("Video integration capture timed out.");
                telemetry.Add(string.Join(",", new[] { (now-wallStart).ToString("F3",CultureInfo.InvariantCulture),stage.ToString(),race.Simulation.Time.ToString("F4",CultureInfo.InvariantCulture),race.PresentationHeld.ToString(),video.IsActive.ToString(),video.Player.frame.ToString(),video.Player.time.ToString("F3",CultureInfo.InvariantCulture),video.Panel.Opacity.alpha.ToString("F3",CultureInfo.InvariantCulture),race.Paused.ToString() }));
                if (stage == 0)
                {
                    if (video.IsActive)
                    {
                        if (!seen) { seen = true; holdTime = race.Simulation.Time; race.PlaybackSpeed = 1; Check(video.IsReady,"Prepared video triggered at fox skill"); }
                        if (race.PresentationHeld && Math.Abs(race.Simulation.Time-holdTime) > .018) throw new Exception("Simulation advanced during movie hold.");
                        if (video.HasFirstFrame && capture < captures.Length && video.Player.time >= captures[capture])
                        { ScreenCapture.CaptureScreenshot(Path.Combine(directory,"movie_"+capture.ToString("00")+".png")); capture++; }
                        if (!paused && video.Player.time >= 3)
                        { video.HUD.ToggleMenu(); paused = true; pausedAt = now; pauseMedia = video.Player.time; ScreenCapture.CaptureScreenshot(Path.Combine(directory,"menu_pause.png")); }
                        if (paused && !resumed && now-pausedAt > .8)
                        {
                            Check(Math.Abs(video.Player.time-pauseMedia) < .2,"Menu pauses movie clock");
                            Check(race.Paused,"Menu owns user pause independently of presentation hold");
                            Check(video.HUD.MenuRoot.GetComponent<Canvas>().sortingOrder > video.Panel.GetComponentInParent<Canvas>().sortingOrder,"Menu renders above the movie");
                            video.HUD.ToggleMenu(); resumed = true;
                        }
                    }
                    else if (seen)
                    {
                        Check(video.HasFirstFrame && resumed,"Decoded video completed after menu resume");
                        Check(capture == captures.Length,"Playback reached the final capture at 11.7 seconds");
                        Check(!race.PresentationHeld && video.Panel.Opacity.alpha == 0,"Natural completion releases hold and hides overlay");
                        Check(race.Simulation.Time > holdTime,"Live race advances during final dissolve");
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"return_00.png"));
                        stage = 1; returnAt = now;
                    }
                    else if (race.Simulation.Time > 42) throw new Exception("Fox skill did not start video.");
                }
                else if (stage == 1 && now-returnAt > 1.8)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(directory,"return_01.png"));
                    var baseline = new Data_RaceSimulation(race.Entrants); baseline.Begin();
                    for (int i=0;i<18000 && baseline.Phase!=E_RacePhase.Result;i++) baseline.Step();
                    race.SkipToResult();
                    Check(baseline.Order.SequenceEqual(race.Simulation.Order) && baseline.Runners.Select(r=>r.FinishTime).SequenceEqual(race.Simulation.Runners.Select(r=>r.FinishTime)),"Video hold preserves finish order and exact finish times");
                    stage=2; seen=false; skipping=false; race.PlaybackSpeed=2; race.RestartRace();
                }
                else if (stage == 2)
                {
                    if (video.IsActive && video.HasFirstFrame && video.Player.time > .8 && !skipping)
                    { video.Panel.SkipButton.onClick.Invoke(); skipping=true; Check(!race.PresentationHeld,"Skip button immediately releases presentation hold"); }
                    if (skipping && !video.IsActive)
                    {
                        Check(video.Panel.Opacity.alpha==0,"Skip completes its short fade");
                        stage=3; race.RestartRace();
                    }
                    else if (!skipping && race.Simulation.Time>42) throw new Exception("Replay did not trigger movie.");
                }
                else if (stage == 3 && video.IsActive && video.HasFirstFrame && video.Player.time > .5)
                {
                    race.RestartRace();
                    Check(!video.IsActive && !race.PresentationHeld && video.Panel.Opacity.alpha==0,"Restart cancels active movie and clears hold");
                    video.Presentation.Cutscenes=false;
                    race.SkipToResult();
                    Check(!video.IsActive && !race.PresentationHeld && race.Simulation.Phase==E_RacePhase.Result,"Skip-to-result cannot launch a video");
                    Complete("PASS");
                }
            }
            catch(Exception exception) { report.Add("FAIL "+exception); Complete("FAIL"); }
            EditorApplication.QueuePlayerLoopUpdate();
        }
        private static void Complete(string status)
        {
            EditorApplication.update -= Tick;
            File.WriteAllLines(Path.Combine(directory,"telemetry.csv"),telemetry);
            File.WriteAllLines(Path.Combine(directory,"report.txt"),report);
            File.WriteAllText(Path.Combine(directory,"completion.txt"),status);
            race.Paused = false;
            Debug.Log("Race skill video integration: "+status);
        }
    }
}
