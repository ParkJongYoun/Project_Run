using System;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class SpriteRaceSceneBuilder
    {
        private const string paintedArt = art + "/PaintedMeadow";

        [MenuItem("Tools/Project Run/2D race/Upgrade meadow artwork")]
        public static string UpgradeMeadow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before updating the stadium.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Scene_2D first.");
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("[Environment]"));
            if (root == null) throw new InvalidOperationException("Missing 2D stadium environment.");
            Undo.RegisterFullObjectHierarchyUndo(root, "Upgrade painted meadow artwork");
            ApplyPaintedMeadow(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "Painted grass, paths, six scenery sprites, shaded rails and garden details saved to Scene_2D.";
        }

        private static void ApplyPaintedMeadow(Transform root)
        {
            environment = root;
            flatMaterial = AssetDatabase.LoadAssetAtPath<Material>(flatArt + "/M_FlatSprite.mat");
            whitePixel = AssetDatabase.LoadAssetAtPath<Sprite>(flatArt + "/Pixel.png");
            ellipse = AssetDatabase.LoadAssetAtPath<Sprite>(flatArt + "/Disc.png");
            var grass = ImportPaintedTexture("Grass", true);
            var sand = ImportPaintedTexture("Sand", true);
            var atlas = ImportPaintedTexture("Props", false);
            var shader = Shader.Find("ProjectRun/Race2D/PaintedMeadow");
            if (shader == null) throw new InvalidOperationException("Missing painted meadow shader.");
            string materialPath = paintedArt + "/M_PaintedMeadow.mat";
            var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (groundMaterial == null)
            {
                groundMaterial = new Material(shader);
                AssetDatabase.CreateAsset(groundMaterial, materialPath);
            }
            groundMaterial.shader = shader;
            groundMaterial.SetTexture("_GrassTex", grass);
            groundMaterial.SetTexture("_SandTex", sand);
            groundMaterial.SetTexture("_StartGridTex", ImportPaintedTexture("StartGrid",false));
            groundMaterial.SetFloat("_GrassScale", 18);
            groundMaterial.SetFloat("_GrassContrast", .68f);
            groundMaterial.SetColor("_TrackTint", Hex("B8D17F").linear);
            EditorUtility.SetDirty(groundMaterial);
            root.Find("img_meadow_course").GetComponent<SpriteRenderer>().sharedMaterial = groundMaterial;

            string[] names = { "Oak", "Cypress", "Hedge", "Grandstand", "Clubhouse", "Flowerbed" };
            // Rectangles include clear gutters; pivots sit on each illustrated ground contact.
            Rect[] rectangles = {
                new Rect(32,512,512,504), new Rect(640,512,280,504), new Rect(1048,512,472,300),
                new Rect(48,24,496,488), new Rect(552,32,472,480), new Rect(1080,32,440,320)
            };
            Vector2[] pivots = {
                new Vector2(.51f,.045f), new Vector2(.48f,.035f), new Vector2(.5f,.045f),
                new Vector2(.5f,.065f), new Vector2(.5f,.09f), new Vector2(.5f,.105f)
            };
            var paintedSprites = new Sprite[6];
            for (int index = 0; index < names.Length; index++)
            {
                string path = paintedArt + "/S_" + names[index] + ".asset";
                var sprite = Sprite.Create(atlas, rectangles[index], pivots[index], 48, 0, SpriteMeshType.FullRect);
                sprite.name = names[index];
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing == null) AssetDatabase.CreateAsset(sprite, path);
                else
                {
                    EditorUtility.CopySerialized(sprite, existing);
                    Object.DestroyImmediate(sprite);
                    sprite = existing;
                    EditorUtility.SetDirty(sprite);
                }
                paintedSprites[index] = sprite;
            }
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null) continue;
                int index = Array.IndexOf(names, renderer.sprite.name);
                if (index < 0) continue;
                renderer.sprite = paintedSprites[index];
                renderer.color = Color.white;
            }
            props = paintedSprites;
            ReplaceRail(root, "group_inner_rail", -10.6f);
            ReplaceRail(root, "group_outer_rail", 10.6f);
            var standingGate = root.Find("group_start_gate");
            if (standingGate != null) Object.DestroyImmediate(standingGate.gameObject);
            ReplacePaintedFeature(root, "img_painted_pond", "Pond", new Vector2(.5f,.45f), 82,
                Data_Race2DProjection.Project(new Vector3(18,0,-3)), Vector3.one, true);
            var previousDetails = root.Find("group_painted_garden_details");
            if (previousDetails != null) Object.DestroyImmediate(previousDetails.gameObject);
            var details = Child(root, "group_painted_garden_details").transform;
            // Keep extra scenery entirely outside the runnable lanes.
            for (int i = 0; i < 12; i++)
            {
                float distance = TrackLength * (i + .35f) / 12;
                Vector3 point = Data_Race2DProjection.Project(TrackPoint(distance, -14.3f));
                var flowers = FlatSprite(details, "img_border_flowers_" + i, paintedSprites[5], point,
                    Data_Race2DProjection.SortOrder(point), Color.white);
                flowers.transform.localScale = Vector3.one * .29f;
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 point = Data_Race2DProjection.Project(new Vector3(10 + i * 4, 0, 6));
                var shrub = FlatSprite(details, "img_pond_garden_" + i, paintedSprites[i % 2 == 0 ? 2 : 5],
                    point, Data_Race2DProjection.SortOrder(point), Color.white);
                shrub.transform.localScale = Vector3.one * .26f;
            }
            AssetDatabase.SaveAssets();
        }

        private static Texture2D ImportPaintedTexture(string name, bool repeat)
        {
            string path = paintedArt + "/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = !repeat;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.anisoLevel = repeat ? 4 : 1;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ReplaceRail(Transform root, string name, float lane)
        {
            var previous = root.Find(name);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var rail = Child(root, name).transform;
            const int count = 140;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = Data_Race2DProjection.Project(TrackPoint(TrackLength * i / count, lane));
                Vector3 b = Data_Race2DProjection.Project(TrackPoint(TrackLength * (i + 1) / count, lane));
                int order = Data_Race2DProjection.SortOrder((a + b) * .5f);
                FlatLine(rail, "img_rail_ground_shadow", a + new Vector3(.3f,-.2f), b + new Vector3(.3f,-.2f),
                    .32f, -18000, new Color(.12f,.22f,.14f,.14f));
                FlatLine(rail, "img_rail_outline", a + Vector3.up * 1.20f, b + Vector3.up * 1.20f,
                    .29f, order, Hex("345A49"));
                FlatLine(rail, "img_rail_body", a + Vector3.up * 1.23f, b + Vector3.up * 1.23f,
                    .19f, order + 1, Hex("D6C89B"));
                FlatLine(rail, "img_rail_top_light", a + Vector3.up * 1.31f, b + Vector3.up * 1.31f,
                    .075f, order + 2, Hex("FFF1C7"));
                if (i % 2 != 0) continue;
                FlatLine(rail, "img_post_outline", a, a + Vector3.up * 1.40f, .25f, order + 3, Hex("345A49"));
                FlatLine(rail, "img_post_wood", a + Vector3.up * .05f, a + Vector3.up * 1.37f,
                    .16f, order + 4, Hex("D6C391"));
                FlatLine(rail, "img_post_light", a + new Vector3(-.045f,.10f), a + new Vector3(-.045f,1.35f),
                    .035f, order + 5, Hex("FFF1CA"));
                var cap = FlatSprite(rail, "img_brass_cap", ellipse, a + Vector3.up * 1.43f, order + 6, Hex("D5AC58"));
                cap.transform.localScale = new Vector3(.31f,.18f,1);
            }
        }

        private static void ReplacePaintedFeature(Transform root, string objectName, string assetName,
            Vector2 pivot, float ppu, Vector3 point, Vector3 scale, bool ground)
        {
            var previous = root.Find(objectName);
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            var texture = ImportPaintedTexture(assetName, false);
            var sprite = Sprite.Create(texture, new Rect(0,0,texture.width,texture.height),pivot,ppu,0,SpriteMeshType.FullRect);
            sprite.name = assetName;
            string path = paintedArt + "/S_" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing == null) AssetDatabase.CreateAsset(sprite,path);
            else
            {
                EditorUtility.CopySerialized(sprite,existing);
                Object.DestroyImmediate(sprite);
                sprite = existing;
                EditorUtility.SetDirty(existing);
            }
            var renderer = FlatSprite(root,objectName,sprite,point,ground ? -17000 : Data_Race2DProjection.SortOrder(point),Color.white);
            renderer.transform.localScale = scale;
        }
    }
}
