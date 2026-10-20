using System;
using System.Linq;
using ProjectRun.Race;
using ProjectRun.Ranch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class RanchPopulationSetup
    {
        [MenuItem("Tools/Project Run/Ranch/Sync residents with cartoon race lineup")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            const string path = "Assets/Scenes/Scene_LobbyTest.unity";
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save lobby test scene edits first.");
                var group = scene.GetRootGameObjects().Single(r => r.name == "RanchWorld").transform.Find("group_monsters");
                var old = group.GetComponentsInChildren<Sys_RanchResident>(true);
                var population = group.GetComponent<Sys_RanchPopulation>();
                if (population == null) population = group.gameObject.AddComponent<Sys_RanchPopulation>();
                if (population.Positions == null || population.Positions.Length != 6)
                    population.Positions = old.Take(6).Select(r => r.transform.localPosition).ToArray();
                if (population.Positions.Length != 6) throw new InvalidOperationException("Missing six ranch resident positions.");
                var placement = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Sys_RanchPlacement>(true)).Single();
                var roster = Resources.Load<Data_CartoonRaceRoster>("Tables/Data_CartoonRaceRoster");
                var entries = roster.Playable.Concat(roster.Opponents).ToArray();
                var residents = new Sys_RanchResident[entries.Length];
                // Use a common ranch/world scale, retaining all character proportions from the showcase.
                const float scale = .45f;
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = entries[i]; string id = entry.Prefab.name.Replace("P_Runner_CartoonRace_", "");
                    var root = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, group);
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    root.name = "obj_resident_" + id; root.SetActive(false);
                    root.transform.localPosition = population.Positions[Mathf.Min(i, 5)]; root.transform.localRotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one * scale;
                    var view = root.GetComponent<RaceRunnerView>();
                    var resident = root.AddComponent<Sys_RanchResident>(); residents[i] = resident;
                    resident.Placement = placement; resident.TargetAnimator = view.MountAnimator;
                    resident.IdleClip = view.IdleClip; resident.LocomotionClip = view.RunClip;
                    string folder = "Assets/ArtBundles/Characters/CartoonMonsters/" + id + "/Animations/" + id;
                    resident.CelebrationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "_Reaction.anim");
                    resident.AttackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "_Attack.anim");
                    resident.RestClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "_Rest.anim");
                    resident.FeedingHead = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "bone_head") ?? resident.TargetAnimator.transform;
                    resident.MoveSpeed = id == "Minorous" ? .68f : .95f;
                    resident.AnimationRate = id == "Minorous" ? .78f : .65f;
                    resident.Clearance = .64f; resident.Seed = 41 + i * 97;
                    resident.TargetAnimator.applyRootMotion = false; resident.TargetAnimator.fireEvents = false;
                    foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
                    foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true)) trail.gameObject.SetActive(false);
                    Object.DestroyImmediate(view);
                    root.SetActive(i < 6);
                }
                foreach (var resident in old) Object.DestroyImmediate(resident.gameObject);
                population.PlayableResidents = residents.Take(3).ToArray(); population.GuestResidents = residents.Skip(3).ToArray();
                EditorUtility.SetDirty(population); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
