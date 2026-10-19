using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static class RanchRevisionSetup
    {
        private const string artRoot="Assets/ArtBundles/Ranch/TripoModelsV2/";
        private const string prefabRoot="Assets/Resources/Prefabs/Ranch/P_Ranch_";
        private const string recordRoot="DataBundles/검증/목장_길확장과신규시설_20261019/";

        public static string PrepareModels()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit mode required.");
            var reports=new List<string>();
            foreach(string id in new[]{"race_track","waterfall","cherry_tree"})
            {
                string folder=artRoot+id;
                var importer=(ModelImporter)AssetImporter.GetAtPath(folder+"/"+id+".fbx");
                importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;
                importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.importTangents=ModelImporterTangents.CalculateMikk;importer.SaveAndReimport();
                foreach(string name in new[]{"BaseColor","Normal"})
                {
                    var texture=(TextureImporter)AssetImporter.GetAtPath(folder+"/Textures/"+name+".jpeg");
                    texture.textureType=name=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                    texture.sRGBTexture=name!="Normal";texture.mipmapEnabled=true;texture.filterMode=FilterMode.Trilinear;
                    texture.anisoLevel=4;texture.maxTextureSize=2048;texture.SaveAndReimport();
                }
                string materialPath=folder+"/M_Ranch_"+id+"_V2.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null)
                {
                    material=new Material(AssetDatabase.LoadAssetAtPath<Material>(RanchPaintedLook.ArtRoot+"/Materials/M_RanchPainted_feed_store_0.mat"));
                    AssetDatabase.CreateAsset(material,materialPath);
                }
                material.shader=Shader.Find(id=="waterfall"?"ProjectRun/Ranch/FlowingWater":"ProjectRun/Ranch/SoftPainted");
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Textures/BaseColor.jpeg"));
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Textures/Normal.jpeg"));
                material.SetFloat("_BumpScale",.85f);material.SetColor("_BaseColor",Color.white);
                EditorUtility.SetDirty(material);
                var size=id=="race_track"?new Vector2Int(6,4):id=="waterfall"?new Vector2Int(3,3):new Vector2Int(2,2);
                var root=new GameObject("P_Ranch_"+id+"_V2",typeof(BoxCollider));
                try
                {
                    var wrapper=new GameObject("model_generated").transform;wrapper.SetParent(root.transform,false);
                    wrapper.localRotation=Quaternion.Euler(0,id=="waterfall"?180:0,0);
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/"+id+".fbx"),wrapper);
                    model.name="model_tripo_"+id;
                    foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                    var bounds=RanchExpansionSetup.BoundsOf(root);
                    float height=id=="race_track"?1.2f:id=="waterfall"?3:4.2f;
                    float scale=Mathf.Min((size.x*1.35f-.22f)/bounds.size.x,(size.y*1.35f-.22f)/bounds.size.z,height/bounds.size.y);
                    wrapper.localScale=Vector3.one*scale;bounds=RanchExpansionSetup.BoundsOf(root);
                    wrapper.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    RanchSceneBuilder.FitSelectionCollider(root,size);
                    if(id=="race_track")
                    {
                        var old=AssetDatabase.LoadAssetAtPath<GameObject>(prefabRoot+id+".prefab").GetComponent<RanchFacilitySpot>();
                        var spot=root.AddComponent<RanchFacilitySpot>();EditorUtility.CopySerialized(old,spot);
                    }
                    var filters=model.GetComponentsInChildren<MeshFilter>();
                    int vertices=filters.Sum(f=>f.sharedMesh.vertexCount),triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3);
                    if(id=="cherry_tree" && vertices>50000)throw new InvalidOperationException("Cherry tree exceeds the vertex budget.");
                    PrefabUtility.SaveAsPrefabAsset(root,prefabRoot+id+"_V2.prefab");
                    reports.Add(id+" vertices="+vertices+" triangles="+triangles+" dimensions="+RanchExpansionSetup.BoundsOf(root).size);
                    ExportGeometry(root,id);
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            AssetDatabase.SaveAssets();File.WriteAllLines(recordRoot+"신규모델_규모.txt",reports);return string.Join("\n",reports);
        }

        private static void ExportGeometry(GameObject root,string id)
        {
            var vertices=new List<string>();var triangles=new List<int>();int offset=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var p=mesh.vertices;var uv=mesh.uv;
                for(int i=0;i<p.Length;i++)
                {
                    var v=root.transform.InverseTransformPoint(filter.transform.TransformPoint(p[i]));
                    vertices.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0:R} {1:R} {2:R} {3:R} {4:R}",v.x,v.y,v.z,uv[i].x,uv[i].y));
                }
                triangles.AddRange(mesh.triangles.Select(t=>t+offset));offset+=p.Length;
            }
            File.WriteAllLines(recordRoot+id+"_정점.txt",vertices);
            File.WriteAllLines(recordRoot+id+"_삼각형.txt",triangles.Select(t=>t.ToString()));
        }

        public static void ActivateModels()
        {
            var catalog=Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog");
            foreach(string id in new[]{"race_track","waterfall","cherry_tree"})
            {
                var definition=catalog.Find(id);
                if(definition==null)
                {
                    definition=new Data_RanchFacility{Id=id,Name="벚꽃나무",Description="경기장 주변을 꾸미는 분홍 벚꽃나무",Category="환경",Footprint=new Vector2Int(2,2)};
                    definition.Portrait=catalog.Find("broadleaf").Portrait;
                    catalog.Facilities=catalog.Facilities.Concat(new[]{definition}).ToArray();
                }
                definition.Prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabRoot+id+"_V2.prefab");
            }
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }

        public static string ScaleNewResidents()
        {
            var group=GameObject.Find("RanchWorld/group_monsters").transform;
            float ratio=(group.Find("obj_resident_Minorous").localScale.x+group.Find("obj_resident_Edgar").localScale.x)*.5f;
            var lines=new List<string>();
            foreach(string id in new[]{"Smokie","Savage","Lunatic"})
            {
                var resident=group.Find("obj_resident_"+id);
                // Explicit baseline avoids shrinking again if authoring is repeated.
                string key=recordRoot+id+"_축소전배율.txt";
                if(!File.Exists(key))File.WriteAllText(key,resident.localScale.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                float original=float.Parse(File.ReadAllText(key),System.Globalization.CultureInfo.InvariantCulture);
                resident.localScale=Vector3.one*original*ratio;
                resident.GetComponent<Sys_RanchResident>().Clearance=.34f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(resident);
                PrefabUtility.RecordPrefabInstancePropertyModifications(resident.GetComponent<Sys_RanchResident>());
                lines.Add(id+": "+original+" -> "+resident.localScale.x+" ("+ratio+")");
            }
            foreach(string id in new[]{"GiantPoring","Minorous","Edgar"})
            {
                var resident=group.Find("obj_resident_"+id).GetComponent<Sys_RanchResident>();resident.Clearance=.62f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(resident);
            }
            File.WriteAllLines(recordRoot+"캐릭터_축소.txt",lines);return string.Join("\n",lines);
        }
    }
}
