using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    // Authoring only. The race prefabs, source FBXs and the user's camera are never edited here.
    public static class RanchVillageSetup
    {
        private const string prefabRoot = "Assets/Resources/Prefabs/Ranch/P_Ranch_";
        private const string artRoot = "Assets/ArtBundles/Ranch/VillageDetails";
        private const string generatedRoot = "Assets/ArtBundles/Ranch/TripoModels/";
        private const string recordRoot = "DataBundles/검증/목장_시설동작과배치수정_20261019";
        private static readonly string[] additions = { "hot_spring", "race_track", "feeding_station", "windmill", "flower_cart", "fountain", "gazebo", "waterfall" };
        [Serializable] private sealed class IndexList { public int[] Indices; }

        public static string PrepareFacilities()
        {
            RequireScene();
            Directory.CreateDirectory(artRoot + "/Meshes");
            Directory.CreateDirectory(artRoot + "/Materials");
            Directory.CreateDirectory(artRoot + "/Animations");
            AssetDatabase.Refresh();
            var report = new List<string>();
            foreach (string id in additions)
            {
                var definition=Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog").Find(id);
                bool revised=definition.Prefab.name.EndsWith("_V2");
                string folder=(revised?"Assets/ArtBundles/Ranch/TripoModelsV2/":generatedRoot)+id;
                foreach (string file in Directory.GetFiles(folder + "/Textures").Where(p => !p.EndsWith(".meta")))
                {
                    string path = file.Replace('\\','/');
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;
                    bool normal = Path.GetFileNameWithoutExtension(path) == "Normal";
                    importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = !normal;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder+(revised?"/M_Ranch_"+id+"_V2.mat":"/M_RanchGenerated_"+id+"_0.mat"));
                bool hasNormal = material.GetTexture("_BumpMap") != null;
                material.SetFloat("_BumpScale", hasNormal ? .85f : 0);
                if (id == "hot_spring" || id == "waterfall" || id == "fountain") material.shader = Shader.Find("ProjectRun/Ranch/FlowingWater");
                EditorUtility.SetDirty(material);
                EditPrefab(id, root =>
                {
                    AddApron(root,id);
                    if (id == "windmill") SplitWindmill(root,material);
                    if (id == "waterfall" || id == "hot_spring" || id == "fountain") AddWater(root,id);
                });
                report.Add(id + ": " + (hasNormal ? "normal map / strength 0.85 / trilinear / anisotropic 4" : "no normal map in source; mesh normals retained"));
            }
            ConfigureSpots();
            AssetDatabase.SaveAssets();
            File.WriteAllLines(recordRoot + "/노말맵_확인.txt",report);
            return string.Join("\n",report);
        }

        private static void RequireScene()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != RanchSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open Scene_LobbyTest in Edit mode.");
        }

        private static void EditPrefab(string id, Action<GameObject> edit)
        {
            var definition=Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog").Find(id);
            string path=definition!=null && definition.Prefab!=null?AssetDatabase.GetAssetPath(definition.Prefab):prefabRoot+id+".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RemoveChild(GameObject root,string name)
        {
            var child = root.transform.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        private static void AddApron(GameObject root,string id)
        {
            RemoveChild(root,"obj_village_apron");
            var definition = Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog").Find(id);
            if (id == "flower_cart") return;
            float x = definition.Footprint.x*.675f+.32f;
            float z = definition.Footprint.y*.675f+.32f;
            var mesh = new Mesh { name="Apron_"+id };
            mesh.vertices = new[] { new Vector3(-x,-.011f,-z-.25f), new Vector3(-x,-.011f,z),
                new Vector3(x,-.011f,z),new Vector3(x,-.011f,-z-.25f) };
            mesh.uv = new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right };
            mesh.triangles = new[] { 0,1,2,0,2,3 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AddMesh(root.transform,"obj_village_apron",SaveMesh(mesh),
                AssetDatabase.LoadAssetAtPath<Material>(RanchPaintedLook.ArtRoot+"/Materials/M_RanchGroundApron.mat"),false);
        }

        private static void SplitWindmill(GameObject root,Material material)
        {
            RemoveChild(root,"obj_windmill_body"); RemoveChild(root,"obj_windmill_rotor");
            var source = root.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == "model_tripo_windmill");
            // Read the immutable FBX mesh every time, so this operation is safe to repeat.
            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(generatedRoot+"windmill/windmill.fbx");
            var mesh = sourceAsset.GetComponentInChildren<MeshFilter>().sharedMesh;
            var selected = new HashSet<int>(JsonUtility.FromJson<IndexList>(File.ReadAllText(recordRoot+"/풍차_날개삼각형.json")).Indices);
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            var pivot = new Vector3(0,1.85f,-.612f);
            var rotation = Quaternion.LookRotation(new Vector3(-.03623f,-.11589f,1).normalized,Vector3.up);
            var body = SliceMesh(mesh,toRoot,selected,false,Vector3.zero,Quaternion.identity,"WindmillBody");
            var rotor = SliceMesh(mesh,toRoot,selected,true,pivot,rotation,"WindmillRotor");
            AddMesh(root.transform,"obj_windmill_body",SaveMesh(body),material,true);
            var blades = AddMesh(root.transform,"obj_windmill_rotor",SaveMesh(rotor),material,true);
            blades.localPosition = pivot - rotation*Vector3.forward*.04f;
            blades.localRotation = rotation;
            blades.gameObject.AddComponent<RanchWindmillRotor>();
            source.GetComponent<Renderer>().enabled = false;
        }

        private static Mesh SliceMesh(Mesh source,Matrix4x4 toRoot,HashSet<int> selected,bool rotor,Vector3 pivot,Quaternion rotation,string name)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(pivot,rotation,Vector3.one).inverse * toRoot;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            var vertices = source.vertices; var normals = source.normals; var tangents=source.tangents; var uv=source.uv;
            var input=source.triangles; var remap=new Dictionary<int,int>();
            var positions=new List<Vector3>(); var outputNormals=new List<Vector3>(); var outputTangents=new List<Vector4>();
            var outputUv=new List<Vector2>(); var triangles=new List<int>();
            for(int i=0;i<input.Length/3;i++)
            {
                if(selected.Contains(i)!=rotor)continue;
                for(int j=0;j<3;j++)
                {
                    int old=input[i*3+j];
                    if(!remap.TryGetValue(old,out int next))
                    {
                        next=positions.Count;remap[old]=next;positions.Add(matrix.MultiplyPoint3x4(vertices[old]));
                        outputNormals.Add(normalMatrix.MultiplyVector(normals[old]).normalized);outputUv.Add(uv[old]);
                        Vector3 t=matrix.MultiplyVector(new Vector3(tangents[old].x,tangents[old].y,tangents[old].z)).normalized;
                        outputTangents.Add(new Vector4(t.x,t.y,t.z,tangents[old].w));
                    }
                    triangles.Add(next);
                }
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(positions);mesh.SetNormals(outputNormals);mesh.SetTangents(outputTangents);
            mesh.SetUVs(0,outputUv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }

        private static void AddWater(GameObject root,string id)
        {
            RemoveChild(root,"group_water_motion");
            var group=new GameObject("group_water_motion").transform;group.SetParent(root.transform,false);
            var fall=WaterMaterial("M_WaterFall",false); var foam=WaterMaterial("M_WaterFoam",true);
            if(id=="fountain")
            {
                var surface=root.GetComponentsInChildren<Renderer>(true).First(r=>r.name=="model_tripo_fountain").sharedMaterial;
                surface.shader=Shader.Find("ProjectRun/Ranch/FlowingWater");
                surface.SetFloat("_WaterMotionStrength",1.8f);
                surface.SetFloat("_WaterMotionSpeed",1.7f);
                EditorUtility.SetDirty(surface);
                Foam(group,id+"_pool",new Vector3(0,.535f,0),.98f,foam);
                Foam(group,id+"_upper_pool",new Vector3(0,1.505f,0),.39f,foam);
                for(int i=0;i<8;i++)
                {
                    float angle=i*Mathf.PI/4;
                    Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                    Vector3 top=Vector3.up*1.63f+direction*.13f;
                    Vector3 middle=Vector3.up*1.05f+direction*.38f;
                    Vector3 bottom=Vector3.up*.535f+direction*.68f;
                    Vector3 side=Vector3.Cross(direction,Vector3.up);
                    Ribbon(group,id+"_jet_top_"+i,top,middle,side,.065f,fall);
                    Ribbon(group,id+"_jet_bottom_"+i,middle,bottom,side,.085f,fall);
                    Foam(group,id+"_splash_"+i,bottom,.18f,foam);
                }
                var drops=new GameObject("obj_fountain_droplets",typeof(ParticleSystem));
                drops.transform.SetParent(group,false);drops.transform.localPosition=Vector3.up*1.58f;
                var particles=drops.GetComponent<ParticleSystem>();
                particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.loop=true;main.playOnAwake=true;
                main.startLifetime=.6f;main.startSpeed=.8f;main.startSize=.065f;
                main.gravityModifier=.65f;main.maxParticles=96;
                var emission=particles.emission;emission.rateOverTime=55;
                var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.23f;
                var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=foam;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            else if(id=="waterfall")
            {
                if(root.name.Contains("_V2"))
                {
                    Ribbon(group,id+"_v2_upper",new Vector3(-.16f,1.92f,.30f),new Vector3(-.16f,1.08f,.19f),Vector3.right,.56f,fall);
                    Ribbon(group,id+"_v2_lower",new Vector3(-.02f,1.08f,-.28f),new Vector3(-.02f,.44f,-.41f),Vector3.right,.58f,fall);
                    Foam(group,id+"_v2_upper",new Vector3(-.16f,1.12f,.14f),.29f,foam);
                    Foam(group,id+"_v2_lower",new Vector3(-.02f,.48f,-.49f),.38f,foam);
                }
                else
                {
                    Ribbon(group,id+"_upper",new Vector3(-.18f,1.49f,.59f),new Vector3(-.18f,.73f,.46f),Vector3.right,.46f,fall);
                    Ribbon(group,id+"_lower",new Vector3(-.28f,.74f,-.13f),new Vector3(-.28f,.25f,-.31f),Vector3.right,.62f,fall);
                    Foam(group,id+"_upper",new Vector3(-.18f,.77f,.42f),.30f,foam);
                    Foam(group,id+"_lower",new Vector3(-.28f,.275f,-.38f),.43f,foam);
                }
            }
            else
            {
                Ribbon(group,id+"_inlet",new Vector3(.59f,.87f,.16f),new Vector3(.43f,.40f,.16f),Vector3.forward,.21f,fall);
                Foam(group,id+"_pool",new Vector3(.38f,.435f,.16f),.30f,foam);
            }
        }

        public static void RefreshGroundAndWater()
        {
            RequireScene();
            foreach(string id in additions)EditPrefab(id,root=>
            {
                AddApron(root,id);
                if(id=="waterfall" || id=="hot_spring" || id=="fountain")AddWater(root,id);
            });
            AssetDatabase.SaveAssets();
        }

        private static Material WaterMaterial(string name,bool foam)
        {
            string path=artRoot+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("ProjectRun/Ranch/WaterAccent"));AssetDatabase.CreateAsset(material,path);}
            material.SetFloat("_FoamMode",foam?1:0);
            material.SetColor("_BaseColor",foam?new Color(.86f,1,1,.85f):new Color(.52f,.94f,.98f,.82f));
            EditorUtility.SetDirty(material);return material;
        }

        private static void Ribbon(Transform parent,string name,Vector3 top,Vector3 bottom,Vector3 side,float width,Material material)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<=6;i++)
            {
                float v=i/6f; Vector3 point=Vector3.Lerp(top,bottom,v);
                point+=new Vector3(-side.z,0,-side.x)*Mathf.Sin(v*Mathf.PI)*.035f;
                float half=width*.5f*Mathf.Lerp(.82f,1.08f,v);
                vertices.Add(point-side*half);vertices.Add(point+side*half);uv.Add(new Vector2(0,v));uv.Add(new Vector2(1,v));
                if(i<6){int a=i*2;triangles.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
            }
            var mesh=new Mesh{name="WaterRibbon_"+name};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();AddMesh(parent,"obj_flow_"+name,SaveMesh(mesh),material,false);
        }

        private static void Foam(Transform parent,string name,Vector3 point,float radius,Material material)
        {
            var mesh=new Mesh{name="WaterFoam_"+name};
            mesh.vertices=new[]{point+new Vector3(-radius,0,-radius),point+new Vector3(-radius,0,radius),
                point+new Vector3(radius,0,radius),point+new Vector3(radius,0,-radius)};
            mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.triangles=new[]{0,1,2,0,2,3};
            mesh.RecalculateNormals();mesh.RecalculateBounds();AddMesh(parent,"obj_foam_"+name,SaveMesh(mesh),material,false);
        }

        private static void ConfigureSpots()
        {
            Spot("hot_spring",E_RanchActivity.Bath,new Vector3(0,.05f,-2.35f),new Vector3(-.12f,.32f,-.10f),Vector3.back,6);
            Spot("feeding_station",E_RanchActivity.Feeding,new Vector3(0,.05f,-1.85f),new Vector3(0,.05f,-1.30f),Vector3.forward,5);
            EditPrefab("feed_store",root=>
            {
                var spot=root.GetComponent<RanchFacilitySpot>();
                if(spot!=null)UnityEngine.Object.DestroyImmediate(spot);
            });
            Spot("shelter",E_RanchActivity.Rest,new Vector3(-1.18f,.05f,-2.35f),new Vector3(-1.18f,.19f,-1.18f),Vector3.back,8);
            Spot("training",E_RanchActivity.Training,new Vector3(-2.1f,.05f,-3.2f),new Vector3(0,.23f,0),Vector3.forward,5);
            EditPrefab("training",root=>root.GetComponent<RanchFacilitySpot>().EntryPoints=new[]
            {new Vector3(-1.85f,.23f,-2.7f),new Vector3(-1.85f,.23f,-.6f)});
            Spot("race_track",E_RanchActivity.Track,new Vector3(-3.2f,.05f,2f),Vector3.zero,Vector3.forward,0);
            EditPrefab("race_track",root=>
            {
                var points=new List<Vector3>();
                // A closed capsule follows the actual dirt lane. Start/finish is at the entrance side.
                points.Add(new Vector3(0,.14f,1.73f)); points.Add(new Vector3(-1.12f,.14f,1.73f));
                for(int i=1;i<=16;i++)
                {float a=Mathf.PI*.5f+Mathf.PI*i/16;points.Add(new Vector3(-1.12f+Mathf.Cos(a)*1.73f,.14f,Mathf.Sin(a)*1.73f));}
                points.Add(new Vector3(1.12f,.14f,-1.73f));
                for(int i=1;i<=16;i++)
                {float a=-Mathf.PI*.5f+Mathf.PI*i/16;points.Add(new Vector3(1.12f+Mathf.Cos(a)*1.73f,.14f,Mathf.Sin(a)*1.73f));}
                // Rotate the closed lap to the southeast gate in the scene (the track faces 180 degrees).
                int start=0;float best=float.PositiveInfinity;
                for(int i=0;i<points.Count;i++)
                {
                    float distance=(points[i]-new Vector3(-2.34f,.14f,1.22f)).sqrMagnitude;
                    if(distance<best){best=distance;start=i;}
                }
                var lap=new List<Vector3>();
                for(int i=0;i<points.Count;i++)lap.Add(points[(start+i)%points.Count]);
                lap.Add(lap[0]);root.GetComponent<RanchFacilitySpot>().TrackPoints=lap.ToArray();
            });
        }

        private static void Spot(string id,E_RanchActivity activity,Vector3 entry,Vector3 point,Vector3 facing,float duration)
        {
            EditPrefab(id,root=>
            {
                var spot=root.GetComponent<RanchFacilitySpot>();if(spot==null)spot=root.AddComponent<RanchFacilitySpot>();
                spot.Activity=activity;spot.Entry=entry;spot.ActionPoint=point;spot.Facing=facing;spot.Duration=duration;
                spot.EntryPoints=Array.Empty<Vector3>();
            });
        }

        public static void ApplyEntranceRevision()
        {
            RequireScene();ConfigureSpots();
            EditPrefab("fountain",root=>AddWater(root,"fountain"));
            foreach(var resident in GameObject.Find("RanchWorld/group_monsters").GetComponentsInChildren<Sys_RanchResident>(true))
            {
                resident.FeedingHead=resident.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t=>t.name=="bone_head") ?? resident.TargetAnimator.transform;
                if(resident.name.Contains("Minorous"))
                {resident.CelebrationClip=null;resident.AttackClip=null;}
                PrefabUtility.RecordPrefabInstancePropertyModifications(resident);
                EditorUtility.SetDirty(resident);
            }
            var placement=UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>();
            // Update the existing instances so unrelated objects and authored transforms retain their identities.
            var revised=RanchDemoLayout.Create();
            if(!RanchGrid.Validate(placement.Catalog,revised,placement.GridSize))
                throw new InvalidOperationException("Revised entrance layout overlaps.");
            foreach(string id in new[]{"hot_spring","waterfall","feed_store"})
            {
                var entry=revised.Facilities.First(f=>f.FacilityId==id);
                var instance=placement.FacilitiesRoot.Cast<Transform>().First(t=>t.name.StartsWith("obj_"+id+"_"));
                Undo.RecordObject(instance,"Revise ranch entrance");
                instance.name="obj_"+id+"_"+entry.InstanceId;
                instance.SetPositionAndRotation(placement.Position(entry),Quaternion.Euler(0,entry.Rotation*90,0));
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.gameObject);
            }
            placement.InitialLayout=revised;EditorUtility.SetDirty(placement);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(placement.gameObject.scene);
        }

        public static void RestoreWaterfallAndAnimateFountain()
        {
            RequireScene();
            EditPrefab("fountain",root=>AddWater(root,"fountain"));
            var placement=UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>();
            var entry=placement.InitialLayout.Facilities.First(f=>f.FacilityId=="waterfall");
            entry.Rotation=0;
            var instance=placement.FacilitiesRoot.Cast<Transform>().First(t=>t.name.StartsWith("obj_waterfall_"));
            Undo.RecordObject(instance,"Restore waterfall direction");
            instance.rotation=Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            EditorUtility.SetDirty(placement);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(placement.gameObject.scene);
        }

        public static string ApplyVillage()
        {
            RequireScene();
            var placement=UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>();
            var group=GameObject.Find("RanchWorld/group_monsters").transform;
            string[] ids={"GiantPoring","Minorous","Edgar","Smokie","Savage","Lunatic"};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];var existing=group.Find("obj_resident_"+id);GameObject monster;
                if(existing!=null)monster=existing.gameObject;
                else
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_"+id+".prefab");
                    if(prefab==null)throw new InvalidOperationException("Missing cartoon resident: "+id);
                    monster=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group);monster.name="obj_resident_"+id;
                    var bounds=RanchExpansionSetup.BoundsOf(monster);
                    monster.transform.localScale*=2.2f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                }
                foreach(var component in monster.GetComponentsInChildren<MonoBehaviour>(true))component.enabled=false;
                foreach(var collider in monster.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                foreach(var body in monster.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.detectCollisions=false;}
                RanchSurfaceRepair.ConfigureResident(monster,id,placement,i);
                var resident=monster.GetComponent<Sys_RanchResident>();
                resident.CelebrationClip=id=="Minorous"?null:Clip(id,"Reaction",SourceClip(id,false));
                resident.AttackClip=id=="Minorous"?null:Clip(id,"Attack",SourceClip(id,true));
                resident.RestClip=Clip(id,"Rest",RestSource(id));
                resident.EatClip=null; // No eating clip was supplied for these six skeletons.
                resident.FeedingHead=resident.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t=>t.name=="bone_head") ?? resident.TargetAnimator.transform;
                PrefabUtility.RecordPrefabInstancePropertyModifications(resident);
                foreach(var component in monster.GetComponentsInChildren<MonoBehaviour>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                foreach(var collider in monster.GetComponentsInChildren<Collider>(true))PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
            RanchExpansionSetup.ApplyLayout();
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(placement.gameObject.scene);
            return "Six cartoon residents, six exclusive activity spots, and revised ranch layout saved. Play mode was not entered.";
        }

        private static AnimationClip Clip(string id,string purpose,string path)
        {
            var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview"));
            if(source==null)throw new InvalidOperationException("Missing animation: "+path);
            string destination=artRoot+"/Animations/"+id+"_"+purpose+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
            if(clip==null){clip=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(clip,destination);}
            else EditorUtility.CopySerialized(source,clip);
            clip.name=id+"_"+purpose;AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(clip);return clip;
        }

        private static string SourceClip(string id,bool attack)
        {
            switch(id)
            {
                case "GiantPoring":return "Assets/OldAsset/Mounts/giantporing_001/animation/giantporing_001@giantporing_jump_01.FBX";
                case "Edgar":return "Assets/OldAsset/Mounts/vehicle_edgar_001/animation/vehicle_edgar_jump_01.FBX";
                case "Minorous":return "Assets/OldAsset/Monsters/resource/ani_fbx_001/morocc_mob/minorous/animation/minorous_001@minorous_skill_01_"+(attack?"fire":"cast")+".FBX";
                case "Smokie":return "Assets/OldAsset/Monsters/resource/ani_fbx_003/geffen_mob/smokie_001/animation/smokie_001@smokie_"+(attack?"skill01_fire":"idle_01")+".FBX";
                case "Savage":return "Assets/OldAsset/Monsters/resource/ani_fbx_002/mjolnir_mob/savage/animation/savage_001@savage_"+(attack?"meleeatk_01":"idle_01")+".FBX";
                default:return "Assets/OldAsset/Monsters/resource/ani_fbx_001/prontera_mob/lunatic/animation/lunatic_001@lunatic_"+(attack?"meleeatk_01":"idle_01")+".FBX";
            }
        }

        private static string RestSource(string id)
        {
            if(id=="GiantPoring")return "Assets/OldAsset/Mounts/giantporing_001/animation/giantporing_001@giantporing_idle_01.FBX";
            if(id=="Edgar")return "Assets/OldAsset/Mounts/vehicle_edgar_001/animation/vehicle_edgar_idle_01.FBX";
            if(id=="Minorous")return "Assets/OldAsset/Monsters/resource/ani_fbx_001/morocc_mob/minorous/animation/minorous_001@minorous_stand_01.FBX";
            return SourceClip(id,false);
        }

        private static Mesh SaveMesh(Mesh source)
        {
            string path=artRoot+"/Meshes/"+source.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){AssetDatabase.CreateAsset(source,path);return source;}
            EditorUtility.CopySerialized(source,mesh);UnityEngine.Object.DestroyImmediate(source);return mesh;
        }

        private static Transform AddMesh(Transform parent,string name,Mesh mesh,Material material,bool shadow)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=shadow;return obj.transform;
        }
    }
}
