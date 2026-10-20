using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.EditorTools
{
    public static class NewMinotaurusCartoonBuilder
    {
        public const string Art = "Assets/ArtBundles/Characters/CartoonMonsters/NewMinotaurus";
        public const string Prefab = "Assets/Resources/Prefabs/Showcase/V_Cartoon_NewMinotaurus.prefab";
        private static readonly string[] motions = { "Idle", "Walk", "Run", "Clap" };

        [MenuItem("Tools/Project Run/Cartoon monsters/Build new minotaurus cartoon look")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = SceneManager.GetSceneByPath(CartoonShowcaseBuilder.ScenePath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Open the saved cartoon scene first.");
            foreach (string folder in new[] { "Materials", "Meshes", "Rig", "Animations" }) Directory.CreateDirectory(Art + "/" + folder);
            AssetDatabase.Refresh();
            // The fragmented source UV atlas loses alignment when repainted generatively.
            // Preserve its 4K detail in dedicated copies and stylize through lighting.
            string texturePath = Art + "/Textures/NewMinotaurus_DetailAlbedo.png";
            string normalPath = Art + "/Textures/NewMinotaurus_DetailNormal.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.SaveAndReimport();
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.sRGBTexture = false; normalImporter.mipmapEnabled = true;
            normalImporter.maxTextureSize = 4096; normalImporter.npotScale = TextureImporterNPOTScale.None;
            normalImporter.textureCompression = TextureImporterCompression.Uncompressed;
            normalImporter.wrapMode = TextureWrapMode.Repeat; normalImporter.filterMode = FilterMode.Trilinear; normalImporter.anisoLevel = 4;
            normalImporter.SaveAndReimport();
            var surface = Copy(AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Characters/CartoonMonsters/Minorous/Materials/Minorous_Anime.mat"), Art + "/Materials/NewMinotaurus_Anime.mat");
            var shader = Shader.Find("ProjectRun/CartoonMonsters/NewMinotaurusAnime");
            if (shader == null) throw new InvalidOperationException("Missing new minotaurus detail shader.");
            surface.shader = shader;
            surface.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            surface.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            surface.SetFloat("_BumpScale", .7f);
            surface.SetColor("_BaseColor", Color.white);
            surface.SetTextureScale("_BaseMap", Vector2.one); surface.SetTextureOffset("_BaseMap", Vector2.zero);
            surface.SetFloat("_Specular", .035f);
            surface.SetFloat("_Gloss", 48f);
            surface.SetFloat("_Softness", .16f);
            surface.SetFloat("_Threshold", .18f);
            surface.SetColor("_ShadeColor", new Color(.66f, .60f, .63f, 1));
            surface.SetFloat("_AmbientStrength", .1f);
            surface.SetFloat("_FillStrength", .2f);
            surface.SetFloat("_RimStrength", .025f);
            surface.SetFloat("_RimPower", 3.5f);
            var outline = Copy(AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Characters/CartoonMonsters/Minorous/Materials/Minorous_AnimeOutline.mat"), Art + "/Materials/NewMinotaurus_AnimeOutline.mat");
            outline.SetFloat("_OutlineWidth", 1.1f);
            EditorUtility.SetDirty(surface); EditorUtility.SetDirty(outline);
            foreach (string motion in motions) Copy(OriginalClip(motion), Art + "/Animations/NewMinotaurus_" + motion + ".anim");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Art + "/Animations/NewMinotaurus.controller");
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(Art + "/Animations/NewMinotaurus.controller");
                var machine = controller.layers[0].stateMachine;
                foreach (string motion in motions)
                {
                    var state = machine.AddState(motion); state.motion = Clip(motion);
                    if (motion == "Idle") machine.defaultState = state;
                }
            }
            var preview = EditorSceneManager.NewPreviewScene();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NewMinotaurusComparisonBuilder.Prefab);
            var root = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(root, preview);
            root.name = "V_Cartoon_NewMinotaurus";
            try
            {
                var animator = root.GetComponentInChildren<Animator>(true);
                animator.avatar = Copy(animator.avatar, Art + "/Rig/NewMinotaurus_Avatar.asset");
                animator.runtimeAnimatorController = controller;
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.sharedMesh = Copy(skin.sharedMesh, Art + "/Meshes/NewMinotaurus.asset");
                    skin.sharedMaterials = Enumerable.Repeat(surface, skin.sharedMaterials.Length).ToArray();
                    var mesh = Copy(skin.sharedMesh, Art + "/Meshes/NewMinotaurus_Outline.asset");
                    var normals = mesh.normals;
                    var sums = new Dictionary<Vector3Int, Vector3>();
                    var vertices = mesh.vertices;
                    var keys = new Vector3Int[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var v = vertices[i] * 10000;
                        keys[i] = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                        Vector3 sum; sums.TryGetValue(keys[i], out sum); sums[keys[i]] = sum + normals[i];
                    }
                    for (int i = 0; i < normals.Length; i++) normals[i] = sums[keys[i]].normalized;
                    mesh.normals = normals; EditorUtility.SetDirty(mesh);
                    var shell = new GameObject("obj_outline").AddComponent<SkinnedMeshRenderer>();
                    shell.transform.SetParent(skin.transform, false);
                    shell.sharedMesh = mesh; shell.bones = skin.bones; shell.rootBone = skin.rootBone; shell.localBounds = skin.localBounds;
                    shell.sharedMaterials = Enumerable.Repeat(outline, skin.sharedMaterials.Length).ToArray();
                    shell.updateWhenOffscreen = true; shell.shadowCastingMode = ShadowCastingMode.Off;
                    shell.receiveShadows = false; shell.lightProbeUsage = LightProbeUsage.Off; shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                if (PrefabUtility.SaveAsPrefabAsset(root, Prefab) == null) throw new InvalidOperationException("Could not save cartoon minotaurus prefab.");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            var show = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Sys_CartoonShowcase>(true)).Single();
            Configure(show);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        private static T Copy<T>(T source, string path) where T : Object
        {
            if (source == null) throw new InvalidOperationException("Missing source for " + path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var copy = Object.Instantiate(source); copy.name = Path.GetFileNameWithoutExtension(path); copy.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(copy, path); return copy;
        }
        private static AnimationClip Clip(string motion) { return AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "/Animations/NewMinotaurus_" + motion + ".anim"); }
        private static AnimationClip OriginalClip(string motion) { return AssetDatabase.LoadAssetAtPath<AnimationClip>(NewMinotaurusComparisonBuilder.Art + "/Animations/Minotaurus_" + motion + ".anim"); }

        public static void Configure(Sys_CartoonShowcase show)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null) return;
            var entry = show.Exhibits.FirstOrDefault(e => e.Root.name == "V_Cartoon_NewMinotaurus");
            if (entry == null) return;
            var current = entry.CartoonRoot;
            Vector3 position = current.localPosition, scale = current.localScale;
            Quaternion rotation = current.localRotation;
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(current.gameObject) != Prefab)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, entry.Root);
                model.transform.SetLocalPositionAndRotation(position, rotation); model.transform.localScale = scale;
                entry.CartoonRoot = model.transform; entry.Animator = model.GetComponentInChildren<Animator>(true);
                Object.DestroyImmediate(current.gameObject);
            }
            entry.Name = "미노타우루스 · 신규";
            entry.KeepAppearance = false;
            entry.Idle = Clip("Idle"); entry.Run = Clip("Run"); entry.Walk = Clip("Walk"); entry.Clap = Clip("Clap");
            entry.OriginalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewMinotaurusComparisonBuilder.Prefab);
            entry.OriginalIdle = OriginalClip("Idle"); entry.OriginalRun = OriginalClip("Run");
            entry.OriginalWalk = OriginalClip("Walk"); entry.OriginalClap = OriginalClip("Clap");
            entry.OriginalPosition = position; entry.OriginalRotation = rotation; entry.OriginalScale = scale;
            entry.Idle.SampleAnimation(entry.Animator.gameObject, .15f);
            var label = show.NameLabels.FirstOrDefault(g => g.name == "txt_name_NewMinotaurus");
            if (label != null) label.GetComponent<TextMesh>().text = "미노 · 신규";
            show.SafeArea.Find("txt_note").GetComponent<UnityEngine.UI.Text>().text = "기존 / 신규 미노 비교 · 원본 ↔ 카툰 전환 · 신규 미노 4종 동작";
            EditorUtility.SetDirty(show);
        }
    }
}
