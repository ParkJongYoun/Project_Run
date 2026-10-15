using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Rendering.Universal;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class RaceSpriteSceneBuilder
    {
        private const string source = "DataBundles/기획/시안/2D뷰세트_탈것6종_경기장전체_v1";
        private const string art = "Assets/ArtBundles/Race2D";
        private const string resources = "Assets/Resources/Race2D";
        private const string prefabs = "Assets/Resources/Prefabs/Race2D";
        private static readonly string[] mounts = { "tiger", "poring", "fox", "ghost", "wolf", "boar" };
        private static readonly string[] views = { "01_side", "02_rear", "03_top" };
        private static readonly string[] sections = { "01_home_straight", "02_first_corner", "03_back_straight", "04_final_corner" };

        [MenuItem("Tools/Project Run/Race/Build both 2D race scenes")]
        public static void BuildBoth()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene before building.");
            ImportArt();
            BuildScene("Assets/Scenes/Scene_2D_3D.unity", E_RaceArtMode.SpritesInWorld);
            BuildScene("Assets/Scenes/Scene_All2D.unity", E_RaceArtMode.AllSprites);
            AssetDatabase.SaveAssets();
            Debug.Log("2D race scenes built with shared simulation, UI and fox movie.");
        }

        private static void ImportArt()
        {
            Directory.CreateDirectory(art + "/Materials");
            Directory.CreateDirectory(resources);
            Directory.CreateDirectory(prefabs);
            var manifest = JObject.Parse(File.ReadAllText(source + "/asset_manifest.json"));
            foreach (var entry in manifest["assets"])
            {
                string relative = (string)entry["file"];
                string destination = art + "/" + relative;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (!File.Exists(destination)) File.Copy(source + "/" + relative, destination);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 250;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = relative.StartsWith("characters/")
                    ? new Vector2(.5f, ((float)entry["height"] - 1 - (float)entry["bounds"]["bottom"]) / (float)entry["height"])
                    : new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            string materialPath = art + "/Materials/M_RaceSprite.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("ProjectRun/RaceSprite");
                if (shader == null) throw new InvalidOperationException("Race sprite shader is not compiled.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            for (int i = 0; i < mounts.Length; i++)
            {
                string path = resources + "/" + mounts[i] + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<Data_RaceSpriteSet>(path);
                if (profile == null) { profile = ScriptableObject.CreateInstance<Data_RaceSpriteSet>(); AssetDatabase.CreateAsset(profile, path); }
                profile.StillViews = views.Select(v => AssetDatabase.LoadAssetAtPath<Sprite>(art + "/characters/" + mounts[i] + "/" + v + ".png")).ToArray();
                profile.WorldHeight = new[] { 4.8f, 4.3f, 4.5f, 4.6f, 4.1f, 4.4f }[i];
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
        }

        public static void BuildScene(string path, E_RaceArtMode mode)
        {
            if (path != "Assets/Scenes/Scene_2D_3D.unity" && path != "Assets/Scenes/Scene_All2D.unity")
                throw new ArgumentException("Only the two requested 2D scenes can be built.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var race = Object.FindFirstObjectByType<Sys_Race>();
            var presentation = Object.FindFirstObjectByType<Sys_RacePresentation>();
            var cameraDirector = Object.FindFirstObjectByType<Sys_RaceCamera>();
            var hud = Object.FindFirstObjectByType<UIPanel_RaceHUD>(FindObjectsInactive.Include);
            if (race == null || presentation == null || cameraDirector == null || hud == null)
                throw new InvalidOperationException("The copied scene is missing shared race/UI systems.");
            var systems = race.transform.parent;
            var spriteSystem = Get<Sys_RaceSpritePresentation>(Child(systems, "Sys_RaceSpritePresentation"));
            spriteSystem.Mode = mode; spriteSystem.Race = race; spriteSystem.Presentation = presentation;
            spriteSystem.Camera = cameraDirector.RaceCamera; cameraDirector.SpriteDirector = spriteSystem;
            var material = AssetDatabase.LoadAssetAtPath<Material>(art + "/Materials/M_RaceSprite.mat");
            var grid = presentation.Views[0].transform.parent;
            for (int i = 0; i < mounts.Length; i++)
            {
                var old = presentation.Views[i];
                var profile = AssetDatabase.LoadAssetAtPath<Data_RaceSpriteSet>(resources + "/" + mounts[i] + ".asset");
                string prefabPath = prefabs + "/P_Runner2D_" + (i + 1).ToString("00") + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    var root = new GameObject("P_Runner2D_" + (i + 1).ToString("00"));
                    var view = root.AddComponent<RaceRunnerView>();
                    view.RunnerId = i;
                    view.CameraTarget = Child(root.transform, "anchor_camera").transform;
                    view.FaceTarget = Child(root.transform, "anchor_face").transform;
                    view.RiderHead = Child(root.transform, "anchor_head").transform;
                    view.MonsterFaceTarget = Child(root.transform, "anchor_mount_face").transform;
                    view.CameraFocusHeight = profile.WorldHeight * .5f;
                    view.HasFootfalls = old.HasFootfalls;
                    var sprite = root.AddComponent<RaceRunnerSpriteView>();
                    sprite.Runner = view; sprite.Art = profile;
                    sprite.Image = Child(root.transform, "obj_sprite").AddComponent<SpriteRenderer>();
                    sprite.Image.sprite = profile.StillViews[0]; sprite.Image.sharedMaterial = material;
                    sprite.Image.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    sprite.Image.receiveShadows = false;
                    view.SpriteVisual = sprite; view.MountRoot = sprite.Image.transform; view.RiderRoot = view.RiderHead;
                    if (old.Shadow != null)
                    {
                        view.Shadow = Object.Instantiate(old.Shadow.gameObject, root.transform).transform;
                        view.Shadow.name = "obj_contact_shadow";
                    }
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Object.DestroyImmediate(root);
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grid);
                instance.transform.position = Data_RaceSimulation.CoursePoint(0, race.Entrants[i].Lane);
                var newView = instance.GetComponent<RaceRunnerView>();
                newView.RunnerId = i;
                newView.SpriteVisual.Race = race; newView.SpriteVisual.Director = spriteSystem;
                // All2D camera uses a dedicated layer so environment meshes cannot leak into its frame.
                newView.SpriteVisual.Image.gameObject.layer = mode == E_RaceArtMode.AllSprites ? 30 : 0;
                newView.SpriteVisual.Image.sprite = profile.StillViews[2];
                newView.RiderHead.position = instance.transform.position + Vector3.up * profile.WorldHeight;
                newView.CameraTarget.position = instance.transform.position + Vector3.up * profile.WorldHeight * .5f;
                presentation.Views[i] = newView;
                race.Entrants[i].Prefab = prefab;
                Object.DestroyImmediate(old.gameObject);
            }
            var camera = spriteSystem.Camera;
            var urp = camera.GetComponent<UniversalAdditionalCameraData>();
            if (urp != null) urp.renderPostProcessing = false;
            if (mode == E_RaceArtMode.AllSprites)
            {
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f,.38f,.15f);
                foreach (var name in new[] { "P_Racecourse_All3D", "P_RaceBroadcastDress", "P_RaceLighting" })
                {
                    var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
                    if (root != null) root.SetActive(false);
                }
                var stage = Child(systems, "obj_2d_stage");
                var current = Get<SpriteRenderer>(Child(stage.transform, "bg_course"));
                var previous = Get<SpriteRenderer>(Child(stage.transform, "bg_previous_course"));
                foreach (var renderer in new[] { current, previous })
                {
                    renderer.gameObject.layer = 30; renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                current.sortingOrder = -100; previous.sortingOrder = -99;
                spriteSystem.Background = current; spriteSystem.PreviousBackground = previous;
                spriteSystem.Backgrounds = sections.SelectMany(s => views.Select(v => AssetDatabase.LoadAssetAtPath<Sprite>(art + "/backgrounds/" + s + "/" + v + ".png"))).ToArray();
                current.sprite = spriteSystem.Backgrounds[2]; previous.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 10;
                camera.transform.SetPositionAndRotation(new Vector3(0,1000,-20), Quaternion.identity);
                current.transform.position = new Vector3(0,1000,10);
                current.transform.localScale = Vector3.one * 20 / current.sprite.bounds.size.y;
            }
            else
            {
                camera.cullingMask = -1; camera.orthographic = false;
                camera.transform.position = new Vector3(74, 47, -43);
                camera.transform.LookAt(Data_RaceSimulation.CoursePoint(0) + Vector3.up * 2);
            }
            AttachVideo(race, presentation, cameraDirector, hud);
            // Scene instances all keep their links to the single existing HUD prefab.
            EditorUtility.SetDirty(race); EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(cameraDirector);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private static void AttachVideo(Sys_Race race, Sys_RacePresentation presentation, Sys_RaceCamera director, UIPanel_RaceHUD hud)
        {
            var canvasRoot = GameObject.Find("UICanvas_Root");
            var canvasObject = Child(canvasRoot.transform, "Canvas_Cutscene");
            var canvas = Get<Canvas>(canvasObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = (int)E_CanvasLayer.Cutscene;
            var scaler = Get<UnityEngine.UI.CanvasScaler>(canvasObject);
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720,1280); scaler.matchWidthOrHeight = 0;
            Get<UnityEngine.UI.GraphicRaycaster>(canvasObject);
            var panel = canvasObject.GetComponentInChildren<UIPanel_RaceSkillVideo>(true);
            if (panel == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UI/Panel/UIPanel_RaceSkillVideo.prefab");
                if (prefab == null) throw new InvalidOperationException("The shared fox video panel is missing.");
                panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasObject.transform)).GetComponent<UIPanel_RaceSkillVideo>();
            }
            panel.SetOpacity(0);
            var system = Get<Sys_RaceSkillVideo>(Child(race.transform.parent, "Sys_RaceSkillVideo"));
            system.Race = race; system.Presentation = presentation; system.Director = director; system.HUD = hud; system.Panel = panel;
            system.Player = Get<VideoPlayer>(system.gameObject); system.Player.playOnAwake = false;
            system.Player.audioOutputMode = VideoAudioOutputMode.None;
            system.RunnerId = 2; system.VideoPath = "Video/FoxUltimate1024.mp4";
        }

        private static GameObject Child(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var child = new GameObject(name); child.transform.SetParent(parent, false); return child;
        }
        private static T Get<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null) component = target.AddComponent<T>();
            return component;
        }
    }
}

