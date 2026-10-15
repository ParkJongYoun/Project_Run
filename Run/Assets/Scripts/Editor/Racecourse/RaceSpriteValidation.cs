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
    // An opt-in Play-mode check of the real movie and shared race lifecycle.
    public static class RaceSpriteValidation
    {
        private static Sys_Race race;
        private static Sys_RaceSkillVideo video;
        private static Sys_RacePresentation presentation;
        private static int stage;
        private static double stageStart, started, frozenRace, frozenMovie;
        private static string scene;
        private static readonly List<string> checks = new List<string>();
        private const string output = "DataBundles/검증/2D레이스_20261016";

        [MenuItem("Tools/Project Run/Race/Validate active race movie and lifecycle")]
        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            EditorApplication.update -= Tick;
            race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            video = UnityEngine.Object.FindFirstObjectByType<Sys_RaceSkillVideo>();
            presentation = UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>();
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            checks.Clear(); started = EditorApplication.timeSinceStartup;
            presentation.Cutscenes = true; race.CountdownSeconds = 0;
            Prepare(); ChangeStage(0); EditorApplication.update += Tick;
        }
        private static void Prepare()
        {
            race.RestartRace(); race.Simulation.Begin();
            for (int i = 0; i < 2350; i++) race.Simulation.Step();
            race.Paused = true;
        }
        private static void Trigger()
        {
            race.Paused = false;
            for (int i = 0; i < 80 && !race.PresentationHeld; i++) race.Simulation.Step();
            Require(video.IsActive && race.PresentationHeld, "fox event starts movie and holds simulation");
        }
        private static void ChangeStage(int value) { stage = value; stageStart = EditorApplication.timeSinceStartup; }
        private static void Require(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks.Add(label);
        }
        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || race == null) throw new InvalidOperationException("Play mode ended during validation.");
                double age = EditorApplication.timeSinceStartup - stageStart;
                if (age > 35) throw new TimeoutException("Stage " + stage + " timed out.");
                switch (stage)
                {
                    case 0:
                        if (!video.IsReady) return;
                        Trigger(); ChangeStage(1); break;
                    case 1:
                        if (!video.HasFirstFrame || video.Player.time < .8) return;
                        race.Paused = true; ChangeStage(2); break;
                    case 2:
                        if (age < .25) return;
                        frozenRace = race.Simulation.Time; frozenMovie = video.Player.time;
                        Directory.CreateDirectory(output);
                        ScreenCapture.CaptureScreenshot(output + "/" + scene + "_movie_paused.png");
                        ChangeStage(3); break;
                    case 3:
                        if (age < .7) return;
                        Require(Math.Abs(race.Simulation.Time - frozenRace) < .00001, "pause freezes simulation");
                        Require(Math.Abs(video.Player.time - frozenMovie) < .06, "pause freezes movie");
                        race.Paused = false; video.SkipVideo(); ChangeStage(4); break;
                    case 4:
                        if (age < .8) return;
                        Require(!video.IsActive && !race.PresentationHeld && video.Panel.Opacity.alpha == 0, "movie skip releases hold and overlay");
                        Require(race.Simulation.Time > frozenRace, "race resumes after movie skip");
                        Prepare(); ChangeStage(5); break;
                    case 5:
                        if (!video.IsReady) return;
                        Trigger(); ChangeStage(6); break;
                    case 6:
                        if (video.IsActive || age < 1) return;
                        Require(age > 10 && !race.PresentationHeld && video.Panel.Opacity.alpha == 0, "full movie ends and dissolves back to live race");
                        Prepare(); ChangeStage(7); break;
                    case 7:
                        if (!video.IsReady) return;
                        Trigger(); ChangeStage(8); break;
                    case 8:
                        if (!video.HasFirstFrame || video.Player.time < .5) return;
                        race.RestartRace(); race.Paused = true;
                        Require(!video.IsActive && !race.PresentationHeld && video.Panel.Opacity.alpha == 0, "restart during movie clears overlay and hold");
                        Require(race.Simulation.Time == 0, "restart resets race time");
                        race.SkipToResult();
                        Require(race.Simulation.Phase == E_RacePhase.Result && !race.PresentationHeld && !video.IsActive, "skip to result leaves no presentation hold");
                        var baseline = new Data_RaceSimulation(race.Entrants); baseline.Begin();
                        for (int i = 0; i < 18000 && baseline.Phase != E_RacePhase.Result; i++) baseline.Step();
                        Require(baseline.Order.SequenceEqual(race.Simulation.Order), "skip result order equals uninterrupted simulation");
                        Require(baseline.Runners.All(r => Math.Abs(r.FinishTime - race.Simulation.Runners[r.Id].FinishTime) < .000001), "all finish times equal uninterrupted simulation");
                        var director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
                        director.ManualShot = -1;
                        int shots = director.SpriteDirector != null ? 4 : 7;
                        for (int i = 0; i < shots; i++) director.CycleCamera();
                        Require(director.ManualShot == -1, "camera cycle returns to automatic mode");
                        ChangeStage(9); break;
                    case 9:
                        if (age < .5) return;
                        ScreenCapture.CaptureScreenshot(output + "/" + scene + "_result.png");
                        Finish(null); break;
                }
            }
            catch (Exception error) { Finish(error.ToString()); }
        }
        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory(output);
            File.WriteAllText(output + "/" + scene + "_validation.json", JsonConvert.SerializeObject(new
            {
                Scene = scene, Passed = error == null, Checks = checks, Error = error,
                Duration = EditorApplication.timeSinceStartup - started,
                Order = race == null ? null : race.Simulation.Order,
                FinishTimes = race == null ? null : race.Simulation.Runners.Select(r => r.FinishTime).ToArray()
            }, Formatting.Indented));
            if (error == null) Debug.Log(scene + ": movie and race validation passed (" + checks.Count + " checks).");
            else Debug.LogError(scene + ": " + error);
        }
    }
}
