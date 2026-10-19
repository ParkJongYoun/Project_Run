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
    // Imports only the eight supplied additions; existing facilities retain their assets and GUIDs.
    public static class RanchExpansionSetup
    {
        private const string recordRoot = "DataBundles/검증/목장_시설확장과조경_20261019";
        private const string prefabRoot = "Assets/Resources/Prefabs/Ranch/";
        private static readonly string[] ids = { "hot_spring", "race_track", "feeding_station", "windmill", "flower_cart", "fountain", "gazebo", "waterfall" };
        private static readonly string[] names = { "돌담 노천온천", "레이스 연습트랙", "몬스터 급식대", "꼬마풍차", "꽃 나무수레", "클로버 돌분수", "꽃덩굴 쉼터정자", "이끼바위 폭포" };
        private static readonly string[] portraits = { "12_돌담_노천온천", "14_타원형_레이스_연습트랙", "15_지붕형_몬스터_급식대", "16_푸른지붕_꼬마풍차", "17_꽃가득_나무수레", "18_클로버_돌분수", "19_꽃덩굴_쉼터정자", "20_이끼바위_2단폭포" };
        private static readonly Vector2Int[] sizes = { new Vector2Int(3,3), new Vector2Int(6,4), new Vector2Int(3,2), new Vector2Int(2,2), new Vector2Int(2,1), new Vector2Int(2,2), new Vector2Int(3,3), new Vector2Int(3,3) };
        private static readonly float[] heights = { 2.1f, 1.2f, 2.8f, 4.1f, 1.5f, 2.2f, 3.4f, 3.0f };

        public static string Import(string id, float yaw = 180)
        {
            RequireScene();
            int index = Array.IndexOf(ids, id);
            if (index < 0) throw new ArgumentException(id);
            string folder = RanchGeneratedImporter.GeneratedRoot + "/" + id;
            string modelPath = folder + "/" + id + ".fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException(modelPath);
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            Texture2D color = null, normal = null;
            foreach (string file in Directory.GetFiles(folder + "/Textures").Where(p => !p.EndsWith(".meta")))
            {
                string path = file.Replace('\\', '/');
                var textureImporter = (TextureImporter)AssetImporter.GetAtPath(path);
                bool isNormal = Path.GetFileNameWithoutExtension(path) == "Normal";
                textureImporter.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                textureImporter.sRGBTexture = !isNormal;
                textureImporter.maxTextureSize = 2048;
                textureImporter.mipmapEnabled = true;
                textureImporter.wrapMode = TextureWrapMode.Repeat;
                textureImporter.SaveAndReimport();
                if (isNormal) normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                else color = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (color == null) throw new InvalidOperationException("Missing albedo: " + id);
            string materialPath = folder + "/M_RanchGenerated_" + id + "_0.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var reference = AssetDatabase.LoadAssetAtPath<Material>(RanchPaintedLook.ArtRoot + "/Materials/M_RanchPainted_feed_store_0.mat");
                material = new Material(reference);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", color);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", normal == null ? 0 : .55f);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Cull", 2);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var root = new GameObject("P_Ranch_" + id, typeof(BoxCollider));
            GameObject prefab;
            string report;
            try
            {
                var wrapper = new GameObject("model_generated").transform;
                wrapper.SetParent(root.transform, false);
                wrapper.localRotation = Quaternion.Euler(0, yaw, 0);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), wrapper);
                model.name = "model_tripo_" + id;
                var renderers = model.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                var bounds = BoundsOf(root);
                float scale = Mathf.Min((sizes[index].x * 1.35f - .22f) / bounds.size.x,
                    (sizes[index].y * 1.35f - .22f) / bounds.size.z, heights[index] / bounds.size.y);
                wrapper.localScale = Vector3.one * scale;
                bounds = BoundsOf(root);
                wrapper.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                RanchSceneBuilder.FitSelectionCollider(root, sizes[index]);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabRoot + root.name + ".prefab");
                var meshes = model.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh).ToArray();
                report = id + " | yaw=" + yaw + " | size=" + BoundsOf(root).size + " | triangles=" + meshes.Sum(m => m.triangles.Length/3);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            string portraitPath = "Assets/ArtBundles/UI/Ranch/Portraits/" + id + ".png";
            File.Copy("DataBundles/아트/목장_시설물_20261019/트리포_입력원화/" + portraits[index] + ".png", portraitPath, true);
            AssetDatabase.ImportAsset(portraitPath);
            var portraitImporter = (TextureImporter)AssetImporter.GetAtPath(portraitPath);
            portraitImporter.textureType = TextureImporterType.Sprite;
            portraitImporter.spriteImportMode = SpriteImportMode.Single;
            portraitImporter.maxTextureSize = 512;
            portraitImporter.mipmapEnabled = false;
            portraitImporter.SaveAndReimport();
            var catalog = Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog");
            var definition = catalog.Find(id);
            if (definition == null)
            {
                definition = new Data_RanchFacility { Id = id };
                catalog.Facilities = catalog.Facilities.Concat(new[] { definition }).ToArray();
            }
            definition.Name = names[index];
            definition.Description = names[index] + " — 목장의 길과 연결해 자유롭게 배치할 수 있는 시설입니다.";
            definition.Category = id == "race_track" || id == "feeding_station" ? "시설" : "조경";
            definition.Footprint = sizes[index];
            definition.Prefab = prefab;
            definition.Portrait = AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(recordRoot);
            File.WriteAllText(recordRoot + "/모델_" + names[index] + ".txt", report);
            return report;
        }

        public static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void RequireScene()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != RanchSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open Scene_LobbyTest in Edit mode.");
        }

        // The approach belongs to the prefab, so it also follows later moves and rotations.
        public static void ConnectEntrances()
        {
            RequireScene();
            foreach (string id in new[] { "feeding_station", "windmill", "gazebo", "hot_spring", "waterfall", "race_track" })
            {
                int index = Array.IndexOf(ids, id);
                string path = prefabRoot + "P_Ranch_" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var previous = root.transform.Find("obj_entrance_path");
                    if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                    float halfWidth = id == "race_track" ? .95f : .72f;
                    float near = -sizes[index].y * 1.35f * .5f - .22f;
                    float far = near + 1.05f;
                    var mesh = new Mesh { name = "EntrancePath" };
                    mesh.vertices = new[] { new Vector3(-halfWidth,-.012f,near), new Vector3(-halfWidth,-.012f,far),
                        new Vector3(halfWidth,-.012f,far), new Vector3(halfWidth,-.012f,near) };
                    mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                    mesh.triangles = new[] { 0,1,2,0,2,3 };
                    mesh.RecalculateNormals(); mesh.RecalculateBounds();
                    string meshPath = RanchGeneratedImporter.GeneratedRoot + "/" + id + "/EntrancePath.asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, meshPath); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); }
                    var approach = new GameObject("obj_entrance_path", typeof(MeshFilter), typeof(MeshRenderer));
                    approach.transform.SetParent(root.transform, false);
                    if (id == "race_track") approach.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    approach.GetComponent<MeshFilter>().sharedMesh = saved;
                    var renderer = approach.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RanchPaintedLook.ArtRoot + "/Materials/M_RanchGroundApron.mat");
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Project Run/Ranch/Apply Garden Facility Layout")]
        public static void ApplyLayout()
        {
            RequireScene();
            ConnectEntrances();
            var placement = UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>();
            var layout = RanchDemoLayout.Create();
            if (!RanchGrid.Validate(placement.Catalog, layout, placement.GridSize))
                throw new InvalidOperationException("Layout overlaps or exceeds the ranch boundary.");
            Undo.RegisterFullObjectHierarchyUndo(placement.FacilitiesRoot.gameObject, "Arrange ranch garden");
            Undo.RecordObject(placement, "Arrange ranch garden");
            placement.InitialLayout = layout;
            foreach (var child in placement.FacilitiesRoot.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (var entry in layout.Facilities)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(placement.Catalog.Find(entry.FacilityId).Prefab, placement.FacilitiesRoot);
                instance.name = "obj_" + entry.FacilityId + "_" + entry.InstanceId;
                instance.transform.SetPositionAndRotation(placement.Position(entry), Quaternion.Euler(0, entry.Rotation * 90, 0));
            }
            placement.GetComponent<Sys_RanchRoads>().Initialize();
            RanchGroundDetails.ApplyBorders(GameObject.Find("RanchWorld/group_ground").transform);
            var residents = GameObject.Find("RanchWorld/group_monsters").transform;
            Vector2Int[] starts = { new Vector2Int(9,5), new Vector2Int(5,11), new Vector2Int(14,4),
                new Vector2Int(3,4), new Vector2Int(11,10), new Vector2Int(16,11) };
            for (int i = 0; i < residents.childCount; i++)
            {
                var point = starts[i % starts.Length];
                residents.GetChild(i).position = placement.GridOrigin + new Vector3((point.x+.5f)*placement.CellSize,.05f,(point.y+.5f)*placement.CellSize);
            }
            EditorUtility.SetDirty(placement);
            // Facility arrangement preserves the user's game camera and Scene View.
            AssetDatabase.SaveAssets();
            var scene = placement.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
