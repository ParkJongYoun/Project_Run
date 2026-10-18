using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Promotes the approved six-monster scene to independent, fixed-look runner assets.</summary>
    public static class CartoonMonsterPrefabBuilder
    {
        public const string ArtRoot = "Assets/ArtBundles/Characters/CartoonMonsters";
        public const string PrefabRoot = "Assets/Resources/Prefabs/Race/CartoonMonsters";
        private const string scenePath = "Assets/Scenes/Scene_OvalToon.unity";
        private const string recordRoot = "DataBundles/LookDev/카툰몬스터_독립에셋_20261017";
        private static readonly string[] ids = { "Minorous", "GiantPoring", "Smokie", "Savage", "Lunatic", "Edgar" };
        private static readonly Dictionary<Object, Object> copies = new Dictionary<Object, Object>();
        private static readonly List<object> manifest = new List<object>();

        [MenuItem("Tools/Project Run/Cartoon monsters/Separate approved six runners")]
        public static void Build()
        {
            RequireSavedEditMode();
            if (Directory.Exists(ArtRoot) || Directory.Exists(PrefabRoot))
                throw new InvalidOperationException("Cartoon assets already exist. Edit the independent assets in place.");
            var scene = EditorSceneManager.OpenScene(scenePath);
            var presentation = Find<Sys_RacePresentation>(scene).Single();
            var race = Find<Sys_Race>(scene).Single();
            if (presentation.Views.Length != ids.Length || race.Entrants.Length != ids.Length)
                throw new InvalidOperationException("Expected the approved six runners.");
            for (int i = 0; i < ids.Length; i++)
            {
                var view = presentation.Views[i];
                if (view.name != "P_Runner_" + ids[i] || view.transform.localScale != Vector3.one)
                    throw new InvalidOperationException("Unexpected runner/root scale: " + view.name);
                if (view.MountAnimator.runtimeAnimatorController != null || view.HasRider)
                    throw new InvalidOperationException("Inspect the runner animation setup before separating it.");
            }
            CaptureBefore();
            copies.Clear();
            manifest.Clear();
            foreach (var comparison in Find<Sys_RaceLookDev>(scene)) comparison.ShowUpdated();

            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                var view = presentation.Views[i];
                if (PrefabUtility.IsPartOfPrefabInstance(view.gameObject))
                    PrefabUtility.UnpackPrefabInstance(view.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var look in view.GetComponentsInChildren<Sys_MonsterLook>(true)) Object.DestroyImmediate(look);
                foreach (var component in view.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new InvalidOperationException("Missing runner script: " + id);
                    RemapReferences(component, id);
                }

                var portrait = (Sprite)CopyResource(race.Entrants[i].FacePortrait, id);
                race.Entrants[i].FacePortrait = portrait;
                race.Entrants[i].Portrait = portrait;
                view.name = "P_Runner_Cartoon_" + id;
                var position = view.transform.localPosition;
                var rotation = view.transform.localRotation;
                view.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                string prefabPath = PrefabRoot + "/" + view.name + ".prefab";
                EnsureFolder(PrefabRoot);
                PrefabUtility.SaveAsPrefabAssetAndConnect(view.gameObject, prefabPath, InteractionMode.AutomatedAction, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save " + prefabPath);
                race.Entrants[i].Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                view.transform.SetLocalPositionAndRotation(position, rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
            }

            // These components retain the old material arrays even when disabled.
            foreach (var comparison in Find<Sys_RaceLookDev>(scene)) Object.DestroyImmediate(comparison.gameObject);
            foreach (var selector in Find<UIItem_RenderStyle>(scene)) Object.DestroyImmediate(selector.gameObject);
            foreach (var styles in Find<Sys_RaceRenderStyle>(scene)) Object.DestroyImmediate(styles);
            var hud = Find<UIPanel_RaceHUD>(scene).Single();
            hud.FocusPortrait.sprite = race.Entrants[hud.SelectedRunnerId].FacePortrait;
            EditorUtility.SetDirty(race);
            AssetDatabase.SaveAssets();
            string report = Validate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save cartoon race.");
            File.WriteAllText(recordRoot + "/asset_manifest.json", JsonConvert.SerializeObject(manifest, Formatting.Indented));
            File.WriteAllText(recordRoot + "/asset_validation.json", report);
            VerifyProtectedFiles();
            Debug.Log("Six independent cartoon runner prefabs saved and validated. Report: " + recordRoot);
        }

        private static Object CopyResource(Object source, string id)
        {
            if (source == null || source is MonoScript) return source;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal)) return source;
            if (sourcePath.StartsWith(ArtRoot + "/", StringComparison.Ordinal)) return source;
            if (copies.TryGetValue(source, out var existing)) return existing;

            string owner = ArtRoot + "/" + id;
            string name = Regex.Replace(source.name, "[^a-zA-Z0-9_-]", "_");
            string destination;
            Object result;
            if (source is Shader shader)
            {
                if (!sourcePath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Inspect shader dependency " + sourcePath);
                destination = ArtRoot + "/Shared/Shaders/" + Path.GetFileName(sourcePath);
                EnsureFolder(Path.GetDirectoryName(destination));
                string text = File.ReadAllText(sourcePath);
                text = Regex.Replace(text, "Shader\\s+\"[^\"]+\"", "Shader \"ProjectRun/CartoonMonsters/" + shader.name.Split('/').Last() + "\"", RegexOptions.None);
                File.WriteAllText(destination, text);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                result = AssetDatabase.LoadAssetAtPath<Shader>(destination);
            }
            else if (source is Texture || source is Sprite)
            {
                bool shared = sourcePath.Contains("/Shared/") || sourcePath.EndsWith("/SoftDisc.png", StringComparison.Ordinal);
                string folder = source is Sprite ? owner + "/Portraits" : (shared ? ArtRoot + "/Shared/Textures" : owner + "/Textures");
                destination = folder + "/" + Path.GetFileName(sourcePath);
                EnsureFolder(folder);
                if (!File.Exists(destination) && !AssetDatabase.CopyAsset(sourcePath, destination))
                    throw new InvalidOperationException("Could not copy " + sourcePath);
                result = source is Sprite ? AssetDatabase.LoadAssetAtPath<Sprite>(destination) : AssetDatabase.LoadAssetAtPath<Texture>(destination);
            }
            else
            {
                string folder, extension;
                if (source is Material material)
                {
                    bool character = material.shader.name.Contains("Anime") || material.shader.name.EndsWith("MonsterOutline", StringComparison.Ordinal);
                    folder = character ? owner + "/Materials" : ArtRoot + "/Shared/Materials";
                    extension = ".mat";
                }
                else if (source is Mesh)
                {
                    folder = sourcePath.Contains("/fbx_shadow/") ? ArtRoot + "/Shared/Meshes" : owner + "/Meshes";
                    extension = ".asset";
                }
                else if (source is AnimationClip) { folder = owner + "/Animations"; extension = ".anim"; }
                else if (source is AvatarMask) { folder = owner + "/Animations"; extension = ".mask"; }
                else if (source is Avatar) { folder = owner + "/Rig"; extension = ".asset"; }
                else throw new InvalidOperationException("Inspect unhandled asset " + source.GetType().Name + ": " + sourcePath);
                EnsureFolder(folder);
                destination = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + extension);
                result = Object.Instantiate(source);
                result.name = source.name;
                result.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(result, destination);
            }
            if (result == null) throw new InvalidOperationException("Missing copied asset: " + destination);
            copies.Add(source, result);
            manifest.Add(new { Character = id, Type = source.GetType().Name, Source = sourcePath, SourceName = source.name, Destination = destination });
            if (!(result is Shader) && !(result is Texture) && !(result is Sprite)) RemapReferences(result, id);
            return result;
        }

        private static void RemapReferences(Object target, string id)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script") continue;
                var source = property.objectReferenceValue;
                if (source == null || !EditorUtility.IsPersistent(source)) continue;
                var copy = CopyResource(source, id);
                if (source == copy) continue;
                property.objectReferenceValue = copy;
                changed = true;
            }
            if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
        }

        public static string Validate()
        {
            var reports = new List<object>();
            foreach (string id in ids)
            {
                string path = PrefabRoot + "/P_Runner_Cartoon_" + id + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Regular)
                    throw new InvalidOperationException("Expected an independent regular prefab: " + path);
                if (prefab.GetComponentsInChildren<Sys_MonsterLook>(true).Length != 0)
                    throw new InvalidOperationException("Old appearance controller retained: " + id);
                var dependencies = AssetDatabase.GetDependencies(path, true);
                var unexpected = dependencies.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)
                    && p != path && !p.StartsWith(ArtRoot + "/", StringComparison.Ordinal)
                    && !p.StartsWith("Assets/Scripts/", StringComparison.Ordinal)).ToArray();
                if (unexpected.Length != 0) throw new InvalidOperationException(id + " still refers to original assets: " + string.Join(", ", unexpected));
                var view = prefab.GetComponent<RaceRunnerView>();
                if (view == null || view.IdleClip == null || view.RunClip == null || view.HasRider
                    || view.MountAnimator == null || view.MountAnimator.avatar == null || !view.MountAnimator.avatar.isValid)
                    throw new InvalidOperationException("Invalid runner/rig: " + id);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    if (renderer.sharedMaterials.Any(m => m == null || m.shader == null || !m.shader.isSupported))
                        throw new InvalidOperationException("Invalid renderer/material: " + id + "/" + renderer.name);
                foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (skin.sharedMesh == null || skin.bones.Any(b => b == null)) throw new InvalidOperationException("Invalid skin: " + id);
                if (prefab.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0))
                    throw new InvalidOperationException("Missing script: " + id);
                reports.Add(new { Id = id, Prefab = path, Independent = true, DependencyCount = dependencies.Length,
                    Materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Select(AssetDatabase.GetAssetPath).Distinct().ToArray(), Dependencies = dependencies });
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.path == scenePath)
            {
                if (Find<Sys_RaceLookDev>(scene).Any() || Find<Sys_MonsterLook>(scene).Any() || Find<Sys_RaceRenderStyle>(scene).Any() || Find<UIItem_RenderStyle>(scene).Any())
                    throw new InvalidOperationException("An old appearance selector remains in the scene.");
                foreach (var view in Find<RaceRunnerView>(scene))
                    if (!PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject).StartsWith(PrefabRoot + "/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Scene still uses an original runner.");
                var entrants = Find<Sys_Race>(scene).Single().Entrants;
                for (int i = 0; i < ids.Length; i++)
                {
                    string expected = PrefabRoot + "/P_Runner_Cartoon_" + ids[i] + ".prefab";
                    if (AssetDatabase.GetAssetPath(entrants[i].Prefab) != expected)
                        throw new InvalidOperationException("Entrant data still uses an original runner: " + ids[i]);
                    if (!AssetDatabase.GetAssetPath(entrants[i].FacePortrait).StartsWith(ArtRoot + "/", StringComparison.Ordinal)
                        || !AssetDatabase.GetAssetPath(entrants[i].Portrait).StartsWith(ArtRoot + "/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Entrant data still uses an original portrait: " + ids[i]);
                }
            }
            return JsonConvert.SerializeObject(reports, Formatting.Indented);
        }

        private static void CaptureBefore()
        {
            Directory.CreateDirectory(recordRoot);
            string backup = recordRoot + "/Scene_OvalToon.before.unity.txt";
            if (File.Exists(backup)) throw new InvalidOperationException("Migration backup already exists; inspect previous work before retrying.");
            File.Copy(scenePath, backup);
            var paths = new HashSet<string>();
            foreach (string id in ids)
                foreach (string path in AssetDatabase.GetDependencies("Assets/Resources/Prefabs/Race/DemoMonsters/P_Runner_" + id + ".prefab", true))
                    if (path.StartsWith("Assets/", StringComparison.Ordinal)) paths.Add(path);
            paths.Add("Assets/Scenes/Scene_Oval.unity");
            paths.Add("Assets/Scenes/Scene_MinorousLookDev.unity");
            paths.Add("Assets/Scenes/Scene_MinorousRaceTest.unity");
            paths.Add("Assets/Scenes/Scene_Title.unity");
            foreach (string folder in new[] { "Assets/ArtBundles/RaceShowcase/MinorousLookDev", "Assets/ArtBundles/RaceShowcase/MonsterLookDev" })
                foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)) paths.Add(path.Replace('\\', '/'));
            var hashes = paths.Where(File.Exists).ToDictionary(p => p, Hash);
            File.WriteAllText(recordRoot + "/protected_hashes.json", JsonConvert.SerializeObject(hashes, Formatting.Indented));
        }

        public static void VerifyProtectedFiles()
        {
            var hashes = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(recordRoot + "/protected_hashes.json"));
            foreach (var entry in hashes)
                if (!File.Exists(entry.Key) || Hash(entry.Key) != entry.Value) throw new InvalidOperationException("Protected original changed: " + entry.Key);
        }

        private static string Hash(string path)
        {
            using (var hash = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static IEnumerable<T> Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static void RequireSavedEditMode()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save current scene changes first.");
        }
    }
}
