using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class RaceMonsterLookValidation
    {
        public const string Output = "DataBundles/검증/몬스터렌더링_20261016";
        private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int captureIndex, captureFrame;
        private static Sys_RaceCamera captureCamera;
        private static Sys_RaceRenderStyle captureStyles;
        public static string CaptureStatus { get; private set; } = "Not started";
        private static void Tick(object component) => component.GetType().GetMethod("LateUpdate", flags).Invoke(component, null);

        public static string Run()
        {
            if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Scene_All3D")
                throw new InvalidOperationException("Play Scene_All3D before validation.");
            var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            var presentation = UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>();
            var director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            var styles = UnityEngine.Object.FindFirstObjectByType<Sys_RaceRenderStyle>();
            var hud = UnityEngine.Object.FindFirstObjectByType<UIPanel_RaceHUD>();
            var controls = hud.GetComponentInChildren<UIItem_RenderStyle>();
            var report = new StringBuilder();
            Action<bool,string> check = (valid,message) => { report.AppendLine((valid ? "PASS " : "FAIL ") + message); if (!valid) throw new InvalidOperationException(message); };
            Directory.CreateDirectory(Output);
            presentation.Cutscenes = false;
            race.RestartRace(); race.Paused = true;
            typeof(Sys_Race).GetProperty("Countdown").SetValue(race, 0f);
            race.Simulation.Begin();
            for (int i = 0; i < 480; i++) race.Simulation.Step();
            foreach (var view in presentation.Views) Tick(view);
            double time = race.Simulation.Time;
            var phases = presentation.Views.Select(v => v.GaitPhase).ToArray();
            var distances = race.Simulation.Runners.Select(r => r.Distance).ToArray();
            check(presentation.Views.Length == 6 && presentation.Views.All(v => !v.HasRider && v.RiderRoot == null && v.RiderAnimator == null), "All six race instances are monster-only.");
            check(presentation.Views.All(v => v.HasPose && v.MountAnimator != null && v.RunClip != null), "All monster animation graphs produce a pose.");
            check(presentation.Views.All(v => v.GetComponentsInChildren<Transform>(true).All(t => t.name != "obj_rider")), "No rider hierarchy remains in race instances.");
            check(controls.Buttons.Length == 3, "Three rendering comparison buttons are connected.");
            foreach (var shot in new[] { E_RaceShot.Tracking, E_RaceShot.Rider })
            {
                director.CaptureShot = (int)shot; director.CaptureRunner = 2;
                typeof(Sys_RaceCamera).GetField("previousShot", flags).SetValue(director, (E_RaceShot)(-1));
                Tick(director);
                UnityEngine.Object.FindFirstObjectByType<Sys_RaceLighting>().AlignToCamera();
                for (int style = 0; style < 3; style++)
                {
                    controls.Buttons[style].onClick.Invoke();
                    check((int)styles.CurrentStyle == style, "UI button selects " + styles.CurrentStyle + " in " + shot + ".");
                    check(presentation.Views.All(v => v.GetComponent<Sys_MonsterLook>().Outlines.All(o => o.enabled == (style == 2))), "Outline visibility matches selected style.");
                }
            }
            for (int runner = 0; runner < 6; runner++)
            {
                director.CaptureShot = (int)E_RaceShot.Rider; director.CaptureRunner = runner;
                typeof(Sys_RaceCamera).GetField("previousShot", flags).SetValue(director, (E_RaceShot)(-1)); Tick(director);
                UnityEngine.Object.FindFirstObjectByType<Sys_RaceLighting>().AlignToCamera();
            }
            check(race.Simulation.Time == time && distances.SequenceEqual(race.Simulation.Runners.Select(r => r.Distance))
                && phases.SequenceEqual(presentation.Views.Select(v => v.GaitPhase)), "Style changes and close-up captures preserve race time, distance and gait phase.");
            var expected = new Data_RaceSimulation(race.Entrants); expected.Begin();
            for (int i = 0; i < 18000 && expected.Phase != E_RacePhase.Result; i++) expected.Step();
            race.SkipToResult(); Tick(hud);
            check(race.Simulation.Phase == E_RacePhase.Result && race.Simulation.Order.SequenceEqual(expected.Order)
                && race.Simulation.Runners.Select(r => r.FinishTime).SequenceEqual(expected.Runners.Select(r => r.FinishTime)), "Skip produces identical order and finish times to a full fixed-step race.");
            int generation = race.Generation; race.RestartRace(); race.Paused = true;
            foreach (var view in presentation.Views) Tick(view); Tick(hud);
            check(race.Generation == generation + 1 && presentation.Views.All(v => v.HasPose), "Replay resets race and all six monster poses.");
            // Leave a useful live race for visual inspection after the checks.
            typeof(Sys_Race).GetProperty("Countdown").SetValue(race, 0f); race.Simulation.Begin();
            for (int i = 0; i < 480; i++) race.Simulation.Step();
            foreach (var view in presentation.Views) Tick(view);
            director.CaptureShot = (int)E_RaceShot.Tracking; director.CaptureRunner = 2;
            typeof(Sys_RaceCamera).GetField("previousShot", flags).SetValue(director, (E_RaceShot)(-1)); Tick(director);
            File.WriteAllText(Output + "/validation.txt",report.ToString());
            captureCamera = director; captureStyles = styles; captureIndex = 0;
            EditorApplication.update -= CaptureTick; PrepareCapture(); EditorApplication.update += CaptureTick;
            return report.ToString();
        }

        private static void PrepareCapture()
        {
            captureCamera.CaptureShot = (int)(captureIndex < 3 ? E_RaceShot.Tracking : E_RaceShot.Rider);
            captureCamera.CaptureRunner = captureIndex < 6 ? 2 : captureIndex - 6;
            captureStyles.SetStyle(captureIndex < 6 ? captureIndex % 3 : 2);
            typeof(Sys_RaceCamera).GetField("previousShot", flags).SetValue(captureCamera, (E_RaceShot)(-1));
            captureFrame = Time.frameCount; CaptureStatus = "Capturing " + (captureIndex + 1) + "/12";
        }

        private static void CaptureTick()
        {
            if (!EditorApplication.isPlaying || captureCamera == null)
            { EditorApplication.update -= CaptureTick; CaptureStatus = "Interrupted"; return; }
            EditorApplication.QueuePlayerLoopUpdate();
            // Enabled skinned outline renderers need real player frames to update their GPU bone buffers.
            if (Time.frameCount - captureFrame < 4) return;
            string file = captureIndex < 3 ? "Tracking_" + captureIndex : captureIndex < 6 ? "Rider_" + (captureIndex - 3) : "Monster_" + (captureIndex - 6);
            SaveCamera(captureCamera.RaceCamera,Output + "/" + file + ".png");
            captureIndex++;
            if (captureIndex < 12) { PrepareCapture(); return; }
            EditorApplication.update -= CaptureTick; CaptureStatus = "Completed 12 captures after skinning updates";
            captureCamera.CaptureShot = (int)E_RaceShot.Tracking; captureCamera.CaptureRunner = 2;
        }

        private static void SaveCamera(Camera camera, string path)
        {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(720,1280,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            Texture2D image = null;
            bool compilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                VolumeManager.instance.Update(camera.transform, camera.GetUniversalAdditionalCameraData().volumeLayerMask);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; image = new Texture2D(720,1280,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,720,1280),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = compilation; RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt); if (image != null) UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
