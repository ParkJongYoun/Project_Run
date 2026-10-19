using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectRun.Ranch;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class RanchDemoSetup
    {
        private const string portraitRoot="Assets/ArtBundles/UI/Ranch/Portraits";
        private const string artRoot=RanchPaintedLook.ArtRoot;

        [MenuItem("Tools/Project Run/Ranch/Apply Road Demo and Construction UI")]
        public static void Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(Application.isPlaying || scene.path!=RanchSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open Scene_LobbyTest in Edit mode first.");
            var placement=UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>();
            ConfigureCatalog(placement.Catalog);
            placement.InitialLayout=RanchDemoLayout.Create();
            foreach(var child in placement.FacilitiesRoot.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach(var entry in placement.InitialLayout.Facilities)
            {
                var definition=placement.Catalog.Find(entry.FacilityId);
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab,placement.FacilitiesRoot);
                obj.name="obj_"+entry.FacilityId+"_"+entry.InstanceId;
                obj.transform.SetPositionAndRotation(placement.Position(entry),Quaternion.Euler(0,entry.Rotation*90,0));
            }
            var roads=placement.GetComponent<Sys_RanchRoads>()??placement.gameObject.AddComponent<Sys_RanchRoads>();
            roads.Placement=placement;roads.Initialize();
            var residents=GameObject.Find("RanchWorld/group_monsters").transform;
            int index=0;
            foreach(Transform resident in residents)
            {
                resident.position=placement.GridOrigin+new Vector3((4.5f+index*5)*placement.CellSize,.05f,7.5f*placement.CellSize);
                index++;
            }
            var camera=Camera.main.GetComponent<Sys_RanchCamera>();
            camera.GroundBounds=new Vector4(-14.7f,-12,14.7f,12);
            camera.GroundViewportBottom=.12f;camera.ResetView();
            RanchGroundDetails.ApplyBorders(GameObject.Find("RanchWorld/group_ground").transform);
            var panel=UnityEngine.Object.FindFirstObjectByType<UIPanel_Ranch>();
            panel.Build();
            PrefabUtility.SaveAsPrefabAsset(panel.gameObject,"Assets/Resources/Prefabs/UI/Panel/UIPanel_Ranch.prefab");
            EditorUtility.SetDirty(placement);EditorUtility.SetDirty(camera);EditorUtility.SetDirty(roads);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        public static void ConfigureCatalog(Data_RanchCatalog catalog)
        {
            Directory.CreateDirectory(portraitRoot);
            string[] ids={"feed_store","shelter","training","house","broadleaf","pine","fence","gate","lamp","flowerbed","rock"};
            string[] names={"01_먹이창고","02_몬스터_축사","03_훈련장","04_목장_관리동","05_활엽수","06_침엽수","07_나무_울타리","08_목장_출입문","09_가로등","10_꽃_화단","11_바위"};
            for(int i=0;i<ids.Length;i++)
            {
                string source="DataBundles/아트/목장_시설물_20261019/트리포_입력원화/"+names[i]+".png";
                ImportPortrait(source,ids[i]);
                var item=catalog.Find(ids[i]);
                if(item!=null)item.Portrait=AssetDatabase.LoadAssetAtPath<Sprite>(portraitRoot+"/"+ids[i]+".png");
            }
            ImportPortrait(artRoot+"/Textures/PathDetailAlbedo.png","road");
            var road=catalog.Find("road")??new Data_RanchFacility();
            road.Id="road";road.Name="흙길";road.Description="잔디 위에 놓는 길. 연결된 길을 따라 몬스터가 이동합니다.";
            road.Footprint=Vector2Int.one;road.IsRoad=true;road.Category="길";
            road.Prefab=RoadPrefab();road.Portrait=AssetDatabase.LoadAssetAtPath<Sprite>(portraitRoot+"/road.png");
            var ordered=new List<Data_RanchFacility>{road};
            ordered.AddRange(catalog.Facilities.Where(item=>item.Id!="road"));
            catalog.Facilities=ordered.ToArray();EditorUtility.SetDirty(catalog);
        }

        private static void ImportPortrait(string source,string id)
        {
            string path=portraitRoot+"/"+id+".png";
            if(!File.Exists(path) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(path)))
            { File.Copy(source,path,true);AssetDatabase.ImportAsset(path); }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(importer==null) { AssetDatabase.ImportAsset(path);importer=(TextureImporter)AssetImporter.GetAtPath(path); }
            bool changed=importer.textureType!=TextureImporterType.Sprite || importer.maxTextureSize!=512 || importer.mipmapEnabled;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.alphaIsTransparency=true;
            if(changed)importer.SaveAndReimport();
        }

        private static GameObject RoadPrefab()
        {
            string meshPath=artRoot+"/Meshes/RanchRoadTile.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(mesh==null)
            {
                mesh=new Mesh{name="RanchRoadTile",vertices=new[]{new Vector3(-.675f,.008f,-.675f),new Vector3(-.675f,.008f,.675f),
                    new Vector3(.675f,.008f,.675f),new Vector3(.675f,.008f,-.675f)},uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right},triangles=new[]{0,1,2,0,2,3}};
                mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);
            }
            string materialPath=artRoot+"/Materials/M_RanchRoadPreview.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null) { material=new Material(Shader.Find("ProjectRun/Ranch/GroundPatch"));AssetDatabase.CreateAsset(material,materialPath); }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(artRoot+"/Textures/PathDetailAlbedo.png"));
            material.SetColor("_BaseColor",new Color(.97f,.93f,.83f));EditorUtility.SetDirty(material);
            var root=new GameObject("P_Ranch_road",typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterial=material;
                root.GetComponent<MeshRenderer>().enabled=false;
                var collider=root.GetComponent<BoxCollider>();collider.center=new Vector3(0,.02f,0);collider.size=new Vector3(1.33f,.08f,1.33f);
                return PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Prefabs/Ranch/P_Ranch_road.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
