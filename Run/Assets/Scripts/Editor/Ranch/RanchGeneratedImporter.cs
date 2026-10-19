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
    public static class RanchGeneratedImporter
    {
        public const string GeneratedRoot = "Assets/ArtBundles/Ranch/TripoModels";
        private const string recordRoot = "DataBundles/아트/목장_시설물_20261019/작업기록/트리포_적용";

        [Serializable]
        public sealed class ModelReport
        {
            public string Id;
            public string ModelPath;
            public int Vertices;
            public int Triangles;
            public int Meshes;
            public int Materials;
            public float Yaw;
            public Vector3 Size;
        }

        public static ModelReport Import(string id, float yaw = 270)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play before importing models.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != RanchSceneBuilder.ScenePath) throw new InvalidOperationException("Open Scene_LobbyTest first.");
            var catalog = Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog");
            var definition = catalog.Find(id) ?? throw new InvalidOperationException("Unknown facility " + id);
            string folder = GeneratedRoot + "/" + id;
            string modelPath = folder + "/" + id + ".fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing FBX " + modelPath);
            string textureFolder = folder + "/Textures";
            Directory.CreateDirectory(textureFolder);
            AssetDatabase.Refresh();
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = true;
            importer.keepQuads = false; // Keep the source FBX intact; Unity renders triangles.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
            importer.ExtractTextures(textureFolder);
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(textureFolder))
            {
                var textureImporter = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (textureImporter == null) continue;
                textureImporter.maxTextureSize = 2048;
                textureImporter.mipmapEnabled = true;
                if (Path.GetFileNameWithoutExtension(path).Equals("Normal", StringComparison.OrdinalIgnoreCase))
                {
                    textureImporter.textureType = TextureImporterType.NormalMap;
                    textureImporter.sRGBTexture = false;
                }
                textureImporter.SaveAndReimport();
            }
            importer.SaveAndReimport();
            string prefabPath = AssetDatabase.GetAssetPath(definition.Prefab);
            string backup = recordRoot + "/교체전_프리팹";
            Directory.CreateDirectory(backup);
            string backupFile = backup + "/" + Path.GetFileName(prefabPath);
            if (!File.Exists(backupFile)) File.Copy(prefabPath, backupFile);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var wrapper = new GameObject("model_generated").transform;
                wrapper.SetParent(root.transform, false);
                wrapper.localRotation = Quaternion.Euler(0, yaw, 0);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, wrapper);
                model.name = "model_tripo_" + id;
                var renderers = model.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("No mesh renderers in " + id);
                var materials = new Dictionary<Material, Material>();
                int index = 0;
                foreach (var renderer in renderers)
                {
                    var assigned = new Material[renderer.sharedMaterials.Length];
                    for (int slot = 0; slot < assigned.Length; slot++)
                    {
                        var original = renderer.sharedMaterials[slot];
                        if (original == null) throw new InvalidOperationException("Missing source material in " + id);
                        if (!materials.TryGetValue(original, out var material))
                        {
                            Texture color = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture;
                            if (color == null) throw new InvalidOperationException("Missing base color texture in " + id + ": " + original.name);
                            string materialPath = folder + "/M_RanchGenerated_" + id + "_" + index++ + ".mat";
                            material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
                            material.SetColor("_BaseColor", Color.white);
                            material.SetTexture("_BaseMap", color);
                            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + "/Normal.png");
                            material.SetTexture("_BumpMap", normal);
                            material.SetFloat("_BumpScale", .8f);
                            if (normal != null) material.EnableKeyword("_NORMALMAP");
                            material.SetFloat("_Metallic", 0);
                            material.SetFloat("_Smoothness", .08f);
                            material.SetFloat("_Cull", 2);
                            material.enableInstancing = true;
                            EditorUtility.SetDirty(material);
                            materials.Add(original, material);
                        }
                        assigned[slot] = material;
                    }
                    renderer.sharedMaterials = assigned;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                var bounds = BoundsOf(renderers);
                float cap = HeightCap(id);
                float scale = Mathf.Min((definition.Footprint.x * 1.35f - .25f) / bounds.size.x,
                    (definition.Footprint.y * 1.35f - .25f) / bounds.size.z, cap / bounds.size.y);
                wrapper.localScale = Vector3.one * scale;
                bounds = BoundsOf(renderers);
                wrapper.position -= new Vector3(bounds.center.x - root.transform.position.x, bounds.min.y - root.transform.position.y, bounds.center.z - root.transform.position.z);
                // Ground surfaces in the generated open facilities contain gaps; keep a shallow bed beneath them.
                if (id == "shelter" || id == "training")
                {
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "obj_ground_repair";
                    floor.transform.SetParent(root.transform, false);
                    floor.transform.localPosition = new Vector3(0, -.03f, 0);
                    float ratio = id == "training" ? .93f : .87f;
                    floor.transform.localScale = new Vector3(BoundsOf(renderers).size.x * ratio, .05f, BoundsOf(renderers).size.z * (id == "training" ? .93f : .83f));
                    UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
                    string floorPath = folder + "/M_BarnFloor.mat";
                    var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(floorPath);
                    if (floorMaterial == null) { floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(floorMaterial, floorPath); }
                    floorMaterial.SetColor("_BaseColor", id == "training" ? new Color(.75f, .65f, .40f) : new Color(.60f, .42f, .20f));
                    floorMaterial.SetFloat("_Smoothness", 0);
                    floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                    EditorUtility.SetDirty(floorMaterial);
                }
                if (id == "lamp") RepairLampPost(root, model, folder, renderers[0].sharedMaterial);
                RanchSceneBuilder.FitSelectionCollider(root, definition.Footprint);
                var meshes = model.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh).ToArray();
                if (meshes.Any(mesh => mesh == null || mesh.uv.Length != mesh.vertexCount)) throw new InvalidOperationException("Missing mesh or UV in " + id);
                var report = new ModelReport { Id = id, ModelPath = modelPath, Meshes = meshes.Length, Materials = materials.Count,
                    Vertices = meshes.Sum(mesh => mesh.vertexCount), Triangles = meshes.Sum(mesh => mesh.triangles.Length / 3),
                    Yaw = yaw, Size = BoundsOf(renderers).size };
                RanchPaintedLook.ApplyToRoot(root, id);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Directory.CreateDirectory(recordRoot);
                File.WriteAllText(recordRoot + "/" + id + "_Unity검증.json", JsonUtility.ToJson(report, true));
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                return report;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Bounds BoundsOf(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void RepairLampPost(GameObject root, GameObject model, string folder, Material wood)
        {
            var source = model.GetComponentInChildren<MeshFilter>();
            var vertices = source.sharedMesh.vertices;
            var uvs = source.sharedMesh.uv;
            Vector3 samplePoint = root.transform.TransformPoint(new Vector3(-.322f, 1.42f, .09f));
            int closest = 0;
            float distance = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                float candidate = (source.transform.TransformPoint(vertices[i]) - samplePoint).sqrMagnitude;
                if (candidate < distance) { distance = candidate; closest = i; }
            }
            var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = "obj_post_repair";
            patch.transform.SetParent(root.transform, false);
            patch.transform.localPosition = new Vector3(-.322f, 1.60f, 0);
            patch.transform.localScale = new Vector3(.19f, .25f, .17f);
            UnityEngine.Object.DestroyImmediate(patch.GetComponent<Collider>());
            string meshPath = folder + "/LampPostRepair.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = UnityEngine.Object.Instantiate(patch.GetComponent<MeshFilter>().sharedMesh);
                mesh.name = "LampPostRepair";
                mesh.uv = mesh.uv.Select(uv => uvs[closest] + (uv - Vector2.one * .5f) * .005f).ToArray();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            patch.GetComponent<MeshFilter>().sharedMesh = mesh;
            patch.GetComponent<Renderer>().sharedMaterial = wood;
        }

        private static float HeightCap(string id)
        {
            switch (id)
            {
                case "training": return 2.8f;
                case "broadleaf": return 4.0f;
                case "pine": return 4.6f;
                case "fence": return 1.1f;
                case "gate": return 1.8f;
                case "lamp": return 2.7f;
                case "flowerbed": return .9f;
                case "rock": return 1.1f;
                default: return 4.5f;
            }
        }

        [MenuItem("Tools/Project Run/Ranch/Validate Generated Models")]
        public static string ValidateGenerated()
        {
            var catalog = Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog");
            var lines = new List<string>();
            foreach (var definition in catalog.Facilities)
            {
                var prefab = definition.Prefab;
                var visual = prefab.transform.Find("model_generated");
                if (visual == null) throw new InvalidOperationException("Not replaced: " + definition.Id);
                if (prefab.transform.localScale != Vector3.one || prefab.transform.localPosition != Vector3.zero)
                    throw new InvalidOperationException("Non-default root transform: " + definition.Id);
                var renderers = visual.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Any(r => r.sharedMaterials.Any(m => m == null ||
                    (m.shader.name != "Universal Render Pipeline/Lit" && m.shader.name != "ProjectRun/Ranch/SoftPainted") || m.GetTexture("_BaseMap") == null)))
                    throw new InvalidOperationException("Invalid material: " + definition.Id);
                var meshes = visual.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                if (meshes.Any(m => m == null || m.vertexCount == 0 || m.uv.Length != m.vertexCount))
                    throw new InvalidOperationException("Invalid mesh or UV: " + definition.Id);
                var bounds = BoundsOf(renderers);
                if (bounds.size.x > definition.Footprint.x * 1.35f + .001f || bounds.size.z > definition.Footprint.y * 1.35f + .001f)
                    throw new InvalidOperationException("Model exceeds grid footprint: " + definition.Id);
                if (Mathf.Abs(bounds.min.y) > .01f || Mathf.Abs(bounds.center.x) > .01f || Mathf.Abs(bounds.center.z) > .01f)
                    throw new InvalidOperationException("Bottom center pivot mismatch: " + definition.Id);
                var collider = prefab.GetComponent<BoxCollider>();
                // Prefab assets are not active physics objects, so Collider.bounds can be empty.
                if (collider == null || collider.center.y + collider.size.y * .5f < bounds.max.y - .01f)
                    throw new InvalidOperationException("Selection collider below roof: " + definition.Id);
                lines.Add("PASS " + definition.Id + " | tri=" + meshes.Sum(m => m.triangles.Length / 3) + " | material, UV, footprint, bottom center, collider");
            }
            var forest = GameObject.Find("RanchWorld").transform.Find("group_forest");
            if (forest.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterials.Any(m => !m.name.StartsWith("M_RanchGenerated_"))))
                throw new InvalidOperationException("Procedural forest meshes remain");
            lines.Add("PASS forest uses generated trees and rocks");
            var ground = GameObject.Find("RanchWorld").transform.Find("group_ground");
            if (!ground.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterials.Any(m => m.name.StartsWith("M_RanchGenerated_fence"))))
                throw new InvalidOperationException("Generated perimeter fence missing");
            lines.Add("PASS perimeter uses generated fence");
            Directory.CreateDirectory(recordRoot);
            File.WriteAllLines(recordRoot + "/생성모델_연결검증.txt", lines);
            return string.Join("\n", lines);
        }
    }
}
