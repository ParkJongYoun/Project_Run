using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static class RanchPaintedLook
    {
        public const string ArtRoot = "Assets/ArtBundles/Ranch/PaintedLook";
        private const string recordRoot = "DataBundles/아트/목장_환경톤_개선_20261019/작업기록";
        private static readonly string[] facilityIds = { "feed_store", "shelter", "training", "house", "lamp", "flowerbed" };

        public static bool IsAvailable => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/MeadowAlbedo.png") != null;

        [MenuItem("Tools/Project Run/Ranch/Apply Painted Ground and Light")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != RanchSceneBuilder.ScenePath)
                throw new InvalidOperationException("Open Scene_LobbyTest in Edit mode first.");
            if (!IsAvailable) throw new InvalidOperationException("Painted ground textures are missing.");
            foreach (string file in new[] { "MeadowAlbedo", "PathAlbedo" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(ArtRoot + "/Textures/" + file + ".png");
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            foreach (string id in facilityIds)
            {
                string path = "Assets/Resources/Prefabs/Ranch/P_Ranch_" + id + ".prefab";
                string backup = recordRoot + "/적용전_프리팹/" + Path.GetFileName(path);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path, backup);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { ApplyToRoot(root, id); PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            ApplyScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // Called by the importer so a later FBX reimport retains this art direction.
        public static void ApplyToRoot(GameObject root, string id)
        {
            if (!IsAvailable || !facilityIds.Contains(id)) return;
            var shader = Shader.Find("ProjectRun/Ranch/SoftPainted");
            if (shader == null) throw new InvalidOperationException("Ranch surface shader missing.");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var assigned = renderer.sharedMaterials;
                for (int i = 0; i < assigned.Length; i++)
                {
                    bool floor = renderer.name == "obj_ground_repair";
                    string sourcePath = RanchGeneratedImporter.GeneratedRoot + "/" + id + "/" +
                        (floor ? "M_BarnFloor.mat" : "M_RanchGenerated_" + id + "_" + i + ".mat");
                    var source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                    if (source == null) throw new InvalidOperationException("Source material missing: " + sourcePath);
                    string path = ArtRoot + "/Materials/M_RanchPainted_" + id + (floor ? "_Floor" : "_" + i) + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                    material.shader = shader;
                    material.SetTexture("_BaseMap", source.GetTexture("_BaseMap") ?? Texture2D.whiteTexture);
                    material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                    material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                    material.SetColor("_BaseColor", floor ? source.GetColor("_BaseColor") : Color.white);
                    material.SetColor("_ShadeColor", new Color(.74f, .79f, .86f));
                    material.SetFloat("_Threshold", .08f);
                    material.SetFloat("_Softness", .25f);
                    material.SetFloat("_AmbientStrength", .09f);
                    material.SetFloat("_FillStrength", .2f);
                    material.SetFloat("_Specular", .008f);
                    material.SetFloat("_Gloss", 48);
                    material.SetFloat("_RimStrength", 0);
                    material.SetFloat("_AlbedoPower", floor ? 1 : .92f);
                    material.SetFloat("_Cull", source.GetFloat("_Cull"));
                    material.SetFloat("_AlphaClip", 0);
                    material.enableInstancing = true;
                    assigned[i] = material;
                    EditorUtility.SetDirty(material);
                }
                renderer.sharedMaterials = assigned;
            }
        }

        // Only ground surfaces and this scene's existing light change here.
        // The fence, gate, trees, rocks, forest meshes, UI, and camera are not rebuilt.
        public static void ApplyScene()
        {
            if (!IsAvailable) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != RanchSceneBuilder.ScenePath || Application.isPlaying)
                throw new InvalidOperationException("Apply the ranch look only in Scene_LobbyTest Edit mode.");
            var ground = GameObject.Find("RanchWorld").transform.Find("group_ground");
            var grass = ground.Find("obj_Mat_Grass");
            if (grass == null) throw new InvalidOperationException("Ranch meadow renderer missing.");
            var shader = Shader.Find("ProjectRun/Ranch/PaintedMeadow");
            if (shader == null) throw new InvalidOperationException("Ranch meadow shader missing.");
            string path = ArtRoot + "/Materials/M_RanchMeadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_GrassMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/MeadowAlbedo.png"));
            material.SetTexture("_PathMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/PathAlbedo.png"));
            material.SetColor("_GrassTint", new Color(.95f, 1, .94f));
            material.SetColor("_PathTint", new Color(1, 1, .98f));
            material.SetColor("_ShadeColor", new Color(.67f, .75f, .70f));
            material.SetFloat("_GrassScale", .15f);
            material.SetFloat("_PathScale", .28f);
            material.SetFloat("_EdgeWidth", .13f);
            material.SetFloat("_EdgeVariation", .20f);
            material.SetFloat("_ShadowStrength", .72f);
            material.SetVector("_MainPath", new Vector4(0, -2.3f, 14.5f, .825f));
            material.SetVector("_EntryPath", new Vector4(.5f, -6.1f, .825f, 4.8f));
            grass.GetComponent<Renderer>().sharedMaterial = material;
            var oldPath = ground.Find("obj_Mat_Path");
            if (oldPath != null) oldPath.gameObject.SetActive(false);
            EditorUtility.SetDirty(material);
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.color = new Color(1, .975f, .94f);
                sun.intensity = 1.04f;
                sun.shadowStrength = .55f;
                sun.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(sun);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.74f, .81f, .90f);
            RenderSettings.ambientEquatorColor = new Color(.64f, .67f, .65f);
            RenderSettings.ambientGroundColor = new Color(.46f, .47f, .43f);
        }
    }
}
