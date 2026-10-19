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
            foreach (string file in new[] { "MeadowAlbedo", "PathAlbedo", "MeadowDetailAlbedo", "PathDetailAlbedo" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(ArtRoot + "/Textures/" + file + ".png");
                bool changed = importer.textureType != TextureImporterType.Default || !importer.sRGBTexture ||
                    !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat ||
                    importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 4 || importer.maxTextureSize != 2048;
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 2048;
                if (changed) importer.SaveAndReimport();
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
                if (renderer.name.StartsWith("obj_ground_detail")) continue;
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
                    material.SetTexture("_BumpMap", floor ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(RanchGeneratedImporter.GeneratedRoot + "/" + id + "/Textures/Normal.png"));
                    material.SetFloat("_BumpScale", floor ? 0 : .85f);
                    material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                    material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                    material.SetColor("_BaseColor", floor ? source.GetColor("_BaseColor") : Color.white);
                    material.SetColor("_ShadeColor", new Color(.51f, .58f, .65f));
                    material.SetFloat("_Threshold", .18f);
                    material.SetFloat("_Softness", .22f);
                    material.SetFloat("_AmbientStrength", .07f);
                    material.SetFloat("_FillStrength", .2f);
                    material.SetFloat("_Specular", .008f);
                    material.SetFloat("_Gloss", 48);
                    material.SetFloat("_RimStrength", 0);
                    material.SetFloat("_AlbedoPower", 1);
                    material.SetFloat("_Cull", source.GetFloat("_Cull"));
                    material.SetFloat("_AlphaClip", 0);
                    material.enableInstancing = true;
                    assigned[i] = material;
                    EditorUtility.SetDirty(material);
                }
                renderer.sharedMaterials = assigned;
            }
            RanchGroundDetails.ApplyFoundation(root, id);
        }

        // Existing protected environment models and their placement are retained.
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
            material.SetTexture("_GrassMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/MeadowDetailAlbedo.png"));
            material.SetTexture("_PathMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/PathDetailAlbedo.png"));
            material.SetColor("_GrassTint", new Color(.94f, 1, .96f));
            material.SetColor("_PathTint", new Color(.97f, .93f, .83f));
            material.SetColor("_ShadeColor", new Color(.37f, .45f, .43f));
            material.SetFloat("_GrassScale", .24f);
            material.SetFloat("_GrassDetail", .72f);
            material.SetFloat("_PathScale", .22f);
            material.SetFloat("_EdgeWidth", .035f);
            material.SetFloat("_EdgeVariation", .10f);
            material.SetFloat("_ShadowStrength", 1);
            material.SetVector("_MainPath", new Vector4(-1.1f, 2.9f, 11.8f, .72f));
            material.SetVector("_EntryPath", new Vector4(.5f, -3.9f, .72f, 7.0f));
            material.SetVector("_SouthPath", new Vector4(-2.1f, -6.9f, 7.6f, .56f));
            grass.GetComponent<Renderer>().sharedMaterial = material;
            var oldPath = ground.Find("obj_Mat_Path");
            if (oldPath != null) oldPath.gameObject.SetActive(false);
            EditorUtility.SetDirty(material);
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(50, 75, 0);
                sun.color = new Color(1, .975f, .92f);
                sun.intensity = 1.08f;
                sun.shadowStrength = .88f;
                sun.shadows = LightShadows.Soft;
                sun.shadowBias = .03f;
                sun.shadowNormalBias = .15f;
                var lightData = sun.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                if (lightData != null)
                {
                    var serialized = new SerializedObject(lightData);
                    serialized.FindProperty("m_UsePipelineSettings").boolValue = false;
                    serialized.ApplyModifiedProperties();
                }
                EditorUtility.SetDirty(sun);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.61f, .72f, .85f);
            RenderSettings.ambientEquatorColor = new Color(.48f, .54f, .48f);
            RenderSettings.ambientGroundColor = new Color(.29f, .34f, .26f);
            var camera = Camera.main;
            var controller = camera.GetComponent<Sys_RanchCamera>();
            if (controller != null) controller.ResetView();
            RanchGroundDetails.ApplyBorders(ground);
        }
    }
}
