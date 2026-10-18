using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectRun.Race.Editor
{
    public static class LobbyAnimationBuilder
    {
        private const string ArtPath = "Assets/ArtBundles/Lobby/WildRose";
        private const string ClipPath = "Assets/Resources/UI/Lobby/WildRose_Idle.asset";
        private const string PanelPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_Lobby.prefab";
        private const string ScenePath = "Assets/Scenes/Scene_Lobby.unity";
        private const string JobPath = "DataBundles/LobbyAnimation/WildRose_v02";

        [Serializable] private class Manifest
        {
            public string SourceVideoSha256;
            public float[] IllustrationRect;
            public int[] CellSize;
            public Page[] Pages;
            public Frame[] Frames;
        }
        [Serializable] private class Page { public string File; public int Width; public int Height; }
        [Serializable] private class Frame { public int Index; public int Page; public int X; public int Y; public float Duration; }

        [MenuItem("Tools/Project Run/Lobby/Install Wild Rose")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before installation.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Scene_Lobby before installation.");
            var project = Path.GetDirectoryName(Application.dataPath);
            var job = Path.Combine(project, JobPath);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(job, "final/clip.json")));
            if (manifest.Frames == null || manifest.Frames.Length < 2) throw new InvalidDataException("No reviewed animation frames.");
            Directory.CreateDirectory(Path.Combine(project, ArtPath));
            Directory.CreateDirectory(Path.Combine(project, "Assets/Resources/UI/Lobby"));
            var sprites = new Sprite[manifest.Frames.Length];
            for (int page = 0; page < manifest.Pages.Length; page++)
            {
                var record = manifest.Pages[page];
                var path = ArtPath + "/" + record.File;
                File.Copy(Path.Combine(job, "final", record.File), Path.Combine(project, path), true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                ConfigureTexture(importer);
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                // Core TextureImporter API keeps this importer usable without adding a Sprite Editor package.
#pragma warning disable CS0618
                importer.spritesheet = manifest.Frames.Where(frame => frame.Page == page).Select(frame => new SpriteMetaData
                {
                    name = "wildrose_" + frame.Index.ToString("D3"),
                    rect = new Rect(frame.X, record.Height - frame.Y - manifest.CellSize[1], manifest.CellSize[0], manifest.CellSize[1]),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                }).ToArray();
#pragma warning restore CS0618
                importer.SaveAndReimport();
                var imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
                foreach (var frame in manifest.Frames.Where(frame => frame.Page == page))
                    sprites[frame.Index] = imported["wildrose_" + frame.Index.ToString("D3")];
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture.width != record.Width || texture.height != record.Height)
                    throw new InvalidDataException("Atlas was resized during import: " + path);
            }
            var clip = AssetDatabase.LoadAssetAtPath<Data_LobbyAnimation>(ClipPath);
            if (clip == null) { clip = ScriptableObject.CreateInstance<Data_LobbyAnimation>(); AssetDatabase.CreateAsset(clip, ClipPath); }
            clip.Frames = sprites;
            clip.Durations = manifest.Frames.Select(frame => frame.Duration).ToArray();
            clip.IllustrationRect = new Rect(manifest.IllustrationRect[0], manifest.IllustrationRect[1], manifest.IllustrationRect[2], manifest.IllustrationRect[3]);
            clip.SourceVideoSha256 = manifest.SourceVideoSha256;
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);

            var background = ImportSingle(job, "background.png", project);
            var shadow = ImportSingle(job, "contact_shadow.png", project);
            var environmentMask = ImportSingle(job, "environment_mask.png", project);
            var maskImporter = (TextureImporter)AssetImporter.GetAtPath(ArtPath + "/environment_mask.png");
            maskImporter.sRGBTexture = false; maskImporter.SaveAndReimport();
            environmentMask = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "/environment_mask.png");
            var environmentMaterialPath = ArtPath + "/Mat_LobbyEnvironment.mat";
            var environmentMaterial = AssetDatabase.LoadAssetAtPath<Material>(environmentMaterialPath);
            if (environmentMaterial == null)
            {
                var shader = Shader.Find("ProjectRun/UI/LobbyEnvironment");
                if (shader == null) throw new InvalidOperationException("Lobby environment shader not imported.");
                environmentMaterial = new Material(shader);
                AssetDatabase.CreateAsset(environmentMaterial, environmentMaterialPath);
            }
            environmentMaterial.SetTexture("_MotionMask", environmentMask.texture);
            environmentMaterial.SetVector("_MovementPixels", new Vector4(20, 8, 14, 0));
            EditorUtility.SetDirty(environmentMaterial); AssetDatabase.SaveAssetIfDirty(environmentMaterial);
            var root = GameObject.Find("UICanvas_Root") ?? new GameObject("UICanvas_Root", typeof(RectTransform));
            var canvasTransform = root.transform.Find("Canvas_Panel");
            if (canvasTransform == null)
            {
                var canvasRect = Rect(root.transform, "Canvas_Panel", Vector2.zero, Vector2.one);
                var canvas = canvasRect.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = (int)E_CanvasLayer.Panel;
                var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
                canvasTransform = canvasRect;
            }
            var previous = canvasTransform.Find("UIPanel_Lobby");
            if (previous != null)
            {
                if (previous.GetComponent<UIPanel_Lobby>() == null) throw new InvalidOperationException("A different object already uses UIPanel_Lobby.");
                var art = previous.Find("group_illustration");
                var room = art.Find("bg_room").GetComponent<Image>();
                room.sprite = background;
                room.material = environmentMaterial;
                var oldCurtain = art.Find("img_curtain");
                if (oldCurtain != null) oldCurtain.gameObject.SetActive(false);
                var player = art.Find("img_characters").GetComponent<UIItem_LobbyAnimation>();
                player.Bind(clip);
                var playerRect = (RectTransform)player.transform;
                var updateBounds = clip.IllustrationRect;
                playerRect.anchorMin = new Vector2(updateBounds.xMin / clip.IllustrationSize.x, 1 - updateBounds.yMax / clip.IllustrationSize.y);
                playerRect.anchorMax = new Vector2(updateBounds.xMax / clip.IllustrationSize.x, 1 - updateBounds.yMin / clip.IllustrationSize.y);
                playerRect.offsetMin = playerRect.offsetMax = Vector2.zero;
                art.Find("img_contact_shadow").GetComponent<Image>().sprite = shadow;
                var environment = room.GetComponent<UIItem_LobbyEnvironment>();
                if (environment == null) environment = room.gameObject.AddComponent<UIItem_LobbyEnvironment>();
                environment.Configure(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(playerRect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(room);
                PrefabUtility.SaveAsPrefabAssetAndConnect(previous.gameObject, PanelPath, InteractionMode.AutomatedAction);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Lobby updated in place: re-keyed frames and visible environment motion.");
                return;
            }
            var panelRect = Rect(canvasTransform, "UIPanel_Lobby", Vector2.zero, Vector2.one);
            panelRect.gameObject.SetActive(false);
            var panel = panelRect.gameObject.AddComponent<UIPanel_Lobby>();
            var surround = Picture(panelRect, "bg_surround", null, new Color(.16f, .115f, .10f, 1));
            Stretch(surround.rectTransform);
            var illustration = Rect(panelRect, "group_illustration", Vector2.zero, Vector2.one);
            var fit = illustration.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = clip.IllustrationSize.x / clip.IllustrationSize.y;
            illustration.gameObject.AddComponent<RectMask2D>();
            var backgroundImage = Picture(illustration, "bg_room", background, Color.white);
            Stretch(backgroundImage.rectTransform);
            backgroundImage.material = environmentMaterial;
            var environmentMotion = backgroundImage.gameObject.AddComponent<UIItem_LobbyEnvironment>();
            Stretch(Picture(illustration, "img_contact_shadow", shadow, Color.white).rectTransform);
            var character = Picture(illustration, "img_characters", clip.Frames[0], Color.white);
            var bounds = clip.IllustrationRect;
            character.rectTransform.anchorMin = new Vector2(bounds.xMin / clip.IllustrationSize.x, 1 - bounds.yMax / clip.IllustrationSize.y);
            character.rectTransform.anchorMax = new Vector2(bounds.xMax / clip.IllustrationSize.x, 1 - bounds.yMin / clip.IllustrationSize.y);
            character.rectTransform.offsetMin = character.rectTransform.offsetMax = Vector2.zero;
            var animation = character.gameObject.AddComponent<UIItem_LobbyAnimation>();
            animation.Bind(clip);
            environmentMotion.Configure(animation);

            var safe = Rect(panelRect, "obj_safe_area", Vector2.zero, Vector2.one);
            var info = Rect(safe, "group_caption", Vector2.zero, Vector2.zero);
            info.pivot = Vector2.zero; info.sizeDelta = new Vector2(238, 156); info.anchoredPosition = new Vector2(28, 28);
            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ArtBundles/UI/SGLReference/Icon/UI_Button_Common.png");
            var frameImage = Picture(info, "bg_frame", frameSprite, new Color(.88f, .72f, .43f, .9f));
            Stretch(frameImage.rectTransform); frameImage.type = Image.Type.Sliced;
            var surface = Picture(info, "img_surface", null, new Color(.10f, .17f, .15f, .92f));
            Stretch(surface.rectTransform); surface.rectTransform.offsetMin = new Vector2(8, 8); surface.rectTransform.offsetMax = new Vector2(-8, -8);
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            Label(info, "txt_title", "와일드 로즈", font, 28, new Vector2(0, 41), new Vector2(214, 40));
            Label(info, "txt_caption", "나른한 오후", font, 17, new Vector2(0, 9), new Vector2(214, 30)).color = new Color(.89f, .84f, .72f);
            var buttonRect = Rect(info, "btn_pause", new Vector2(.5f, .5f), new Vector2(.5f, .5f));
            buttonRect.sizeDelta = new Vector2(198, 38); buttonRect.anchoredPosition = new Vector2(0, -43);
            var button = buttonRect.gameObject.AddComponent<Button>();
            var buttonFrame = Picture(buttonRect, "bg_frame", frameSprite, new Color(.88f, .72f, .43f));
            Stretch(buttonFrame.rectTransform); buttonFrame.type = Image.Type.Sliced;
            var buttonSurface = Picture(buttonRect, "img_surface", null, new Color(.16f, .26f, .21f));
            Stretch(buttonSurface.rectTransform); buttonSurface.rectTransform.offsetMin = new Vector2(6, 4); buttonSurface.rectTransform.offsetMax = new Vector2(-6, -4);
            buttonSurface.raycastTarget = true; button.targetGraphic = buttonSurface;
            var pauseLabel = Label(buttonRect, "txt_label", "움직임 멈추기", font, 17, Vector2.zero, new Vector2(190, 34));
            panel.Configure(safe, animation, button, pauseLabel);
            panelRect.gameObject.SetActive(true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(panelRect.gameObject, PanelPath, InteractionMode.AutomatedAction);
            if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            if (Object.FindFirstObjectByType<Mgr_UI>() == null)
            {
                var managers = GameObject.Find("[Managers]") ?? new GameObject("[Managers]");
                var manager = new GameObject("Mgr_UI"); manager.transform.SetParent(managers.transform, false); manager.AddComponent<Mgr_UI>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = panelRect.gameObject;
            Debug.Log("Lobby installed: " + sprites.Length + " frames, " + clip.Duration + " seconds, " + manifest.Pages.Length + " atlas pages.");
        }

        private static void ConfigureTexture(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.isReadable = false;
            importer.ClearPlatformTextureSettings("WebGL");
            importer.ClearPlatformTextureSettings("Standalone");
        }

        private static Sprite ImportSingle(string job, string file, string project)
        {
            var path = ArtPath + "/" + file;
            File.Copy(Path.Combine(job, "final", file), Path.Combine(project, path), true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            ConfigureTexture(importer); importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static Image Picture(Transform parent, string name, Sprite sprite, Color color)
        {
            var image = Rect(parent, name, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false; return image;
        }
        private static Text Label(Transform parent, string name, string value, Font font, int size, Vector2 position, Vector2 bounds)
        {
            var text = Rect(parent, name, new Vector2(.5f, .5f), new Vector2(.5f, .5f)).gameObject.AddComponent<Text>();
            text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = bounds;
            text.text = value; text.font = font; text.fontSize = size; text.color = new Color(1, .97f, .88f); text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            return text;
        }
    }
}
