using System;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRun.Editor
{
    public static class AutoSpriteRaceImporter
    {
        public const string Sets = "Assets/Resources/Race2D/AutoSprite";
        private const string art = "Assets/ArtBundles/Race2D/AutoSprite";

        [Serializable]
        private sealed class ImportSettings
        {
            public string CharacterId;
            public int DirectionCount, FrameCount, Columns, RowsPerDirection, CellSize;
            public int[] DirectionFrameCounts;
            public float Duration, WorldSize, PivotX, PivotY;
        }

        [Serializable]
        private sealed class DirectionCalibration
        {
            public string CharacterId;
            public float[] Scales;
            // Coordinates in the 256-pixel runtime cell, measured from its top-left.
            public Vector2[] GroundAnchors;
        }

        public static Data_DirectionalRunSprites Load(string id)
        {
            return AssetDatabase.LoadAssetAtPath<Data_DirectionalRunSprites>(Sets + "/Run_" + id + ".asset");
        }

        [MenuItem("Tools/Project Run/2D race/Import AutoSprite monsters")]
        public static string ImportAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            foreach (string id in SpriteRaceCapture.Ids) Import(id);
            return "Imported six original monsters with eight screen-space directions each.";
        }

        public static string Import(string id)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            int characterIndex = Array.IndexOf(SpriteRaceCapture.Ids, id);
            if (characterIndex < 0) throw new ArgumentException(id);
            string folder = art + "/" + id;
            var settings = JsonUtility.FromJson<ImportSettings>(File.ReadAllText(folder + "/Import.json"));
            if (settings.CharacterId != id || settings.DirectionCount != 8 || settings.Duration <= 0)
                throw new InvalidDataException("Invalid AutoSprite import settings: " + id);
            string calibrationPath = folder + "/DirectionCalibration.json";
            var calibration = File.Exists(calibrationPath)
                ? JsonUtility.FromJson<DirectionCalibration>(File.ReadAllText(calibrationPath)) : null;
            if (calibration != null)
            {
                if (calibration.CharacterId != id || calibration.Scales == null || calibration.Scales.Length != 8
                    || calibration.GroundAnchors == null || calibration.GroundAnchors.Length != 8)
                    throw new InvalidDataException("Invalid directional calibration: " + id);
                for (int direction = 0; direction < 8; direction++)
                {
                    float scale = calibration.Scales[direction];
                    Vector2 anchor = calibration.GroundAnchors[direction];
                    if (!(scale > 0 && scale <= 2) || !(anchor.x >= 0 && anchor.x <= settings.CellSize)
                        || !(anchor.y >= 0 && anchor.y <= settings.CellSize))
                        throw new InvalidDataException("Invalid directional scale/anchor: " + id + "/" + direction);
                }
            }
            string texturePath = folder + "/Run.png";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture.width != settings.Columns * settings.CellSize
                || texture.height != settings.RowsPerDirection * 8 * settings.CellSize)
                throw new InvalidDataException("Atlas was resized during import: " + id);
            SpriteRaceCapture.Ensure(Sets);
            string setPath = Sets + "/Run_" + id + ".asset";
            var set = Load(id);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<Data_DirectionalRunSprites>();
                AssetDatabase.CreateAsset(set, setPath);
            }
            var oldSprites = AssetDatabase.LoadAllAssetsAtPath(setPath).OfType<Sprite>().ToDictionary(s => s.name);
            string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            set.Frames = new Sprite[settings.FrameCount * 8];
            set.CharacterName = SpriteRaceCapture.Names[characterIndex];
            set.SourcePrefabPath = "";
            set.DirectionCount = 8;
            set.FramesPerDirection = settings.FrameCount;
            set.DirectionFrameCounts = settings.DirectionFrameCounts;
            set.ClipDuration = settings.Duration;
            set.ScreenSpaceDirections = true;
            set.CapturePitch = 40;
            set.CaptureYaw = 0;
            set.DirectionAngles = new float[8];
            for (int direction = 0; direction < 8; direction++)
            {
                float yaw = direction * 45f * Mathf.Deg2Rad;
                set.DirectionAngles[direction] = Mathf.Repeat(Mathf.Atan2(Mathf.Sin(yaw),
                    Mathf.Cos(yaw) * Mathf.Sin(set.CapturePitch * Mathf.Deg2Rad)) * Mathf.Rad2Deg, 360);
            }
            set.WorldSize = settings.WorldSize;
            set.GroundPivot = new Vector2(settings.PivotX, settings.PivotY);
            set.SpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Race2D/FlatCourse/M_FlatSprite.mat");
            for (int direction = 0; direction < 8; direction++)
            for (int frame = 0; frame < settings.FrameCount; frame++)
            {
                int row = direction * settings.RowsPerDirection + frame / settings.Columns;
                var rect = new Rect(frame % settings.Columns * settings.CellSize,
                    texture.height - (row + 1) * settings.CellSize, settings.CellSize, settings.CellSize);
                float scale = calibration == null ? 1 : calibration.Scales[direction];
                Vector2 pivot = calibration == null ? set.GroundPivot : new Vector2(
                    calibration.GroundAnchors[direction].x / settings.CellSize,
                    1 - calibration.GroundAnchors[direction].y / settings.CellSize);
                var sprite = Sprite.Create(texture, rect, pivot,
                    settings.CellSize / (settings.WorldSize * scale), 0, SpriteMeshType.FullRect);
                sprite.name = id + "_" + directions[direction] + "_" + frame.ToString("D2");
                Sprite existing;
                if (oldSprites.TryGetValue(sprite.name, out existing))
                {
                    EditorUtility.CopySerialized(sprite, existing);
                    UnityEngine.Object.DestroyImmediate(sprite);
                    sprite = existing;
                    EditorUtility.SetDirty(existing);
                    oldSprites.Remove(sprite.name);
                }
                else AssetDatabase.AddObjectToAsset(sprite, set);
                set.Frames[direction * settings.FrameCount + frame] = sprite;
            }
            foreach (var obsolete in oldSprites.Values) UnityEngine.Object.DestroyImmediate(obsolete, true);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return id + ": " + set.Frames.Length + " sprites, " + texture.format;
        }

        [MenuItem("Tools/Project Run/2D race/Apply AutoSprite monsters to Scene_2D")]
        public static string ApplyToScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Define_Scene.Sprite2D)
                throw new InvalidOperationException("Open Scene_2D first.");
            foreach (string id in SpriteRaceCapture.Ids)
            {
                var set = Load(id);
                if (set == null || set.Frames.Any(s => s == null)) throw new InvalidOperationException("Missing set: " + id);
                string path = "Assets/Resources/Prefabs/Race/Sprites2D/P_Runner_2D_" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<RaceDirectionalSpriteView>();
                    view.SpriteSet = set;
                    view.Display.sprite = set.Frame(2, 0);
                    view.Display.sharedMaterial = set.SpriteMaterial;
                    view.Display.transform.localScale = Vector3.one * 1.12f;
                    view.GroundShadow.localScale = new Vector3(2.7f, 1f, 1);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var view in UnityEngine.Object.FindObjectsByType<RaceDirectionalSpriteView>(FindObjectsSortMode.None))
            {
                var set = Load(SpriteRaceCapture.Ids[view.RunnerId]);
                view.SpriteSet = set;
                view.Display.sprite = set.Frame(2, 0);
                view.Display.sharedMaterial = set.SpriteMaterial;
                view.Display.transform.localScale = Vector3.one * 1.12f;
                view.GroundShadow.localScale = new Vector3(2.7f, 1f, 1);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.Display);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.Display.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.GroundShadow);
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Scene_2D and all six runner prefabs now use the original AutoSprite monsters.";
        }
    }
}
