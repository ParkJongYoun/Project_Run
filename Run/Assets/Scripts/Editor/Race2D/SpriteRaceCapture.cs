using System;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class SpriteRaceCapture
    {
        public const string Art = "Assets/ArtBundles/Race2D";
        public const string Sets = "Assets/Resources/Race2D/Sprites";
        public const int Directions = 16;
        public const int Frames = 12;
        public const int Cell = 192;
        public static readonly string[] Ids = { "Minorous", "GiantPoring", "Smokie", "Savage", "Lunatic", "Edgar" };
        public static readonly string[] Names = { "미노타우로스", "자이언트 포링", "스모키", "새비지", "루나틱", "호랑이 에드가" };

        public static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Ensure(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }

        [MenuItem("Tools/Project Run/2D race/Capture all six monsters")]
        public static void CaptureAll()
        {
            foreach (string id in Ids) Capture(id);
        }

        public static string Capture(string id)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture in Edit mode.");
            int index = Array.IndexOf(Ids, id);
            if (index < 0) throw new ArgumentException(id);
            Ensure(Art + "/Atlases"); Ensure(Art + "/Materials"); Ensure(Sets);
            string sourcePath = "Assets/Resources/Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_" + id + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null) throw new InvalidOperationException("Missing source: " + sourcePath);
            var scene = EditorSceneManager.NewPreviewScene();
            PlayableGraph graph = default;
            Texture2D atlas = null;
            try
            {
                var actor = Object.Instantiate(source);
                SceneManager.MoveGameObjectToScene(actor, scene);
                actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var view = actor.GetComponent<RaceRunnerView>();
                foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var particle in actor.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
                foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
                    if (!(renderer is SkinnedMeshRenderer)) renderer.enabled = false;
                var renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray();
                view.MountAnimator.applyRootMotion = false;
                view.MountAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Directional sprite bake " + id);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var clip = AnimationClipPlayable.Create(graph, view.RunClip);
                clip.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph, "capture", view.MountAnimator).SetSourcePlayable(clip);
                graph.Play();

                var cameraObject = new GameObject("CaptureCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene; camera.enabled = false; camera.orthographic = true;
                camera.transform.rotation = Quaternion.Euler(38, 35, 0);
                camera.nearClipPlane = .1f; camera.farClipPlane = 300;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.allowHDR = false; camera.allowMSAA = false;
                var additional = camera.GetUniversalAdditionalCameraData();
                additional.renderPostProcessing = false; additional.antialiasing = AntialiasingMode.None;
                additional.renderShadows = false;
                var lightObject = new GameObject("CaptureKeyLight");
                SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
                light.color = new Color(1, .96f, .89f); light.intensity = 1.15f;
                light.transform.rotation = Quaternion.Euler(48, -30, 0);
                light.shadows = LightShadows.None;

                // Shared projection bounds over every direction AND pose: no per-frame recentering or foot jitter.
                Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
                for (int d = 0; d < Directions; d++)
                {
                    actor.transform.rotation = Quaternion.Euler(0, d * 360f / Directions, 0);
                    for (int f = 0; f < Frames; f++)
                    {
                        Pose(view, graph, clip, f);
                        foreach (var renderer in renderers)
                        {
                            var mesh = new Mesh(); renderer.BakeMesh(mesh);
                            foreach (var vertex in mesh.vertices)
                            {
                                Vector3 p = renderer.transform.TransformPoint(vertex);
                                Vector2 q = new Vector2(Vector3.Dot(p, camera.transform.right), Vector3.Dot(p, camera.transform.up));
                                min = Vector2.Min(min, q); max = Vector2.Max(max, q);
                            }
                            Object.DestroyImmediate(mesh);
                        }
                    }
                }
                float size = Mathf.Max(max.x - min.x, max.y - min.y) * 1.16f;
                Vector2 center = (min + max) * .5f;
                Vector2 pivot = Vector2.one * .5f - center / size;
                camera.orthographicSize = size * .5f;
                camera.transform.position = camera.transform.right * center.x + camera.transform.up * center.y - camera.transform.forward * 100;
                atlas = new Texture2D(Cell * Frames, Cell * Directions, TextureFormat.RGBA32, false);
                for (int d = 0; d < Directions; d++)
                {
                    actor.transform.rotation = Quaternion.Euler(0, d * 360f / Directions, 0);
                    for (int f = 0; f < Frames; f++)
                    {
                        Pose(view, graph, clip, f);
                        var image = FoxSpriteCapture.Render(camera, Cell, Cell);
                        atlas.SetPixels(f * Cell, (Directions - 1 - d) * Cell, Cell, Cell, image.GetPixels());
                        Object.DestroyImmediate(image);
                    }
                }
                atlas.Apply();
                string atlasPath = Art + "/Atlases/Run_" + id + ".png";
                File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 4096; importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
                string materialPath = Art + "/Materials/M_DirectionalSprite.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    var shader = Shader.Find("ProjectRun/Race2D/DirectionalSprite");
                    if (shader == null) throw new InvalidOperationException("Missing directional sprite shader.");
                    material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
                }
                string setPath = Sets + "/Run_" + id + ".asset";
                var set = AssetDatabase.LoadAssetAtPath<Data_DirectionalRunSprites>(setPath);
                if (set == null) { set = ScriptableObject.CreateInstance<Data_DirectionalRunSprites>(); AssetDatabase.CreateAsset(set, setPath); }
                foreach (var old in AssetDatabase.LoadAllAssetsAtPath(setPath).OfType<Sprite>()) Object.DestroyImmediate(old, true);
                set.CharacterName = Names[index]; set.SourcePrefabPath = sourcePath;
                set.DirectionCount = Directions; set.FramesPerDirection = Frames;
                set.ClipDuration = view.RunClip.length; set.WorldSize = size; set.GroundPivot = pivot;
                set.SpriteMaterial = material; set.Frames = new Sprite[Directions * Frames];
                for (int d = 0; d < Directions; d++) for (int f = 0; f < Frames; f++)
                {
                    var sprite = Sprite.Create(texture, new Rect(f * Cell, (Directions - 1 - d) * Cell, Cell, Cell), pivot,
                        Cell / size, 0, SpriteMeshType.FullRect);
                    sprite.name = id + "_d" + d.ToString("00") + "_f" + f.ToString("00");
                    AssetDatabase.AddObjectToAsset(sprite, set);
                    set.Frames[d * Frames + f] = sprite;
                }
                EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
                return id + ": " + set.Frames.Length + " frames, world size " + size.ToString("F2") + ", pivot " + pivot;
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                if (atlas != null) Object.DestroyImmediate(atlas);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Pose(RaceRunnerView view, PlayableGraph graph, AnimationClipPlayable clip, int frame)
        {
            clip.SetTime(view.RunClip.length * frame / Frames);
            graph.Evaluate(0);
            view.MountRoot.localPosition = Vector3.zero;
            view.MountRoot.localRotation = Quaternion.identity;
        }
    }
}
