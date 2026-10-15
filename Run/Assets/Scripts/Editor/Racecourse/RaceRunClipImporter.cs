using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEngine;

namespace ProjectRun.Editor
{
    // Imports reviewed RGBA pages as textures. Runtime Sprite.Create consumes the
    // saved grid; this does not modify unsupported Sprite Editor frame metadata.
    public static class RaceRunClipImporter
    {
        public static string ImportReviewed()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            const string root = "DataBundles/영상/2D탈것_달리기_20261016/sprites";
            int count = 0;
            foreach (var manifest in Directory.GetFiles(root, "clip.json", SearchOption.AllDirectories))
            {
                var data = JObject.Parse(File.ReadAllText(manifest));
                if ((string)data["reviewStatus"] != "reviewed" || ((JObject)data["review"]).Properties().Any(p => (string)p.Value != "passed")) continue;
                string mount = (string)data["mount"], view = (string)data["view"];
                string destination = "Assets/ArtBundles/Race2D/animations/" + mount + "/" + view;
                Directory.CreateDirectory(destination);
                var pages = data["pages"].Select(p =>
                {
                    string file = (string)p["file"];
                    string path = destination + "/" + file;
                    string source = Path.Combine(Path.GetDirectoryName(manifest), file);
                    if (!File.Exists(path) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(path)))
                        File.Copy(source, path, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = true;
                    importer.sRGBTexture = true; importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
                    importer.maxTextureSize = 4096;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.compressionQuality = 100;
                    importer.SaveAndReimport();
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture.width != (int)p["width"] || texture.height != (int)p["height"])
                        throw new InvalidOperationException("Animation texture was unexpectedly resized: " + path);
                    return texture;
                }).ToArray();
                var profile = AssetDatabase.LoadAssetAtPath<Data_RaceSpriteSet>("Assets/Resources/Race2D/" + mount + ".asset");
                int index = view == "01_side" ? 0 : view == "02_rear" ? 1 : 2;
                profile.RunViews[index]?.Release();
                profile.RunViews[index] = new Data_RaceSpriteClip
                {
                    Pages = pages, FrameCount = (int)data["frameCount"], CellSize = (int)data["cellSize"],
                    Columns = (int)data["columns"], FramesPerPage = (int)data["framesPerPage"],
                    Duration = (float)data["duration"], Durations = data["durations"].Values<float>().ToArray(),
                    PixelsPerUnit = (float)data["pixelsPerUnit"], Pivot = new Vector2((float)data["pivot"][0], (float)data["pivot"][1])
                };
                EditorUtility.SetDirty(profile); count++;
            }
            AssetDatabase.SaveAssets();
            return count + " reviewed run clips imported without resizing.";
        }
    }
}
