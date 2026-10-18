using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race.Editor
{
    public static class LobbyModularBuilder
    {
        private const string JobPath = "DataBundles/LobbyAnimation/WildRose_Modular_v01";
        private const string ArtPath = "Assets/ArtBundles/Lobby/WildRose/Modular_v01";
        private const string ResourcePath = "Assets/Resources/UI/Lobby/Modular_v01";
        private const string PanelPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_Lobby.prefab";
        private static readonly string[] clipKeys = { "maid_cat_idle", "maid_cat_cheer", "curtain_left_idle", "curtain_right_idle" };

        [Serializable] private class Manifest
        {
            public string SourceVideoSha256;
            public float[] IllustrationRect;
            public int[] CellSize;
            public Page[] Pages;
            public Frame[] Frames;
            public bool PixelCopyVerified;
        }
        [Serializable] private class Page { public string File; public int Width; public int Height; }
        [Serializable] private class Frame { public int Index; public int Page; public int X; public int Y; public float Duration; }

        [MenuItem("Tools/Project Run/Lobby/Install Modular Wild Rose")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before installation.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Scene_Lobby.unity" || scene.isDirty)
                throw new InvalidOperationException("Open the saved Scene_Lobby first; preserve any pending edits.");
            string project = Path.GetDirectoryName(Application.dataPath);
            var clips = clipKeys.ToDictionary(key => key, key => ImportClip(project, key));
            var background = ImportSingle(project, "background.png");
            Sprite foreground = File.Exists(Path.Combine(project, JobPath, "foreground.png")) ? ImportSingle(project, "foreground.png") : null;
            GameObject root = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var panel = root.GetComponent<UIPanel_Lobby>();
                var art = root.transform.Find("group_illustration");
                var room = art.Find("bg_room").GetComponent<Image>();
                room.sprite = background;
                room.material = null;
                var environment = room.GetComponent<UIItem_LobbyEnvironment>();
                if (environment != null) environment.enabled = false;
                var oldCurtain = art.Find("img_curtain");
                if (oldCurtain != null) oldCurtain.gameObject.SetActive(false);
                var maid = art.Find("img_maid_cat") ?? art.Find("img_characters");
                if (maid == null) throw new InvalidOperationException("Missing existing lobby character layer.");
                maid.name = "img_maid_cat";
                var maidPlayer = maid.GetComponent<UIItem_LobbyAnimation>();
                Configure(maidPlayer, clips["maid_cat_idle"], clips["maid_cat_cheer"], 0);
                var left = GetLayer(art, "img_curtain_left");
                Configure(left, clips["curtain_left_idle"], null, 0);
                var right = GetLayer(art, "img_curtain_right");
                Configure(right, clips["curtain_right_idle"], null, .23f);
                var butler = GetImage(art, "img_butler");
                DemoBuilder.ConfigureStillButler(butler);
                left.transform.SetSiblingIndex(room.transform.GetSiblingIndex() + 1);
                right.transform.SetSiblingIndex(left.transform.GetSiblingIndex() + 1);
                butler.transform.SetSiblingIndex(right.transform.GetSiblingIndex() + 1);
                if (foreground != null)
                {
                    var fg = GetImage(art, "img_room_foreground");
                    fg.sprite = foreground;
                    fg.transform.SetSiblingIndex(butler.transform.GetSiblingIndex() + 1);
                    fg.rectTransform.anchorMin = Vector2.zero;
                    fg.rectTransform.anchorMax = Vector2.one;
                    fg.rectTransform.offsetMin = fg.rectTransform.offsetMax = Vector2.zero;
                }
                art.Find("img_contact_shadow").SetAsLastSibling();
                maid.SetAsLastSibling();
                panel.Configure(root.transform.Find("obj_safe_area") as RectTransform, maidPlayer,
                    root.transform.Find("obj_safe_area/group_caption/btn_pause").GetComponent<Button>(),
                    root.transform.Find("obj_safe_area/group_caption/btn_pause/txt_label").GetComponent<Text>());
                panel.ConfigureAmbient(new[] { left, right });
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Modular Wild Rose installed: three animated layers and one still butler.");
        }

        private static Data_LobbyAnimation ImportClip(string project, string key)
        {
            string source = Path.Combine(project, JobPath, "jobs", key, "final");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(source, "clip.json")));
            if (!manifest.PixelCopyVerified || manifest.Frames == null || manifest.Frames.Length < 2)
                throw new InvalidDataException("Missing reviewed atlas: " + key);
            Directory.CreateDirectory(Path.Combine(project, ArtPath, key));
            Directory.CreateDirectory(Path.Combine(project, ResourcePath));
            var frames = new Sprite[manifest.Frames.Length];
            for (int page = 0; page < manifest.Pages.Length; page++)
            {
                var data = manifest.Pages[page];
                string target = ArtPath + "/" + key + "/" + data.File;
                File.Copy(Path.Combine(source, data.File), Path.Combine(project, target), true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                ConfigureTexture(importer);
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
#pragma warning disable CS0618
                importer.spritesheet = manifest.Frames.Where(f => f.Page == page).Select(f => new SpriteMetaData
                {
                    name = key + "_" + f.Index.ToString("D3"),
                    rect = new Rect(f.X, data.Height - f.Y - manifest.CellSize[1], manifest.CellSize[0], manifest.CellSize[1]),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
                }).ToArray();
#pragma warning restore CS0618
                importer.SaveAndReimport();
                var loaded = AssetDatabase.LoadAllAssetsAtPath(target).OfType<Sprite>().ToDictionary(s => s.name);
                foreach (var frame in manifest.Frames.Where(f => f.Page == page)) frames[frame.Index] = loaded[key + "_" + frame.Index.ToString("D3")];
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                if (texture.width != data.Width || texture.height != data.Height) throw new InvalidDataException("Resized atlas: " + target);
            }
            string clipPath = ResourcePath + "/" + key + ".asset";
            var clip = AssetDatabase.LoadAssetAtPath<Data_LobbyAnimation>(clipPath);
            if (clip == null) { clip = ScriptableObject.CreateInstance<Data_LobbyAnimation>(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.Frames = frames;
            clip.Durations = manifest.Frames.Select(f => f.Duration).ToArray();
            var r = manifest.IllustrationRect;
            clip.IllustrationRect = new Rect(r[0], r[1], r[2], r[3]);
            clip.SourceVideoSha256 = manifest.SourceVideoSha256;
            EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        private static Sprite ImportSingle(string project, string file)
        {
            Directory.CreateDirectory(Path.Combine(project, ArtPath));
            string target = ArtPath + "/" + file;
            File.Copy(Path.Combine(project, JobPath, file), Path.Combine(project, target), true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            ConfigureTexture(importer); importer.spriteImportMode = SpriteImportMode.Single; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(target);
        }

        private static void ConfigureTexture(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096; importer.isReadable = false;
            importer.ClearPlatformTextureSettings("WebGL"); importer.ClearPlatformTextureSettings("Standalone");
        }

        private static Image GetImage(Transform parent, string name)
        {
            var existing = parent.Find(name);
            var image = existing != null ? existing.GetComponent<Image>() :
                new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); image.raycastTarget = false; return image;
        }

        private static UIItem_LobbyAnimation GetLayer(Transform parent, string name)
        {
            var image = GetImage(parent, name);
            return image.GetComponent<UIItem_LobbyAnimation>() ?? image.gameObject.AddComponent<UIItem_LobbyAnimation>();
        }

        private static void Configure(UIItem_LobbyAnimation player, Data_LobbyAnimation idle, Data_LobbyAnimation cheer, float phase)
        {
            player.ConfigureStates(idle, cheer, 3, 6, phase);
            player.GetComponent<Image>().material = null;
            player.GetComponent<Image>().raycastTarget = false;
            var rect = (RectTransform)player.transform; var r = idle.IllustrationRect; var size = idle.IllustrationSize;
            rect.anchorMin = new Vector2(r.xMin / size.x, 1 - r.yMax / size.y);
            rect.anchorMax = new Vector2(r.xMax / size.x, 1 - r.yMin / size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (cheer != null && cheer.IllustrationRect != idle.IllustrationRect)
                throw new InvalidDataException("Idle and cheer must have the identical common crop and placement.");
        }
    }
}
