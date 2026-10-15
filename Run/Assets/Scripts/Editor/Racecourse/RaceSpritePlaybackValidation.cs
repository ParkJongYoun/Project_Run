using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    // Opt-in integration check: exercise the actual common HUD and rendered sprites.
    public static class RaceSpritePlaybackValidation
    {
        private static Sys_Race race;
        private static UIPanel_RaceHUD hud;
        private static RaceRunnerSpriteView[] views;
        private static HashSet<string>[] observed;
        private static string[] frozenSprites;
        private static readonly List<string> checks = new List<string>();
        private static int angle, stage;
        private static double stageStart, baselineTime, baselineFrameTime, frozenTime;
        private static string scene;
        private const string output = "DataBundles/검증/2D레이스_20261016";

        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            EditorApplication.update -= Tick;
            race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            hud = UnityEngine.Object.FindFirstObjectByType<UIPanel_RaceHUD>();
            views = UnityEngine.Object.FindObjectsByType<RaceRunnerSpriteView>(FindObjectsSortMode.None).OrderBy(v => v.Runner.RunnerId).ToArray();
            if (views.Length != 6) throw new InvalidOperationException("Expected six sprite runners.");
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            checks.Clear(); angle = 0;
            hud.Presentation.Cutscenes = false;
            race.CountdownSeconds = 0; race.PlaybackSpeed = 1;
            BeginAngle(); EditorApplication.update += Tick;
        }

        private static void BeginAngle()
        {
            race.RestartRace(); race.Simulation.Begin();
            for (int i = 0; i < 120; i++) race.Simulation.Step();
            hud.Director.ManualShot = angle;
            observed = views.Select(v => new HashSet<string>()).ToArray();
            ChangeStage(0);
        }

        private static void ChangeStage(int value)
        {
            stage = value; stageStart = EditorApplication.timeSinceStartup;
            baselineTime = race.Simulation.Time;
            baselineFrameTime = Time.unscaledTimeAsDouble;
        }

        private static string[] Sprites() => views.Select(v => v.Image.sprite == null ? "null" : v.Image.sprite.name).ToArray();
        private static void Require(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks.Add(((E_RaceSpriteAngle)angle) + ": " + label);
        }
        private static void Observe()
        {
            var names = Sprites();
            for (int i = 0; i < names.Length; i++) observed[i].Add(names[i]);
        }
        private static void CheckMotion()
        {
            for (int i = 0; i < views.Length; i++)
            {
                var clip = views[i].Art.RunViews[angle];
                bool animated = clip != null && clip.FrameCount > 1;
                Require(animated ? observed[i].Count > 1 : observed[i].Count == 1,
                    views[i].Art.name + (animated ? " rendered frames advance" : " retains original still fallback"));
                Require(views[i].Image.sprite != null && Mathf.Abs(views[i].Image.sprite.bounds.size.y - views[i].Art.StillViews[angle].bounds.size.y) < .001f,
                    views[i].Art.name + " preserves original canvas scale");
            }
        }
        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || race == null) throw new InvalidOperationException("Play mode ended during validation.");
                double age = EditorApplication.timeSinceStartup - stageStart;
                if (age > 10) throw new TimeoutException("Stage " + stage);
                switch (stage)
                {
                    case 0:
                        if (age < .4) return;
                        Directory.CreateDirectory(output);
                        ScreenCapture.CaptureScreenshot(output + "/" + scene + "_animated_" + (E_RaceSpriteAngle)angle + ".png");
                        ChangeStage(1); break;
                    case 1:
                        Observe();
                        if (age < 1) return;
                        CheckMotion();
                        Require(Math.Abs((race.Simulation.Time - baselineTime) / (Time.unscaledTimeAsDouble - baselineFrameTime) - 1) < .15, "1x simulation rate against Unity frame clock");
                        hud.MenuButton.onClick.Invoke();
                        Require(race.Paused && hud.MenuRoot.activeSelf, "menu button pauses race");
                        ChangeStage(2); break;
                    case 2:
                        if (age < .2) return;
                        frozenSprites = Sprites(); frozenTime = race.Simulation.Time;
                        ChangeStage(3); break;
                    case 3:
                        if (age < .55) return;
                        Require(Sprites().SequenceEqual(frozenSprites), "menu pause freezes all rendered frames");
                        Require(race.Simulation.Time == frozenTime, "menu pause freezes race time");
                        hud.MenuCloseButton.onClick.Invoke();
                        Require(!race.Paused && !hud.MenuRoot.activeSelf, "close button resumes race");
                        hud.SpeedButton.onClick.Invoke();
                        Require(race.PlaybackSpeed == 2, "speed button sets 2x");
                        observed = views.Select(v => new HashSet<string>()).ToArray();
                        ChangeStage(4); break;
                    case 4:
                        Observe();
                        if (age < 1) return;
                        CheckMotion();
                        Require(Math.Abs((race.Simulation.Time - baselineTime) / (Time.unscaledTimeAsDouble - baselineFrameTime) - 2) < .2, "2x simulation rate against Unity frame clock");
                        hud.SpeedButton.onClick.Invoke();
                        Require(race.PlaybackSpeed == 1, "speed button returns to 1x");
                        if (++angle < 3) BeginAngle(); else Finish(null);
                        break;
                }
            }
            catch (Exception error) { Finish(error.ToString()); }
        }
        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory(output);
            File.WriteAllText(output + "/" + scene + "_sprite_playback.json", JsonConvert.SerializeObject(new
            {
                Scene = scene, Passed = error == null, Checks = checks, Error = error,
                AnimatedViews = views.Sum(v => v.Art.RunViews.Count(c => c != null && c.FrameCount > 1))
            }, Formatting.Indented));
            race.PlaybackSpeed = 1;
            hud.Presentation.Cutscenes = true;
            hud.Director.ManualShot = -1;
            race.RestartRace(); race.Paused = true;
            if (error == null) Debug.Log(scene + ": sprite playback validation passed (" + checks.Count + " checks).");
            else Debug.LogError(scene + ": " + error);
        }
    }
}
