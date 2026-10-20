using System;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.EditorTools
{
    /// <summary>Adds the Mixamo model as a separate exhibit; never changes the race lineup.</summary>
    public static class NewMinotaurusComparisonBuilder
    {
        public const string Art = "Assets/ArtBundles/Characters/NewMinotaurus";
        public const string Prefab = "Assets/Resources/Prefabs/Showcase/V_NewMinotaurus.prefab";
        private static readonly string[] motions = { "Idle", "Walk", "Run", "Clap" };

        [MenuItem("Tools/Project Run/Cartoon monsters/Add new minotaurus comparison")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = SceneManager.GetSceneByPath(CartoonShowcaseBuilder.ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(CartoonShowcaseBuilder.ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Cartoon scene has unsaved changes.");
            BuildAssets();
            var show = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Sys_CartoonShowcase>(true)).Single();
            ApplyToScene(show);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void BuildAssets()
        {
            foreach (string folder in new[] { "Animations", "Materials", "Meshes", "Rig", "Textures" }) Directory.CreateDirectory(Art + "/" + folder);
            Directory.CreateDirectory(Path.GetDirectoryName(Prefab));
            AssetDatabase.Refresh();
            Avatar avatar = null;
            foreach (string motion in motions)
            {
                string path = Art + "/Source/Minotaurus_" + motion + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing FBX: " + path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = motion == "Idle" ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = motion == "Idle" ? null : avatar;
                importer.importAnimation = true;
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = "Minotaurus_" + motion;
                    clip.loopTime = motion != "Clap";
                    clip.loopPose = motion != "Clap";
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                if (motion == "Idle")
                {
                    avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                    if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("New minotaurus Humanoid Avatar is invalid.");
                }
                var source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                Copy(source, Art + "/Animations/Minotaurus_" + motion + ".anim");
            }
            string baseColor = Directory.GetFiles(Art + "/Textures", "*BaseColor.png").Single();
            string normalPath = Directory.GetFiles(Art + "/Textures", "*Normal_Bake.png").Single();
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
            string materialPath = Art + "/Materials/Minotaurus_Surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "Minotaurus_Surface";
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColor));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", .25f);
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            string controllerPath = Art + "/Animations/Minotaurus.controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                foreach (string motion in motions)
                {
                    var state = machine.AddState(motion);
                    state.motion = Clip(motion);
                    if (motion == "Idle") machine.defaultState = state;
                }
            }

            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("V_NewMinotaurus");
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var visual = new GameObject("obj_visual").transform;
                visual.SetParent(root.transform, false);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Source/Minotaurus_Idle.fbx");
                var model = Object.Instantiate(source, visual);
                model.name = "obj_model";
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.avatar = Copy(avatar, Art + "/Rig/Minotaurus_Avatar.asset");
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.fireEvents = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.sharedMesh = Copy(skin.sharedMesh, Art + "/Meshes/Minotaurus.asset");
                    skin.sharedMaterials = Enumerable.Repeat(material, skin.sharedMaterials.Length).ToArray();
                    skin.updateWhenOffscreen = true;
                }
                Clip("Idle").SampleAnimation(model, .15f);
                var bounds = BoundsOf(root);
                visual.localScale = Vector3.one * (3.35f / bounds.size.y);
                bounds = BoundsOf(root);
                visual.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                if (PrefabUtility.SaveAsPrefabAsset(root, Prefab) == null) throw new InvalidOperationException("Could not save new comparison prefab.");
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static T Copy<T>(T source, string path) where T : Object
        {
            var target = AssetDatabase.LoadAssetAtPath<T>(path);
            if (target != null) { EditorUtility.CopySerialized(source, target); EditorUtility.SetDirty(target); return target; }
            target = Object.Instantiate(source); target.name = Path.GetFileNameWithoutExtension(path); target.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(target, path); return target;
        }

        private static AnimationClip Clip(string motion) { return AssetDatabase.LoadAssetAtPath<AnimationClip>(Art + "/Animations/Minotaurus_" + motion + ".anim"); }

        private static Bounds BoundsOf(GameObject root)
        {
            var mesh = new Mesh(); bool first = true; var bounds = new Bounds();
            try
            {
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    skin.BakeMesh(mesh, true);
                    foreach (var v in mesh.vertices)
                    {
                        var point = skin.transform.TransformPoint(v);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); }
            if (first) throw new InvalidOperationException("No new minotaurus mesh.");
            return bounds;
        }

        public static void ApplyToScene(Sys_CartoonShowcase show)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) == null) return;
            var scene = show.gameObject.scene;
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var entries = show.Exhibits.ToList();
            var stage = all.Single(t => t.name == "obj_stage");
            foreach (string id in new[] { "BlueDragon", "ButlerWhisper", "FlameBird" })
            {
                entries.RemoveAll(e => e.Root.name == "V_Cartoon_" + id);
                foreach (string name in new[] { "V_Cartoon_" + id, "obj_plinth_" + id, "txt_name_" + id })
                {
                    var item = all.FirstOrDefault(t => t != null && t.name == name);
                    if (item != null) Object.DestroyImmediate(item.gameObject);
                }
            }
            var old = entries.Single(e => e.Root.name == "V_Cartoon_Minorous");
            Vector3 position = old.Root.position + new Vector3(5.1f, 0, 0);
            var poring = entries.Single(e => e.Root.name == "V_Cartoon_Poring");
            if (Vector3.Distance(poring.Root.position, position) < 1)
            {
                Vector3 delta = new Vector3(0, 0, 8);
                poring.Root.position += delta; poring.Focus += delta;
                foreach (string name in new[] { "obj_plinth_Poring", "txt_name_Poring" }) all.Single(t => t != null && t.name == name).position += delta;
            }
            var added = entries.FirstOrDefault(e => e.Root.name == "V_Cartoon_NewMinotaurus");
            if (added == null)
            {
                var wrapper = new GameObject("V_Cartoon_NewMinotaurus").transform;
                wrapper.SetParent(old.Root.parent, false); wrapper.position = position; wrapper.rotation = old.Root.rotation;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), wrapper);
                var animator = model.GetComponentInChildren<Animator>(true);
                Clip("Idle").SampleAnimation(animator.gameObject, .15f);
                // Match the visible height of the existing exhibit for direct comparison.
                float height = BoundsOf(old.Animator.gameObject).size.y;
                var bounds = BoundsOf(model);
                model.transform.localScale *= height / bounds.size.y;
                bounds = BoundsOf(model);
                model.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
                bounds = BoundsOf(model);
                added = new Data_CartoonExhibit { Name = "미노타우루스 · 신규 Mixamo", Root = wrapper, CartoonRoot = model.transform,
                    Animator = animator, Idle = Clip("Idle"), Run = Clip("Run"), Walk = Clip("Walk"), Clap = Clip("Clap"),
                    KeepAppearance = true, Focus = bounds.center, Radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) };
                entries.Insert(entries.IndexOf(old) + 1, added);
                var pedestal = Object.Instantiate(all.Single(t => t != null && t.name == "obj_plinth_Minorous").gameObject, stage);
                pedestal.name = "obj_plinth_NewMinotaurus"; pedestal.transform.position += position - old.Root.position;
                var label = Object.Instantiate(all.Single(t => t != null && t.name == "txt_name_Minorous").gameObject, stage);
                label.name = "txt_name_NewMinotaurus"; label.transform.position += position - old.Root.position;
                label.GetComponent<TextMesh>().text = "미노 · 신규 Mixamo";
            }
            show.Exhibits = entries.ToArray();
            show.NameLabels = stage.GetComponentsInChildren<TextMesh>(true).Select(t => t.gameObject).ToArray();
            AddButton(show, "btn_compare_mino", "미노 비교", show.CompareMinotaurus);
            AddButton(show, "btn_walk", "걷기 (신규)", show.PlayWalk);
            AddButton(show, "btn_clap", "박수 (신규)", show.PlayClap);
            string[] buttons = { "btn_all", "btn_compare_mino", "btn_previous", "btn_next", "btn_rotate", "btn_idle", "btn_walk", "btn_run", "btn_clap", "btn_title" };
            for (int i = 0; i < buttons.Length; i++)
            {
                var rect = (RectTransform)show.SafeArea.Find(buttons[i]);
                rect.anchoredPosition = new Vector2((i - 4.5f) * 122, -320); rect.sizeDelta = new Vector2(116, 42);
                var text = rect.GetComponentInChildren<UnityEngine.UI.Text>(); text.fontSize = 17;
                ((RectTransform)text.transform).sizeDelta = new Vector2(112, 40);
            }
            var note = show.SafeArea.Find("txt_note").GetComponent<UnityEngine.UI.Text>();
            note.text = "기존 미노 옆 신규 Mixamo 비교 · 걷기·박수는 신규 미노만 재생";
            show.Status.text = "전체 " + entries.Count + "종 · 카툰 · 대기";
            NewMinotaurusCartoonBuilder.Configure(show);
            EditorUtility.SetDirty(show);
        }

        private static void AddButton(Sys_CartoonShowcase show, string name, string label, UnityEngine.Events.UnityAction action)
        {
            if (show.SafeArea.Find(name) != null) return;
            var copy = Object.Instantiate(show.SafeArea.Find("btn_idle").gameObject, show.SafeArea);
            copy.name = name;
            var button = copy.GetComponent<UnityEngine.UI.Button>();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            copy.GetComponentInChildren<UnityEngine.UI.Text>().text = label;
        }
    }
}
