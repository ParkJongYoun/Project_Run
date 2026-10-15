using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class RaceSpriteCameraCapture
    {
        private static Sys_Race race;
        private static Sys_RaceCamera camera;
        private static int index;
        private static double changedAt;
        private static bool captured;
        private static readonly float[] distances = { 15, 150, 350, 550 };
        public static void Start()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            EditorApplication.update -= Tick;
            race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            camera = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>().Cutscenes = false;
            race.CountdownSeconds = 0; race.RestartRace(); race.Simulation.Begin();
            for (int i = 0; i < 120; i++) race.Simulation.Step();
            race.Paused = true; index = 0; SetCase(); EditorApplication.update += Tick;
        }
        private static void SetCase()
        {
            camera.ManualShot = index % 3;
            foreach (var runner in race.Simulation.Runners)
            {
                runner.Distance = runner.PreviousDistance = distances[index / 3] + runner.Id * 2;
            }
            changedAt = EditorApplication.timeSinceStartup; captured = false;
        }
        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            double age = EditorApplication.timeSinceStartup - changedAt;
            if (!captured && age > 1.2)
            {
                const string folder = "DataBundles/검증/2D레이스_20261016/camera_matrix";
                Directory.CreateDirectory(folder);
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                ScreenCapture.CaptureScreenshot(folder + "/" + scene + "_" + index.ToString("00") + ".png");
                captured = true;
            }
            if (captured && age > 1.6)
            {
                if (++index < 12) SetCase();
                else { EditorApplication.update -= Tick; Debug.Log("Twelve section/camera captures completed."); }
            }
        }
    }
}
