using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Editor-only construction of the first all-3D environment. No race simulation.</summary>
    public static partial class All3DRacecourseBuilder
    {
        private const string artPath = "Assets/ArtBundles/Racecourse/All3D";
        private const string prefabPath = "Assets/Resources/Prefabs/Environment/P_Racecourse_All3D.prefab";
        private const string rootName = "P_Racecourse_All3D";
        private const int segments = 256;
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Geometry> geometry = new Dictionary<string, Geometry>();
        private static Transform root;
        private static System.Random random;

        [MenuItem("Tools/Project Run/All 3D/Build racecourse")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Scene_Oval.unity" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Open Scene_Oval in Edit mode before building the course.");
            EnsureFolder(artPath + "/Materials");
            EnsureFolder(artPath + "/Meshes");
            EnsureFolder(artPath + "/Textures");
            EnsureFolder("Assets/Resources/Prefabs/Environment");
            // Replace only this tool's generated root, preserving unrelated user scene objects.
            var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            root = new GameObject(rootName).transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build racecourse");
            materials.Clear();
            geometry.Clear();
            random = new System.Random(20261015);
            PrepareMaterials();
            PrepareRefinedMaterials();
            BuildTrack();
            BuildGrandstandV2();
            BuildGardenV2();
            BuildFacilitiesV2();
            BuildSurroundingsV2();
            foreach (var item in geometry.Values) item.Save(root);
            AddAnchors();
            ConfigureLighting();
            ConfigureRefinedLighting();
            ViewShowcase();
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, prefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(15, 0, -68), Quaternion.Euler(16, 205, 0), 90, false, true);
            Debug.Log("All3D racecourse built: " + root.GetComponentsInChildren<Renderer>().Length + " renderers. " + prefabPath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Color Hex(string text)
        {
            ColorUtility.TryParseHtmlString("#" + text, out var color);
            return color;
        }

        private static Material Mat(string name, string color, Texture texture = null, bool cutout = false)
        {
            string path = artPath + "/Materials/Mat_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Hex(color));
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", name == "Water" ? 0.5f : 0.12f);
            material.SetFloat("_Metallic", 0);
            material.enableInstancing = true;
            if (cutout)
            {
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", 0.42f);
                material.SetFloat("_Cull", 0);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = 2450;
                material.SetOverrideTag("RenderType", "TransparentCutout");
            }
            EditorUtility.SetDirty(material);
            materials[name] = material;
            return material;
        }

        private static Texture2D SurfaceTexture(string name, Color a, Color b)
        {
            // Small tileable colour textures; these are engine surface data, not AI artwork.
            const int size = 256;
            string path = artPath + "/Textures/Tex_" + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            var noise = new System.Random(715);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n00 = Mathf.PerlinNoise(u * 7, v * 7);
                float n10 = Mathf.PerlinNoise((u - 1) * 7, v * 7);
                float n01 = Mathf.PerlinNoise(u * 7, (v - 1) * 7);
                float n11 = Mathf.PerlinNoise((u - 1) * 7, (v - 1) * 7);
                float n = Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v);
                pixels[y * size + x] = Color.Lerp(a, b, Mathf.Clamp01(n * 0.85f + (float)noise.NextDouble() * 0.25f));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void PrepareMaterials()
        {
            var turf = SurfaceTexture("Turf", Hex("728C40"), Hex("B3C87A"));
            var sand = SurfaceTexture("Sand", Hex("BFA473"), Hex("EAD5A4"));
            Mat("TurfLight", "A8D05D", turf); Mat("TurfDark", "93BE52", turf);
            Mat("Meadow", "9ABF66", turf); Mat("Dirt", "EBD5AC", sand);
            Mat("White", "F3F3E5"); Mat("RailGreen", "237A65");
            Mat("Concrete", "C4D5D2"); Mat("Path", "E4DCC6"); Mat("Asphalt", "607E81");
            Mat("Navy", "173D50"); Mat("Teal", "248E91"); Mat("Glass", "6CAEB9");
            Mat("Gold", "EFC563"); Mat("Coral", "E88169"); Mat("SeatBlue", "5C9CB7");
            Mat("SeatMint", "82C7B7"); Mat("SeatCream", "F0DDAD");
            Mat("Water", "4DAEB9"); Mat("WaterEdge", "A5D6C1");
            Mat("Hedge", "367454"); Mat("LeafLight", "75A95D"); Mat("LeafDark", "508458");
            Mat("Trunk", "80664B"); Mat("Hill", "71977B"); Mat("HillFar", "93B2A0");
            Mat("OakBark", "FFFFFF", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldAsset/Environment/worldboss/etc/wb_bg_iys/barkOak001.png"));
            Mat("OakLeaves", "BADE9B", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldAsset/Environment/worldboss/etc/wb_bg_iys/BranchesOak001.tif"), true);
            Mat("Rock", "FFFFFF", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldAsset/Environment/mjolnir/mj_rock001.tga"));
        }

        private static Geometry Geo(string group, string material)
        {
            string key = group + "__" + material;
            if (!geometry.TryGetValue(key, out var data))
            {
                data = new Geometry(group, material);
                geometry.Add(key, data);
            }
            return data;
        }

        private static Vector3 CoursePoint(float radius, float index, float height = 0)
        {
            float section = Mathf.Repeat(index, segments) / 64f;
            if (section < 1) return new Vector3(Mathf.Lerp(-90, 90, section), height, -radius);
            if (section < 2)
            {
                float angle = Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, section - 1);
                return new Vector3(90 + Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            }
            if (section < 3) return new Vector3(Mathf.Lerp(90, -90, section - 2), height, radius);
            float left = Mathf.Lerp(Mathf.PI / 2, Mathf.PI * 1.5f, section - 3);
            return new Vector3(-90 + Mathf.Cos(left) * radius, height, Mathf.Sin(left) * radius);
        }

        private static void Ring(string group, string material, float inner, float outer, float height, bool striped = false)
        {
            for (int i = 0; i < segments; i++)
            {
                string mat = striped && (i / 8) % 2 == 0 ? "TurfLight" : material;
                Geo(group, mat).Quad(CoursePoint(inner, i, height), CoursePoint(inner, i + 1, height),
                    CoursePoint(outer, i + 1, height), CoursePoint(outer, i, height), 0.18f);
            }
        }

        private static void BuildTrack()
        {
            Geo("01_Ground", "Meadow").Box(new Vector3(0, -0.45f, 0), new Vector3(4000, 0.7f, 4000));
            Ring("02_TurfCourse", "TurfDark", 60, 77, 0.10f, true);
            Ring("03_InnerDirtCourse", "Dirt", 46, 55, 0.08f);
            Ring("04_MaintenanceRoad", "Asphalt", 85, 91, 0.04f);
            Ring("04_MaintenanceRoad", "White", 87.9f, 88.05f, 0.055f);
            Ring("05_Promenade", "Path", 78.5f, 84, 0.045f);
            Ring("03_InnerDirtCourse", "Path", 45.5f, 46, 0.09f);
            foreach (float radius in new[] { 59.8f, 77.3f, 45.6f })
            {
                string group = "06_Rails_" + radius.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
                for (int i = 0; i < segments; i++)
                {
                    Geo(group, "White").Tube(CoursePoint(radius, i, 1.35f), CoursePoint(radius, i + 1, 1.35f), 0.105f, 6);
                    Geo(group, "RailGreen").Tube(CoursePoint(radius, i, 0.65f), CoursePoint(radius, i + 1, 0.65f), 0.045f, 5);
                    if (i % 2 == 0)
                    {
                        var foot = CoursePoint(radius, i, 0.04f);
                        var top = CoursePoint(radius, i, 1.32f);
                        Geo(group, "RailGreen").Tube(foot, top, 0.08f, 8);
                        var outward = (foot - new Vector3(Mathf.Clamp(foot.x,-90,90),foot.y,0)).normalized;
                        Geo(group, "RailGreen").Tube(top, foot + outward * 0.55f, 0.065f, 6);
                        Geo(group, "White").Box(top, new Vector3(0.3f,0.13f,0.27f));
                    }
                }
            }
            // The ground and lane surfaces are static meshes; no per-frame environment scripts.
        }

        private static void BuildGrandstand()
        {
            const string g = "10_MainGrandstand";
            Geo(g, "Concrete").Box(new Vector3(0, 0.65f, -108), new Vector3(184, 1.3f, 29));
            Geo(g, "Navy").Box(new Vector3(0, 5.5f, -119), new Vector3(178, 10, 4));
            Geo(g, "Glass").Box(new Vector3(0, 8.7f, -116.85f), new Vector3(176, 3.2f, 0.25f));
            // Twelve open seating blocks with stepped concrete and coloured seat rows.
            for (int block = 0; block < 12; block++)
            {
                float x = -82.5f + block * 15;
                for (int row = 0; row < 12; row++)
                {
                    float y = 1.3f + row * 0.57f, z = -96.7f - row * 1.55f;
                    Geo(g, "Concrete").Box(new Vector3(x, y * 0.5f, z), new Vector3(13.5f, y, 1.56f));
                    string seat = (block + row / 4) % 3 == 0 ? "SeatCream" : (block % 2 == 0 ? "SeatBlue" : "SeatMint");
                    for (int col = 0; col < 13; col++)
                    {
                        float sx = x - 5.8f + col * 0.96f;
                        Geo(g, seat).Box(new Vector3(sx, y + 0.25f, z), new Vector3(0.72f, 0.16f, 0.65f));
                        Geo(g, seat).Box(new Vector3(sx, y + 0.57f, z - 0.31f), new Vector3(0.72f, 0.52f, 0.12f));
                    }
                }
                // Individual canopy bays, posts and exposed diagonal roof trusses.
                Geo(g, "Navy").Tube(new Vector3(x, 0, -117), new Vector3(x, 15, -117), 0.3f, 8);
                Geo(g, "White").Box(new Vector3(x, 14.6f, -106), new Vector3(14.6f, 0.45f, 26), Quaternion.Euler(-4, 0, 0));
                Geo(g, "Teal").Box(new Vector3(x, 13.7f, -93), new Vector3(14.6f, 1.1f, 0.32f));
                Geo(g, "Navy").Tube(new Vector3(x, 12.7f, -117), new Vector3(x, 13.8f, -94), 0.16f, 6);
                Geo(g, "Navy").Tube(new Vector3(x, 9.3f, -117), new Vector3(x, 13.8f, -100), 0.13f, 6);
            }
            Geo(g, "Teal").Box(new Vector3(0, 1.75f, -94.9f), new Vector3(182, 1.8f, 0.3f));
            Geo(g, "Gold").Box(new Vector3(0, 2.75f, -94.85f), new Vector3(182, 0.12f, 0.4f));
            for (int i = -3; i <= 3; i++)
            {
                Geo(g, "Navy").Box(new Vector3(i * 24, 1.8f, -94.6f), new Vector3(18, 1.2f, 0.1f));
                Label("StandPanel_" + i, "PROJECT RUN", new Vector3(i * 24, 1.8f, -94.48f), 0.42f, Hex("F4E7BF"), Quaternion.Euler(0, 180, 0));
            }
            // A compact hospitality tower anchors the eastern end of the stand.
            const string t = "11_Clubhouse";
            Geo(t, "White").Box(new Vector3(103, 6.3f, -110), new Vector3(22, 12.6f, 25));
            Geo(t, "Navy").Box(new Vector3(103, 14, -110), new Vector3(16, 3, 19));
            for (int floor = 0; floor < 3; floor++)
            {
                Geo(t, "Glass").Box(new Vector3(103, 2.8f + floor * 4, -97.4f), new Vector3(20, 2.8f, 0.2f));
                Geo(t, "Teal").Box(new Vector3(103, 4.5f + floor * 4, -96.8f), new Vector3(23, 0.3f, 1.6f));
            }
            Geo(t, "Gold").Box(new Vector3(103, 16, -110), new Vector3(18, 0.4f, 21));
            Label("ClubhouseTitle", "RUN PARK", new Vector3(103, 14, -100.35f), 0.85f, Color.white, Quaternion.Euler(0, 180, 0));
        }

        private static void Ellipse(string group, string material, Vector3 center, float rx, float rz, int count = 80)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count, b = (i + 1) * Mathf.PI * 2 / count;
                Geo(group, material).Triangle(center, center + new Vector3(Mathf.Cos(b) * rx, 0, Mathf.Sin(b) * rz), center + new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz));
            }
        }

        private static void BuildGarden()
        {
            Ring("20_InfieldGarden", "Path", 36, 39, 0.045f);
            Geo("20_InfieldGarden", "Path").Box(new Vector3(0, 0.02f, 0), new Vector3(5, 0.08f, 74));
            Geo("20_InfieldGarden", "Path").Box(new Vector3(0, 0.02f, 0), new Vector3(232, 0.08f, 4));
            foreach (float x in new[] { -53f, 53f })
            {
                var center = new Vector3(x, 0.1f, 13);
                Ellipse("21_ReflectingPools", "Path", center, 26, 13);
                Ellipse("21_ReflectingPools", "WaterEdge", center + Vector3.up * 0.025f, 24.3f, 11.4f);
                Ellipse("21_ReflectingPools", "Water", center + Vector3.up * 0.05f, 23, 10.2f);
                for (int i = 0; i < 4; i++)
                {
                    float px = x - 12 + i * 8;
                    Geo("21_ReflectingPools", "WaterEdge").Tube(new Vector3(px - 2.5f, 0.16f, 12), new Vector3(px + 2.5f, 0.16f, 12), 0.035f, 4);
                }
            }
            Ellipse("22_CentralPlaza", "Path", new Vector3(0, 0.1f, 0), 11, 11);
            Ellipse("22_CentralPlaza", "Teal", new Vector3(0, 0.12f, 0), 7, 7);
            Geo("22_CentralPlaza", "White").Tube(new Vector3(0, 0.15f, 0), new Vector3(0, 1.1f, 0), 3, 24);
            Geo("22_CentralPlaza", "Gold").Tube(new Vector3(0, 1.1f, 0), new Vector3(0, 3.8f, 0), 0.55f, 12);
            Geo("22_CentralPlaza", "Gold").Ellipsoid(new Vector3(0, 4.8f, 0), new Vector3(2.8f, 1.4f, 1.4f), 16, 10);
            // Planting beds are kept outside both running surfaces.
            for (int i = 0; i < 18; i++)
            {
                float x = -110 + i * 13;
                foreach (float z in new[] { -26f, 29f })
                {
                    if (Mathf.Abs(x) < 12) continue;
                    Geo("23_FlowerBorders", "Hedge").Box(new Vector3(x, 0.5f, z), new Vector3(9.4f, 1, 2.2f));
                    for (int k = 0; k < 8; k++)
                        Geo("23_FlowerBorders", i % 3 == 0 ? "Coral" : "Gold").Ellipsoid(new Vector3(x - 3.5f + k, 1.07f, z), new Vector3(0.42f, 0.2f, 0.6f), 6, 4);
                }
            }
            foreach (float x in new[] { -118f, -92f, 92f, 118f })
            foreach (float z in new[] { -17f, 17f })
                Oak(new Vector3(x, 0, z), 8 + (float)random.NextDouble() * 2);
            for (int i = 0; i < 14; i++)
            {
                float angle = i * 2 * Mathf.PI / 14;
                Rock(new Vector3(-53 + Mathf.Cos(angle) * 24, 0.12f, 13 + Mathf.Sin(angle) * 11), 0.5f + (float)random.NextDouble());
            }
            for (int i = -4; i <= 4; i++)
            {
                if (i == 0) continue;
                float x = i * 22;
                Geo("24_GardenBenches", "Trunk").Box(new Vector3(x, 0.65f, -6), new Vector3(3, 0.18f, 0.85f));
                Geo("24_GardenBenches", "Trunk").Box(new Vector3(x, 1.15f, -6.4f), new Vector3(3, 0.75f, 0.13f));
                foreach (float dx in new[] { -1f, 1f }) Geo("24_GardenBenches", "Navy").Box(new Vector3(x + dx, 0.3f, -6), new Vector3(0.16f, 0.6f, 0.7f));
            }
            // Low perimeter hedge breaks up the inner dirt course without blocking close cameras.
            for (int i = 0; i < segments; i += 4)
            {
                var a = CoursePoint(42, i, 0.65f);
                var b = CoursePoint(42, i + 4, 0.65f);
                Geo("25_InfieldHedge", "Hedge").Beam(a, b, 1.3f, 1.8f);
            }
        }

        private static void BuildFacilities()
        {
            const string g = "30_FinishAndMarkers";
            // Open finish posts: no low bar crossing a camera or runner path.
            foreach (float z in new[] { -59f, -78.3f })
            {
                Geo(g, "White").Box(new Vector3(49, 3.2f, z), new Vector3(0.55f, 6.4f, 0.55f));
                Geo(g, "Teal").Box(new Vector3(49, 5.4f, z), new Vector3(0.7f, 1.6f, 0.7f));
                Geo(g, "Gold").Ellipsoid(new Vector3(49, 6.6f, z), new Vector3(0.45f, 0.45f, 0.45f), 12, 8);
            }
            Geo(g, "White").Box(new Vector3(49, 0.13f, -68.5f), new Vector3(0.35f, 0.025f, 17));
            Label("FinishLabel", "FINISH", new Vector3(49, 4.5f, -78.7f), 0.5f, Hex("173D50"), Quaternion.identity);
            for (int i = 0; i < 8; i++)
            {
                var pos = CoursePoint(79.8f, 18 + i * 32, 0);
                Geo(g, "White").Tube(pos, pos + Vector3.up * 3, 0.11f, 8);
                var rotation = Quaternion.LookRotation((pos - new Vector3(Mathf.Clamp(pos.x, -90, 90), 0, 0)).normalized);
                Geo(g, i % 2 == 0 ? "Coral" : "Teal").Box(pos + Vector3.up * 2.8f, new Vector3(2.1f, 1, 0.14f), rotation);
                Label("CourseSector_" + i, (i + 1).ToString("00"), pos + Vector3.up * 2.8f + rotation * new Vector3(0, 0, -0.12f), 0.55f, Color.white, rotation);
            }
            // North-side large video board, directed at the main straight.
            const string s = "31_Scoreboard";
            foreach (float x in new[] { -12f, 12f }) Geo(s, "Navy").Box(new Vector3(x, 6, 101), new Vector3(0.9f, 12, 1));
            Geo(s, "Navy").Box(new Vector3(0, 13, 101), new Vector3(35, 12, 1.5f));
            Geo(s, "Teal").Box(new Vector3(0, 13, 100.2f), new Vector3(32.6f, 9.8f, 0.12f));
            Geo(s, "Gold").Box(new Vector3(0, 17.7f, 100.1f), new Vector3(32.6f, 0.15f, 0.1f));
            Label("ScoreboardHeading", "RUN PARK", new Vector3(0, 15, 100), 2.1f, Color.white, Quaternion.identity);
            Label("ScoreboardSubheading", "TURF  /  GRAND PRIX", new Vector3(0, 11.7f, 100), 0.8f, Hex("F1D382"), Quaternion.identity);
            Label("ScoreboardFootnote", "WELCOME TO THE RACE", new Vector3(0, 9.2f, 100), 0.46f, Color.white, Quaternion.identity);
            // White lamps are geometry only; sunlight is the single real-time light.
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = CoursePoint(98, i * segments / 12f + 12);
                if (p.z < -80 && Mathf.Abs(p.x) < 120) continue;
                Geo("32_LightMasts", "White").Tube(p, p + Vector3.up * 21, 0.22f, 8);
                Geo("32_LightMasts", "Navy").Box(p + Vector3.up * 20.4f, new Vector3(5.4f, 1.2f, 0.7f));
                for (int k = 0; k < 5; k++) Geo("32_LightMasts", "SeatCream").Box(p + new Vector3(-2 + k, 20.4f, -0.4f), new Vector3(0.75f, 0.75f, 0.1f));
            }
            for (int i = -5; i <= 5; i++)
            {
                var p = new Vector3(i * 17, 0, -125);
                Geo("33_Banners", "White").Tube(p, p + Vector3.up * 13, 0.09f, 6);
                Geo("33_Banners", i % 2 == 0 ? "Coral" : "Teal").Box(p + new Vector3(1.25f, 10.8f, 0), new Vector3(2.5f, 3.8f, 0.06f));
            }
        }

        private static void BuildSurroundings()
        {
            // Close trees use an existing genuinely volumetric oak mesh; distant planting is low-poly.
            for (int i = 0; i < 34; i++)
            {
                var p = CoursePoint(104 + (i % 3) * 3, i * segments / 34f);
                if (p.z < -80 && Mathf.Abs(p.x) < 130) continue;
                Oak(p, 10 + (float)random.NextDouble() * 5);
            }
            for (int i = 0; i < 100; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float x = Mathf.Cos(angle) * (215 + (float)random.NextDouble() * 45);
                float z = Mathf.Sin(angle) * (150 + (float)random.NextDouble() * 40);
                LowTree(new Vector3(x, 0, z), 7 + (float)random.NextDouble() * 8);
            }
            Geo("42_DistantHills", "Meadow").Landscape();
            // Entry walk behind the main grandstand.
            Geo("43_EntryPlaza", "Path").Box(new Vector3(0, 0.01f, -139), new Vector3(220, 0.08f, 22));
            for (int i = -6; i <= 6; i++)
            {
                LowTree(new Vector3(i * 16, 0, -150), 8);
                Geo("43_EntryPlaza", "Concrete").Box(new Vector3(i * 16, 0.4f, -150), new Vector3(4, 0.8f, 4));
            }
        }

        private static void Oak(Vector3 position, float height)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OldAsset/Environment/worldboss/etc/wb_bg_iys/oak_tree.FBX");
            var parent = Group("40_LegacyOakTrees");
            var wrapper = new GameObject("Oak").transform;
            wrapper.SetParent(parent, false);
            wrapper.position = position;
            wrapper.rotation = Quaternion.Euler(0, (float)random.NextDouble() * 360, 0);
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(source, wrapper);
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in tree.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterials = new[] { materials["OakBark"], materials["OakLeaves"] };
            }
            float scale = height / bounds.size.y;
            tree.transform.localScale *= scale;
            // Imported oak pivot is at ground level; keep source mesh and importer untouched.
            tree.transform.localPosition = new Vector3(0, -bounds.min.y * scale, 0);
            tree.isStatic = true;
        }

        private static void Rock(Vector3 position, float scale)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OldAsset/Environment/mjolnir/mj_rock001.FBX");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, Group("26_LegacyRocks"));
            go.transform.localScale = Vector3.one * scale;
            go.GetComponentInChildren<Renderer>().sharedMaterial = materials["Rock"];
            var bounds = go.GetComponentInChildren<Renderer>().bounds;
            go.transform.position += position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            go.isStatic = true;
        }

        private static void LowTree(Vector3 p, float height)
        {
            Geo("41_DistantTrees", "Trunk").Tube(p, p + Vector3.up * height * 0.7f, height * 0.035f, 6);
            for (int i = 0; i < 3; i++)
            {
                var offset = new Vector3((i - 1) * height * 0.13f, height * (0.63f + i * 0.08f), (i % 2) * height * 0.11f);
                Geo("41_DistantTrees", i % 2 == 0 ? "LeafLight" : "LeafDark").Ellipsoid(p + offset, new Vector3(height * 0.25f, height * 0.28f, height * 0.24f), 9, 6);
            }
        }

        private static Transform Group(string name)
        {
            var child = root.Find(name);
            if (child != null) return child;
            child = new GameObject(name).transform;
            child.SetParent(root, false);
            return child;
        }

        private static void Label(string name, string text, Vector3 position, float height, Color color, Quaternion rotation)
        {
            MeshLabel(text,position,height,color,rotation);
        }

        private static void AddAnchors()
        {
            var anchors = Group("50_LayoutAnchors");
            foreach (var item in new[] {
                new KeyValuePair<string, Vector3>("Finish_Center", new Vector3(49, 0.12f, -68.5f)),
                new KeyValuePair<string, Vector3>("HomeStraight_Center", new Vector3(0, 0.12f, -68.5f)),
                new KeyValuePair<string, Vector3>("BackStraight_Center", new Vector3(0, 0.12f, 68.5f)),
                new KeyValuePair<string, Vector3>("EastBend_Center", new Vector3(158.5f, 0.12f, 0)),
                new KeyValuePair<string, Vector3>("WestBend_Center", new Vector3(-158.5f, 0.12f, 0)) })
            {
                var anchor = new GameObject(item.Key).transform;
                anchor.SetParent(anchors, false);
                anchor.localPosition = item.Value;
            }
        }

        private static void ConfigureLighting()
        {
            var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (light == null) light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(48, -35, 0);
            light.color = new Color(1, 0.965f, 0.88f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.65f;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("A6BFCE");
            RenderSettings.ambientEquatorColor = Hex("85977B");
            RenderSettings.ambientGroundColor = Hex("576A45");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("B9D9E2");
            RenderSettings.fogStartDistance = 600;
            RenderSettings.fogEndDistance = 1600;
            string skyPath = artPath + "/Materials/Mat_Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetColor("_SkyTint", new Color(0.5f, 0.64f, 0.75f));
            sky.SetColor("_GroundColor", Hex("9DBBA9"));
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetFloat("_Exposure", 1.15f);
            sky.SetFloat("_SunSize", 0.025f);
            RenderSettings.skybox = sky;
            EditorUtility.SetDirty(sky);
        }

        [MenuItem("Tools/Project Run/All 3D/Camera - overview")]
        public static void ViewOverview() => SetCamera(new Vector3(430, 410, 225), new Vector3(0, 0, -12), 50);

        [MenuItem("Tools/Project Run/All 3D/Camera - home straight")]
        public static void ViewHomeStraight() => SetCamera(new Vector3(-40, 6.5f, -55), new Vector3(20, 1.5f, -71), 48);

        [MenuItem("Tools/Project Run/All 3D/Camera - bend")]
        public static void ViewBend() => SetCamera(new Vector3(172, 13, -58), new Vector3(150, 0, -19), 53);

        private static void SetCamera(Vector3 position, Vector3 target, float fov)
        {
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/Scene_Oval.unity") return;
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera", typeof(AudioListener)).AddComponent<Camera>();
                camera.tag = "MainCamera";
            }
            Undo.RecordObject(camera.transform, "Racecourse camera view");
            Undo.RecordObject(camera, "Racecourse camera view");
            camera.transform.position = position;
            camera.transform.LookAt(target);
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.15f;
            camera.farClipPlane = 1500;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null) cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.renderPostProcessing = true;
            RenderSettings.fogStartDistance = position.y > 100 ? 900 : 180;
            RenderSettings.fogEndDistance = position.y > 100 ? 1800 : 900;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private sealed class Geometry
        {
            private readonly string group;
            private readonly string material;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            private readonly Dictionary<int, Vector3> smoothNormals = new Dictionary<int, Vector3>();

            public Geometry(string group, string material) { this.group = group; this.material = material; }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(a.x, a.z) * 0.3f); uv.Add(new Vector2(b.x, b.z) * 0.3f); uv.Add(new Vector2(c.x, c.z) * 0.3f);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float tiling = 0.3f)
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                foreach (var p in new[] { a, b, c, d }) uv.Add(new Vector2(p.x, p.z) * tiling);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
                triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
            }

            public void GlyphQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,CharacterInfo glyph)
            {
                int start=vertices.Count;Quad(a,b,c,d);
                uv[start]=glyph.uvBottomLeft;uv[start+1]=glyph.uvTopLeft;
                uv[start+2]=glyph.uvTopRight;uv[start+3]=glyph.uvBottomRight;
            }

            public void AppendMesh(Mesh source,Matrix4x4 matrix)
            {
                int start=vertices.Count;var points=source.vertices;var tex=source.uv;var normals=source.normals;
                var normalMatrix=matrix.inverse.transpose;
                for(int i=0;i<points.Length;i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(points[i]));uv.Add(tex[i]);
                    smoothNormals[start+i]=normalMatrix.MultiplyVector(normals[i]).normalized;
                }
                foreach(int index in source.triangles)triangles.Add(start+index);
            }

            public void Box(Vector3 center, Vector3 size, Quaternion rotation = default)
            {
                if (rotation == default) rotation = Quaternion.identity;
                Vector3 h = size * 0.5f;
                Vector3[] v = { new Vector3(-h.x,-h.y,-h.z), new Vector3(h.x,-h.y,-h.z), new Vector3(h.x,h.y,-h.z), new Vector3(-h.x,h.y,-h.z),
                    new Vector3(-h.x,-h.y,h.z), new Vector3(h.x,-h.y,h.z), new Vector3(h.x,h.y,h.z), new Vector3(-h.x,h.y,h.z) };
                for (int i = 0; i < v.Length; i++) v[i] = center + rotation * v[i];
                Quad(v[0],v[3],v[2],v[1]); Quad(v[4],v[5],v[6],v[7]);
                Quad(v[0],v[4],v[7],v[3]); Quad(v[1],v[2],v[6],v[5]);
                Quad(v[3],v[7],v[6],v[2]); Quad(v[0],v[1],v[5],v[4]);
            }

            public void Beam(Vector3 a, Vector3 b, float height, float width)
                => Box((a + b) * 0.5f, new Vector3(width, height, Vector3.Distance(a, b) + 0.05f), Quaternion.LookRotation(b - a));

            public void Tube(Vector3 a, Vector3 b, float radius, int sides)
            {
                var direction = (b - a).normalized;
                var right = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.95f ? Vector3.forward : Vector3.up).normalized;
                var up = Vector3.Cross(direction, right).normalized;
                for (int i = 0; i < sides; i++)
                {
                    float t = i * Mathf.PI * 2 / sides, next = (i + 1) * Mathf.PI * 2 / sides;
                    Vector3 p = (right * Mathf.Cos(t) + up * Mathf.Sin(t)) * radius;
                    Vector3 q = (right * Mathf.Cos(next) + up * Mathf.Sin(next)) * radius;
                    Quad(a + p, a + q, b + q, b + p);
                    Triangle(a, a + q, a + p); Triangle(b, b + p, b + q);
                }
            }

            public void Ellipsoid(Vector3 center, Vector3 radius, int slices, int rings)
            {
                for (int y = 0; y < rings; y++)
                for (int x = 0; x < slices; x++)
                {
                    Vector3 Point(int ix, int iy)
                    {
                        float a = ix * Mathf.PI * 2 / slices, b = iy * Mathf.PI / rings;
                        return center + Vector3.Scale(radius, new Vector3(Mathf.Sin(b) * Mathf.Cos(a), Mathf.Cos(b), Mathf.Sin(b) * Mathf.Sin(a)));
                    }
                    int start = vertices.Count;
                    Quad(Point(x,y), Point(x+1,y), Point(x+1,y+1), Point(x,y+1));
                    for (int i = start; i < start + 4; i++)
                    {
                        var p = vertices[i] - center;
                        smoothNormals[i] = new Vector3(p.x / (radius.x * radius.x), p.y / (radius.y * radius.y), p.z / (radius.z * radius.z)).normalized;
                    }
                }
            }

            public void Landscape()
            {
                float Height(float x, float z)
                {
                    float distance = new Vector2(x / 300f, z / 240f).magnitude;
                    float fade = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 1.9f, distance));
                    float noise = Mathf.PerlinNoise(x * 0.004f + 17, z * 0.004f + 29);
                    float detail = Mathf.PerlinNoise(x * 0.01f + 3, z * 0.01f + 7);
                    return -0.13f + fade * (12 + noise * 90 + detail * 14);
                }
                const int step = 25;
                for (int z = -1000; z < 1000; z += step)
                for (int x = -1000; x < 1000; x += step)
                {
                    int start = vertices.Count;
                    Quad(new Vector3(x, Height(x,z), z), new Vector3(x, Height(x,z+step), z+step),
                        new Vector3(x+step, Height(x+step,z+step), z+step), new Vector3(x+step, Height(x+step,z), z));
                    for (int i = start; i < start + 4; i++)
                    {
                        var p = vertices[i];
                        smoothNormals[i] = new Vector3((Height(p.x-1,p.z)-Height(p.x+1,p.z))*0.5f, 1,
                            (Height(p.x,p.z-1)-Height(p.x,p.z+1))*0.5f).normalized;
                    }
                }
            }

            public void Save(Transform parent)
            {
                string name = group + "_" + material;
                string path = artPath + "/Meshes/" + name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
                mesh.Clear();
                mesh.name = name;
                mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                if (smoothNormals.Count > 0)
                {
                    var normals = mesh.normals;
                    foreach (var item in smoothNormals) normals[item.Key] = item.Value;
                    mesh.normals = normals;
                }
                EditorUtility.SetDirty(mesh);
                var go = new GameObject(material, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(Group(group), false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = materials[material];
                go.isStatic = true;
                if (group == "02_TurfCourse" || group == "03_InnerDirtCourse") go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }
}
