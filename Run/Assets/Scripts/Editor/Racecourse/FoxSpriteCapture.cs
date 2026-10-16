using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    /// <summary>Captures live playable poses through the existing race camera, with a shared crop for both feet.</summary>
    public static class FoxSpriteCapture
    {
        public const string Output = "DataBundles/FoxImpostor";
        [Serializable] public sealed class CaptureInfo
        {
            public string Shot;
            public float WorldSize;
            public Vector2 Pivot;
            public Vector3 ViewDirection;
            public Vector3 CameraEuler;
            public float FieldOfView;
            public float LeftPhase;
            public float RightPhase;
            public float AnchorHeight = 1.5f;
        }
        private static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, flags).SetValue(target, value);
        private static void Tick(object target) => target.GetType().GetMethod("LateUpdate", flags).Invoke(target, null);

        public static string Prepare()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
            var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            race.RestartRace(); race.Simulation.Begin();
            for (int i = 0; i < 360; i++) race.Simulation.Step();
            Set(race, "<Countdown>k__BackingField", 0f); race.Paused = true;
            var presentation = UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>();
            foreach (var view in presentation.Views) { Tick(view); Set(view, "displayedSpeed", race.Simulation.Runners[view.RunnerId].Speed); Tick(view); }
            Directory.CreateDirectory(Output + "/Captures");
            return "Live race paused at six seconds; poses and camera ready.";
        }

        private static void Pose(RaceRunnerView view, float phase)
        {
            Set(view, "gaitClock", (double)(phase * view.RunClip.length / view.GallopsPerClip)); Tick(view);
        }

        private static Vector3[] Vertices(RaceRunnerView view)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            foreach (var renderer in view.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); renderer.BakeMesh(mesh);
                foreach (var vertex in mesh.vertices) vertices.Add(renderer.transform.TransformPoint(vertex));
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            return vertices.ToArray();
        }

        public static string Capture(int shot)
        {
            var director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            var view = director.Presentation.Views[2];
            var left = view.MountRoot.GetComponentsInChildren<Transform>().First(t => t.name == "Bip001 L Hand");
            var right = view.MountRoot.GetComponentsInChildren<Transform>().First(t => t.name == "Bip001 R Hand");
            float leftPhase = 0, rightPhase = 0, max = float.MinValue, min = float.MaxValue;
            for (int i = 0; i < 40; i++)
            {
                float phase = i / 40f; Pose(view, phase);
                float delta = view.transform.InverseTransformPoint(left.position).z - view.transform.InverseTransformPoint(right.position).z;
                if (delta > max) { max = delta; leftPhase = phase; }
                if (delta < min) { min = delta; rightPhase = phase; }
            }
            Pose(view, leftPhase);
            director.CaptureShot = shot; director.CaptureRunner = 2;
            Set(director, "previousShot", (E_RaceShot)(-1)); Tick(director);
            UnityEngine.Object.FindFirstObjectByType<Sys_RaceLighting>().AlignToCamera();
            var source = director.RaceCamera;
            string prefix = Output + "/Captures/" + ((E_RaceShot)shot);
            var sceneImage = Render(source, 720, 1280);
            File.WriteAllBytes(prefix + "_scene.png", sceneImage.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(sceneImage);
            var a = Vertices(view); Pose(view, rightPhase); var b = Vertices(view);
            Vector2 lo = Vector2.one * float.MaxValue, hi = Vector2.one * float.MinValue;
            foreach (var vertex in a.Concat(b))
            {
                Vector3 p = source.WorldToViewportPoint(vertex);
                lo = Vector2.Min(lo, p); hi = Vector2.Max(hi, p);
            }
            // A square crop in screen pixels, padded for antialiasing and the redraw silhouette.
            float sizeY = Mathf.Max(hi.y - lo.y, (hi.x - lo.x) * source.aspect) * 1.22f;
            Vector2 center = (lo + hi) * .5f;
            var rect = new Rect(center.x - sizeY / source.aspect * .5f, center.y - sizeY * .5f, sizeY / source.aspect, sizeY);
            var anchor = source.WorldToViewportPoint(view.transform.position + Vector3.up * 1.5f);
            var info = new CaptureInfo {
                Shot = ((E_RaceShot)shot).ToString(),
                Pivot = new Vector2((anchor.x - rect.x) / rect.width, (anchor.y - rect.y) / rect.height),
                WorldSize = 2 * anchor.z * Mathf.Tan(source.fieldOfView * Mathf.Deg2Rad * .5f) * sizeY,
                ViewDirection = view.transform.InverseTransformDirection((source.transform.position - view.Focus).normalized),
                CameraEuler = source.transform.eulerAngles, FieldOfView = source.fieldOfView,
                LeftPhase = leftPhase, RightPhase = rightPhase
            };
            File.WriteAllText(prefix + ".json", JsonUtility.ToJson(info, true));
            var go = new GameObject("FoxCaptureCamera"); var camera = go.AddComponent<Camera>(); camera.CopyFrom(source);
            camera.enabled = false; camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 30;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.None;
            var crop = Matrix4x4.identity;
            crop.m00 = 1 / rect.width; crop.m11 = 1 / rect.height;
            crop.m03 = (1 - 2 * rect.center.x) / rect.width; crop.m13 = (1 - 2 * rect.center.y) / rect.height;
            camera.projectionMatrix = crop * source.projectionMatrix;
            var renderers = view.GetComponentsInChildren<SkinnedMeshRenderer>();
            var layers = renderers.Select(r => r.gameObject.layer).ToArray();
            var sheet = new Texture2D(2048, 1024, TextureFormat.RGBA32, false);
            try
            {
                foreach (var renderer in renderers) renderer.gameObject.layer = 30;
                for (int frame = 0; frame < 2; frame++)
                {
                    Pose(view, frame == 0 ? leftPhase : rightPhase);
                    var image = Render(camera, 1024, 1024);
                    File.WriteAllBytes(prefix + (frame == 0 ? "_left.png" : "_right.png"), image.EncodeToPNG());
                    sheet.SetPixels(frame * 1024, 0, 1024, 1024, image.GetPixels());
                    UnityEngine.Object.DestroyImmediate(image);
                }
                sheet.Apply(); File.WriteAllBytes(prefix + "_pair.png", sheet.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = layers[i];
                UnityEngine.Object.DestroyImmediate(sheet); UnityEngine.Object.DestroyImmediate(go);
                director.CaptureShot = director.CaptureRunner = -1;
            }
            return prefix + "_pair.png";
        }

        public static Texture2D Render(Camera camera, int width, int height)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); return image;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }

        public static string CalibrateAnchors()
        {
            Prepare();
            var director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            var view = director.Presentation.Views[2];
            foreach (E_RaceShot shot in Enum.GetValues(typeof(E_RaceShot)))
            {
                string path = Output + "/Captures/" + shot + ".json";
                string json = File.ReadAllText(path);
                if (json.Contains("AnchorHeight")) continue;
                var info = JsonUtility.FromJson<CaptureInfo>(json);
                Pose(view, info.LeftPhase); director.CaptureShot = (int)shot; director.CaptureRunner = 2;
                Set(director, "previousShot", (E_RaceShot)(-1)); Tick(director);
                var camera = director.RaceCamera;
                var ground = camera.WorldToViewportPoint(view.transform.position);
                var anchor = camera.WorldToViewportPoint(view.transform.position + Vector3.up * 1.5f);
                float cropHeight = info.WorldSize / (2 * ground.z * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
                info.Pivot += new Vector2((anchor.x - ground.x) * camera.aspect / cropHeight, (anchor.y - ground.y) / cropHeight);
                info.WorldSize *= anchor.z / ground.z;
                info.AnchorHeight = 1.5f;
                File.WriteAllText(path, JsonUtility.ToJson(info, true));
            }
            director.CaptureShot = director.CaptureRunner = -1;
            return "All twelve pivots calibrated to a plane through the body, preserving ground projection.";
        }
    }
}
