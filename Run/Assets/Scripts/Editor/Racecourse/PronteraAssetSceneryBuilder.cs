using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Arranges imported environment models. Does not generate decorative meshes.</summary>
    public static class PronteraAssetSceneryBuilder
    {
        public const string ScenePath = "Assets/Scenes/Scene_PronteraToon.unity";
        public const string PrefabPath = "Assets/Resources/Prefabs/Environment/P_Racecourse_PronteraAssets.prefab";
        private const string source = "Assets/OldAsset/Environment/";
        private const string art = "Assets/ArtBundles/Racecourse/PronteraAssets";
        private const string baseline = "Assets/Resources/Prefabs/Environment/P_Racecourse_PronteraWorld.prefab";
        private static readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
        private static readonly HashSet<string> usedSources = new HashSet<string>();
        private static readonly string[] foundationNames = {
            "obj_terrain_WorldGround", "obj_town_paving_Cobble", "obj_road_Road", "obj_road_Cobble",
            "obj_road_DesertRoad", "obj_road_Wood", "obj_river_pool_Water",
            "obj_oasis_Water", "obj_distant_water_DistantWater"
        };
        private static Transform root, forest, town, desert, borders;
        private static Data_RaceCourse course;
        private static MeshCollider ground;
        private static System.Random random;
        private static Scene scene;

        [MenuItem("Tools/Project Run/Prontera World/Arrange existing environment assets")]
        public static string Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes first.");
            Directory.CreateDirectory(art + "/Materials"); AssetDatabase.Refresh();
            scene = EditorSceneManager.OpenScene(ScenePath);
            course = Object.FindFirstObjectByType<Sys_Race>().Course;
            if (course == null || !course.IsValid) throw new InvalidOperationException("Existing course is missing.");
            materials.Clear(); usedSources.Clear(); random = new System.Random(20261018);
            foreach (var old in scene.GetRootGameObjects())
                if (old.name == "P_Racecourse_PronteraWorld" || old.name == "P_Racecourse_PronteraAssets") Object.DestroyImmediate(old);
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(baseline);
            root = ((GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, scene)).transform;
            PrefabUtility.UnpackPrefabInstance(root.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            // Keep the established driving surface and water planes. Every visible prop below is an imported model.
            foreach (Transform child in root.Cast<Transform>().ToArray())
            {
                if (foundationNames.Contains(child.name)) continue;
                if (child.name == "obj_payon_forest")
                {
                    foreach (Transform item in child.Cast<Transform>().ToArray())
                        if (item.name != "obj_forest_canopy" && !item.name.StartsWith("obj_payon_")) Object.DestroyImmediate(item.gameObject);
                    RestoreSourceMaterials(child);
                    continue;
                }
                Object.DestroyImmediate(child.gameObject);
            }
            forest = root.Find("obj_payon_forest"); town = Group("obj_prontera_town");
            desert = Group("obj_sograt_desert"); borders = Group("obj_trackside_assets");
            var terrain = root.Find("obj_terrain_WorldGround");
            ground = terrain.gameObject.AddComponent<MeshCollider>(); ground.sharedMesh = terrain.GetComponent<MeshFilter>().sharedMesh;
            Physics.SyncTransforms();
            ForestVerge(); Town(); Desert(); Trackside(); Bridge(); ClearDrivingCorridor();
            Object.DestroyImmediate(ground); ground = null;
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            string result = Validate();
            Directory.CreateDirectory("DataBundles/검증/프론테라에셋_20261017");
            File.WriteAllText("DataBundles/검증/프론테라에셋_20261017/asset_sources.txt", string.Join("\n", usedSources.OrderBy(x => x)));
            File.WriteAllText("DataBundles/검증/프론테라에셋_20261017/validation.txt", result);
            return result;
        }

        private static Transform Group(string name)
        { var t = new GameObject(name).transform; t.SetParent(root, false); return t; }
        private static float Rand(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private static float GroundY(Vector3 p)
        {
            if (ground.Raycast(new Ray(new Vector3(p.x, 500, p.z), Vector3.down), out var hit, 800)) return hit.point.y;
            return -60;
        }
        private static Vector3 OnGround(float x, float z) { var p = new Vector3(x, 0, z); p.y = GroundY(p); return p; }
        private static float Clearance(Vector3 p)
        {
            float closest = float.MaxValue;
            for (int i = 0; i < course.Points.Length - 3; i += 3)
            {
                var a = course.Points[i]; var b = course.Points[i + 3]; a.y = b.y = p.y;
                var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(.01f, ab.sqrMagnitude));
                closest = Mathf.Min(closest, Vector3.Distance(p, a + ab * t));
            }
            return closest;
        }
        private static float Nearest(float x, float z)
        {
            int index = 0; float best = float.MaxValue;
            for (int i = 0; i < course.Points.Length; i++)
            { float distance = (new Vector2(course.Points[i].x - x, course.Points[i].z - z)).sqrMagnitude; if (distance < best) { best = distance; index = i; } }
            return course.Distances[index];
        }

        private static Material Convert(Material original)
        {
            if (original == null) throw new InvalidOperationException("Source model has an unassigned material.");
            if (materials.TryGetValue(original, out var result)) return result;
            string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
            string name = string.Concat(original.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
            string path = art + "/Materials/Mat_" + name + "_" + id + ".mat";
            result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null) { result = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(result, path); }
            result.shader = Shader.Find("Universal Render Pipeline/Lit");
            // The source UVs, albedo, and atlas mapping carry the authored surface detail.
            result.SetTexture("_BaseMap", original.mainTexture);
            result.SetTextureScale("_BaseMap", original.mainTextureScale);
            result.SetTextureOffset("_BaseMap", original.mainTextureOffset);
            result.SetColor("_BaseColor", Color.white); result.SetFloat("_Metallic", 0); result.SetFloat("_Smoothness", .08f);
            result.SetFloat("_Cull", 0); result.SetFloat("_AlphaClip", 1); result.SetFloat("_Cutoff", .35f);
            result.EnableKeyword("_ALPHATEST_ON"); result.renderQueue = 2450; result.SetOverrideTag("RenderType", "TransparentCutout");
            result.enableInstancing = true; EditorUtility.SetDirty(result); materials[original] = result; return result;
        }
        private static void RestoreSourceMaterials(Transform parent)
        {
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>(true))
            {
                var original = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer);
                if (original == null) throw new InvalidOperationException("Source renderer was lost: " + renderer.name);
                renderer.sharedMaterials = original.sharedMaterials.Select(Convert).ToArray();
                usedSources.Add(AssetDatabase.GetAssetPath(original));
            }
        }
        private static Transform Place(string relative, Vector3 p, float height, float yaw, Transform parent, string part = null, float width = 0)
        {
            string path = source + relative;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Missing source model " + path);
            var wrapper = new GameObject("obj_" + (part ?? Path.GetFileNameWithoutExtension(relative)).Replace(' ', '_')).transform;
            wrapper.SetParent(parent, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene); instance.transform.SetParent(wrapper, false);
            var selected = part == null ? null : part.Split('|');
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.EndsWith("_indi") || renderer.name.Contains("indicatorskill") || renderer.name.EndsWith("_glow"))
                { renderer.gameObject.SetActive(false); continue; }
                if (selected != null && !selected.Contains(renderer.name)) { renderer.gameObject.SetActive(false); continue; }
                renderer.sharedMaterials = renderer.sharedMaterials.Select(Convert).ToArray();
            }
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            var visible = instance.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (visible.Length == 0) throw new InvalidOperationException("No visible source part: " + path + " / " + part);
            var bounds = visible[0].bounds; foreach (var r in visible) bounds.Encapsulate(r.bounds);
            float scale = width > 0 ? width / Mathf.Max(.01f, bounds.size.x) : height / Mathf.Max(.01f, bounds.size.y);
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            wrapper.localScale = Vector3.one * scale; wrapper.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            usedSources.Add(path + (part == null ? "" : " # " + part)); return wrapper;
        }

        private static void ForestVerge()
        {
            string[] shrubs = { "pronterafield_pull_01", "pronterafield_pull_02", "pronterafield_pull_03" };
            string[] stones = { "pronterafield_stone_01", "pronterafield_stone_02", "pronterafield_stone_03" };
            for (float s = 5; s < course.Length; s += 8)
            {
                var center = course.Point(s); if (center.x > 80 || (center.z > 30 && center.x > -88)) continue;
                float yaw = Quaternion.LookRotation(course.Forward(s)).eulerAngles.y;
                for (int side = -1; side <= 1; side += 2)
                {
                    var p = course.Point(s, side * Rand(14.5f, 19)); p.y = GroundY(p);
                    if (p.y < -3 || (Mathf.Abs(p.z + 89) < 14 && p.x < -85)) continue;
                    Place("pronterafield/pronterafield_nature_01.FBX", p, Rand(.9f, 1.65f), yaw + (side > 0 ? 90 : -90), forest, shrubs[(int)s % 3]);
                    if ((int)s % 3 == 0)
                    { var flower = p + new Vector3(Rand(-2, 2), 0, Rand(-2, 2)); flower.y = GroundY(flower); Place("pronterafield/pf_flower_01.FBX", flower, Rand(.7f, 1.2f), yaw + 90, forest); }
                    if ((int)s % 5 == 0)
                    { var rock = course.Point(s + 3, side * Rand(17, 21)); rock.y = GroundY(rock) - .15f; if (rock.y > -3) Place("pronterafield/pronterafield_nature_01.FBX", rock, Rand(.65f, 1.2f), Rand(0, 360), forest, stones[(int)s % 3]); }
                }
            }
            // A small number of textured boulders at the water edge replaces the repeated gray rock row.
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2 / 28; var p = OnGround(-12 + Mathf.Sin(a) * 64, -67 + Mathf.Cos(a) * 42);
                if (p.y < -4.3f || Clearance(p) < 15) continue;
                Place("pronterafield/pronterafield_nature_01.FBX", p - Vector3.up * .25f, Rand(1.3f, 3.5f), Rand(0, 360), forest, stones[i % 3]);
            }
            // Original trees form a layered skyline without procedural rock towers.
            for (int i = 0; i < 58; i++)
            {
                var p = OnGround(Rand(-240, -95), Rand(-155, 205));
                if (p.y < -3 || Clearance(p) < 23) continue;
                Place("worldboss/etc/wb_bg_iys/oak_tree.FBX", p, Rand(15, 25), Rand(0, 360), forest);
            }
        }

        private static void Town()
        {
            Place("prontera_city/prontera_castle_fountain.FBX", new Vector3(0, 42, 98), 11, 0, town);
            Place("prontera_city/prontera_to_01_library.FBX", new Vector3(48, 42, 154), 29, 0, town);
            Place("prontera_city/prontera_to_01_castle.FBX", new Vector3(-39, 42, 164), 35, 0, town);
            Place("prontera_city/prontera_castle_flower_garden.FBX", new Vector3(24, 42, 123), 8, 0, town, "prontera_castle_flower_garden_mesh|prontera_castle_flower_garden_mesh_badak");
            int index = 0;
            for (float z = 34; z < 163; z += 23) for (float x = -69; x <= 69; x += 23)
            {
                var p = new Vector3(x, 42, z); if (Clearance(p) < 22 || Vector2.Distance(new Vector2(x, z), new Vector2(0, 100)) < 30 || z > 123) continue;
                float yaw = z < 60 ? 180 : x < 0 ? -75 : 75;
                if (index % 4 == 0) Place("prontera_city/prontera_castle_general_store.FBX", p, 13, yaw, town);
                else if (index % 4 == 1) Place("prontera_city/prontera_castle_house.FBX", p, 14, yaw, town, "prontera_castle_house_0" + (index % 5 + 1));
                else Place("pronterafield/pronterafield_house_01.FBX", p, 15, yaw, town);
                index++;
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -79 + i * 19; var p = OnGround(x, 196 + (i % 2) * 14);
                Place("prontera_city/prontera_castle_house.FBX", p, Rand(14, 20), 180, town, "prontera_castle_house_0" + (i % 5 + 1));
            }
            // Authored stone balustrades also supply the exposed city foundation.
            for (float x = -83; x <= 83; x += 8.2f)
            { Place("izlude_city/izcity_fence001a.FBX", new Vector3(x, 32.4f, 25), 11.6f, 0, town); Place("izlude_city/izcity_fence001a.FBX", new Vector3(x, 32.4f, 182), 11.6f, 180, town); }
            for (float z = 32; z <= 175; z += 8.2f)
            {
                if (Mathf.Abs(z - 96) < 22) continue;
                Place("izlude_city/izcity_fence001a.FBX", new Vector3(-87, 32.4f, z), 11.6f, 90, town);
                Place("izlude_city/izcity_fence001a.FBX", new Vector3(87, 32.4f, z), 11.6f, -90, town);
            }
            foreach (var p in new[] { new Vector3(-25,42,116),new Vector3(27,42,103),new Vector3(-64,42,133),new Vector3(65,42,131),new Vector3(-13,42,129) })
            {
                Place("worldboss/etc/wb_bg_iys/oak_tree.FBX", p, 15, Rand(0, 360), town);
                Place("prontera_city/prontera_castle_flower_garden.FBX", p, .65f, 0, town, "Object107");
            }
            for (float s = 0; s < course.Length; s += 19)
            {
                var p = course.Point(s); if (Mathf.Abs(p.x) > 80 || p.z < 38) continue;
                float yaw = Quaternion.LookRotation(course.Forward(s)).eulerAngles.y;
                for (int side = -1; side <= 1; side += 2)
                {
                    var edge = course.Point(s, side * 12.6f); edge.y = 42;
                    Place("prontera_city/prontera_city_large_structure_01.FBX", edge, 5.5f, yaw + (side < 0 ? 90 : -90), town, "prontera_city_large_structure_02");
                    var flowers = course.Point(s + 5, side * 13); flowers.y = 42;
                    Place("pronterafield/pf_flower_01.FBX", flowers, 1.4f, yaw + 90, town);
                }
            }
            foreach (float x in new[] { -19f, 19f })
            {
                Place("prontera_city/prontera_city_bench_01.FBX", new Vector3(x, 42, 115), 1.2f, x < 0 ? 90 : -90, town, "prontera_city_bench_02");
                Place("prontera_city/prontera_city_small_structure_01.FBX", new Vector3(x, 42, 117.5f), 1, 0, town, "prontera_city_small_structure_01 1");
            }
            // Readable storefronts and planted benches line the plaza, with a clear racing corridor.
            foreach (var p in new[] { new Vector3(-59,42,125),new Vector3(61,42,126),new Vector3(-69,42,64),new Vector3(70,42,60) })
            {
                float yaw = p.x < 0 ? 90 : -90;
                Place("prontera_city/prontera_castle_general_store.FBX", p, 14, yaw, town);
                Place("prontera_city/prontera_castle_card_carriage.FBX", p + new Vector3(p.x < 0 ? 11 : -11,0,-6), 4.5f, yaw, town);
            }
            Place("prontera_city/prontera_castle_swordsman_guild.FBX", new Vector3(-3,42,143), 20, 0, town);
            Place("prontera_city/prontera_castle_adventurer_guild.FBX", new Vector3(-70,42,149), 23, 0, town);
            Place("prontera_city/prontera_castle_adventurer_guild.FBX", new Vector3(71,42,153), 20, 0, town);
            foreach (var p in new[] {new Vector3(-29,42,107),new Vector3(29,42,110),new Vector3(-17,42,86),new Vector3(17,42,86),new Vector3(-44,42,117),new Vector3(45,42,119)})
                Place("prontera_city/prontera_city_nature_02.FBX", p, 1.6f, p.x < 0 ? 90 : -90, town);
            for (float s = 0; s < course.Length; s += 12)
            {
                var p = course.Point(s); if (Mathf.Abs(p.x) > 75 || p.z < 38) continue;
                float yaw = Quaternion.LookRotation(course.Forward(s)).eulerAngles.y;
                var edge = course.Point(s, 15.5f); edge.y = 42;
                Place("prontera_city/prontera_city_nature_02.FBX", edge, 1.4f, yaw + 90, town);
            }
        }

        private static void Desert()
        {
            // Source sandstone formations retain their own texture, silhouette and UVs.
            for (int i = 0; i < 38; i++)
            {
                var p = OnGround(Rand(104, 250), Rand(-130, 175));
                if (p.y < 0 || Clearance(p) < 24 || Vector2.Distance(new Vector2(p.x, p.z), new Vector2(157, -4)) < 35) continue;
                if (i % 3 == 0)
                    Place("housing_outside_town/town_morocc/housing_outside_town_morocc_stone.FBX", p - Vector3.up * .6f, Rand(8, 16), Rand(0, 360), desert);
                else Place("tutorial/hsd_rock001.FBX", p - Vector3.up * .35f, Rand(2.5f, 6), Rand(0, 360), desert);
            }
            for (int i = 0; i < 23; i++)
            {
                float a = i * 2.39996f, radius = Rand(27, 38); var p = OnGround(157 + Mathf.Cos(a) * radius, -4 + Mathf.Sin(a) * radius);
                if (Clearance(p) < 19) continue;
                Place("housing_outside_town/town_morocc/housing_outside_town_morocc_tree01.FBX", p, Rand(11, 16), Rand(0, 360), desert);
                Place("housing_outside_town/town_morocc/housing_outside_town_morocc_cactus.FBX", p + Vector3.right * 3, Rand(1.7f, 2.6f), Rand(0, 360), desert);
            }
            for (float s = 0; s < course.Length; s += 11)
            {
                var p = course.Point(s); if (p.x < 110 || p.z < -120) continue;
                int side = ((int)s % 2) == 0 ? -1 : 1; var edge = course.Point(s, side * 16); edge.y = GroundY(edge);
                if (edge.y < 0 || Vector2.Distance(new Vector2(edge.x, edge.z), new Vector2(157, -4)) < 25) continue;
                Place("housing_outside_town/town_morocc/housing_outside_town_morocc_cactus.FBX", edge, Rand(2, 3.3f), Rand(0, 360), desert);
                var stone = course.Point(s + 4, side * Rand(14, 19)); stone.y = GroundY(stone) - .15f;
                Place("tutorial/hsd_rock001.FBX", stone, Rand(.8f, 1.8f), Rand(0, 360), desert);
                var pebbles = course.Point(s - 2, -side * Rand(13, 16)); pebbles.y = GroundY(pebbles) + .04f;
                Place("desert/moroc_fieldstone001.FBX", pebbles, 0, Rand(0, 360), desert, null, Rand(2, 3.5f));
            }
            // Palm clusters on the outer bend keep the oasis visible from the race camera.
            for (float s = 745; s < 955; s += 42)
            {
                var left = course.Point(s, -28); var right = course.Point(s, 28);
                var oasis = new Vector3(157,0,-4); left.y = right.y = 0;
                var p = (left - oasis).sqrMagnitude > (right - oasis).sqrMagnitude ? left : right;
                p.y = GroundY(p); if (p.y < 0 || Clearance(p) < 22) continue;
                Place("housing_outside_town/town_morocc/housing_outside_town_morocc_tree01.FBX", p, Rand(13, 17), Rand(0, 360), desert);
                var companion = p + new Vector3(5,0,5); companion.y = GroundY(companion);
                Place("housing_outside_town/town_morocc/housing_outside_town_morocc_tree01.FBX", companion, Rand(9, 12), Rand(0,360), desert);
            }
            Place("desert/moroc_tent001a.FBX", OnGround(207, -6), 11, -90, desert);
            Place("desert/moroc_tent001a.FBX", OnGround(190, -27), 7, -60, desert);
            Place("desert_city2/morocc_city_smithy.FBX", OnGround(205, 28), 15, -90, desert);
            for (int i = 0; i < 5; i++)
            {
                var p = OnGround(195 + (i % 2) * 17, 62 + i * 22);
                Place("desert_city2/morocc_wall.FBX", p, Rand(8, 12), 15 + i * 37, desert);
                var pillar = p + new Vector3(9, 0, -7); pillar.y = GroundY(pillar);
                Place("pyramid_dn/prmd_dn_brickpillar001.FBX", pillar, Rand(10, 15), i * 23, desert, "prmd_dn_brickpillar001");
            }
        }

        private static void Trackside()
        {
            for (float s = 0; s < course.Length; s += 4)
            {
                var p = course.Point(s); bool city = Mathf.Abs(p.x) < 82 && p.z > 40;
                float yaw = Quaternion.LookRotation(course.Forward(s)).eulerAngles.y;
                for (int side = -1; side <= 1; side += 2)
                {
                    var edge = course.Point(s, side * 10.7f);
                    if (Mathf.Abs(s - Nearest(-139, -89)) < 18) continue;
                    if (city) Place("izlude_city/izcity_fence002a.FBX", edge - Vector3.up * .12f, 0, yaw + 90, borders, null, 4);
                    else Place("pronterafield/pf_wood_fence_01.FBX", edge - Vector3.up * .25f, 1.45f, yaw + 90, borders);
                }
            }
            foreach (float s in new[] { 0f, Nearest(-87, 96), Nearest(87, 96) })
            {
                float yaw = Quaternion.LookRotation(course.Forward(s)).eulerAngles.y;
                for (int side = -1; side <= 1; side += 2)
                    Place("prontera_city/prontera_city_large_structure_01.FBX", course.Point(s, side * 12), 7.5f, yaw, borders, "prontera_city_large_structure_02");
            }
            // Source signposts mark the regional junctions without fabricated archways.
            foreach (var location in new[] { new Vector2(-98,-28),new Vector2(-87,96),new Vector2(87,96),new Vector2(120,-124) })
            {
                float s = Nearest(location.x, location.y); var p = course.Point(s, 13.5f); p.y = GroundY(p);
                Place("prontera_city/prontera_city_large_structure_01.FBX", p, 4.5f, Quaternion.LookRotation(course.Forward(s)).eulerAngles.y, borders, "prontera_city_large_structure_03");
            }
        }

        private static void Bridge()
        {
            float center = Nearest(-139, -89);
            for (int row = -6; row <= 6; row++) for (int col = -1; col <= 1; col++)
            {
                float s = center + row * 2.8f; var p = course.Point(s, col * 6); p.y += .02f;
                var model = Place("pronterafield/prontera_wood_bridge.FBX", p, 0, 0, borders, null, 6.15f);
                model.localScale = Vector3.Scale(model.localScale, new Vector3(1, .5f, .29f));
                model.rotation = Quaternion.LookRotation(course.Forward(s));
            }
            for (float s = center - 18; s <= center + 18; s += 5.5f)
                for (int side = -1; side <= 1; side += 2)
                    Place("mjolnir/mj_woodenbridge001.FBX", course.Point(s, side * 10.3f) - Vector3.up * 3.7f, 5.2f,
                        Quaternion.LookRotation(course.Forward(s)).eulerAngles.y + 90, borders);
        }

        private static void ClearDrivingCorridor()
        {
            var remove = new HashSet<GameObject>();
            foreach (var renderer in forest.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.name != "oak_tree") continue;
                bool overlaps = false;
                for (float s = 0; s < course.Length && !overlaps; s += 3)
                    foreach (float lane in new[] { -7f, 0f, 7f })
                        if (renderer.bounds.Contains(course.Point(s, lane) + Vector3.up * 1.3f)) { overlaps = true; break; }
                if (!overlaps) continue;
                var item = renderer.transform;
                while (item.parent != forest && item.parent.name != "obj_forest_canopy") item = item.parent;
                remove.Add(item.gameObject);
            }
            foreach (var item in remove) Object.DestroyImmediate(item);
        }

        public static string Validate()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path != ScenePath) throw new InvalidOperationException("Open the cartoon Prontera scene.");
            var environment = GameObject.Find("P_Racecourse_PronteraWorld");
            if (environment == null) throw new InvalidOperationException("Environment is missing.");
            var failures = new List<string>(); var sources = new HashSet<string>(); int imported = 0;
            foreach (var filter in environment.GetComponentsInChildren<MeshFilter>())
            {
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (path.StartsWith(source)) { imported++; sources.Add(path); }
                else if (!foundationNames.Contains(filter.name)) failures.Add("Non-source decoration: " + filter.name);
            }
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>())
                if (renderer.sharedMaterials.Any(m => m == null || m.shader == null || m.shader.name.Contains("InternalError"))) failures.Add("Invalid material: " + renderer.name);
            var race = Object.FindFirstObjectByType<Sys_Race>();
            if (race.Entrants.Length != 6 || race.Entrants.Any(e => !AssetDatabase.GetAssetPath(e.Prefab).Contains("/CartoonMonsters/"))) failures.Add("Cartoon roster changed.");
            string report = "Scene: " + active.path + "\nImported scenery mesh instances: " + imported + "\nDistinct source FBX files: " + sources.Count +
                "\nDecorative meshes are imported assets; existing road, terrain and water surfaces retained.\nCourse: " + race.Course.Length.ToString("F2") + " m; cartoon monsters: " + race.Entrants.Length +
                "\nIssues: " + (failures.Count == 0 ? "NONE" : string.Join("; ", failures.Distinct()));
            if (failures.Count > 0) throw new InvalidOperationException(report);
            return report;
        }
    }
}
