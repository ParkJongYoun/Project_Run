using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class MonsterRaceLookDevBuilder
    {
        public const string ScenePath = "Assets/Scenes/Scene_OvalToon.unity";
        private const string art = "Assets/ArtBundles/RaceShowcase/MonsterLookDev";
        private const string minoArt = "Assets/ArtBundles/RaceShowcase/MinorousLookDev";

        [MenuItem("Tools/Project Run/Look development/Create six-monster race")]
        public static void Build()
        {
            RequireSavedEditMode();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The look development race already exists; edit it in place.");
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Scene_Oval.unity", ScenePath))
                throw new InvalidOperationException("Unable to copy the source race.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var race = Find<Sys_Race>(scene).Single();
            var presentation = Find<Sys_RacePresentation>(scene).Single();
            if (race.Entrants.Length != 6 || presentation.Views.Length != 6)
                throw new InvalidOperationException("Expected the existing six-monster race.");

            foreach (var light in Find<Light>(scene)) light.enabled = false;
            foreach (var volume in Find<Volume>(scene)) volume.enabled = false;
            foreach (var lighting in Find<Sys_RaceLighting>(scene)) lighting.enabled = false;
            foreach (var styles in Find<Sys_RaceRenderStyle>(scene)) styles.enabled = false;
            foreach (var styles in Find<UIItem_RenderStyle>(scene)) styles.gameObject.SetActive(false);
            // The old fox video is not one of these six monsters. Their race skill gestures stay active.
            foreach (var video in Find<Sys_RaceSkillVideo>(scene)) video.gameObject.SetActive(false);
            foreach (var video in Find<UIPanel_RaceSkillVideo>(scene)) video.gameObject.SetActive(false);
            CopyLookEnvironment(scene);

            Directory.CreateDirectory(art + "/Materials");
            Directory.CreateDirectory(art + "/Portraits");
            AssetDatabase.Refresh();
            var entries = new List<Sys_RaceLookDev.Surface>();
            for (int i = 0; i < presentation.Views.Length; i++)
            {
                var view = presentation.Views[i];
                view.gameObject.SetActive(true);
                string id = view.name.Replace("P_Runner_", "");
                var look = view.GetComponent<Sys_MonsterLook>();
                if (look == null || look.Surfaces.Length != 1)
                    throw new InvalidOperationException("Inspect unexpected material layout: " + view.name);
                var surface = look.Surfaces[0].Renderer;
                var material = MakeMaterial(id, surface.sharedMaterial);
                entries.Add(new Sys_RaceLookDev.Surface { Renderer = surface,
                    Original = surface.sharedMaterials, Updated = new[] { material } });
                surface.sharedMaterial = material;
                surface.receiveShadows = true;
                foreach (var outline in look.Outlines)
                {
                    string outlinePath = art + "/Materials/" + id + "_AnimeOutline.mat";
                    var updated = new Material(AssetDatabase.LoadAssetAtPath<Material>(minoArt + "/Materials/Minorous_AnimeOutline.mat"));
                    updated.name = id + "_AnimeOutline";
                    updated.SetFloat("_OutlineWidth", id == "Lunatic" ? .45f : .65f);
                    if (id == "Lunatic") updated.SetColor("_OutlineColor", new Color(.28f, .22f, .32f, 1));
                    AssetDatabase.CreateAsset(updated, outlinePath);
                    entries.Add(new Sys_RaceLookDev.Surface { Renderer = outline,
                        Original = outline.sharedMaterials, Updated = new[] { updated } });
                    outline.sharedMaterial = updated;
                    outline.enabled = true;
                }
                // Keep the source runner prefab and its animation tuning intact.
                string portraitPath = art + "/Portraits/FacePortrait_" + id + ".png";
                CapturePortrait(view, portraitPath);
                race.Entrants[i].FacePortrait = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
            }
            var systems = scene.GetRootGameObjects().Single(root => root.name == "[Systems]");
            var controller = new GameObject("Sys_RaceLookDev");
            controller.transform.SetParent(systems.transform, false);
            controller.AddComponent<Sys_RaceLookDev>().Configure(entries.ToArray());
            var hud = Find<UIPanel_RaceHUD>(scene).Single();
            hud.FocusPortrait.sprite = race.Entrants[hud.SelectedRunnerId].FacePortrait;
            foreach (var text in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (text.name == "txt_course") text.text = "잔디 790 m · 몬스터 6종";
            foreach (var camera in Find<Camera>(scene))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.requiresDepthOption = CameraOverrideOption.On;
            }
            SaveScene(scene);
            AddBuildScene();
            Debug.Log("Six updated monsters ready: " + ScenePath);
        }

        [MenuItem("Tools/Project Run/Look development/Refresh monster portraits")]
        public static void RefreshPortraits()
        {
            RequireSavedEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var race = Find<Sys_Race>(scene).Single();
            var presentation = Find<Sys_RacePresentation>(scene).Single();
            for (int i = 0; i < presentation.Views.Length; i++)
            {
                var view = presentation.Views[i];
                bool independent = view.name.StartsWith("P_Runner_Cartoon_", StringComparison.Ordinal);
                string id = view.name.Replace(independent ? "P_Runner_Cartoon_" : "P_Runner_", "");
                string path = independent
                    ? CartoonMonsterPrefabBuilder.ArtRoot + "/" + id + "/Portraits/FacePortrait_" + id + ".png"
                    : art + "/Portraits/FacePortrait_" + id + ".png";
                CapturePortrait(view, path);
                race.Entrants[i].FacePortrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (independent) race.Entrants[i].Portrait = race.Entrants[i].FacePortrait;
            }
            var hud = Find<UIPanel_RaceHUD>(scene).Single();
            hud.FocusPortrait.sprite = race.Entrants[hud.SelectedRunnerId].FacePortrait;
            SaveScene(scene);
        }

        public static void CapturePortrait(RaceRunnerView source, string path)
        {
            // Sample a private clone: legacy bind poses and face sockets can lie outside the visible model.
            var clone = Object.Instantiate(source.gameObject);
            var bakedRoot = new GameObject("obj_portrait_capture");
            var meshes = new List<Mesh>();
            Camera camera = null;
            RenderTexture target = null;
            Texture2D texture = null;
            var previousTarget = RenderTexture.active;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                var view = clone.GetComponent<RaceRunnerView>();
                if (view.IdleClip != null) view.IdleClip.SampleAnimation(view.MountAnimator.gameObject, .15f);
                var renderers = new List<Renderer>();
                foreach (var skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.enabled || skin.name.StartsWith("obj_outline")) continue;
                    var mesh = new Mesh();
                    skin.BakeMesh(mesh);
                    meshes.Add(mesh);
                    var part = new GameObject("obj_portrait_part", typeof(MeshFilter), typeof(MeshRenderer));
                    part.layer = 31;
                    part.transform.SetParent(bakedRoot.transform, false);
                    part.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                    part.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    renderer.renderingLayerMask = 3;
                    renderers.Add(renderer);
                }
                if (renderers.Count == 0) throw new InvalidOperationException("No portrait mesh: " + source.name);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                var direction = clone.transform.TransformDirection(new Vector3(.5f, .24f, 1).normalized);
                var rotation = Quaternion.LookRotation(-direction, Vector3.up);
                float distance = 0;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var point = Quaternion.Inverse(rotation) * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                            distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y)) / Mathf.Tan(19 * Mathf.Deg2Rad) + point.z);
                        }
                camera = new GameObject("obj_portrait_camera").AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 31;
                camera.fieldOfView = 38;
                camera.nearClipPlane = .05f;
                camera.transform.SetPositionAndRotation(bounds.center + direction * distance * 1.04f, rotation);
                target = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                ShaderUtil.allowAsyncCompilation = false;
                camera.Render();
                RenderTexture.active = target;
                texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = previousTarget;
                if (camera != null) { camera.targetTexture = null; Object.DestroyImmediate(camera.gameObject); }
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (texture != null) Object.DestroyImmediate(texture);
                Object.DestroyImmediate(bakedRoot);
                Object.DestroyImmediate(clone);
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static Material MakeMaterial(string id, Material original)
        {
            if (id == "Minorous") return AssetDatabase.LoadAssetAtPath<Material>(minoArt + "/Materials/Minorous_Anime.mat");
            string texturePath = art + "/Textures/" + id + "_AnimeAlbedo.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture " + texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(minoArt + "/Materials/Minorous_Anime.mat"));
            material.name = id + "_Anime";
            material.shader = AssetDatabase.LoadAssetAtPath<Shader>(art + "/Shaders/MonsterAnime.shader");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            foreach (string property in new[] { "_Cull", "_AlphaClip", "_Cutoff" })
                if (original.HasProperty(property)) material.SetFloat(property, original.GetFloat(property));
            material.SetFloat("_Specular", id == "Orc" ? .04f : .015f);
            material.SetFloat("_RimStrength", .055f);
            if (id == "Lunatic")
            {
                material.SetColor("_BaseColor", new Color(.88f, .88f, .90f, 1));
                material.SetColor("_ShadeColor", new Color(.76f, .77f, .88f, 1));
            }
            AssetDatabase.CreateAsset(material, art + "/Materials/" + id + "_Anime.mat");
            return material;
        }

        private static void CopyLookEnvironment(Scene target)
        {
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Scene_MinorousRaceTest.unity", OpenSceneMode.Additive);
            try
            {
                foreach (string name in new[] { "obj_lookdev_lighting", "obj_race_lookdev_color" })
                {
                    var original = Find<Transform>(source).Single(tr => tr.name == name);
                    var copy = Object.Instantiate(original.gameObject);
                    copy.name = name;
                    SceneManager.MoveGameObjectToScene(copy, target);
                }
            }
            finally { EditorSceneManager.CloseScene(source, true); }
            SceneManager.SetActiveScene(target);
            // The group camera is farther away than the one-monster preview camera.
            string profilePath = art + "/Monster_RaceVolume.asset";
            if (!AssetDatabase.CopyAsset(minoArt + "/Minorous_RaceVolume.asset", profilePath))
                throw new InvalidOperationException("Unable to create the group-race volume profile.");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile.TryGet<DepthOfField>(out var depth))
            {
                depth.gaussianStart.Override(70);
                depth.gaussianEnd.Override(120);
                EditorUtility.SetDirty(depth);
            }
            Find<Volume>(target).Single(volume => volume.name == "obj_race_lookdev_color").sharedProfile = profile;
            RenderSettings.sun = Find<Light>(target).Single(light => light.name == "light_key");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.60f, .70f, .85f);
            RenderSettings.ambientEquatorColor = new Color(.46f, .49f, .57f);
            RenderSettings.ambientGroundColor = new Color(.30f, .25f, .22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.72f, .80f, .87f);
            RenderSettings.fogStartDistance = 55;
            RenderSettings.fogEndDistance = 180;
            foreach (var renderer in Find<Renderer>(target).Where(r => r.name == "TurfDark" || r.name == "TurfLight"))
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(minoArt + "/Materials/" + renderer.name + "_LookDev.mat");
        }

        [MenuItem("Tools/Project Run/Look development/Connect race selection to title")]
        public static void ConnectTitle()
        {
            RequireSavedEditMode();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("Create the updated race before connecting it.");
            string prefabPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_PlayMode.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ConfigureTitle(prefab.GetComponent<UIPanel_PlayMode>());
                PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var title = EditorSceneManager.OpenScene("Assets/Scenes/Scene_Title.unity");
            ConfigureTitle(Find<UIPanel_PlayMode>(title).Single());
            SaveScene(title);
            AddBuildScene();
        }

        private static void ConfigureTitle(UIPanel_PlayMode panel) => PlayModeMenuBuilder.ConfigureTitle(panel);

        private static void AddBuildScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(scene => scene.path == ScenePath);
            if (existing == null) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else existing.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static T[] Find<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void SaveScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    if (PrefabUtility.IsPartOfPrefabInstance(component.gameObject))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void RequireSavedEditMode()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene changes first.");
        }
    }
}
