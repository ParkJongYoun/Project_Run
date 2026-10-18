using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        [MenuItem("Tools/Project Run/Race/Apply distinct mount lineup")]
        public static void ApplyMountVariety()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Scene_Oval.unity")
                throw new InvalidOperationException("Open Scene_Oval in Edit mode.");
            var race = Object.FindFirstObjectByType<Sys_Race>();
            var presentation = Object.FindFirstObjectByType<Sys_RacePresentation>();
            if (race == null || race.Entrants.Length != mounts.Length || presentation.Views.Length != mounts.Length)
                throw new InvalidOperationException("Build the six-rider scene first.");
            foreach (int index in Enumerable.Range(0, mounts.Length))
            {
                var old = presentation.Views[index];
                Transform parent = old.transform.parent;
                Vector3 position = old.transform.position;
                Quaternion rotation = old.transform.rotation;
                var prefab = BuildRunner(index);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.SetPositionAndRotation(position, rotation);
                presentation.Views[index] = instance.GetComponent<RaceRunnerView>();
                presentation.Views[index].RunnerId = index;
                Object.DestroyImmediate(old.gameObject);
                var entry = race.Entrants[index];
                entry.Prefab = prefab; entry.Name = runnerNames[index]; entry.Mount = mountNames[index]; entry.SkillName = skills[index];
                entry.Portrait = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/UI/Portrait_" + index + ".png");
            }
            RaceMonsterLookBuilder.ConfigureScene();
            // Replacing a mount preserves the scene bindings and portrait GUIDs.
            EditorUtility.SetDirty(race); EditorUtility.SetDirty(presentation);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene);
            Debug.Log("Six distinct mounts saved.");
        }
    }
}

