using System;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProjectRun.EditorTools
{
    /// <summary>Creates an isolated copy; never edits the race scene or source monster assets.</summary>
    public static class MinorousLookDevBuilder
    {
        public const string ScenePath = CartoonShowcaseBuilder.ScenePath;
        public const string ArtRoot = "Assets/ArtBundles/RaceShowcase/MinorousLookDev";

        [MenuItem("Project Run/Minorous Look/Create Isolated Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before creating the preview.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Preview scene already exists. Open it instead of overwriting it.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before creating the preview.");
            var shader = Shader.Find("ProjectRun/MinorousAnime");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "/Textures/Minorous_Albedo.png");
            if (shader == null || texture == null) throw new InvalidOperationException("Import the preview shader and texture first.");
            if (UniversalRenderPipeline.asset == null) throw new InvalidOperationException("URP is required.");
            if (!AssetDatabase.CopyAsset("Assets/Scenes/Scene_Oval.unity", ScenePath))
                throw new InvalidOperationException("Unable to copy the race scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var oldRunner = UnityEngine.Object.FindObjectsByType<RaceRunnerView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(view => view.name.Contains("Minorous"));
            var position = oldRunner.transform.position + Vector3.right * 3;
            var rotation = oldRunner.transform.rotation;
            foreach (var runner in UnityEngine.Object.FindObjectsByType<RaceRunnerView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                runner.gameObject.SetActive(false);
            foreach (var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (behaviour != null && behaviour.GetType().Namespace?.StartsWith("ProjectRun") == true)
                    behaviour.enabled = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "[Managers]" || root.name == "[Systems]")
                {
                    root.SetActive(false);
                    root.name += "_RaceDisabled";
                }
            }
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                canvas.gameObject.SetActive(false);
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                camera.gameObject.SetActive(false);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                light.enabled = false;
            foreach (var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                volume.enabled = false;
            foreach (var tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tr.name.StartsWith("obj_start_pad") || tr.name.StartsWith("obj_distance_board") || tr.name.StartsWith("obj_distance_post") || tr.name.StartsWith("txt_lane_"))
                    tr.gameObject.SetActive(false);

            var systems = new GameObject("[Systems]");
            var subject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Race/DemoMonsters/P_Runner_Minorous.prefab"));
            PrefabUtility.UnpackPrefabInstance(subject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            subject.name = "V_MinorousLookDev";
            subject.transform.SetPositionAndRotation(position, rotation);
            var view = subject.GetComponent<RaceRunnerView>();
            var animator = view.MountAnimator;
            var idle = view.IdleClip;
            foreach (var particle in subject.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
            foreach (var trail in subject.GetComponentsInChildren<TrailRenderer>(true)) trail.gameObject.SetActive(false);
            if (view.Shadow != null) view.Shadow.gameObject.SetActive(true);
            UnityEngine.Object.DestroyImmediate(view);
            var look = subject.GetComponent<Sys_MonsterLook>();
            if (look != null) UnityEngine.Object.DestroyImmediate(look);
            var surface = subject.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "minoruros_001");
            var outline = subject.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.StartsWith("obj_outline"));
            surface.enabled = true;
            surface.receiveShadows = true;
            outline.enabled = true;
            var original = surface.sharedMaterial;
            var originalOutline = outline.sharedMaterial;
            var material = new Material(shader) { name = "Minorous_Anime" };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_ShadeColor", new Color(.78f,.80f,.88f,1));
            material.SetFloat("_Threshold", .05f);
            material.SetFloat("_Softness", .13f);
            material.SetFloat("_Specular", .045f);
            material.SetFloat("_Gloss", 48);
            material.SetFloat("_RimStrength", .09f);
            material.SetFloat("_RimPower", 3.5f);
            material.SetColor("_RimColor", new Color(.80f,.89f,1,1));
            material.SetFloat("_AmbientStrength", .2f);
            material.SetFloat("_FillStrength", .45f);
            Directory.CreateDirectory(ArtRoot + "/Materials");
            AssetDatabase.CreateAsset(material, ArtRoot + "/Materials/Minorous_Anime.mat");
            var outlineMaterial = new Material(originalOutline) { name = "Minorous_AnimeOutline" };
            outlineMaterial.SetColor("_OutlineColor", new Color(.14f,.085f,.10f,1));
            outlineMaterial.SetFloat("_OutlineWidth", .65f);
            AssetDatabase.CreateAsset(outlineMaterial, ArtRoot + "/Materials/Minorous_AnimeOutline.mat");
            surface.sharedMaterial = material;
            outline.sharedMaterial = outlineMaterial;
            if (idle != null) idle.SampleAnimation(animator.gameObject, .15f);

            var controller = new GameObject("Sys_MinorousLookDev");
            controller.transform.SetParent(systems.transform);
            var preview = controller.AddComponent<Sys_MinorousLookDev>();
            var serialized = new SerializedObject(preview);
            SetReference(serialized, "Surface", surface);
            SetReference(serialized, "OriginalMaterial", original);
            SetReference(serialized, "LookDevMaterial", material);
            SetReference(serialized, "Outline", outline);
            SetReference(serialized, "OriginalOutline", originalOutline);
            SetReference(serialized, "LookDevOutline", outlineMaterial);
            SetReference(serialized, "TargetAnimator", animator);
            SetReference(serialized, "IdleClip", idle);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var target = surface.bounds.center + Vector3.up * .22f;
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var previewCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            previewCamera.transform.position = surface.bounds.center + new Vector3(6.6f, 1f, 6.6f);
            previewCamera.transform.LookAt(target);
            previewCamera.fieldOfView = 30;
            previewCamera.nearClipPlane = .1f;
            previewCamera.farClipPlane = 500;
            previewCamera.allowHDR = true;
            previewCamera.clearFlags = CameraClearFlags.Skybox;
            var cameraData = previewCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.volumeLayerMask = 1;
            cameraData.requiresDepthOption = CameraOverrideOption.On;

            var rig = new GameObject("obj_lookdev_lighting");
            var key = MakeLight(rig.transform, "light_key", new Vector3(5,7,-4), new Color(1,.965f,.91f), 1.1f, LightShadows.Soft);
            MakeLight(rig.transform, "light_fill", new Vector3(3,2,7), new Color(.68f,.81f,1), .45f, LightShadows.None);
            MakeLight(rig.transform, "light_rim", new Vector3(-4,5,2), new Color(1,.87f,.67f), .7f, LightShadows.None);
            RenderSettings.sun = key;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.60f,.70f,.85f);
            RenderSettings.ambientEquatorColor = new Color(.46f,.49f,.57f);
            RenderSettings.ambientGroundColor = new Color(.30f,.25f,.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.72f,.80f,.87f);
            RenderSettings.fogStartDistance = 55;
            RenderSettings.fogEndDistance = 180;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ArtRoot + "/Minorous_LookDevVolume.asset");
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.Neutral;
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = .1f;
            color.contrast.value = 3;
            color.saturation.value = -3;
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.value = 13;
            dof.gaussianEnd.value = 25;
            dof.gaussianMaxRadius.value = 1.2f;
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            var volumeObject = new GameObject("obj_lookdev_color");
            var newVolume = volumeObject.AddComponent<Volume>();
            newVolume.isGlobal = true;
            newVolume.priority = 100;
            newVolume.sharedProfile = profile;

            // The isolated scene uses calm turf colors to keep the character readable.
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("Turf")))
            {
                var ground = new Material(renderer.sharedMaterial) { name = renderer.name + "_LookDev" };
                ground.SetTexture("_BaseMap", Texture2D.whiteTexture);
                ground.SetColor("_BaseColor", renderer.name == "TurfDark"
                    ? new Color(.40f,.53f,.29f,1) : new Color(.45f,.58f,.32f,1));
                AssetDatabase.CreateAsset(ground, ArtRoot + "/Materials/" + ground.name + ".mat");
                renderer.sharedMaterial = ground;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = subject;
            Debug.Log("Minorous look preview created: " + ScenePath);
        }

        private static void SetReference(SerializedObject serialized, string name, UnityEngine.Object value)
            => serialized.FindProperty(name).objectReferenceValue = value;

        private static Light MakeLight(Transform parent, string name, Vector3 direction, Color color, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.rotation = Quaternion.LookRotation(-direction.normalized);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowBias = .005f;
            light.shadowNormalBias = .06f;
            return light;
        }
    }
}
