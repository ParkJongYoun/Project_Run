using System;
using System.Collections.Generic;
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
        public const string ScenePath = "Assets/Scenes/Scene_2D.unity";
        private const string art = SpriteRaceCapture.Art;
        private static Transform environment;
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static int meshIndex;
        public static float TrackLength => 160 + Mathf.PI * 56;

        public static Vector3 TrackPoint(float distance, float lane = 0)
        {
            float s = Mathf.Repeat(distance + 20, TrackLength), radius = 28 + lane;
            if (s < 80) return new Vector3(-40 + s, .14f, -radius);
            s -= 80;
            if (s < Mathf.PI * 28) { float a = s / 28 - Mathf.PI / 2; return new Vector3(40 + Mathf.Cos(a) * radius, .14f, Mathf.Sin(a) * radius); }
            s -= Mathf.PI * 28;
            if (s < 80) return new Vector3(40 - s, .14f, radius);
            s -= 80;
            float angle = s / 28 + Mathf.PI / 2;
            return new Vector3(-40 + Mathf.Cos(angle) * radius, .14f, Mathf.Sin(angle) * radius);
        }

        // Retained for the previous 3D-backdrop prototype; the normal builder now creates a flat 2D course.
        public static string Build3DBackdrop()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Current scene has unsaved edits.");
            var sets = SpriteRaceCapture.Ids.Select(id => AssetDatabase.LoadAssetAtPath<Data_DirectionalRunSprites>(SpriteRaceCapture.Sets + "/Run_" + id + ".asset")).ToArray();
            if (sets.Any(s => s == null)) throw new InvalidOperationException("Capture all six monsters first.");
            foreach (string folder in new[] { art + "/Meshes", art + "/Materials", "Assets/Resources/Race2D", "Assets/Resources/Prefabs/Race/Sprites2D" }) SpriteRaceCapture.Ensure(folder);
            materials.Clear(); meshIndex = 0;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Scene_2D";
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.78f, .83f, .8f);
            RenderSettings.fog = false;
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.color = new Color(1, .96f, .87f);
            light.transform.rotation = Quaternion.Euler(50, -35, 0); light.shadows = LightShadows.Soft;
            light.shadowStrength = .55f; light.shadowBias = .06f; light.shadowNormalBias = .25f;
            RenderSettings.sun = light;
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 17;
            camera.nearClipPlane = .1f; camera.farClipPlane = 500;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Hex("B8D9C4");
            camera.allowHDR = false;
            camera.gameObject.AddComponent<AudioListener>();
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = false; additional.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            environment = new GameObject("[Environment] 3D meadow racecourse").transform;
            BuildEnvironment();
            var course = Asset<Data_RaceCourse>("Assets/Resources/Race2D/Course_Meadow.asset");
            course.CourseName = "초원 몬스터 컵"; course.RoadWidth = 20;
            course.Points = new Vector3[673]; course.Distances = new float[673];
            for (int i = 0; i < course.Points.Length; i++) { course.Distances[i] = TrackLength * i / 672; course.Points[i] = TrackPoint(course.Distances[i]); }
            EditorUtility.SetDirty(course);
            var systems = new GameObject("[Systems]").transform;
            var race = Child(systems, "Sys_Race").AddComponent<Sys_Race>();
            race.Course = course; race.CountdownSeconds = 3; race.PlaybackSpeed = 1;
            var director = Child(systems, "Sys_SpriteRaceCamera").AddComponent<Sys_SpriteRaceCamera>();
            director.Race = race; director.RaceCamera = camera;
            race.Entrants = new Data_RunnerSetup[6];
            var colors = new[] { Hex("FFC65A"), Hex("EF8DAC"), Hex("B596E4"), Hex("F0985A"), Hex("78C9E0"), Hex("96C768") };
            float[] speeds = { 12.5f, 12.25f, 12.4f, 12.2f, 12.55f, 12.35f };
            var runners = new GameObject("[Runners] 2D sprites only").transform;
            for (int i = 0; i < 6; i++)
            {
                var root = new GameObject("P_Runner_2D_" + SpriteRaceCapture.Ids[i]);
                var view = root.AddComponent<RaceDirectionalSpriteView>();
                view.SpriteSet = sets[i]; view.RunnerId = i;
                var display = Child(root.transform, "img_directional_monster").AddComponent<SpriteRenderer>();
                display.sharedMaterial = sets[i].SpriteMaterial; display.shadowCastingMode = ShadowCastingMode.Off;
                display.sprite = sets[i].Frame(4, 0); display.transform.rotation = Quaternion.Euler(38, 35, 0);
                view.Display = display;
                var shadow = Primitive(root.transform, "obj_ground_shadow", PrimitiveType.Quad, new Vector3(0, .035f, 0), new Vector3(2.5f, 1.65f, 1), Mat("shadow", "29443D"));
                shadow.transform.rotation = Quaternion.Euler(90, 0, 0);
                shadow.GetComponent<MeshRenderer>().sharedMaterial = ShadowMaterial();
                view.GroundShadow = shadow.transform;
                string prefabPath = "Assets/Resources/Prefabs/Race/Sprites2D/" + root.name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, runners);
                view = instance.GetComponent<RaceDirectionalSpriteView>();
                view.Race = race; view.RaceCamera = camera;
                float lane = -7.5f + i * 3;
                instance.transform.position = course.Point(0, lane);
                race.Entrants[i] = new Data_RunnerSetup {
                    Name = sets[i].CharacterName, Mount = sets[i].CharacterName, SkillName = "초원 질주", Color = colors[i], Prefab = prefab,
                    BaseSpeed = speeds[i], EarlyPower = i == 5 ? .85f : i == 0 ? -.1f : .1f,
                    LatePower = i == 0 ? 1.1f : .25f, SurgeTime = 10 + i * 1.5f, SurgePower = 1.7f,
                    SurgeDuration = 4, Lane = lane
                };
                // Identity marker is independent of the baked character artwork.
                var marker = Primitive(instance.transform, "obj_runner_color", PrimitiveType.Cylinder, new Vector3(0, .015f, 0), new Vector3(1.15f, .015f, 1.15f), Mat("runner_" + i, ColorUtility.ToHtmlStringRGB(colors[i])));
                marker.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var managers = new GameObject("[Managers]").transform;
            var ui = Child(managers, "Mgr_UI").AddComponent<Mgr_UI>();
            BuildHUD(race, director, ui, colors);
            camera.transform.rotation = Quaternion.Euler(38, 35, 0);
            camera.transform.position = course.Point(0) + Vector3.up - camera.transform.forward * 150;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            Selection.activeGameObject = camera.gameObject;
            return "Created " + ScenePath + ": six sprite-only runners, 16 directions x 12 frames, " + TrackLength.ToString("F0") + " m 3D course.";
        }

        private static void BuildEnvironment()
        {
            Primitive(environment, "obj_meadow_base", PrimitiveType.Cube, new Vector3(0, -.7f, 0), new Vector3(220, 1.4f, 150), Mat("meadow", "689C61"));
            Ring("obj_outer_sand_path", 11, 15, .025f, Mat("sand", "DBC494"), Mat("sand", "DBC494"));
            Ring("obj_turf_course", -10, 10, .12f, Mat("turf_light", "78B96B"), Mat("turf_dark", "71B064"));
            Ring("obj_inner_curb", -10.5f, -10, .16f, Mat("curb", "E5E4C7"), Mat("curb", "E5E4C7"));
            Ring("obj_outer_curb", 10, 10.5f, .16f, Mat("curb", "E5E4C7"), Mat("curb", "E5E4C7"));
            Rails(-10.6f); Rails(10.6f);
            for (int i = 0; i < 6; i++)
            {
                float z = -20.5f - i * 3;
                Primitive(environment, "obj_grid_mark_" + i, PrimitiveType.Cube, new Vector3(-20, .165f, z), new Vector3(.22f, .025f, 1.9f), Mat("ivory", "F8F1D9"));
            }
            for (int i = 0; i < 20; i++) for (int j = 0; j < 2; j++)
                Primitive(environment, "obj_finish_checker", PrimitiveType.Cube, new Vector3(-20 + j * .6f, .175f, -37.5f + i), new Vector3(.6f, .035f, 1), Mat((i+j)%2==0?"ivory":"ink", (i+j)%2==0?"F8F1D9":"2B4C42"));
            FinishPost(new Vector3(-20, 0, -40)); FinishPost(new Vector3(-20, 0, -16));
            for (int i = 0; i < 38; i++)
            {
                float d = TrackLength * i / 38;
                Vector3 p = TrackPoint(d, 19 + (i%3)*1.6f);
                Tree(p, 2.6f + (i%4)*.35f, i);
                if (i%2==0) FlowerBed(TrackPoint(d + 4, 15.8f), i);
            }
            // Infield garden, pavilion and a small reflective ornamental pool.
            Primitive(environment, "obj_infield_garden", PrimitiveType.Cube, new Vector3(0, .025f, 0), new Vector3(62, .08f, 25), Mat("garden", "82AD6F"));
            for (int i = 0; i < 9; i++)
            {
                Tree(new Vector3(-31 + i * 7.5f, 0, 9), 2.9f, i);
                if (i%2==0) FlowerBed(new Vector3(-30 + i * 7, 0, -10), i);
            }
            Primitive(environment, "obj_pool_border", PrimitiveType.Cylinder, new Vector3(18, .15f, 0), new Vector3(13, .2f, 8), Mat("pool_stone", "DCDAB9"));
            Primitive(environment, "obj_pool_water", PrimitiveType.Cylinder, new Vector3(18, .37f, 0), new Vector3(11.8f, .035f, 6.8f), Mat("water", "78BECB"));
            Primitive(environment, "obj_pool_island", PrimitiveType.Cylinder, new Vector3(18, .5f, 0), new Vector3(3.2f, .16f, 2.8f), Mat("curb", "E5E4C7"));
            Tree(new Vector3(18, .6f, 0), 2.4f, 0);
            Pavilion(new Vector3(-15, 0, 0));
            Grandstand(new Vector3(0, 0, -55));
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(-56 + i * 26, 0, 54);
                Primitive(environment, "obj_banner_pole", PrimitiveType.Cylinder, p + Vector3.up * 3, new Vector3(.2f, 3, .2f), Mat("ivory", "F8F1D9"));
                Primitive(environment, "obj_banner", PrimitiveType.Cube, p + new Vector3(.9f, 4.8f, 0), new Vector3(1.8f, 2.2f, .12f), Mat("banner_" + i, i%2==0?"DA985E":"4E8D80"));
            }
            WorldText("txt_infield_club", "MEADOW\nMONSTER CLUB", new Vector3(-15, 4.2f, -3), 1.35f, Hex("F9EDC9"));
        }

        private static void Ring(string name, float inner, float outer, float y, Material a, Material b)
        {
            const int count = 336;
            var vertices = new Vector3[(count+1)*2];
            var ta = new List<int>(); var tb = new List<int>();
            for (int i = 0; i <= count; i++)
            {
                var p = TrackPoint(TrackLength*i/count, inner); p.y=y; vertices[i*2]=p;
                p=TrackPoint(TrackLength*i/count, outer); p.y=y; vertices[i*2+1]=p;
                if(i==count) continue;
                var t = i/6%2==0?ta:tb; int n=i*2;
                t.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});
            }
            var mesh = new Mesh { name=name, vertices=vertices, subMeshCount=2 };
            mesh.SetTriangles(ta,0); mesh.SetTriangles(tb,1); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            SaveMesh(mesh, name);
            var go=Child(environment,name); go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials=new[]{a,b};
        }

        private static void Rails(float lane)
        {
            int count=168; var white=Mat("rail", "EBEAD2"); var dark=Mat("rail_base", "537669");
            var root=Child(environment,lane<0?"obj_inner_rails":"obj_outer_rails").transform;
            for(int i=0;i<count;i++)
            {
                float d=TrackLength*i/count; Vector3 p=TrackPoint(d,lane), next=TrackPoint(TrackLength*(i+1)/count,lane);
                var beam=Primitive(root,"obj_rail",PrimitiveType.Cube,(p+next)*.5f+Vector3.up*1.1f,new Vector3(.18f,.22f,Vector3.Distance(p,next)+.05f),white);
                beam.transform.rotation=Quaternion.LookRotation(next-p);
                if(i%2==0)
                {
                    Primitive(root,"obj_post",PrimitiveType.Cube,p+Vector3.up*.55f,new Vector3(.22f,1.1f,.22f),white);
                    Primitive(root,"obj_foot",PrimitiveType.Cube,p+Vector3.up*.07f,new Vector3(.45f,.14f,.45f),dark);
                }
            }
        }

        private static void Tree(Vector3 p,float height,int variant)
        {
            var root=Child(environment,"obj_topiary_"+variant).transform; root.position=p;
            Primitive(root,"obj_trunk",PrimitiveType.Cylinder,new Vector3(0,height*.35f,0),new Vector3(.5f,height*.35f,.5f),Mat("bark","85664E"));
            Primitive(root,"obj_leaf_lower",PrimitiveType.Sphere,new Vector3(0,height*.75f,0),new Vector3(height*.85f,height*.9f,height*.8f),Mat("leaf_dark","39725A"));
            Primitive(root,"obj_leaf_upper",PrimitiveType.Sphere,new Vector3(-.12f,height*1.05f,-.1f),new Vector3(height*.74f,height*.8f,height*.73f),Mat("leaf_"+(variant%3),new[]{"5B925E","629965","71A36A"}[variant%3]));
        }
        private static void FlowerBed(Vector3 p,int variant)
        {
            var root=Child(environment,"obj_flowerbed").transform; root.position=p;
            Primitive(root,"obj_border",PrimitiveType.Cube,new Vector3(0,.18f,0),new Vector3(4,.36f,1.8f),Mat("planter","C1B58C"));
            Primitive(root,"obj_hedge",PrimitiveType.Cube,new Vector3(0,.45f,0),new Vector3(3.7f,.5f,1.5f),Mat("leaf_dark","39725A"));
            for(int i=0;i<7;i++) Primitive(root,"obj_flower",PrimitiveType.Sphere,new Vector3(-1.5f+i*.5f,.8f,(i%2)*.5f-.25f),Vector3.one*.45f,Mat("flower_"+variant%3,new[]{"F3D083","D88F94","EDE4C5"}[variant%3]));
        }
        private static void FinishPost(Vector3 p)
        {
            Primitive(environment,"obj_finish_column",PrimitiveType.Cube,p+Vector3.up*2,new Vector3(.65f,4,.65f),Mat("ivory","F8F1D9"));
            Primitive(environment,"obj_finish_cap",PrimitiveType.Sphere,p+Vector3.up*4.2f,Vector3.one*1.1f,Mat("gold","E8B45A"));
        }
        private static void Pavilion(Vector3 p)
        {
            var root=Child(environment,"obj_club_pavilion").transform;root.position=p;
            Primitive(root,"obj_platform",PrimitiveType.Cube,new Vector3(0,.2f,0),new Vector3(14,.4f,8),Mat("curb","E5E4C7"));
            foreach(float x in new[]{-6f,6f})foreach(float z in new[]{-3f,3f}) Primitive(root,"obj_column",PrimitiveType.Cylinder,new Vector3(x,2,z),new Vector3(.4f,2,.4f),Mat("ivory","F8F1D9"));
            Primitive(root,"obj_roof",PrimitiveType.Cube,new Vector3(0,4,0),new Vector3(15,.45f,9),Mat("roof","407F72"));
            Primitive(root,"obj_roof_trim",PrimitiveType.Cube,new Vector3(0,3.7f,0),new Vector3(15.3f,.22f,9.3f),Mat("gold","E8B45A"));
        }
        private static void Grandstand(Vector3 p)
        {
            var root=Child(environment,"obj_grandstand").transform;root.position=p;
            for(int row=0;row<3;row++)
            {
                Primitive(root,"obj_tier",PrimitiveType.Cube,new Vector3(0,.6f+row*.7f,-row*2),new Vector3(36,1.2f+row*1.4f,2.4f),Mat("stand","C8C4A0"));
                for(int i=0;i<18;i++) Primitive(root,"obj_seat",PrimitiveType.Cube,new Vector3(-17+i*2,1.5f+row*1.4f,-row*2),new Vector3(1.25f,.3f,1.1f),Mat("seat_"+i%2,i%2==0?"D99D70":"6A9C8C"));
            }
            foreach(float x in new[]{-17f,0,17f}) Primitive(root,"obj_canopy_column",PrimitiveType.Cylinder,new Vector3(x,3.7f,-3),new Vector3(.35f,3.7f,.35f),Mat("ivory","F8F1D9"));
            for(int i=0;i<12;i++) Primitive(root,"obj_canopy_stripe",PrimitiveType.Cube,new Vector3(-16.5f+i*3,7.4f,-3),new Vector3(3,.3f,9),Mat("canopy_"+i%2,i%2==0?"E6D6AE":"538F7A"));
        }

        private static Material ShadowMaterial()
        {
            string path=art+"/Materials/M_GroundShadow.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            var shader=Shader.Find("ProjectRun/Race2D/GroundShadow");
            material=new Material(shader);AssetDatabase.CreateAsset(material,path);return material;
        }
        private static void WorldText(string name,string text,Vector3 p,float size,Color color)
        {
            var go=Child(environment,name);go.transform.position=p;go.transform.rotation=Quaternion.Euler(38,35,0);
            var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=64;label.characterSize=size/10;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;
        }
        private static T Asset<T>(string path) where T:ScriptableObject
        {
            var value=AssetDatabase.LoadAssetAtPath<T>(path);if(value!=null)return value;
            value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);return value;
        }
        private static void SaveMesh(Mesh mesh,string name)
        {
            string path=art+"/Meshes/"+name+"_"+meshIndex+++".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old!=null){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);}else AssetDatabase.CreateAsset(mesh,path);
        }
        private static GameObject Child(Transform parent,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go;
        }
        private static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;
            Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        private static Material Mat(string name,string hex)
        {
            if(materials.TryGetValue(name,out var result))return result;
            string path=art+"/Materials/M_"+name+".mat";
            result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null){result=new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));AssetDatabase.CreateAsset(result,path);}
            result.SetColor("_BaseColor",Hex(hex));result.SetFloat("_Smoothness",.05f);result.enableInstancing=true;
            EditorUtility.SetDirty(result);materials.Add(name,result);return result;
        }
        private static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var value);return value;}
    }
}
