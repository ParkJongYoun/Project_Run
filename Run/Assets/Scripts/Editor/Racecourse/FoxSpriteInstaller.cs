using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class FoxSpriteInstaller
    {
        public const string Art = "Assets/ArtBundles/FoxImpostor";
        public const string SetPath = "Assets/Resources/FoxImpostor/FoxSpriteSet.asset";

        private static Texture2D LoadPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
            return texture;
        }

        private static Rect PairBounds(Texture2D sheet)
        {
            int width = sheet.width / 2, height = sheet.height;
            var pixels = sheet.GetPixels32();
            int xMin = width, yMin = height, xMax = 0, yMax = 0, transparent = 0;
            for (int y = 0; y < height; y++) for (int x = 0; x < sheet.width; x++)
            {
                if (pixels[y * sheet.width + x].a < 64) { transparent++; continue; }
                int localX = x % width;
                xMin = Math.Min(xMin, localX); xMax = Math.Max(xMax, localX);
                yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
            }
            if (transparent < pixels.Length * .15 || xMax <= xMin || yMax <= yMin) throw new InvalidDataException("Missing alpha/subject: " + sheet.name);
            return Rect.MinMaxRect((float)xMin / width, (float)yMin / height, (float)(xMax + 1) / width, (float)(yMax + 1) / height);
        }

        [MenuItem("Project Run/Fox 2D/Install camera sprites in All3D")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Scene_Oval") throw new InvalidOperationException("Open Scene_Oval first.");
            Directory.CreateDirectory(Art + "/Frames"); Directory.CreateDirectory(Art + "/Materials");
            Directory.CreateDirectory("Assets/Resources/FoxImpostor"); AssetDatabase.Refresh();
            var set = AssetDatabase.LoadAssetAtPath<Data_FoxSpriteSet>(SetPath);
            if (set == null) { set = ScriptableObject.CreateInstance<Data_FoxSpriteSet>(); AssetDatabase.CreateAsset(set, SetPath); }
            var shader = Shader.Find("ProjectRun/FoxImpostor");
            if (shader == null) throw new InvalidOperationException("Fox sprite shader not compiled.");
            string materialPath = Art + "/Materials/Mat_FoxSprite.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            set.SpriteMaterial = material;
            var shots = (E_RaceShot[])Enum.GetValues(typeof(E_RaceShot));
            set.Shots = new Data_FoxSpriteSet.ShotFrames[shots.Length];
            var report = new System.Text.StringBuilder("shot,worldSize,pivotX,pivotY,alphaBoundsWidth,alphaBoundsHeight\n");
            for (int i = 0; i < shots.Length; i++)
            {
                string shot = shots[i].ToString();
                var info = JsonUtility.FromJson<FoxSpriteCapture.CaptureInfo>(File.ReadAllText(FoxSpriteCapture.Output + "/Captures/" + shot + ".json"));
                var raw = LoadPng(FoxSpriteCapture.Output + "/Captures/" + shot + "_pair.png");
                var sheet = LoadPng(Art + "/Textures/" + shot + ".png");
                var rawBounds = PairBounds(raw); var bounds = PairBounds(sheet);
                // Shared pair registration retains the source movement; never normalize each foot separately.
                float factor = bounds.height / rawBounds.height;
                Vector2 pivot = bounds.center + (info.Pivot - rawBounds.center) * factor;
                float worldSize = info.WorldSize / factor;
                var frames = new Sprite[2];
                for (int frame = 0; frame < 2; frame++)
                {
                    string path = Art + "/Frames/" + shot + (frame == 0 ? "_Left.png" : "_Right.png");
                    int width = sheet.width / 2;
                    var image = new Texture2D(width, sheet.height, TextureFormat.RGBA32, false);
                    image.SetPixels(sheet.GetPixels(frame * width, 0, width, sheet.height)); image.Apply();
                    File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 1024;
                    importer.spritePixelsPerUnit = sheet.height / worldSize;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot; settings.spriteMeshType = SpriteMeshType.FullRect;
                    importer.SetTextureSettings(settings); importer.SaveAndReimport();
                    frames[frame] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (frames[frame] == null) throw new InvalidDataException("Sprite import failed: " + path);
                }
                set.Shots[i] = new Data_FoxSpriteSet.ShotFrames { Shot = shots[i], Left = frames[0], Right = frames[1], ViewDirection = info.ViewDirection, LeftPhase = info.LeftPhase, RightPhase = info.RightPhase, AnchorHeight = info.AnchorHeight };
                report.AppendLine(shot + "," + worldSize + "," + pivot.x + "," + pivot.y + "," + bounds.width + "," + bounds.height);
                UnityEngine.Object.DestroyImmediate(raw); UnityEngine.Object.DestroyImmediate(sheet);
            }
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
            var presentation = UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>();
            var view = presentation.Views[2];
            var experiment = view.GetComponent<RaceFoxSpriteView>();
            if (experiment == null) experiment = Undo.AddComponent<RaceFoxSpriteView>(view.gameObject);
            Undo.RecordObject(experiment, "Install fox camera sprites");
            experiment.Runner = view; experiment.Race = presentation.Race;
            experiment.Director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            experiment.SpriteSet = set; experiment.UseSprites = true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(experiment);
            EditorUtility.SetDirty(experiment);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene); EditorSceneManager.SaveScene(view.gameObject.scene);
            Directory.CreateDirectory(FoxSpriteCapture.Output + "/Review");
            File.WriteAllText(FoxSpriteCapture.Output + "/Review/import-registration.csv", report.ToString());
            Debug.Log("Installed 12 camera pairs / 24 fox sprites in Scene_Oval. Toggle RaceFoxSpriteView.UseSprites to compare.");
        }

        [MenuItem("Project Run/Fox 2D/Toggle original 3D comparison")]
        public static void Toggle()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<RaceFoxSpriteView>();
            if (view != null) { Undo.RecordObject(view, "Toggle fox comparison"); view.ToggleComparison(); EditorUtility.SetDirty(view); }
        }
    }
}
