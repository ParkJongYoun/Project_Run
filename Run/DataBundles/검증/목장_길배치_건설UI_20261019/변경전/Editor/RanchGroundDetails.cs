using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRun.Editor
{
    // Small ground accents belong to the facility prefab so moving a building moves its apron too.
    public static class RanchGroundDetails
    {
        private const string artRoot = RanchPaintedLook.ArtRoot;

        public static void ApplyFoundation(GameObject root, string id)
        {
            if (id != "shelter" && id != "house" && id != "feed_store" && id != "training") return;
            var old = root.transform.Find("group_ground_details");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("group_ground_details").transform;
            group.SetParent(root.transform, false);
            float x = id == "shelter" ? 2.45f : id == "training" ? 2.5f : 1.85f;
            float z = id == "training" ? 2.65f : 1.65f;
            var soil = new Mesh { name = "RanchApron_" + id };
            soil.vertices = new[] { new Vector3(-x-.3f,-.012f,-z-.75f), new Vector3(-x-.3f,-.012f,z+.25f),
                new Vector3(x+.3f,-.012f,z+.25f), new Vector3(x+.3f,-.012f,-z-.75f) };
            soil.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            soil.triangles = new[] { 0,1,2,0,2,3 }; soil.RecalculateNormals(); soil.RecalculateBounds();
            var soilMaterial = Material("M_RanchGroundApron", "ProjectRun/Ranch/GroundPatch");
            soilMaterial.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(artRoot + "/Textures/PathDetailAlbedo.png"));
            soilMaterial.SetColor("_BaseColor", new Color(.97f,.93f,.83f)); EditorUtility.SetDirty(soilMaterial);
            AddMesh(group, "obj_ground_detail_apron", Save(soil), soilMaterial);
            var painter = new Painter(id == "shelter" ? 37 : id == "house" ? 51 : id == "training" ? 63 : 79);
            foreach (float sign in new[] { -1f, 1f })
            {
                painter.Cluster(new Vector3(sign*(x+.10f),0,-z+.35f), .46f, 25, true);
                painter.Cluster(new Vector3(sign*(x-.2f),0,z+.10f), .50f, 22, false);
            }
            AddMesh(group, "obj_ground_detail_garden", Save(painter.Mesh("RanchGarden_"+id)),
                Material("M_RanchGroundFoliage", "ProjectRun/Ranch/GroundFoliage"));
            if (id == "training" || id == "shelter")
            {
                // A shallow continuous bed covers the generated floor's disconnected patches.
                var floor = root.transform.Find("obj_ground_repair");
                if (floor != null)
                {
                    floor.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ArtBundles/Ranch/Meshes/BeveledBlock.asset");
                    floor.localPosition = new Vector3(0, id == "training" ? .09f : .055f, 0);
                    floor.localScale = new Vector3(id == "training" ? 4.52f : 4.48f, .22f, id == "training" ? 4.9f : 2.96f);
                    var earth = Material("M_RanchFacilityFloor_" + id, "ProjectRun/Ranch/SoftPainted");
                    earth.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(artRoot + "/Textures/PathDetailAlbedo.png"));
                    earth.SetTextureScale("_BaseMap", new Vector2(1.2f,1.2f));
                    earth.SetColor("_BaseColor", id == "training" ? new Color(1,.94f,.77f) : new Color(.89f,.77f,.47f));
                    earth.SetColor("_ShadeColor", new Color(.51f,.58f,.65f));
                    earth.SetFloat("_AlbedoPower",1); earth.SetFloat("_BumpScale",0);
                    earth.SetFloat("_AmbientStrength",.07f);earth.SetFloat("_Specular",0);
                    earth.SetFloat("_RimStrength",0);earth.SetFloat("_Softness",.22f);
                    floor.GetComponent<Renderer>().sharedMaterial = earth;
                    if (id == "training")
                    {
                        // Feather the soil height back into the meadow; no stretched vertical cube sides.
                        floor.GetComponent<MeshFilter>().sharedMesh = Save(TrainingBed());
                        floor.localPosition=Vector3.zero;floor.localScale=Vector3.one;
                        earth.SetTextureScale("_BaseMap",Vector2.one);
                    }
                    EditorUtility.SetDirty(earth);
                }
                if (id == "shelter") AddHay(group);
            }
        }

        public static void ApplyBorders(Transform ground)
        {
            var old = ground.Find("group_ground_accents");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("group_ground_accents").transform; group.SetParent(ground,false);
            var painter = new Painter(1973);
            for (int i=0;i<23;i++)
            {
                float x=-12.8f+i*1.14f;
                painter.Cluster(new Vector3(x,0,10.45f),.52f,17,i%4==0);
                if (Mathf.Abs(x-.5f)>2.1f) painter.Cluster(new Vector3(x,0,-10.35f),.45f,15,i%3==0);
            }
            for (int i=0;i<17;i++)
            {
                float z=-9.6f+i*1.18f;
                painter.Cluster(new Vector3(-13.15f,0,z),.46f,16,i%3==0);
                painter.Cluster(new Vector3(13.15f,0,z),.46f,16,i%4==0);
            }
            // Sparse roadside accents leave the construction lawn clear and readable.
            foreach (var p in new[] { new Vector3(-11,0,1.8f),new Vector3(-5.7f,0,1.8f),new Vector3(3,0,1.8f),
                new Vector3(8.7f,0,1.8f),new Vector3(-10,0,-7.7f),new Vector3(-4.5f,0,-7.7f),
                new Vector3(1.55f,0,-1),new Vector3(-.6f,0,-4.8f),new Vector3(7,0,-6.8f) })
                painter.Cluster(p,.42f,20,true);
            AddMesh(group,"obj_ground_detail_verges",Save(painter.Mesh("RanchVergeAccents")),
                Material("M_RanchGroundFoliage","ProjectRun/Ranch/GroundFoliage"));
            // Low tufts give the lawn a real silhouette while keeping grid cells readable.
            var lawn = new Painter(1991);
            var random = new System.Random(651);
            var placement = UnityEngine.Object.FindFirstObjectByType<ProjectRun.Ranch.Sys_RanchPlacement>();
            for (int i=0;i<950;i++)
            {
                float x=-23+(float)random.NextDouble()*48,z=-16+(float)random.NextDouble()*37;
                bool outside=Mathf.Abs(x)>13.5f || Mathf.Abs(z)>10.8f;
                bool path=(Mathf.Abs(z-2.9f)<1.12f && Mathf.Abs(x+1.1f)<12.3f) ||
                    (Mathf.Abs(x-.5f)<1.08f && z<3.5f) ||
                    (Mathf.Abs(z+6.9f)<.9f && x>-10.2f && x<6.1f) ||
                    (z>2.8f && z<5.3f && (Mathf.Abs(x+9.45f)<.9f || Mathf.Abs(x+2.7f)<.9f));
                if(path)continue;
                bool underFacility=false;
                if(placement!=null)foreach(var item in placement.InitialLayout.Facilities)
                {
                    var definition=placement.Catalog.Find(item.FacilityId);if(definition==null)continue;
                    var size=definition.Size(item.Rotation);
                    var min=placement.GridOrigin+new Vector3(item.X*placement.CellSize,0,item.Y*placement.CellSize);
                    if(x>min.x-.25f && x<min.x+size.x*placement.CellSize+.25f &&
                       z>min.z-.25f && z<min.z+size.y*placement.CellSize+.25f) { underFacility=true;break; }
                }
                if(underFacility)continue;
                lawn.Cluster(new Vector3(x,0,z),outside?.32f:.23f,outside?12:9,false,outside?.68f:.33f);
            }
            AddMesh(group,"obj_ground_detail_lawn",Save(lawn.Mesh("RanchLawnTufts")),
                Material("M_RanchGroundFoliage","ProjectRun/Ranch/GroundFoliage"));
            AddEntrance(group);
        }

        private static void AddHay(Transform group)
        {
            var hay=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Ranch/Materials/Mat_Hay.mat");
            var rope=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Ranch/Materials/Mat_WoodLight.mat");
            for(int i=0;i<3;i++)
            {
                var p=i==0?new Vector3(-1.66f,.39f,.54f):i==1?new Vector3(1.68f,.39f,.55f):new Vector3(1.68f,.79f,.55f);
                Block(group,"obj_ground_detail_hay",p,new Vector3(.85f,.43f,.67f),hay);
                foreach(float dx in new[]{-.23f,.23f})
                    Block(group,"obj_ground_detail_rope",p+Vector3.right*dx,new Vector3(.035f,.446f,.684f),rope);
            }
        }

        private static Mesh TrainingBed()
        {
            var vertices=new List<Vector3>{new Vector3(0,.20f,0)};
            var triangles=new List<int>();
            for(int ring=0;ring<2;ring++)
            {
                float x=ring==0?2.34f:2.66f,z=ring==0?2.55f:2.87f,y=ring==0?.20f:-.004f,r=.23f;
                vertices.AddRange(new[]{new Vector3(-x,y,-z+r),new Vector3(-x,y,z-r),
                    new Vector3(-x+r,y,z),new Vector3(x-r,y,z),new Vector3(x,y,z-r),
                    new Vector3(x,y,-z+r),new Vector3(x-r,y,-z),new Vector3(-x+r,y,-z)});
            }
            for(int i=0;i<8;i++)
            {
                int next=(i+1)%8;
                triangles.AddRange(new[]{0,1+i,1+next,1+i,9+i,9+next,1+i,9+next,1+next});
            }
            var mesh=new Mesh{name="RanchTrainingBed"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            mesh.SetUVs(0,vertices.Select(p=>new Vector2(p.x,p.z)*.22f).ToList());
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
        }

        private static void AddEntrance(Transform group)
        {
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Ranch/Materials/Mat_Wood.mat");
            var cap=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Ranch/Materials/Mat_WoodLight.mat");
            var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtBundles/Ranch/Materials/Mat_Stone.mat");
            for(int i=0;i<2;i++)
            {
                var end=new Vector3(i==0?-1.6f:2.6f,0,-10.95f);
                var gate=new Vector3(i==0?-1.23f:1.23f,0,-10.125f);
                Block(group,"obj_ground_detail_entry_foot",end+Vector3.up*.15f,new Vector3(.43f,.3f,.43f),stone);
                Block(group,"obj_ground_detail_entry_post",end+Vector3.up*.74f,new Vector3(.26f,1.2f,.26f),wood);
                Block(group,"obj_ground_detail_entry_cap",end+Vector3.up*1.36f,new Vector3(.34f,.10f,.34f),cap);
                foreach(float y in new[]{.45f,.88f})
                {
                    var beam=Block(group,"obj_ground_detail_entry_rail",(end+gate)*.5f+Vector3.up*y,
                        new Vector3(.13f,.16f,Vector3.Distance(end,gate)),wood);
                    beam.localRotation=Quaternion.LookRotation(gate-end);
                }
            }
            for(int i=0;i<4;i++)
                Block(group,"obj_ground_detail_entry_step",new Vector3(-.95f+i*.66f,.025f,-10.65f),new Vector3(.63f,.10f,.70f),stone);
        }

        private static Transform Block(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;
            obj.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ArtBundles/Ranch/Meshes/BeveledBlock.asset");
            obj.GetComponent<MeshRenderer>().sharedMaterial=material;return obj.transform;
        }

        private static Material Material(string name,string shader)
        {
            string path=artRoot+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path); }
            return material;
        }
        private static Mesh Save(Mesh source)
        {
            Directory.CreateDirectory(artRoot+"/Meshes");
            string path=artRoot+"/Meshes/"+source.name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null) { AssetDatabase.CreateAsset(source,path);return source; }
            EditorUtility.CopySerialized(source,mesh);UnityEngine.Object.DestroyImmediate(source);return mesh;
        }
        private static void AddMesh(Transform parent,string name,Mesh mesh,Material material)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
        }
        private sealed class Painter
        {
            private readonly System.Random random;
            private readonly List<Vector3> vertices=new List<Vector3>();
            private readonly List<int> triangles=new List<int>();
            private readonly List<Color> colors=new List<Color>();
            public Painter(int seed) { random=new System.Random(seed); }
            private float Next() => (float)random.NextDouble();
            private void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
            {
                int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
                colors.Add((color*.78f).linear);colors.Add(color.linear);colors.Add(color.linear);
            }
            public void Cluster(Vector3 center,float radius,int blades,bool flowers,float heightScale=1)
            {
                for(int i=0;i<blades;i++)
                {
                    float angle=Next()*Mathf.PI*2, r=Mathf.Sqrt(Next())*radius;
                    var p=center+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);
                    float height=(.16f+Next()*.28f)*heightScale,width=.025f+Next()*.025f;
                    var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width;
                    var tip=p+Vector3.up*height+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*(.10f*heightScale);
                    var color=Color.Lerp(new Color(.29f,.46f,.13f),new Color(.64f,.75f,.25f),Next());
                    Triangle(p-side,p+side,tip,color);
                }
                if(!flowers)return;
                for(int i=0;i<5;i++)
                {
                    var p=center+new Vector3((Next()-.5f)*radius, .22f+Next()*.15f,(Next()-.5f)*radius);
                    var petal=i%3==0?new Color(1,.78f,.26f):new Color(.98f,.95f,.80f);
                    for(int k=0;k<5;k++)
                    {
                        float a=k*Mathf.PI*.4f;var d=new Vector3(Mathf.Cos(a),.22f,Mathf.Sin(a))*.09f;
                        var side=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*.035f;
                        Triangle(p,p+d+side,p+d*1.25f,petal);Triangle(p,p+d*1.25f,p+d-side,petal);
                    }
                    Triangle(p+Vector3.up*.006f+Vector3.left*.032f,p+Vector3.up*.006f+Vector3.forward*.036f,
                        p+Vector3.up*.006f+Vector3.right*.032f,new Color(.96f,.63f,.12f));
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh { name=name };mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetColors(colors);
                mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
