using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Non-destructive motion experiment. RunClip always retains the original reference.</summary>
    public static class RaceMonsterMotionPreview
    {
        private const string OutputFolder = "Assets/ArtBundles/RaceShowcase/Animations/DemoMonsters/MotionPreview";
        private const string PrefabFolder = "Assets/Resources/Prefabs/Race/DemoMonsters/";
        private static readonly string[] ids = { "Minorous", "Orc" };

        [MenuItem("Project Run/Monster Motion/Build and Apply Preview")]
        public static void BuildAndApply()
        {
            RequireEditMode();
            Directory.CreateDirectory(OutputFolder);
            AssetDatabase.Refresh();
            foreach (string id in ids) Build(id);
            SetEnabled(true);
        }

        [MenuItem("Project Run/Monster Motion/Use Preview")]
        public static void UsePreview() { SetEnabled(true); }

        [MenuItem("Project Run/Monster Motion/Restore Original")]
        public static void RestoreOriginal() { SetEnabled(false); }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Switch monster motion before entering Play Mode.");
        }

        private static void Build(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "P_Runner_" + id + ".prefab");
            var clone = Object.Instantiate(prefab);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var view = clone.GetComponent<RaceRunnerView>();
                var root = view.MountAnimator.transform;
                var bones = root.GetComponentsInChildren<Transform>(true);
                var originalRun = view.RunClip;
                var run = Object.Instantiate(originalRun);
                run.name = id + "_Run_ArmSwing";
                AnimationUtility.SetAnimationEvents(run, Array.Empty<AnimationEvent>());
                var arms = bones.Where(t => t.name.EndsWith("upperarm") || t.name.EndsWith("forearm")).ToArray();
                var rotations = arms.ToDictionary(t => t, t => new List<Quaternion>());
                var props = id == "Orc" ? new[] { bones.First(t => t.name == "bone_prop1"), bones.First(t => t.name == "bone_prop2") } : Array.Empty<Transform>();
                var propHands = id == "Orc" ? new[] { bones.First(t => t.name == "bone_r_hand"), bones.First(t => t.name == "bone_l_hand") } : Array.Empty<Transform>();
                originalRun.SampleAnimation(root.gameObject, 0);
                var propOffsets = props.Select((t, i) => propHands[i].InverseTransformPoint(t.position)).ToArray();
                var propRotations = props.Select((t, i) => Quaternion.Inverse(propHands[i].rotation) * t.rotation).ToArray();
                var bakedPropPositions = props.ToDictionary(t => t, t => new List<Vector3>());
                var bakedPropRotations = props.ToDictionary(t => t, t => new List<Quaternion>());
                const int samples = 64;
                var meanLegAngle = new float[2];
                for (int frame = 0; frame < samples; frame++)
                {
                    originalRun.SampleAnimation(root.gameObject, originalRun.length * frame / samples);
                    for (int side = 0; side < 2; side++)
                        meanLegAngle[side] += LegAngle(root, bones, side) / samples;
                }
                for (int frame = 0; frame <= samples; frame++)
                {
                    originalRun.SampleAnimation(root.gameObject, originalRun.length * (frame % samples) / samples);
                    for (int side = 0; side < 2; side++)
                    {
                        string prefix = side == 0 ? "bone_l_" : "bone_r_";
                        var upper = bones.First(t => t.name == prefix + "upperarm");
                        var forearm = bones.First(t => t.name == prefix + "forearm");
                        var hand = bones.First(t => t.name == prefix + "hand");
                        float lateral = Mathf.Sign(root.InverseTransformPoint(upper.position).x);
                        float amplitude = id == "Minorous" ? 38 : (side == 0 ? 22 : 30);
                        float angle = Mathf.Clamp(-(LegAngle(root, bones, side) - meanLegAngle[side]) * 0.7f, -amplitude, amplitude);
                        float pitch = (angle - 5) * Mathf.Deg2Rad;
                        var desiredUpper = root.TransformDirection(new Vector3(lateral * 0.17f, -Mathf.Cos(pitch), Mathf.Sin(pitch)));
                        upper.rotation = Quaternion.FromToRotation(forearm.position - upper.position, desiredUpper) * upper.rotation;
                        // Keep elbows bent, with hands forward and clear of the torso. The shield arm swings less.
                        float elbow = (id == "Minorous" ? 78 : 68) * Mathf.Deg2Rad;
                        var desiredForearm = root.TransformDirection(new Vector3(lateral * 0.05f, -Mathf.Cos(pitch + elbow), Mathf.Sin(pitch + elbow)));
                        forearm.rotation = Quaternion.FromToRotation(hand.position - forearm.position, desiredForearm) * forearm.rotation;
                    }
                    foreach (var arm in arms) rotations[arm].Add(arm.localRotation);
                    for (int i = 0; i < props.Length; i++)
                    {
                        props[i].SetPositionAndRotation(propHands[i].TransformPoint(propOffsets[i]), propHands[i].rotation * propRotations[i]);
                        bakedPropPositions[props[i]].Add(props[i].localPosition);
                        bakedPropRotations[props[i]].Add(props[i].localRotation);
                    }
                }
                foreach (var arm in arms)
                    WriteRotations(run, AnimationUtility.CalculateTransformPath(arm, root), rotations[arm], originalRun.length);
                foreach (var prop in props)
                {
                    string path = AnimationUtility.CalculateTransformPath(prop, root);
                    WriteRotations(run, path, bakedPropRotations[prop], originalRun.length);
                    WriteVectors(run, path, "m_LocalPosition", bakedPropPositions[prop], originalRun.length);
                }
                run.EnsureQuaternionContinuity();
                SaveAsset(run, OutputFolder + "/" + run.name + ".anim");

                string sourceFolder = id == "Minorous"
                    ? "Assets/OldAsset/Monsters/resource/ani_fbx_001/morocc_mob/minorous/animation"
                    : "Assets/OldAsset/Monsters/resource/ani_fbx_002/unused/orc_axe_001/animation";
                var sources = AssetDatabase.FindAssets("t:AnimationClip", new[] { sourceFolder })
                    .Select(AssetDatabase.GUIDToAssetPath).Distinct()
                    .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>())
                    .Where(c => !c.name.StartsWith("__preview__")).ToArray();
                var cast = Object.Instantiate(sources.First(c => c.name.EndsWith("cast")));
                var fire = Object.Instantiate(sources.First(c => c.name.EndsWith("fire")));
                try
                {
                    AnimationUtility.SetAnimationEvents(cast, Array.Empty<AnimationEvent>());
                    AnimationUtility.SetAnimationEvents(fire, Array.Empty<AnimationEvent>());
                    var mask = new AvatarMask { name = id + "_Skill_Arms" };
                    mask.transformCount = bones.Length;
                    for (int i = 0; i < bones.Length; i++)
                    {
                        string path = AnimationUtility.CalculateTransformPath(bones[i], root);
                        mask.SetTransformPath(i, path);
                        mask.SetTransformActive(i, IsArmPath(path));
                    }
                    SaveAsset(mask, OutputFolder + "/" + mask.name + ".mask");
                    var skillBones = bones.Where(t => IsArmPath(AnimationUtility.CalculateTransformPath(t, root))).ToArray();
                    var skillRotations = skillBones.ToDictionary(t => t, t => new List<Quaternion>());
                    var skillPositions = skillBones.ToDictionary(t => t, t => new List<Vector3>());
                    var skillScales = skillBones.ToDictionary(t => t, t => new List<Vector3>());
                    float duration = id == "Minorous" ? 1.8f : 1.65f;
                    const int skillSamples = 100;
                    for (int frame = 0; frame <= skillSamples; frame++)
                    {
                        float phase = (float)frame / skillSamples;
                        if (phase < 0.38f) cast.SampleAnimation(root.gameObject, phase / 0.38f * cast.length);
                        else fire.SampleAnimation(root.gameObject, (phase - 0.38f) / 0.62f * fire.length);
                        foreach (var bone in skillBones)
                        {
                            skillRotations[bone].Add(bone.localRotation);
                            skillPositions[bone].Add(bone.localPosition);
                            skillScales[bone].Add(bone.localScale);
                        }
                    }
                    var skill = new AnimationClip { name = id + "_Skill_Arms", frameRate = 60 };
                    foreach (var bone in skillBones)
                    {
                        string path = AnimationUtility.CalculateTransformPath(bone, root);
                        WriteRotations(skill, path, skillRotations[bone], duration);
                        WriteVectors(skill, path, "m_LocalPosition", skillPositions[bone], duration);
                        WriteVectors(skill, path, "m_LocalScale", skillScales[bone], duration);
                    }
                    skill.EnsureQuaternionContinuity();
                    SaveAsset(skill, OutputFolder + "/" + skill.name + ".anim");
                }
                finally { Object.DestroyImmediate(cast); Object.DestroyImmediate(fire); }
            }
            finally { Object.DestroyImmediate(clone); }
        }

        private static float LegAngle(Transform root, Transform[] bones, int side)
        {
            string prefix = side == 0 ? "bone_l_" : "bone_r_";
            var thigh = bones.First(t => t.name == prefix + "thigh");
            var calf = bones.First(t => t.name == prefix + "calf");
            var direction = root.InverseTransformDirection(calf.position - thigh.position);
            return Mathf.Atan2(direction.z, -direction.y) * Mathf.Rad2Deg;
        }

        private static bool IsArmPath(string path)
        {
            return path.Contains("bone_l_clavicle") || path.Contains("bone_r_clavicle");
        }

        private static void WriteRotations(AnimationClip clip, string path, List<Quaternion> values, float duration)
        {
            for (int component = 0; component < 4; component++)
            {
                int index = component;
                WriteCurve(clip, path, "m_LocalRotation." + "xyzw"[component], values.Select(q => q[index]).ToArray(), duration);
            }
        }

        private static void WriteVectors(AnimationClip clip, string path, string property, List<Vector3> values, float duration)
        {
            for (int component = 0; component < 3; component++)
            {
                int index = component;
                WriteCurve(clip, path, property + "." + "xyz"[component], values.Select(v => v[index]).ToArray(), duration);
            }
        }

        private static void WriteCurve(AnimationClip clip, string path, string property, float[] values, float duration)
        {
            // Most finger positions and scales are constant; retain the duration without baking redundant keys.
            if (values.All(value => Mathf.Abs(value - values[0]) < 0.000001f))
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property),
                    AnimationCurve.Linear(0, values[0], duration, values[0]));
                return;
            }
            var keys = new Keyframe[values.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(duration * i / (keys.Length - 1), values[i]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++) curve.SmoothTangents(i, 0);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static void SaveAsset(Object generated, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing == null) AssetDatabase.CreateAsset(generated, path);
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(generated);
            }
        }

        private static void SetEnabled(bool enabled)
        {
            RequireEditMode();
            foreach (string id in ids)
            {
                string path = PrefabFolder + "P_Runner_" + id + ".prefab";
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(contents.GetComponent<RaceRunnerView>(), id, enabled);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            foreach (var view in Object.FindObjectsByType<RaceRunnerView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string id = ids.FirstOrDefault(candidate => view.name == "P_Runner_" + candidate);
                if (id == null) continue;
                Undo.RecordObject(view, enabled ? "Use monster motion preview" : "Restore original monster motion");
                Configure(view, id, enabled);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path == "Assets/Scenes/Scene_Oval.unity") EditorSceneManager.SaveScene(scene);
        }

        private static void Configure(RaceRunnerView view, string id, bool enabled)
        {
            var serialized = new SerializedObject(view);
            if (enabled)
            {
                var run = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputFolder + "/" + id + "_Run_ArmSwing.anim");
                var skill = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputFolder + "/" + id + "_Skill_Arms.anim");
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(OutputFolder + "/" + id + "_Skill_Arms.mask");
                if (run == null || skill == null || mask == null) throw new InvalidOperationException("Build motion preview assets first.");
                serialized.FindProperty("PreviewRunClip").objectReferenceValue = run;
                serialized.FindProperty("SkillGestureClip").objectReferenceValue = skill;
                serialized.FindProperty("SkillGestureMask").objectReferenceValue = mask;
                serialized.FindProperty("SkillGestureDuration").floatValue = id == "Minorous" ? 1.8f : 1.65f;
                serialized.FindProperty("SkillGestureWeight").floatValue = 1;
                ConfigureProps(serialized, view, id);
            }
            serialized.FindProperty("UseMonsterMotionPreview").boolValue = enabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureProps(SerializedObject serialized, RaceRunnerView view, string id)
        {
            var entries = serialized.FindProperty("MotionPropAttachments");
            entries.arraySize = id == "Orc" ? 2 : 0;
            if (id != "Orc") return;
            var clone = Object.Instantiate(view.gameObject);
            clone.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var copy = clone.GetComponent<RaceRunnerView>();
                copy.RunClip.SampleAnimation(copy.MountAnimator.gameObject, 0);
                var sourceBones = copy.MountAnimator.GetComponentsInChildren<Transform>(true);
                var targetBones = view.MountAnimator.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < 2; i++)
                {
                    string propName = i == 0 ? "bone_prop1" : "bone_prop2";
                    string handName = i == 0 ? "bone_r_hand" : "bone_l_hand";
                    var prop = sourceBones.First(t => t.name == propName);
                    var hand = sourceBones.First(t => t.name == handName);
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("Prop").objectReferenceValue = targetBones.First(t => t.name == propName);
                    entry.FindPropertyRelative("Hand").objectReferenceValue = targetBones.First(t => t.name == handName);
                    entry.FindPropertyRelative("LocalPosition").vector3Value = hand.InverseTransformPoint(prop.position);
                    entry.FindPropertyRelative("LocalRotation").quaternionValue = Quaternion.Inverse(hand.rotation) * prop.rotation;
                }
            }
            finally { Object.DestroyImmediate(clone); }
        }
    }
}
