using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        private const string look = art + "/LookDevelopment";

        [MenuItem("Tools/Project Run/Race/Apply rider motion and lit materials")]
        public static void ApplyPolish()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Scene_All3D.unity")
                throw new InvalidOperationException("Open Scene_All3D in Edit mode.");
            Ensure(look + "/Materials"); Ensure(look + "/Textures"); Ensure(look + "/Lighting");
            for (int i = 0; i < mounts.Length; i++)
            {
                string path = output + "/P_Runner_" + (i + 1).ToString("00") + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureRunnerMotion(root.GetComponent<RaceRunnerView>(), i);
                    ApplyRunnerMaterials(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var view in Object.FindObjectsByType<RaceRunnerView>(FindObjectsSortMode.None))
            {
                ConfigureRunnerMotion(view, view.RunnerId);
                ApplyRunnerMaterials(view.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                EditorUtility.SetDirty(view);
            }
            ConfigureRaceLighting();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureRunnerMotion(RaceRunnerView view, int index)
        {
            // Edgar: one two-step stride; poring: two bounces; fox: one gallop; throne: one hover.
            view.RunReferenceSpeed = new[] { 11.5f, 13.0f, 9.3f, 13.0f, 25.8f, 12.3f }[index] * (modelScale / 2.2f);
            view.GallopsPerClip = index == 1 || index == 4 || index == 5 ? 2 : 1;
            view.RiderMotion = new[] { 0.95f, 0.9f, 0.8f, 0.7f, 0.8f, 0.8f }[index];
            view.CameraFocusHeight = new[] { 2.6f, 2.25f, 2.25f, 2.7f, 2.25f, 2.4f }[index] * modelScale / 2.2f;
            view.HasFootfalls = index != 1 && index != 3;
            view.Posture = index == 0 ? E_RiderPosture.Carried : index == 1 ? E_RiderPosture.Kneeling : index == 3 ? E_RiderPosture.Seated : E_RiderPosture.Straddling;
            view.UseRiderFacingCloseup = index == 0;
            view.FaceForwardAxis = new Vector3(0.0137f, 0.9999f, 0.0001f);
            // The outermost poring needs a forward / outer angle above the rail,
            // keeping both the tiger and boar out of the line of sight.
            view.BurstCameraOffset = index == 1 ? new Vector3(2.8f, 1.6f, 8) : new Vector3(-11, 0.9f, 4);
            view.BurstTravel = index == 1 ? 3 : 6;
        }

        private static void ApplyRunnerMaterials(GameObject root)
        {
            Ensure(look + "/Materials"); Ensure(look + "/Textures");
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = skin.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = LitMaterial(materials[i]);
                skin.sharedMaterials = materials;
                skin.shadowCastingMode = ShadowCastingMode.On;
                skin.receiveShadows = true;
                skin.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                skin.lightProbeUsage = LightProbeUsage.BlendProbes;
                skin.renderingLayerMask |= 2u;
                EditorUtility.SetDirty(skin);
                if (PrefabUtility.IsPartOfPrefabInstance(skin)) PrefabUtility.RecordPrefabInstancePropertyModifications(skin);
            }
        }

        private static Material LitMaterial(Material source)
        {
            if (source == null) throw new InvalidOperationException("Missing original runner material.");
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath.StartsWith(look + "/Materials/", StringComparison.Ordinal))
            {
                string shortId = Path.GetFileNameWithoutExtension(sourcePath).Split('_').Last();
                string originalId = AssetDatabase.FindAssets("t:Material", new[] { "Assets/ArtBundles/LegacyURP" })
                    .First(g => g.StartsWith(shortId, StringComparison.Ordinal));
                sourcePath = AssetDatabase.GUIDToAssetPath(originalId);
                source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            }
            string id = AssetDatabase.AssetPathToGUID(sourcePath).Substring(0, 8);
            string name = source.name.Replace(" ", "_");
            string path = look + "/Materials/Mat_Race_" + name + "_" + id + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("URP Lit is unavailable.");
            var originalTexture = source.GetTexture("_BaseMap") as Texture2D;
            if (originalTexture == null) throw new InvalidOperationException("Missing base texture: " + sourcePath);
            var texture = CopyRaceTexture(originalTexture);
            if (create) material = new Material(shader) { name = "Mat_Race_" + name, enableInstancing = true };
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            material.SetFloat("_WorkflowMode", 0); // Specular workflow keeps cloth and skin non-metallic.
            material.EnableKeyword("_SPECULAR_SETUP");
            material.SetFloat("_SpecularHighlights", 1);
            material.SetFloat("_EnvironmentReflections", 1);
            material.SetFloat("_ReceiveShadows", 1);
            material.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2);
            bool face = name.StartsWith("face");
            bool hair = name.StartsWith("hair");
            bool silver = name.Contains("warrior") || name.Contains("defender") || name.Contains("vehicle_wolf") || name.Contains("vehicle_savage");
            bool gold = name.Contains("firewolf") || name.Contains("butlerwhisper");
            bool jelly = name.Contains("giantporing");
            // A small painted-light contribution keeps faces legible underneath overhanging hair.
            if (face)
            {
                material.SetTexture("_EmissionMap", texture);
                material.SetColor("_EmissionColor", new Color(0.18f, 0.17f, 0.16f));
                material.EnableKeyword("_EMISSION");
                material.SetFloat("_ReceiveShadows", 0);
                material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            if (silver || gold)
            {
                material.SetTexture("_SpecGlossMap", MakeSpecularMap(originalTexture, gold));
                material.EnableKeyword("_SPECGLOSSMAP");
                material.SetFloat("_Smoothness", 1);
            }
            else
            {
                float specular = face ? 0.022f : hair ? 0.055f : jelly ? 0.065f : 0.035f;
                material.SetColor("_SpecColor", new Color(specular, specular, specular));
                material.SetFloat("_Smoothness", face ? 0.22f : hair ? 0.43f : jelly ? 0.5f : 0.25f);
            }
            bool cutout = source.IsKeywordEnabled("_ALPHATEST_ON");
            material.SetFloat("_AlphaClip", cutout ? 1 : 0);
            material.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.4f);
            if (cutout) material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
            material.renderQueue = cutout ? 2450 : 2000;
            if (create) AssetDatabase.CreateAsset(material, path);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CopyRaceTexture(Texture2D original)
        {
            string source = AssetDatabase.GetAssetPath(original);
            string path = look + "/Textures/" + Path.GetFileNameWithoutExtension(source) + "_" + AssetDatabase.AssetPathToGUID(source).Substring(0, 8) + ".png";
            if (!File.Exists(path))
            {
                File.Copy(source, path);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = 1024;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = ((TextureImporter)AssetImporter.GetAtPath(source)).alphaIsTransparency;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D MakeSpecularMap(Texture2D original, bool gold)
        {
            string source = AssetDatabase.GetAssetPath(original);
            string path = look + "/Textures/Spec_" + original.name + (gold ? "_Gold" : "") + ".png";
            if (!File.Exists(path))
            {
                // These old atlases have no material masks. Estimate only neutral armour / muted gold;
                // keep saturated cloth, skin and flame colours rough. This is not a normal-map substitute.
                var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                pixels.LoadImage(File.ReadAllBytes(source));
                var data = pixels.GetPixels();
                for (int i = 0; i < data.Length; i++)
                {
                    var c = data[i];
                    float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    float saturation = max > 0.001f ? (max - min) / max : 0;
                    float mask = gold
                        ? Mathf.InverseLerp(0.55f, 0.82f, c.g / Mathf.Max(c.r, 0.01f)) * Mathf.InverseLerp(0.23f, 0.48f, c.b / Mathf.Max(c.r, 0.01f))
                        : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.08f, 0.27f, saturation));
                    // White ghost bodies share the throne atlas; only warm gold trim is metallic.
                    if (gold) mask *= 1 - Mathf.SmoothStep(0.65f, 0.9f, c.b / Mathf.Max(c.r, 0.01f));
                    mask *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.17f, 0.45f, max));
                    var reflectance = Color.Lerp(new Color(0.035f, 0.035f, 0.035f), gold ? new Color(0.34f, 0.25f, 0.11f) : new Color(0.28f, 0.30f, 0.33f), mask);
                    // PNG colour channels are sRGB; glossiness in alpha remains linear.
                    var encoded = reflectance.gamma; encoded.a = Mathf.Lerp(0.23f, 0.57f, mask);
                    data[i] = encoded;
                }
                pixels.SetPixels(data); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG()); Object.DestroyImmediate(pixels);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture = true; importer.mipmapEnabled = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100; importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ConfigureRaceLighting()
        {
            Ensure(look + "/Lighting");
            var root = GetRoot("P_RaceLighting");
            var fill = Get<Light>(GetChild(root.transform, "obj_sky_fill"));
            fill.type = LightType.Directional; fill.color = new Color(0.92f, 0.95f, 1);
            fill.intensity = 0.75f; fill.shadows = LightShadows.None;
            Get<UniversalAdditionalLightData>(fill.gameObject).renderingLayers = 2u;
            var lighting = Get<Sys_RaceLighting>(GetChild(GetRoot("[Systems]").transform, "Sys_RaceLighting"));
            lighting.ViewCamera = Camera.main; lighting.CharacterFill = fill; lighting.AlignToCamera();
            var probe = Get<ReflectionProbe>(GetChild(root.transform, "obj_course_reflection"));
            probe.transform.position = new Vector3(0, 8, 0);
            probe.size = new Vector3(500, 100, 340); probe.center = Vector3.zero;
            probe.mode = ReflectionProbeMode.Baked; probe.resolution = 128; probe.hdr = true;
            probe.boxProjection = false; probe.intensity = 0.75f; probe.importance = 10;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.nearClipPlane = 0.5f; probe.farClipPlane = 500;
            var views = Object.FindObjectsByType<RaceRunnerView>(FindObjectsSortMode.None);
            var activeStates = views.Select(v => v.gameObject.activeSelf).ToArray();
            try
            {
                foreach (var view in views) view.gameObject.SetActive(false);
                if (!Lightmapping.BakeReflectionProbe(probe, look + "/Lighting/RaceEnvironment.exr"))
                    throw new InvalidOperationException("Could not bake the race environment reflection.");
            }
            finally { for (int i = 0; i < views.Length; i++) views[i].gameObject.SetActive(activeStates[i]); }
            Camera.main.allowMSAA = true;
            // Keep the scene's existing SMAA. Its desktop renderer is deferred, so MSAA is ineffective.
        }
    }
}

