using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectRun.EditorTools
{
    // Bake only arm rotations into independent cartoon clips; keep the source gait intact.
    public static class CartoonRunArmBuilder
    {
        private const string RecordFolder = "DataBundles/검증/카툰_에드가_미노_팔달리기_20261020";
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != CartoonShowcaseBuilder.ScenePath || scene.isDirty) throw new InvalidOperationException("Open the saved cartoon scene first.");
            Directory.CreateDirectory(RecordFolder + "/수정전");
            Backup(scene.path);
            foreach (string id in new[] { "Edgar", "Minorous" }) BuildOne(id);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void Backup(string path)
        {
            string target = RecordFolder + "/수정전/" + Path.GetFileName(path);
            if (!File.Exists(target)) File.Copy(path, target);
        }

        private static void BuildOne(string id)
        {
            string folder = "Assets/ArtBundles/Characters/CartoonMonsters/" + id + "/Animations/";
            string prefabPath = "Assets/Resources/Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_" + id + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + id + "_Run.anim");
            Backup(prefabPath); Backup(AssetDatabase.GetAssetPath(source));
            var clone = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            clone.hideFlags = HideFlags.HideAndDontSave;
            var clip = Object.Instantiate(source);
            clip.name = id + "_Run_FreeArms";
            try
            {
                var view = clone.GetComponent<RaceRunnerView>();
                var root = view.MountAnimator.transform;
                var bones = root.GetComponentsInChildren<Transform>(true);
                Func<string, Transform> bone = name => bones.First(t => t.name == name);
                var arms = id == "Edgar"
                    ? new[] { bone("Bip001 L Clavicle"), bone("Bip001 L UpperArm"), bone("Bip001 L Forearm"), bone("Bip001 L Hand") }
                    : new[] { bone("bone_l_upperarm"), bone("bone_l_forearm"), bone("bone_r_upperarm"), bone("bone_r_forearm") };
                var rotations = arms.ToDictionary(t => t, t => new List<Quaternion>());
                const int count = 96;
                var means = new float[2];
                if (id == "Minorous")
                    for (int i = 0; i < count; i++)
                    {
                        source.SampleAnimation(root.gameObject, source.length * i / count);
                        for (int side = 0; side < 2; side++) means[side] += LegAngle(root, bone, side) / count;
                    }
                for (int i = 0; i <= count; i++)
                {
                    float time = source.length * (i % count) / count;
                    if (id == "Edgar")
                    {
                        // Opposite half-cycle of the unmodified right arm, reflected across the model's sagittal plane.
                        source.SampleAnimation(root.gameObject, Mathf.Repeat(time + source.length * .5f, source.length));
                        Vector3 upper = Mirror(root, bone("Bip001 R Forearm").position - bone("Bip001 R UpperArm").position);
                        Vector3 fore = Mirror(root, bone("Bip001 R Hand").position - bone("Bip001 R Forearm").position);
                        Vector3 palm = Mirror(root, bone("socket_r_hand").position - bone("Bip001 R Hand").position);
                        source.SampleAnimation(root.gameObject, time);
                        Vector3 shoulder = Mirror(root, bone("Bip001 R UpperArm").position - bone("Bip001 R Clavicle").position);
                        Aim(arms[0], arms[1], shoulder);
                        Aim(arms[1], arms[2], upper);
                        Aim(arms[2], arms[3], fore);
                        Aim(arms[3], bone("socket_l_hand"), palm);
                    }
                    else
                    {
                        source.SampleAnimation(root.gameObject, time);
                        for (int side = 0; side < 2; side++)
                        {
                            string prefix = side == 0 ? "bone_l_" : "bone_r_";
                            var upper = bone(prefix + "upperarm"); var fore = bone(prefix + "forearm"); var hand = bone(prefix + "hand");
                            float swing = Mathf.Clamp(-(LegAngle(root, bone, side) - means[side]) * .8f, -34, 34);
                            float pitch = (swing - 6) * Mathf.Deg2Rad;
                            float lateral = Mathf.Sign(root.InverseTransformPoint(upper.position).x);
                            Aim(upper, fore, root.TransformDirection(new Vector3(lateral * .22f, -Mathf.Cos(pitch), Mathf.Sin(pitch))));
                            float elbow = (68 + 7 * Mathf.Sin(pitch)) * Mathf.Deg2Rad;
                            Aim(fore, hand, root.TransformDirection(new Vector3(lateral * .10f, -Mathf.Cos(pitch + elbow), Mathf.Sin(pitch + elbow))));
                        }
                    }
                    foreach (var arm in arms) rotations[arm].Add(arm.localRotation);
                }
                foreach (var arm in arms)
                {
                    string path = AnimationUtility.CalculateTransformPath(arm, root);
                    // Remove any Euler representation before replacing quaternion curves.
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.path == path && b.propertyName.IndexOf("Euler", StringComparison.OrdinalIgnoreCase) >= 0))
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                    for (int c = 0; c < 4; c++)
                    {
                        var keys = new Keyframe[count + 1];
                        for (int k = 0; k <= count; k++) keys[k] = new Keyframe(source.length * k / count, rotations[arm][k][c]);
                        var curve = new AnimationCurve(keys);
                        for (int k = 0; k <= count; k++) curve.SmoothTangents(k, 0);
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[c]), curve);
                    }
                }
                clip.EnsureQuaternionContinuity();
                string output = folder + clip.name + ".anim";
                var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
                if (saved == null) { AssetDatabase.CreateAsset(clip, output); saved = clip; }
                else { EditorUtility.CopySerialized(clip, saved); Object.DestroyImmediate(clip); }
                var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var runner = prefab.GetComponent<RaceRunnerView>(); runner.RunClip = saved;
                    var serialized = new SerializedObject(runner);
                    if (serialized.FindProperty("UseMonsterMotionPreview").boolValue)
                        serialized.FindProperty("PreviewRunClip").objectReferenceValue = saved;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                var showcase = Object.FindFirstObjectByType<Sys_CartoonShowcase>();
                foreach (var entry in showcase.Exhibits.Where(e => e.Root.name == "V_Cartoon_" + id)) entry.Run = saved;
                EditorUtility.SetDirty(showcase);
            }
            finally { Object.DestroyImmediate(clone); }
        }

        private static Vector3 Mirror(Transform root, Vector3 direction)
        {
            var local = root.InverseTransformDirection(direction); local.x = -local.x;
            return root.TransformDirection(local);
        }
        private static void Aim(Transform joint, Transform child, Vector3 direction)
        {
            joint.rotation = Quaternion.FromToRotation(child.position - joint.position, direction) * joint.rotation;
        }
        private static float LegAngle(Transform root, Func<string, Transform> bone, int side)
        {
            string prefix = side == 0 ? "bone_l_" : "bone_r_";
            var direction = root.InverseTransformDirection(bone(prefix + "calf").position - bone(prefix + "thigh").position);
            return Mathf.Atan2(direction.z, -direction.y) * Mathf.Rad2Deg;
        }
    }
}
