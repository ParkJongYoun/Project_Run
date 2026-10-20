using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.EditorTools
{
    public static class CartoonRaceRosterBuilder
    {
        private const string prefabFolder = "Assets/Resources/Prefabs/Race/CartoonRaceLineup";
        private const string portraitFolder = "Assets/ArtBundles/UI/CartoonRaceLineup";
        private const string rosterPath = "Assets/Resources/Tables/Data_CartoonRaceRoster.asset";
        private static readonly string[] ids = { "Poring", "Minorous", "Pecopeco", "Lunatic", "BaphometJr", "Alpaca" };
        private static readonly string[] names = { "포링", "미노타우르스", "페코페코", "루나틱", "바포메트 주니어", "알파카" };

        [MenuItem("Tools/Project Run/Cartoon monsters/Sync race lineup from showcase")]
        public static void Sync()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var sourceScene = SceneManager.GetSceneByPath(CartoonShowcaseBuilder.ScenePath);
            bool openedSource = !sourceScene.isLoaded;
            var previous = SceneManager.GetActiveScene();
            if (openedSource) sourceScene = EditorSceneManager.OpenScene(CartoonShowcaseBuilder.ScenePath, OpenSceneMode.Additive);
            var showcase = sourceScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Sys_CartoonShowcase>(true)).Single();
            Directory.CreateDirectory(prefabFolder); Directory.CreateDirectory(portraitFolder); AssetDatabase.Refresh();
            var entries = new List<Data_RunnerSetup>();
            try
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    var exhibit = showcase.Exhibits.Single(e => e.Root.name == "V_Cartoon_" + ids[i]);
                    var root = Object.Instantiate(exhibit.CartoonRoot.gameObject);
                    root.name = "P_Runner_CartoonRace_" + ids[i];
                    try
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(root))
                            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                        root.SetActive(true);
                        // Bake the exhibit parent AND prefab instance scale into a visual child.
                        var scale = Vector3.Scale(exhibit.Root.localScale, exhibit.CartoonRoot.localScale);
                        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                        var children = root.transform.Cast<Transform>().ToArray();
                        var visual = new GameObject("obj_visual").transform; visual.SetParent(root.transform, false);
                        foreach (var child in children) child.SetParent(visual, false);
                        visual.localScale = scale;
                        var view = root.GetComponent<RaceRunnerView>(); view.enabled = true;
                        view.CameraFocusHeight *= scale.y;
                        var sprite = Capture(root, view, ids[i]);
                        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabFolder + "/" + root.name + ".prefab");
                        if (prefab == null) throw new InvalidOperationException("Failed to save " + ids[i]);
                        entries.Add(new Data_RunnerSetup { Name = names[i], Mount = names[i], SkillName = "스퍼트", Prefab = prefab,
                            Portrait = sprite, FacePortrait = sprite, Color = Color.HSVToRGB((i * .137f) % 1, .55f, .95f) });
                    }
                    finally { Object.DestroyImmediate(root); }
                }
                var roster = AssetDatabase.LoadAssetAtPath<Data_CartoonRaceRoster>(rosterPath);
                if (roster == null) { roster = ScriptableObject.CreateInstance<Data_CartoonRaceRoster>(); AssetDatabase.CreateAsset(roster, rosterPath); }
                roster.Playable = entries.Take(3).ToArray(); roster.Opponents = entries.Skip(3).ToArray(); EditorUtility.SetDirty(roster);
                var art = AssetDatabase.LoadAssetAtPath<Data_DemoArt>("Assets/Resources/UI/Demo/Data_DemoArt.asset");
                var images = art.Images.ToList();
                foreach (string suffix in new[] { "_face", "_body", "_run" })
                {
                    var key = "Pecopeco" + suffix; var image = images.Find(e => e.Key == key);
                    if (image == null) { image = new Data_DemoSprite { Key = key }; images.Add(image); }
                    image.Sprite = entries[2].Portrait;
                }
                art.Images = images.ToArray(); EditorUtility.SetDirty(art);
                AssetDatabase.SaveAssets();
                var racePath = Define_Scene.Path(Define_Scene.OvalToon);
                var scene = SceneManager.GetSceneByPath(racePath); bool openedRace = !scene.isLoaded;
                if (openedRace) scene = EditorSceneManager.OpenScene(racePath, OpenSceneMode.Additive);
                if (scene.isDirty) throw new InvalidOperationException("Save OvalToon edits first.");
                var race = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Sys_Race>(true)).Single();
                race.Entrants = entries.Take(6).Select(e => e.Copy()).ToArray();
                var presentation = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Sys_RacePresentation>(true)).Single();
                Transform parent = presentation.Views.First(v => v != null).transform.parent;
                foreach (var view in presentation.Views) if (view != null) Object.DestroyImmediate(view.gameObject);
                presentation.Views = new RaceRunnerView[6];
                for (int i = 0; i < 6; i++)
                {
                    var runner = (GameObject)PrefabUtility.InstantiatePrefab(race.Entrants[i].Prefab, parent);
                    runner.transform.localPosition = new Vector3((i - 2.5f) * 4, 0, 0);
                    presentation.Views[i] = runner.GetComponent<RaceRunnerView>(); presentation.Views[i].RunnerId = i;
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                if (openedRace) EditorSceneManager.CloseScene(scene, true);
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Select(s => new EditorBuildSettingsScene(s.path, s.path == Define_Scene.Path(Define_Scene.Sprite2D) ? false : s.enabled)).ToArray();
                Debug.Log("CARTOON_RACE_ROSTER_SYNCED: 3 playable + 3 fixed opponents; showcase proportions preserved.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (openedSource) EditorSceneManager.CloseScene(sourceScene, true);
            }
        }

        private static Sprite Capture(GameObject root, RaceRunnerView view, string id)
        {
            // A private layer isolates portraits from the open showcase and race scenes.
            var layers = root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject.layer).ToArray();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms) t.gameObject.layer = 31;
            var cameraObject = new GameObject("Camera_Portrait"); var camera = cameraObject.AddComponent<Camera>();
            var texture = new RenderTexture(384, 384, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(384, 384, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                if (view.IdleClip != null) view.IdleClip.SampleAnimation(view.MountAnimator.gameObject, .15f);
                var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.18f;
                camera.transform.position = bounds.center + new Vector3(0, bounds.size.y * .1f, Mathf.Max(20, bounds.size.z * 4)); camera.transform.LookAt(bounds.center);
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 384, 384), 0, 0); image.Apply();
                var path = portraitFolder + "/" + id + ".png"; File.WriteAllBytes(path, image.EncodeToPNG()); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            finally
            {
                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
                RenderTexture.active = previous; camera.targetTexture = null; texture.Release();
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(texture); Object.DestroyImmediate(image);
            }
        }
    }
}
