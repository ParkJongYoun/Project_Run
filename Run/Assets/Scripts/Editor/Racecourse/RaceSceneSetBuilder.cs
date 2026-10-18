using System;
using System.Linq;
using System.Text;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Reuses the three authored races, and fills only the missing original-monster Prontera combination.</summary>
    public static class RaceSceneSetBuilder
    {
        [MenuItem("Tools/Project Run/Arrange four race scenes")]
        public static string Arrange()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before arranging scenes.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before arranging scenes.");
            var report = new StringBuilder();
            Rename("Scene_All3D", Define_Scene.Oval, report);
            Rename("Scene_MonsterLookDevRace", Define_Scene.OvalToon, report);
            Rename("Scene_PronteraWorldRace", Define_Scene.PronteraToon, report);
            string originalWorld = Define_Scene.Path(Define_Scene.Prontera);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(originalWorld) == null)
            {
                if (!AssetDatabase.CopyAsset(Define_Scene.Path(Define_Scene.PronteraToon), originalWorld))
                    throw new InvalidOperationException("Could not create the missing Prontera/original combination.");
                ConfigureOriginalMonsters(EditorSceneManager.OpenScene(originalWorld));
                report.AppendLine("Created only the missing combination: " + Define_Scene.Prontera);
            }
            else report.AppendLine("Reused existing " + Define_Scene.Prontera);
            PlayModeMenuBuilder.ConnectTitle();
            report.Append(Validate());
            return report.ToString();
        }

        private static void Rename(string previous, string next, StringBuilder report)
        {
            string from = Define_Scene.Path(previous), to = Define_Scene.Path(next);
            bool oldExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(from) != null;
            bool newExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(to) != null;
            if (oldExists && newExists) throw new InvalidOperationException("Both scene names exist; refusing to overwrite: " + previous + ", " + next);
            if (!oldExists && !newExists) throw new InvalidOperationException("Missing authored scene: " + from);
            if (oldExists)
            {
                string guid = AssetDatabase.AssetPathToGUID(from);
                string error = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (AssetDatabase.AssetPathToGUID(to) != guid) throw new InvalidOperationException("Scene GUID changed during rename.");
                // Preserve unrelated/disabled build scenes while remapping this scene's path.
                var settings = EditorBuildSettings.scenes;
                foreach (var entry in settings) if (entry.path == from) entry.path = to;
                EditorBuildSettings.scenes = settings;
                report.AppendLine("Renamed with GUID preserved: " + previous + " -> " + next);
            }
            else report.AppendLine("Reused existing " + next);
        }

        private static void ConfigureOriginalMonsters(Scene target)
        {
            var original = EditorSceneManager.OpenPreviewScene(Define_Scene.Path(Define_Scene.Oval));
            try
            {
                var originalRace = Find<Sys_Race>(original).Single();
                var originalViews = Find<Sys_RacePresentation>(original).Single().Views;
                var styles = Find<Sys_RaceRenderStyle>(original).FirstOrDefault();
                var race = Find<Sys_Race>(target).Single();
                var presentation = Find<Sys_RacePresentation>(target).Single();
                if (originalViews.Length != presentation.Views.Length) throw new InvalidOperationException("Runner count mismatch.");
                for (int i = 0; i < originalViews.Length; i++)
                {
                    var old = presentation.Views[i];
                    var source = originalViews[i];
                    var setup = JsonUtility.FromJson<Data_RunnerSetup>(JsonUtility.ToJson(originalRace.Entrants[i]));
                    if (setup.Prefab == null) throw new InvalidOperationException("Missing original runner prefab.");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(setup.Prefab, target);
                    instance.transform.SetParent(old.transform.parent, false);
                    var modifications = PrefabUtility.GetPropertyModifications(source);
                    if (modifications != null)
                        PrefabUtility.SetPropertyModifications(instance, modifications.Where(mod => mod.objectReference == null || EditorUtility.IsPersistent(mod.objectReference)).ToArray());
                    instance.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
                    instance.transform.localScale = source.transform.localScale;
                    instance.name = source.name;
                    var view = instance.GetComponent<RaceRunnerView>();
                    view.RunnerId = i;
                    var look = view.GetComponent<Sys_MonsterLook>();
                    if (look != null && styles != null) look.Apply(styles.InitialStyle);
                    race.Entrants[i] = setup;
                    presentation.Views[i] = view;
                    Object.DestroyImmediate(old.gameObject);
                    foreach (var component in instance.GetComponentsInChildren<Component>(true))
                        if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                var hud = Find<UIPanel_RaceHUD>(target).Single();
                var selected = race.Entrants[hud.SelectedRunnerId];
                hud.FocusPortrait.sprite = selected.FacePortrait != null ? selected.FacePortrait : selected.Portrait;
                EditorUtility.SetDirty(race); EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(hud);
                EditorSceneManager.MarkSceneDirty(target);
                if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Could not save the new combination.");
            }
            finally { EditorSceneManager.ClosePreviewScene(original); }
        }

        public static string Validate()
        {
            var report = new StringBuilder();
            string[] names = { Define_Scene.Oval, Define_Scene.Prontera, Define_Scene.OvalToon, Define_Scene.PronteraToon };
            for (int index = 0; index < names.Length; index++)
            {
                string path = Define_Scene.Path(names[index]);
                if (!EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == path)) throw new InvalidOperationException("Scene not in build: " + path);
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var race = Find<Sys_Race>(scene).Single();
                    var presentation = Find<Sys_RacePresentation>(scene).Single();
                    bool world = index % 2 == 1, toon = index >= 2;
                    if (world != (race.Course != null)) throw new InvalidOperationException("Incorrect course: " + names[index]);
                    if (race.Entrants.Length != 6 || presentation.Views.Length != 6) throw new InvalidOperationException("Expected six monsters.");
                    for (int i = 0; i < 6; i++)
                    {
                        var view = presentation.Views[i];
                        var prefab = race.Entrants[i].Prefab;
                        string prefabPath = AssetDatabase.GetAssetPath(prefab);
                        if (toon != prefabPath.Contains("/CartoonMonsters/")) throw new InvalidOperationException("Incorrect character asset: " + names[index]);
                        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject) != prefabPath) throw new InvalidOperationException("Runner reference mismatch.");
                        if (view.HasRider || view.MountAnimator == null || view.RunClip == null || view.CameraTarget == null) throw new InvalidOperationException("Invalid monster setup.");
                        if (race.Entrants[i].FacePortrait == null) throw new InvalidOperationException("Missing monster portrait.");
                    }
                    var menu = Find<UIPanel_PlayMode>(scene).Single();
                    if (menu.Scenes.Length != 1 || menu.Scenes[0] != Define_Scene.Title) throw new InvalidOperationException("Missing title return link.");
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) throw new InvalidOperationException("Missing scene script.");
                    report.AppendLine(names[index] + ": " + (world ? "Prontera" : "Oval") + " / " + (toon ? "CartoonMonsters" : "DemoMonsters") + " / 6 runners / title return OK");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            var title = EditorSceneManager.OpenPreviewScene(Define_Scene.Path(Define_Scene.Title));
            try
            {
                var menu = Find<UIPanel_PlayMode>(title).Single();
                if (!menu.Scenes.SequenceEqual(names.Concat(new[] { Define_Scene.Lobby })) || menu.Buttons.Length != menu.Scenes.Length) throw new InvalidOperationException("Incorrect title selection mapping.");
                if (menu.Buttons.Any(button => button == null || button.targetGraphic == null || !button.targetGraphic.raycastTarget)) throw new InvalidOperationException("Invalid title button.");
                if (Find<UnityEngine.EventSystems.EventSystem>(title).Length != 1 || Find<UnityEngine.UI.GraphicRaycaster>(title).Length == 0) throw new InvalidOperationException("Invalid title input setup.");
                if (!EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == Define_Scene.Path(Define_Scene.Lobby))) throw new InvalidOperationException("Lobby is not in the build.");
                report.AppendLine("Title: four race selections and lobby; input and build entries OK.");
            }
            finally { EditorSceneManager.ClosePreviewScene(title); }
            return report.ToString();
        }

        private static T[] Find<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
