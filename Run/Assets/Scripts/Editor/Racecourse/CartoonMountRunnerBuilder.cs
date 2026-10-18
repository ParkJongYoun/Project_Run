using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Independent, riderless cartoon copies of the two existing mount runners.</summary>
    public static class CartoonMountRunnerBuilder
    {
        private const string art = CartoonMonsterPrefabBuilder.ArtRoot;
        private const string prefabs = CartoonMonsterPrefabBuilder.PrefabRoot;
        private const string record = "DataBundles/LookDev/자이언트포링_에드가_카툰교체_20261017";
        private static readonly string[] ids = { "GiantPoring", "Edgar" };
        private static readonly string[] sources = { "P_Runner_02", "P_Runner_01" };
        private static readonly string[] names = { "자이언트 포링", "호랑이 에드가" };
        private static readonly int[] slots = { 1, 5 };
        private static readonly string[] scenes = { "Assets/Scenes/Scene_OvalToon.unity", "Assets/Scenes/Scene_PronteraToon.unity" };

        [MenuItem("Tools/Project Run/Cartoon monsters/Build Poring and Edgar copies")]
        public static string Build()
        {
            RequireSavedEditMode();
            Directory.CreateDirectory(record);
            for (int i = 0; i < ids.Length; i++) CreateRunner(i);
            AssetDatabase.SaveAssets();
            foreach (string path in scenes) ReplaceInScene(path);
            AssetDatabase.SaveAssets();
            string result = Validate();
            File.WriteAllText(record + "/asset_validation.json", result);
            return result;
        }

        private static T Copy<T>(T source, string path) where T : Object
        {
            if (source == null) throw new InvalidOperationException("Missing source for " + path);
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result != null) return result;
            result = Object.Instantiate(source);
            result.name = source.name; result.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static void CreateRunner(int index)
        {
            string id = ids[index], owner = art + "/" + id;
            foreach (string folder in new[] { "Textures", "Materials", "Meshes", "Rig", "Animations", "Portraits" })
                Directory.CreateDirectory(owner + "/" + folder);
            AssetDatabase.Refresh();
            string texturePath = owner + "/Textures/" + id + "_AnimeAlbedo.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Generate and save the cartoon atlas first: " + texturePath);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.SaveAndReimport();

            var original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Race/" + sources[index] + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "P_Runner_Cartoon_" + id;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var look in instance.GetComponentsInChildren<Sys_MonsterLook>(true)) Object.DestroyImmediate(look);
                var view = instance.GetComponent<RaceRunnerView>();
                if (view.RiderRoot != null) Object.DestroyImmediate(view.RiderRoot.gameObject);
                view.RiderRoot = view.SeatBone = view.RiderPelvis = view.RiderHead = null;
                view.RiderAnimator = null; view.RidingClip = null; view.UseSocketAttachment = false;
                view.UseRiderFacingCloseup = false; view.FaceTarget = view.MonsterFaceTarget;
                view.RunnerId = slots[index];
                view.MountAnimator.runtimeAnimatorController = null;
                view.MountAnimator.avatar = Copy(view.MountAnimator.avatar, owner + "/Rig/" + id + "_Avatar.asset");
                view.IdleClip = Copy(view.IdleClip, owner + "/Animations/" + id + "_Idle.anim");
                view.RunClip = Copy(view.RunClip, owner + "/Animations/" + id + "_Run.anim");

                var template = AssetDatabase.LoadAssetAtPath<Material>(art + "/Smokie/Materials/Smokie_Anime.mat");
                var surface = Copy(template, owner + "/Materials/" + id + "_Anime.mat");
                surface.name = id + "_Anime";
                surface.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                surface.SetColor("_BaseColor", Color.white);
                surface.SetFloat("_Specular", index == 0 ? .025f : .015f);
                surface.SetFloat("_RimStrength", .055f);
                var outlineTemplate = AssetDatabase.LoadAssetAtPath<Material>(art + "/Smokie/Materials/Smokie_AnimeOutline.mat");
                var outline = Copy(outlineTemplate, owner + "/Materials/" + id + "_AnimeOutline.mat");
                outline.name = id + "_AnimeOutline"; outline.SetFloat("_OutlineWidth", .65f);
                foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    bool isOutline = skin.name.StartsWith("obj_outline", StringComparison.Ordinal);
                    if (!isOutline)
                    {
                        if (skin.sharedMaterials.Length != 1) throw new InvalidOperationException("Inspect material slots: " + skin.name);
                        foreach (string property in new[] { "_Cull", "_AlphaClip", "_Cutoff" })
                            if (skin.sharedMaterial.HasProperty(property)) surface.SetFloat(property, skin.sharedMaterial.GetFloat(property));
                        surface.SetTextureScale("_BaseMap", skin.sharedMaterial.mainTextureScale);
                        surface.SetTextureOffset("_BaseMap", skin.sharedMaterial.mainTextureOffset);
                    }
                    skin.sharedMesh = Copy(skin.sharedMesh, owner + "/Meshes/" + (isOutline ? id + "_Outline" : id) + ".asset");
                    skin.sharedMaterials = new[] { isOutline ? outline : surface }; skin.enabled = true;
                }
                foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    filter.sharedMesh = Copy(filter.sharedMesh, owner + "/Meshes/" + filter.sharedMesh.name + ".asset");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true).Where(r => !(r is SkinnedMeshRenderer)))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    {
                        // Effects already have independent cartoon copies; preserve each source's material role.
                        var shared = AssetDatabase.LoadAssetAtPath<Material>(art + "/Shared/Materials/" + m.name + ".mat");
                        if (shared == null) throw new InvalidOperationException("Missing cartoon effect material: " + m.name);
                        return shared;
                    }).ToArray();
                }
                EditorUtility.SetDirty(surface); EditorUtility.SetDirty(outline);
                string prefab = prefabs + "/" + instance.name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefab);
                MonsterRaceLookDevBuilder.CapturePortrait(view, owner + "/Portraits/FacePortrait_" + id + ".png");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void ReplaceInScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var race = Object.FindFirstObjectByType<Sys_Race>();
            var presentation = Object.FindFirstObjectByType<Sys_RacePresentation>();
            for (int i = 0; i < ids.Length; i++)
            {
                int slot = slots[i]; string id = ids[i]; var old = presentation.Views[slot];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs + "/P_Runner_Cartoon_" + id + ".prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(old.transform.parent, false);
                instance.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
                var view = instance.GetComponent<RaceRunnerView>(); view.RunnerId = slot;
                presentation.Views[slot] = view;
                race.Entrants[slot].Name = race.Entrants[slot].Mount = names[i];
                race.Entrants[slot].Prefab = prefab;
                var portrait = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/" + id + "/Portraits/FacePortrait_" + id + ".png");
                race.Entrants[slot].Portrait = race.Entrants[slot].FacePortrait = portrait;
                Object.DestroyImmediate(old.gameObject);
                foreach (var component in instance.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            var hud = Object.FindFirstObjectByType<UIPanel_RaceHUD>();
            hud.FocusPortrait.sprite = race.Entrants[hud.SelectedRunnerId].FacePortrait;
            EditorUtility.SetDirty(race); EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        public static string Validate()
        {
            var report = new List<object>();
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i], path = prefabs + "/P_Runner_Cartoon_" + id + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular) throw new InvalidOperationException("Expected regular prefab: " + path);
                var dependencies = AssetDatabase.GetDependencies(path, true);
                var originalRefs = dependencies.Where(p => p.StartsWith("Assets/") && p != path && !p.StartsWith(art + "/") && !p.StartsWith("Assets/Scripts/")).ToArray();
                if (originalRefs.Length != 0) throw new InvalidOperationException("Original dependencies remain: " + string.Join(", ", originalRefs));
                var view = prefab.GetComponent<RaceRunnerView>();
                if (view.HasRider || view.RiderRoot != null || view.RiderAnimator != null || view.RidingClip != null || view.MountAnimator.avatar == null || !view.MountAnimator.avatar.isValid || view.IdleClip == null || view.RunClip == null)
                    throw new InvalidOperationException("Invalid riderless rig: " + id);
                if (prefab.GetComponent<Sys_MonsterLook>() != null) throw new InvalidOperationException("Old material selector: " + id);
                foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterials.Any(m => m == null || !m.shader.isSupported)) throw new InvalidOperationException("Broken material: " + r.name);
                foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (skin.sharedMesh == null || skin.bones.Any(b => b == null)) throw new InvalidOperationException("Invalid skin: " + id);
                report.Add(new { Id = id, Prefab = path, Regular = true, Rider = false, Dependencies = dependencies });
            }
            foreach (var path in scenes)
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var race = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Sys_Race>(true)).Single();
                    var presentation = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Sys_RacePresentation>(true)).Single();
                    for (int i = 0; i < ids.Length; i++)
                    {
                        int slot = slots[i]; string expected = prefabs + "/P_Runner_Cartoon_" + ids[i] + ".prefab";
                        if (AssetDatabase.GetAssetPath(race.Entrants[slot].Prefab) != expected || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(presentation.Views[slot].gameObject) != expected)
                            throw new InvalidOperationException("Scene roster reference mismatch: " + path);
                        if (race.Entrants[slot].FacePortrait == null || race.Entrants[slot].Name != names[i]) throw new InvalidOperationException("Missing portrait/name: " + path);
                    }
                    report.Add(new { Scene = path, Roster = race.Entrants.Select(e => e.Name).ToArray() });
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            return JsonConvert.SerializeObject(report, Formatting.Indented);
        }

        private static void RequireSavedEditMode()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes first.");
        }
    }
}
