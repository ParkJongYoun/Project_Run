using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Builds the A-concept race scene from preserved Ragnarok models and course-specific scenery.</summary>
    public static partial class PronteraWorldRaceBuilder
    {
        public const string ScenePath = "Assets/Scenes/Scene_PronteraToon.unity";
        private const string art = "Assets/ArtBundles/Racecourse/PronteraWorld";
        private const string source = "Assets/OldAsset/Environment/";
        private const string prefab = "Assets/Resources/Prefabs/Environment/P_Racecourse_PronteraWorld.prefab";
        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Geometry> batches = new Dictionary<string, Geometry>();
        private static readonly Dictionary<string, Material> converted = new Dictionary<string, Material>();
        private static Transform root, forest, town, desert;
        private static Data_RaceCourse course;
        private static System.Random random;

        // Keep existing editor callers on the source-asset arrangement; the old construction is archival only.
        public static string Build() => PronteraAssetSceneryBuilder.Build();

        private static string BuildLegacy()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before building.");
            Ensure(art + "/Materials"); Ensure(art + "/Meshes"); Ensure(art + "/Data");
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset("Assets/Scenes/Scene_OvalToon.unity", ScenePath))
                    throw new InvalidOperationException("Could not copy the monster race scene.");
            }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "P_Racecourse_All3D" || go.name == "P_RaceBroadcastDress" || go.name == "P_Racecourse_PronteraWorld") Object.DestroyImmediate(go);
            root = new GameObject("P_Racecourse_PronteraWorld").transform;
            forest = Group("obj_payon_forest"); town = Group("obj_prontera_town"); desert = Group("obj_sograt_desert");
            random = new System.Random(20261017);
            mats.Clear(); batches.Clear(); converted.Clear();
            Materials(); RefinedMaterials(); Path(); SculptedLandscape(); RefinedRoad(); DenseForest(); CitadelTown(); SandstoneDesert(); FestivalDetails();
            foreach (var batch in batches.Values) batch.Save();
            var race = Object.FindFirstObjectByType<Sys_Race>();
            race.Course = course;
            var cameraSystem = Object.FindFirstObjectByType<Sys_RaceCamera>();
            cameraSystem.LandscapeComposition = true;
            cameraSystem.CameraDistanceScale = 1;
            EditorUtility.SetDirty(race); EditorUtility.SetDirty(cameraSystem);
            foreach (var text in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if(text.text.Contains("초원컵"))text.text=text.text.Replace("초원컵","월드컵");
                if(text.name=="txt_course")text.text="복합 " + Mathf.RoundToInt(course.Length).ToString("N0") + " m · 몬스터 6종";
                EditorUtility.SetDirty(text);
            }
            var cam = Camera.main;
            cam.transform.position = new Vector3(10, 305, -357);
            cam.transform.LookAt(new Vector3(0, 26, 35));
            cam.fieldOfView = 53; cam.farClipPlane = 1600; cam.nearClipPlane = .15f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = ColorHtml("AFCAD6"); RenderSettings.fogStartDistance = 650; RenderSettings.fogEndDistance = 1350;
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null) continue;
                string path = art + "/Materials/Profile_" + volume.name + ".asset";
                if (!File.Exists(path)) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile), path);
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (volume.sharedProfile.TryGet<UnityEngine.Rendering.Universal.DepthOfField>(out var depth)) depth.active = false;
                EditorUtility.SetDirty(volume.sharedProfile);
            }
            cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ColorHtml("9FBED1"); RenderSettings.ambientEquatorColor = ColorHtml("8D997D"); RenderSettings.ambientGroundColor = ColorHtml("535747");
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.enabled = light.gameObject.name == "Directional Light";
                if (light.enabled)
                { light.intensity = 1.45f; light.color = ColorHtml("FFF1D8"); light.transform.rotation = Quaternion.Euler(48, -35, 0); light.shadows = LightShadows.Soft; light.shadowStrength = .85f; light.shadowBias=.05f; RenderSettings.sun=light; }
            }
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, prefab, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 10), Quaternion.Euler(53, 0, 0), 350, false, true);
            return "Prontera World built: " + course.Length.ToString("F1") + " m, " + root.GetComponentsInChildren<Renderer>().Length + " renderers. " + ScenePath;
        }

        private static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Ensure(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), System.IO.Path.GetFileName(path));
        }
        private static Transform Group(string name) { var g = new GameObject(name).transform; g.SetParent(root, false); return g; }
        private static Color ColorHtml(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        private static float Rand(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        private static Material Mat(string name, string hex, string texture = null)
        {
            string path = art + "/Materials/Mat_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", ColorHtml(hex)); m.SetFloat("_Smoothness", .08f); m.SetFloat("_Metallic", 0);
            m.SetTexture("_BaseMap", texture == null ? null : AssetDatabase.LoadAssetAtPath<Texture>(texture));
            m.enableInstancing = true; EditorUtility.SetDirty(m); mats[name] = m; return m;
        }
        private static void Materials()
        {
            string turf = "Assets/ArtBundles/Racecourse/All3D/Textures/Tex_Turf.png";
            string sand = "Assets/ArtBundles/Racecourse/All3D/Textures/Tex_Sand.png";
            Mat("Grass", "B2C88A", turf); Mat("ForestFloor", "8DA879", turf); Mat("DryGrass", "D8C18D", sand); Mat("Sand", "F4D095", sand);
            var ground=Mat("WorldGround","FFFFFF");ground.shader=Shader.Find("ProjectRun/Racecourse/WorldGround");
            ground.SetTexture("_GrassMap",AssetDatabase.LoadAssetAtPath<Texture>(turf));ground.SetTexture("_SandMap",AssetDatabase.LoadAssetAtPath<Texture>(sand));EditorUtility.SetDirty(ground);
            Mat("Road", "FFE3AC", sand); Mat("DesertRoad", "F4DAB2", sand);
            Mat("Cobble", "F4F2D7", source + "prontera_city/pr_map_01.png");
            Mat("Stone", "D3D4C0", source + "prontera_city/pr_map_01.png");
            Mat("DesertRock", "D4AA76", sand);
            Mat("Sandstone", "DCB780", source + "prontera_city/pr_map_03.png");
            Mat("Wood", "74533B"); Mat("Gold", "DDB963"); Mat("Ivory", "FFF3CC"); Mat("Blue", "326C9A"); Mat("Red", "C75F4D");
            Mat("Dark", "37434B"); Mat("Roof", "AE5942"); Mat("Leaf", "54834C");
            var water = Mat("Water", "3BADC1"); water.shader = Shader.Find("ProjectRun/Racecourse/GardenWater");
            water.SetColor("_BaseColor", ColorHtml("167C92")); water.SetColor("_ShallowColor", ColorHtml("8DE5DB"));
            EditorUtility.SetDirty(water);
        }
        private static float Height(float x, float z)
        {
            float baseHeight=42*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-147,50,z));
            float desertHeight=z< -35?32*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-147,-35,z)):32+10*Mathf.SmoothStep(0,1,Mathf.InverseLerp(25,90,z));
            return Mathf.Lerp(baseHeight,desertHeight,Mathf.SmoothStep(0,1,Mathf.InverseLerp(80,110,x)));
        }
        private static Vector3 Ground(float x, float z) => new Vector3(x, Height(x, z), z);
        private static void Path()
        {
            Vector2[] knots = {
                new Vector2(0,-140),new Vector2(-65,-140),new Vector2(-114,-119),new Vector2(-146,-90),
                new Vector2(-126,-51),new Vector2(-95,-28),new Vector2(-120,5),new Vector2(-160,33),
                new Vector2(-164,77),new Vector2(-124,110),new Vector2(-85,97),new Vector2(-52,96),
                new Vector2(-30,67),new Vector2(0,55),new Vector2(34,67),new Vector2(51,96),new Vector2(85,96),
                new Vector2(124,112),new Vector2(159,94),new Vector2(171,63),new Vector2(144,37),new Vector2(115,19),
                new Vector2(116,-13),new Vector2(154,-36),new Vector2(184,-62),new Vector2(170,-100),
                new Vector2(120,-124),new Vector2(60,-139) };
            var points = new List<Vector3>();
            for (int i = 0; i < knots.Length; i++)
            {
                var a = knots[(i + knots.Length - 1) % knots.Length]; var b = knots[i];
                var c = knots[(i + 1) % knots.Length]; var d = knots[(i + 2) % knots.Length];
                for (int j = 0; j < 36; j++)
                {
                    float t = j / 36f;
                    // Uniform cubic B-spline rounds each control polygon corner enough for six offset lanes.
                    float u=1-t;
                    Vector2 p=(a*u*u*u+b*(3*t*t*t-6*t*t+4)+c*(-3*t*t*t+3*t*t+3*t+1)+d*t*t*t)/6f;
                    points.Add(Ground(p.x, p.y) + Vector3.up * .16f);
                }
            }
            points.Add(points[0]);
            course = AssetDatabase.LoadAssetAtPath<Data_RaceCourse>(art + "/Data/Data_Course_PronteraWorld.asset");
            if (course == null) { course = ScriptableObject.CreateInstance<Data_RaceCourse>(); AssetDatabase.CreateAsset(course, art + "/Data/Data_Course_PronteraWorld.asset"); }
            course.Points = points.ToArray(); course.Distances = new float[points.Count]; course.RoadWidth = 18;
            for (int i = 1; i < points.Count; i++) course.Distances[i] = course.Distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            EditorUtility.SetDirty(course);
        }
        private static Vector3 ClosestRoadPoint(Vector3 p)
        {
            float best = float.MaxValue;
            Vector3 closest=course.Points[0];
            for (int i = 0; i < course.Points.Length-1; i += 3)
            {
                var a=course.Points[i];var b=course.Points[Mathf.Min(i+3,course.Points.Length-1)];
                var ab=b-a;ab.y=0;var ap=p-a;ap.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(ap,ab)/Mathf.Max(.001f,ab.sqrMagnitude));
                var sample=Vector3.Lerp(a,b,t);var delta=sample-p;delta.y=0;
                if(delta.sqrMagnitude<best){best=delta.sqrMagnitude;closest=sample;}
            }
            return closest;
        }
        private static float RoadDistance(Vector3 p) {var delta=ClosestRoadPoint(p)-p;delta.y=0;return delta.magnitude;}
        private static float TerrainY(float x, float z)
        {
            return SculptedHeight(x,z);
        }
        private static void Sign(string text,Vector3 position,Quaternion rotation,float scale,Transform parent)
        {
            var g=new GameObject("txt_"+text);g.transform.SetParent(parent,false);g.transform.SetPositionAndRotation(position,rotation);g.transform.localScale=Vector3.one*scale;
            var t=g.AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.fontSize=64;t.characterSize=.28f;t.color=ColorHtml("FFF1BB");
        }
        private static float NearestDistance(Vector3 target)
        {
            float best=float.MaxValue,d=0;
            for(int i=0;i<course.Points.Length;i++){var p=course.Points[i]-target;p.y=0;if(p.sqrMagnitude<best){best=p.sqrMagnitude;d=course.Distances[i];}}return d;
        }
        private static Transform Model(string relative,string name,Vector3 position,float height,float yaw,Transform parent,string part=null)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(source+relative);
            if(asset==null)throw new InvalidOperationException("Missing source model: "+relative);
            var wrapper=new GameObject(name).transform;wrapper.SetParent(parent,false);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);instance.transform.SetParent(wrapper,false);
            // Some old FBXs contain alternative modules in the same coordinates. Keep only the selected mesh.
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if(part!=null && renderer.name!=part){renderer.gameObject.SetActive(false);continue;}
                renderer.sharedMaterials=renderer.sharedMaterials.Select(ConvertMaterial).ToArray();
            }
            foreach(var collider in instance.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var animator in instance.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            var renderers=instance.GetComponentsInChildren<Renderer>().Where(x=>x.enabled).ToArray();
            if(renderers.Length==0)throw new InvalidOperationException("No renderers: "+relative+" / "+part);
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float scale=height/Mathf.Max(.1f,bounds.size.y);
            instance.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            wrapper.localScale=Vector3.one*scale;wrapper.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            return wrapper;
        }
        private static Material ConvertMaterial(Material original)
        {
            if(original==null)return mats["Stone"];
            string id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original))+"_"+original.name;
            if(converted.TryGetValue(id,out var cached))return cached;
            string safe=string.Concat(id.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_'));
            var material=Mat("Source_"+safe,"FFFFFF");
            var texture=original.mainTexture; material.SetTexture("_BaseMap",texture);
            material.SetFloat("_Cull",0);
            string label=(original.name+" "+(texture==null?"":texture.name)+" "+original.shader.name).ToLowerInvariant();
            if(label.Contains("water") || label.Contains("wa1_") || label.Contains("wa2_"))
            {material.shader=mats["Water"].shader;material.CopyPropertiesFromMaterial(mats["Water"]);}
            else if(label.Contains("branch")||label.Contains("tree")||label.Contains("nature")||label.Contains("leaf")||label.Contains("alpha")||label.Contains("chain")||label.Contains("fire"))
            {material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.4f);material.SetFloat("_Cull",0);material.EnableKeyword("_ALPHATEST_ON");material.renderQueue=2450;material.SetOverrideTag("RenderType","TransparentCutout");}
            if(label.Contains("branchesoak"))material.SetColor("_BaseColor",ColorHtml("B0D8A3"));
            EditorUtility.SetDirty(material);converted[id]=material;return material;
        }
        private static Geometry Geo(string name,string mat)
        {string key=name+"_"+mat;if(!batches.TryGetValue(key,out var geo)){geo=new Geometry(key,mat);batches[key]=geo;}return geo;}
        private static void Disc(string name,string mat,Vector3 center,float rx,float rz)
        {for(int i=0;i<80;i++){float a=i*Mathf.PI/40,b=(i+1)*Mathf.PI/40;Geo(name,mat).Triangle(center,center+new Vector3(Mathf.Sin(a)*rx,0,Mathf.Cos(a)*rz),center+new Vector3(Mathf.Sin(b)*rx,0,Mathf.Cos(b)*rz));}}
        private static void Beam(string name,string mat,Vector3 a,Vector3 b,float thickness)
        {Box(name,mat,(a+b)/2,new Vector3(thickness,thickness,Vector3.Distance(a,b)),Quaternion.LookRotation(b-a));}
        private static void Box(string name,string mat,Vector3 center,Vector3 size,Quaternion rotation=default)
        {
            if(rotation==default)rotation=Quaternion.identity;
            var p=new Vector3[8];for(int i=0;i<8;i++)p[i]=center+rotation*Vector3.Scale(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f),size);
            var g=Geo(name,mat);g.Quad(p[0],p[2],p[3],p[1]);g.Quad(p[5],p[7],p[6],p[4]);g.Quad(p[4],p[6],p[2],p[0]);g.Quad(p[1],p[3],p[7],p[5]);g.Quad(p[2],p[6],p[7],p[3]);g.Quad(p[4],p[0],p[1],p[5]);
        }
        private sealed class Geometry
        {
            private readonly string name,material;
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<Vector2> uv=new List<Vector2>();
            private readonly List<int> triangles=new List<int>();
            public Geometry(string name,string material){this.name=name;this.material=material;}
            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x,a.z)*.2f,new Vector2(b.x,b.z)*.2f,new Vector2(c.x,c.z)*.2f});triangles.AddRange(new[]{n,n+1,n+2});}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float tile=.2f)
            {int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});foreach(var p in new[]{a,b,c,d})uv.Add(new Vector2(p.x+p.y,p.z+p.y)*tile);triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            public void Save()
            {
                string path=art+"/Meshes/Mesh_"+name+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
                mesh.name="Mesh_"+name;mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                var g=new GameObject("obj_"+name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mats[material];
                if(name.StartsWith("road_"))g.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
        }
    }
}
