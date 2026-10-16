using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        private const string art = "Assets/ArtBundles/RaceShowcase";
        private const string resources = "Assets/Resources";
        private const string output = "Assets/Resources/Prefabs/Race";
        private const float modelScale = 1.6f;
        private static readonly string[] mounts = { "vehicle_edgar_001", "giantporing_001", "fox_001", "butlerwhisper_001", "vehicle_wolf_001", "vehicle_savage_001" };
        private static readonly string[] runnerNames = { "호랑이 에드가", "자이언트 포링", "구미호", "유령 왕좌", "검은늑대", "철갑멧돼지" };
        private static readonly string[] mountNames = { "호랑이 에드가", "자이언트 포링", "구미호", "유령 왕좌", "검은늑대", "철갑멧돼지" };
        private static readonly string[] skills = { "맹호 돌진", "탄력 대시", "여우의 잔상", "유령 행진", "질풍 추격", "대지의 포효" };
        private static readonly Color[] colors = { Hex("49A9DA"), Hex("ED83A1"), Hex("B38AD6"), Hex("E7B84E"), Hex("67B77C"), Hex("E98C4E") };
        // Measured animated mesh extents at scale 1.6, with clearance inside the 17 m turf course.
        private static readonly float[] lanes = { 1.66f, 6.75f, -7.19f, -2.29f, -5.06f, 4.35f };
        private static Font font;

        [MenuItem("Tools/Project Run/Race/Build six-monster showcase")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Scene_All3D.unity" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open Scene_All3D in Edit mode.");
            Ensure(art + "/Animations"); Ensure(art + "/Materials"); Ensure(art + "/UI");
            Ensure(output); Ensure(resources + "/Prefabs/UI/Panel"); Ensure(resources + "/Audio/Race");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            PrepareVisualAssets();
            var setups = new Data_RunnerSetup[mounts.Length];
            for (int i = 0; i < setups.Length; i++)
            {
                var prefab = BuildRunner(i);
                setups[i] = new Data_RunnerSetup {
                    Name = runnerNames[i], Mount = mountNames[i], SkillName = skills[i], Color = colors[i], Prefab = prefab,
                    BaseSpeed = new[] { 13.02f, 13.46f, 12.45f, 12.73f, 12.61f, 13.23f }[i],
                    EarlyPower = new[] { 0.0f, 0.5f, -0.25f, 1.05f, 0.65f, -0.1f }[i],
                    LatePower = new[] { 0.55f, 0.7f, 0.8f, 1.05f, 0.7f, 1.1f }[i],
                    SurgeTime = new[] { 17f, 29f, 40f, 49f, 9f, 23f }[i], SurgePower = new[] { 3.0f, 3.1f, 3.1f, 3.5f, 2.9f, 3.2f }[i],
                    SurgeDuration = 5.5f, Lane = lanes[i],
                    Portrait = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/UI/Portrait_" + i + ".png"),
                    FacePortrait = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/UI/FacePortrait_" + i + ".png")
                };
            }
            var systems = GetRoot("[Systems]");
            var raceObject = GetChild(systems.transform, "Sys_Race");
            var race = Get<Sys_Race>(raceObject); race.Entrants = setups;
            var presentation = Get<Sys_RacePresentation>(GetChild(systems.transform, "Sys_RacePresentation"));
            presentation.Race = race;
            var grid = GetChild(systems.transform, "obj_race_grid");
            // Only replace this builder's generated runner instances.
            for (int i = grid.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(grid.transform.GetChild(i).gameObject);
            presentation.Views = new RaceRunnerView[setups.Length];
            for (int i = 0; i < setups.Length; i++)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(setups[i].Prefab, grid.transform);
                g.transform.position = Data_RaceSimulation.CoursePoint(0, setups[i].Lane);
                g.transform.rotation = Quaternion.LookRotation(Data_RaceSimulation.CourseForward(0));
                presentation.Views[i] = g.GetComponent<RaceRunnerView>();
                presentation.Views[i].RunnerId = i;
            }
            var camera = Camera.main;
            if (camera == null) camera = Object.FindFirstObjectByType<Camera>();
            camera.tag = "MainCamera"; camera.nearClipPlane = 0.1f; camera.farClipPlane = 1500;
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            var director = Get<Sys_RaceCamera>(GetChild(systems.transform, "Sys_RaceCamera"));
            director.Race = race; director.Presentation = presentation; director.RaceCamera = camera;
            var managers = GetRoot("[Managers]");
            Get<Mgr_UI>(GetChild(managers.transform, "Mgr_UI"));
            var sound = Get<Mgr_Sound>(GetChild(managers.transform, "Mgr_Sound"));
            sound.Hoof = AssetDatabase.LoadAssetAtPath<AudioClip>(resources + "/Audio/Race/hoof.wav");
            sound.Burst = AssetDatabase.LoadAssetAtPath<AudioClip>(resources + "/Audio/Race/burst.wav");
            sound.Chime = AssetDatabase.LoadAssetAtPath<AudioClip>(resources + "/Audio/Race/chime.wav");
            sound.Wind = AssetDatabase.LoadAssetAtPath<AudioClip>(resources + "/Audio/Race/wind.wav");
            BuildMapDress();
            BuildHUD(race, presentation, director);
            ConfigureRaceLighting();
            RaceMonsterLookBuilder.ConfigureScene();
            camera.transform.position = new Vector3(62, 9, -49);
            camera.transform.LookAt(new Vector3(49, 2.3f, -68.5f)); camera.fieldOfView = 48;
            PlayerSettings.runInBackground = true;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Six-monster race showcase saved to Scene_All3D.");
        }

        private static GameObject BuildRunner(int index)
        {
            string source = "Assets/ArtBundles/LegacyURP/Mounts/Prefabs/M_" + mounts[index] + "_pawn.prefab";
            var root = new GameObject("P_Runner_" + (index + 1).ToString("00"));
            var view = root.AddComponent<RaceRunnerView>(); view.RunnerId = index;
            var model = new GameObject("M_Runner_" + (index + 1).ToString("00")).transform; model.SetParent(root.transform, false);
            var mount = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OldAsset/Mounts/prefabs/" + mounts[index] + "_pawn.prefab"), model);
            var convertedMount = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            foreach (var skin in mount.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var convertedSkin = convertedMount.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.sharedMesh == skin.sharedMesh);
                if (convertedSkin == null) throw new InvalidOperationException("Missing converted mount material " + skin.name);
                skin.sharedMaterials = convertedSkin.sharedMaterials;
            }
            mount.name = "obj_mount"; mount.transform.localPosition = Vector3.zero; mount.transform.localRotation = Quaternion.identity;
            mount.transform.localScale = Vector3.one * modelScale;
            view.MountRoot = mount.transform; view.MountAnimator = mount.GetComponent<Animator>();
            var originalClips = view.MountAnimator.runtimeAnimatorController.animationClips;
            view.RunClip = CopyClip(originalClips.First(c => c.name.Contains("run")), mounts[index] + "_Run", true);
            view.IdleClip = CopyClip(originalClips.First(c => c.name.Contains("stand")), mounts[index] + "_Idle", true);
            view.MountAnimator.runtimeAnimatorController = null;
            view.MountAnimator.applyRootMotion = false;
            view.CameraTarget = Anchor(root.transform, "anchor_camera", new Vector3(0, 2.25f, 0));
            view.FaceTarget = Anchor(root.transform, "anchor_face", new Vector3(0, 3.8f, 0));
            view.MonsterFaceTarget = mount.GetComponentsInChildren<Transform>().First(t => t.name == "socket_head_mid");
            ConfigureRunnerMotion(view, index);
            AddEffects(view, colors[index]);
            view.IdleClip.SampleAnimation(mount, 0.2f);
            mount.transform.localPosition = Vector3.zero; mount.transform.localRotation = Quaternion.identity;
            view.CameraTarget.localPosition = Vector3.up * view.CameraFocusHeight;
            view.FaceTarget.position = view.MonsterFaceTarget.position;
            foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                s.updateWhenOffscreen = true; s.shadowCastingMode = ShadowCastingMode.On;
            }
            ApplyRunnerMaterials(root);
            CapturePortrait(root, index);
            CapturePortrait(root, index, true);
            RaceMonsterLookBuilder.ConfigureRunner(view);
            string path = output + "/P_Runner_" + (index + 1).ToString("00") + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static AnimationClip CopyClip(AnimationClip source, string name, bool loop)
        {
            string path = art + "/Animations/" + name + ".anim";
            var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (copy == null) { copy = Object.Instantiate(source); copy.name = name; AssetDatabase.CreateAsset(copy, path); }
            AnimationUtility.SetAnimationEvents(copy, new AnimationEvent[0]);
            var settings = AnimationUtility.GetAnimationClipSettings(copy); settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            return copy;
        }
        private static Color Hex(string value) { Color result; ColorUtility.TryParseHtmlString("#" + value, out result); return result; }
        private static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Ensure(Path.GetDirectoryName(path).Replace('\\', '/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        private static GameObject GetRoot(string name) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name) ?? new GameObject(name);
        private static GameObject GetChild(Transform parent, string name)
        {
            var child = parent.Find(name); if (child != null) return child.gameObject;
            var g = new GameObject(name); g.transform.SetParent(parent, false); return g;
        }
        private static T Get<T>(GameObject g) where T : Component
        {
            var component = g.GetComponent<T>();
            return component != null ? component : g.AddComponent<T>();
        }
        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = position; return g;
        }
    }
}
