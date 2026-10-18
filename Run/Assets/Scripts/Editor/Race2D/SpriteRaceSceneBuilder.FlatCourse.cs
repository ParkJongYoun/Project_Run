using System;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class SpriteRaceSceneBuilder
    {
        private const string flatArt = art + "/FlatCourse";
        private static Material flatMaterial;
        private static Sprite whitePixel;
        private static Sprite ellipse;
        private static Sprite[] props;

        [MenuItem("Tools/Project Run/2D race/Build Scene_2D")]
        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Current scene has unsaved edits.");
            var sets = SpriteRaceCapture.Ids.Select(AutoSpriteRaceImporter.Load).ToArray();
            if (sets.Any(s => s == null || s.DirectionCount != 8 || s.Frames.Length != s.DirectionCount * s.FramesPerDirection || s.Frames.Any(frame => frame == null)))
                throw new InvalidOperationException("Import all six AutoSprite directional sets first.");
            foreach (string path in new[] { flatArt, "Assets/Resources/Race2D", "Assets/Resources/Prefabs/Race/Sprites2D" }) SpriteRaceCapture.Ensure(path);
            PrepareFlatArt();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Define_Scene.Sprite2D;
            RenderSettings.fog = false;
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 0; light.shadows = LightShadows.None;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 15;
            camera.nearClipPlane = .1f; camera.farClipPlane = 200; camera.allowHDR = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("579548");
            camera.transparencySortMode = TransparencySortMode.Orthographic;
            camera.gameObject.AddComponent<AudioListener>();
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = false; additional.renderShadows = false; additional.antialiasing = AntialiasingMode.None;
            environment = new GameObject("[Environment] 2D meadow stadium").transform;
            BuildFlatEnvironment();
            ApplyPaintedMeadow(environment);

            var course = Asset<Data_RaceCourse>("Assets/Resources/Race2D/Course_Meadow.asset");
            course.CourseName = "초원 2D 몬스터 컵"; course.RoadWidth = 20;
            course.Points = new Vector3[673]; course.Distances = new float[673];
            for (int i = 0; i < 673; i++) { course.Distances[i] = TrackLength * i / 672; course.Points[i] = TrackPoint(course.Distances[i]); }
            EditorUtility.SetDirty(course);
            var systems = new GameObject("[Systems]").transform;
            var race = Child(systems, "Sys_Race").AddComponent<Sys_Race>();
            race.Course = course; race.CountdownSeconds = 3; race.PlaybackSpeed = 1;
            var director = Child(systems, "Sys_SpriteRaceCamera").AddComponent<Sys_SpriteRaceCamera>();
            director.Race = race; director.RaceCamera = camera; director.FlatScene = true;
            director.FollowSize = 15; director.OverviewSize = 55;
            race.Entrants = new Data_RunnerSetup[6];
            var colors = new[] { Hex("FFC65A"), Hex("EF8DAC"), Hex("B596E4"), Hex("F0985A"), Hex("78C9E0"), Hex("96C768") };
            float[] speeds = { 12.5f, 12.25f, 12.4f, 12.2f, 12.55f, 12.35f };
            var runners = new GameObject("[Runners] 2D monsters").transform;
            for (int i = 0; i < 6; i++)
            {
                var root = new GameObject("P_Runner_2D_" + SpriteRaceCapture.Ids[i]);
                var view = root.AddComponent<RaceDirectionalSpriteView>();
                view.SpriteSet = sets[i]; view.RunnerId = i; view.FlatScene = true;
                var display = FlatSprite(root.transform, "img_directional_monster", sets[i].Frame(2, 0), Vector3.zero, 0, Color.white);
                display.transform.localScale = Vector3.one * 1.12f;
                view.Display = display;
                var shadow = FlatSprite(root.transform, "img_ground_shadow", ellipse, new Vector3(.25f, -.08f, 0), -2, new Color(.08f, .22f, .11f, .3f));
                shadow.transform.localScale = new Vector3(2.7f, 1f, 1); view.GroundShadow = shadow.transform;
                var marker = FlatSprite(root.transform, "img_runner_color", ellipse, new Vector3(0, -.12f, 0), -1, colors[i]);
                marker.transform.localScale = new Vector3(1.05f, .3f, 1); view.IdentityMarker = marker;
                var head = Child(root.transform, "obj_name_anchor"); head.transform.localPosition = Vector3.up * 4.7f;
                var focus = Child(root.transform, "obj_camera_anchor"); focus.transform.localPosition = Vector3.up * 1.5f;
                string prefabPath = "Assets/Resources/Prefabs/Race/Sprites2D/" + root.name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath); Object.DestroyImmediate(root);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, runners);
                view = instance.GetComponent<RaceDirectionalSpriteView>(); view.Race = race; view.RaceCamera = camera;
                float lane = -7.5f + i * 3;
                instance.transform.position = Data_Race2DProjection.Project(course.Point(0, lane));
                view.Display.sortingOrder = Data_Race2DProjection.SortOrder(instance.transform.position);
                view.GroundShadow.GetComponent<SpriteRenderer>().sortingOrder = view.Display.sortingOrder - 2;
                view.IdentityMarker.sortingOrder = view.Display.sortingOrder - 1;
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                race.Entrants[i] = new Data_RunnerSetup {
                    Name = sets[i].CharacterName, Mount = sets[i].CharacterName, SkillName = "초원 질주", Color = colors[i], Prefab = prefab,
                    BaseSpeed = speeds[i], EarlyPower = i == 5 ? .85f : i == 0 ? -.1f : .1f,
                    LatePower = i == 0 ? 1.1f : .25f, SurgeTime = 10 + i * 1.5f, SurgePower = 1.7f, SurgeDuration = 4, Lane = lane
                };
            }
            var managers = new GameObject("[Managers]").transform;
            var ui = Child(managers, "Mgr_UI").AddComponent<Mgr_UI>();
            BuildHUD(race, director, ui, colors);
            Vector3 initial = Data_Race2DProjection.Project(course.Point(0) + course.Forward(0) * 5);
            camera.transform.position = new Vector3(initial.x, initial.y, -100);
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            Selection.activeGameObject = camera.gameObject;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(165, 105, 1)), false);
            return "Scene_2D saved: flat XY course, sprite scenery and six original AutoSprite monsters with eight directions. No 3D renderers.";
        }

        private static void PrepareFlatArt()
        {
            flatMaterial = AssetDatabase.LoadAssetAtPath<Material>(flatArt + "/M_FlatSprite.mat");
            if (flatMaterial == null)
            {
                var shader = Shader.Find("ProjectRun/Race2D/FlatSprite");
                if (shader == null) throw new InvalidOperationException("Missing flat sprite shader.");
                flatMaterial = new Material(shader); AssetDatabase.CreateAsset(flatMaterial, flatArt + "/M_FlatSprite.mat");
            }
            whitePixel = PixelAsset("Pixel", 4, 4, 4, (x, y) => Color.white);
            ellipse = PixelAsset("Disc", 64, 64, 64, (x, y) => new Vector2((x - 31.5f) / 31, (y - 31.5f) / 31).sqrMagnitude <= 1 ? Color.white : Color.clear);
            string atlasPath = flatArt + "/MeadowProps.png";
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            // Explicit art rectangles avoid the narrow overlap between the tree bases and lower props.
            Rect[] rectangles = { new Rect(10, 512, 504, 512), new Rect(520, 498, 510, 526), new Rect(1050, 510, 486, 514),
                new Rect(10, 8, 504, 496), new Rect(520, 10, 522, 488), new Rect(1060, 50, 476, 446) };
            string[] names = { "Oak", "Cypress", "Hedge", "Grandstand", "Clubhouse", "Flowerbed" };
            Vector2[] pivots = { new Vector2(.5f, .03f), new Vector2(.5f, .025f), new Vector2(.5f, .04f),
                new Vector2(.5f,.035f), new Vector2(.5f,.035f), new Vector2(.5f,.04f) };
            props = new Sprite[6];
            for (int i = 0; i < 6; i++)
            {
                string path = flatArt + "/S_" + names[i] + ".asset";
                var sprite = Sprite.Create(texture, rectangles[i], pivots[i], 48, 0, SpriteMeshType.FullRect);
                sprite.name = names[i];
                var old = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (old == null) { AssetDatabase.CreateAsset(sprite, path); props[i] = sprite; }
                else { EditorUtility.CopySerialized(sprite, old); Object.DestroyImmediate(sprite); props[i] = old; EditorUtility.SetDirty(old); }
            }
        }

        private static void BuildFlatEnvironment()
        {
            var ground = PixelAsset("MeadowCourse", 2560, 1792, 12.8f, GroundPixel);
            FlatSprite(environment, "img_meadow_course", ground, Vector3.zero, -20000, Color.white);
            BuildFlatRails(-10.6f); BuildFlatRails(10.6f);
            for (int i = 0; i < 54; i++)
            {
                float d = TrackLength * i / 54;
                PlaceProp(i % 3 == 0 ? 0 : 1, TrackPoint(d, 22 + i % 3), .85f + (i % 4) * .08f, "obj_outer_tree_" + i);
                if (i % 2 == 0) PlaceProp(2, TrackPoint(d + 2.8f, 15.7f), .38f, "obj_hedge_" + i);
            }
            for (int i = 0; i < 10; i++)
            {
                PlaceProp(i % 3 == 0 ? 0 : 2, new Vector3(-32 + i * 7, 0, 10), i % 3 == 0 ? .65f : .38f, "obj_infield_garden_" + i);
                if (i % 2 == 0) PlaceProp(5, new Vector3(-30 + i * 6, 0, -12), .45f, "obj_infield_flowers_" + i);
            }
            PlaceProp(4, new Vector3(-15, 0, 2), 1.2f, "obj_clubhouse");
            for (int i = 0; i < 3; i++) PlaceProp(3, new Vector3(-25 + i * 17, 0, 52), 1.15f, "obj_grandstand_" + i);
            for (int i = 0; i < 4; i++) PlaceProp(5, new Vector3(-33 + i * 17, 0, 43), .55f, "obj_stand_flowers_" + i);
            BuildFlatGate();
            FinishFlag(new Vector3(-20, 0, -39.3f)); FinishFlag(new Vector3(-20, 0, -16.7f));
        }

        private static Color GroundPixel(int x, int y)
        {
            Vector3 p = Data_Race2DProjection.Ground(new Vector2((x - 1280) / 12.8f, (y - 896) / 12.8f));
            float dx = Mathf.Max(0, Mathf.Abs(p.x) - 40);
            float radius = Mathf.Sqrt(dx * dx + p.z * p.z);
            int hash = unchecked((x * 73856093) ^ (y * 19349663)) & 255;
            Color color = Hex(hash < 35 ? "559743" : hash > 225 ? "66A44C" : "5E9E47");
            if (radius < 17.4f) color = Hex(((int)(p.x / 5) & 1) == 0 ? "73B54C" : "6FB048");
            if (radius > 39 && radius < 45) color = Hex(hash < 40 ? "C9B17E" : "D8C18B");
            if (radius >= 18 && radius <= 38)
            {
                color = Hex(((int)(p.x / 7) & 1) == 0 ? "8ED05A" : "88C952");
                if (hash < 5) color *= .94f;
                // Restrained dashed lane guides, leaving the sprites clearly readable.
                float lane = Mathf.Repeat(radius - 20.5f, 3);
                if ((lane < .065f || lane > 2.935f) && ((int)(p.x * .5f + p.z * .2f) & 3) == 0) color = Hex("B6DD7E");
                if (p.z < -18 && Mathf.Abs(p.x + 20) < .8f)
                    color = Hex(((Mathf.FloorToInt((p.x + 20.8f) / .8f) + Mathf.FloorToInt(-p.z)) & 1) == 0 ? "F5F3CE" : "355848");
            }
            if (Mathf.Abs(radius - 17.7f) < .22f || Mathf.Abs(radius - 38.4f) < .23f) color = Hex("E5DEB2");
            if (radius < 16 && Mathf.Abs(p.z) < 1.7f) color = Hex("D8C18B");
            float pond = new Vector2((p.x - 18) / 10, (p.z + 3) / 7).sqrMagnitude;
            if (pond < 1.14f) color = Hex("DDD4A5");
            if (pond < 1) color = Hex(hash < 8 ? "A3E0C3" : pond > .77f ? "67B7B0" : "77C9C1");
            return color;
        }

        private static void BuildFlatRails(float lane)
        {
            var root = Child(environment, lane < 0 ? "group_inner_rail" : "group_outer_rail").transform;
            const int count = 140;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = Data_Race2DProjection.Project(TrackPoint(TrackLength * i / count, lane));
                Vector3 b = Data_Race2DProjection.Project(TrackPoint(TrackLength * (i + 1) / count, lane));
                int order = Data_Race2DProjection.SortOrder((a + b) * .5f);
                FlatLine(root, "img_rail_dark", a + Vector3.up * 1.2f, b + Vector3.up * 1.2f, .22f, order, Hex("326B55"));
                FlatLine(root, "img_rail_highlight", a + Vector3.up * 1.32f, b + Vector3.up * 1.32f, .1f, order + 1, Hex("E9EBCB"));
                if (i % 2 == 0) FlatLine(root, "img_rail_post", a, a + Vector3.up * 1.32f, .14f, order, Hex("D7DCC1"));
            }
        }

        private static void BuildFlatGate()
        {
            var root = Child(environment, "group_start_gate").transform;
            // Stalls remain behind the starting line; no collision or race judgement depends on this decoration.
            for (int i = 0; i <= 6; i++)
            {
                Vector3 a = Data_Race2DProjection.Project(new Vector3(-29, 0, -19 - i * 3));
                Vector3 b = Data_Race2DProjection.Project(new Vector3(-34, 0, -19 - i * 3));
                int order = Data_Race2DProjection.SortOrder(a);
                FlatLine(root, "img_stall_floor", a, b, .28f, order, Hex("84978A"));
                FlatLine(root, "img_gate_pillar", b, b + Vector3.up * 4, .2f, order, Hex("376D59"));
                FlatLine(root, "img_stall_roof", a + Vector3.up * 4, b + Vector3.up * 4, .4f, order, Hex("2E7058"));
                FlatLine(root, "img_stall_roof_edge", a + Vector3.up * 4.2f, b + Vector3.up * 4.2f, .12f, order + 1, Hex("ADD590"));
                if (i < 6)
                {
                    Vector3 c = Data_Race2DProjection.Project(new Vector3(-29, 0, -22 - i * 3));
                    FlatLine(root, "img_gate_header", a + Vector3.up * 4, c + Vector3.up * 4, .5f, order, Hex("3F8263"));
                }
            }
        }

        private static void FinishFlag(Vector3 point)
        {
            Vector3 p = Data_Race2DProjection.Project(point); int order = Data_Race2DProjection.SortOrder(p);
            FlatLine(environment, "img_finish_post", p, p + Vector3.up * 4.8f, .23f, order, Hex("F4EBC8"));
            for (int y = 0; y < 3; y++) for (int x = 0; x < 4; x++)
            {
                var tile = FlatSprite(environment, "img_finish_checker", whitePixel, p + new Vector3(.35f + x * .45f, 4.6f - y * .45f), order + 1, (x + y) % 2 == 0 ? Hex("F9F3D7") : Hex("2A5441"));
                tile.transform.localScale = new Vector3(.45f, .45f, 1);
            }
        }

        private static void PlaceProp(int index, Vector3 point, float scale, string name)
        {
            Vector3 projected = Data_Race2DProjection.Project(point);
            var sprite = FlatSprite(environment, name, props[index], projected, Data_Race2DProjection.SortOrder(projected), Color.white);
            sprite.transform.localScale = Vector3.one * scale;
        }

        private static SpriteRenderer FlatSprite(Transform parent, string name, Sprite sprite, Vector3 position, int order, Color color)
        {
            var renderer = Child(parent, name).AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sharedMaterial = flatMaterial; renderer.color = color; renderer.sortingOrder = order;
            renderer.transform.localPosition = position; return renderer;
        }

        private static void FlatLine(Transform parent, string name, Vector3 a, Vector3 b, float width, int order, Color color)
        {
            var line = FlatSprite(parent, name, whitePixel, (a + b) * .5f, order, color);
            line.transform.localScale = new Vector3(Vector3.Distance(a, b) + .06f, width, 1);
            line.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        }

        private static Sprite PixelAsset(string name, int width, int height, float ppu, Func<int, int, Color> pixel)
        {
            string path = flatArt + "/" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = ppu;
            importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point; importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
