using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.EditorTools
{
    public static class CartoonShowcaseBuilder
    {
        public const string ScenePath = "Assets/Scenes/Scene_CartoonTest.unity";
        private const string art = "Assets/ArtBundles/Characters/CartoonMonsters";
        private const string prefabs = "Assets/Resources/Prefabs/Race/CartoonMonsters";
        private const string stageArt = "Assets/ArtBundles/CartoonTest";
        private const string records = "DataBundles/아트/카툰탈것6종_전시확장_20261019/작업기록";
        private static readonly string[] newIds = { "Poring", "Pecopeco", "Deviruchi", "Baphomet", "BaphometJr", "Fox", "Alpaca", "BlueDragon", "Horse", "ButlerWhisper", "FlameBird", "PorterPuppy" };
        private static readonly string[] sources = {
            "Assets/OldAsset/Monsters/prefabs/prontera_mob/poring_001_pawn.prefab",
            "Assets/OldAsset/Monsters/prefabs/morocc_mob/pecopeco_001_pawn.prefab",
            "Assets/OldAsset/Monsters/prefabs/geffen_mob/deviruchi_001_pawn.prefab",
            "Assets/OldAsset/Monsters/prefabs/mjolnir_mob/baphomet_001_pawn.prefab",
            "Assets/OldAsset/Monsters/prefabs/mjolnir_mob/bapojunior_001_pawn.prefab",
            "Assets/OldAsset/Mounts/prefabs/fox_001_pawn.prefab",
            "Assets/OldAsset/Mounts/prefabs/rides_alpaca01_pawn.prefab",
            "Assets/OldAsset/Mounts/prefabs/vehicle_bluedragon_001_pawn.prefab",
            "Assets/OldAsset/Mounts/prefabs/horse_001_pawn.prefab",
            "Assets/OldAsset/Mounts/prefabs/butlerwhisper_001_pawn.prefab",
            "Assets/OldAsset/Mounts/prefabs/vehicle_flamebird_001_pawn.prefab",
            "Assets/OldAsset/Mounts/prefabs/vehicle_porterpuppy_001_pawn.prefab" };
        private static readonly string[] ids = { "Minorous", "Poring", "Pecopeco", "Lunatic", "Deviruchi", "BaphometJr", "Alpaca", "BlueDragon", "Horse", "ButlerWhisper", "FlameBird", "PorterPuppy", "Fox", "GiantPoring", "Smokie", "Savage", "Edgar" };
        private static readonly string[] names = { "미노타우루스", "포링", "페코페코", "루나틱", "데비루치", "바포메트 주니어", "알파카", "청룡", "말", "집사 위스퍼", "불꽃새", "짐꾼 강아지", "여우 · 탈것 원본", "자이언트 포링", "스모키", "새비지", "에드가" };

        [MenuItem("Tools/Project Run/Cartoon monsters/Build cartoon comparison scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            Directory.CreateDirectory(records);
            BuildAssets();
            AssetDatabase.SaveAssets();
            if (!File.Exists(ScenePath))
            {
                throw new InvalidOperationException("Missing cartoon comparison scene: " + ScenePath);
            }
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Cartoon scene contains unsaved edits.");
            SceneManager.SetActiveScene(scene);
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            try { BuildStage(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
            UpdateSceneLinks();
            AssetDatabase.SaveAssets();
            File.WriteAllText(records + "/에셋검증.json", Validate());
            Debug.Log("Cartoon comparison saved: " + ScenePath);
        }

        public static void BuildAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < newIds.Length; i++) BuildMonster(i);
            AssetDatabase.SaveAssets();
        }

        private static void BuildMonster(int index)
        {
            string id = newIds[index], owner = art + "/" + id;
            string target = prefabs + "/P_Runner_Cartoon_" + id + ".prefab";
            if (File.Exists(target)) return;
            foreach (string folder in new[] { "Meshes", "Rig", "Animations", "Materials", "Textures" }) Directory.CreateDirectory(owner + "/" + folder);
            AssetDatabase.Refresh();
            string texturePath = owner + "/Textures/" + id + "_AnimeAlbedo.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing generated atlas: " + texturePath);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true;
            importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
            importer.SaveAndReimport();
            var previewScene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("P_Runner_Cartoon_" + id);
            SceneManager.MoveGameObjectToScene(root, previewScene);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sources[index]);
            var model = Object.Instantiate(source, root.transform);
            model.name = "obj_model";
            try
            {
                var animator = model.GetComponentInChildren<Animator>(true);
                var clips = animator.runtimeAnimatorController.animationClips;
                var idle = clips.First(c => c.name.EndsWith("_stand_01", StringComparison.Ordinal) || c.name.EndsWith("_stand", StringComparison.Ordinal));
                var run = clips.First(c => c.name.EndsWith("_run_01", StringComparison.Ordinal) || c.name.EndsWith("_run", StringComparison.Ordinal));
                animator.runtimeAnimatorController = null;
                animator.avatar = Copy(animator.avatar, owner + "/Rig/" + id + "_Avatar.asset");
                animator.applyRootMotion = false;
                foreach (var behavior in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behavior);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "shadow").ToArray()) Object.DestroyImmediate(renderer.gameObject);
                var surface = Copy(AssetDatabase.LoadAssetAtPath<Material>(art + "/Smokie/Materials/Smokie_Anime.mat"), owner + "/Materials/" + id + "_Anime.mat");
                surface.name = id + "_Anime";
                surface.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                surface.SetColor("_BaseColor", Color.white);
                var outline = Copy(AssetDatabase.LoadAssetAtPath<Material>(art + "/Smokie/Materials/Smokie_AnimeOutline.mat"), owner + "/Materials/" + id + "_AnimeOutline.mat");
                outline.name = id + "_AnimeOutline";
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var original = skin.sharedMaterial;
                    foreach (string property in new[] { "_Cull", "_AlphaClip", "_Cutoff" }) if (original.HasProperty(property)) surface.SetFloat(property, original.GetFloat(property));
                    surface.SetTextureScale("_BaseMap", original.mainTextureScale); surface.SetTextureOffset("_BaseMap", original.mainTextureOffset);
                    skin.sharedMesh = Copy(skin.sharedMesh, owner + "/Meshes/" + skin.name + ".asset");
                    skin.sharedMaterials = Enumerable.Repeat(surface, skin.sharedMaterials.Length).ToArray();
                    skin.updateWhenOffscreen = true;
                    var shell = new GameObject("obj_outline").AddComponent<SkinnedMeshRenderer>();
                    shell.transform.SetParent(skin.transform, false);
                    shell.sharedMesh = SmoothOutline(skin.sharedMesh, owner + "/Meshes/" + skin.name + "_Outline.asset");
                    shell.bones = skin.bones; shell.rootBone = skin.rootBone; shell.localBounds = skin.localBounds;
                    shell.sharedMaterials = Enumerable.Repeat(outline, skin.sharedMaterials.Length).ToArray();
                    shell.updateWhenOffscreen = true; shell.shadowCastingMode = ShadowCastingMode.Off;
                    shell.receiveShadows = false; shell.lightProbeUsage = LightProbeUsage.Off; shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                var view = root.AddComponent<RaceRunnerView>();
                view.MountAnimator = animator; view.MountRoot = model.transform;
                view.IdleClip = Copy(idle, owner + "/Animations/" + id + "_Idle.anim");
                view.RunClip = Copy(run, owner + "/Animations/" + id + "_Run.anim");
                view.Trails = Array.Empty<TrailRenderer>();
                view.IdleClip.SampleAnimation(animator.gameObject, .15f);
                var bounds = BoundsOf(root);
                var focus = new GameObject("obj_camera_target").transform; focus.SetParent(root.transform); focus.position = bounds.center;
                view.CameraTarget = view.FaceTarget = view.MonsterFaceTarget = focus;
                EditorUtility.SetDirty(surface); EditorUtility.SetDirty(outline);
                if (PrefabUtility.SaveAsPrefabAsset(root, target) == null) throw new InvalidOperationException("Cannot save " + target);
            }
            finally { Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(previewScene); }
        }

        private static T Copy<T>(T source, string path) where T : Object
        {
            if (source == null) throw new InvalidOperationException("Missing resource: " + path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path); if (existing != null) return existing;
            var copy = Object.Instantiate(source); copy.name = source.name; copy.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(copy, path); return copy;
        }
        private static Mesh SmoothOutline(Mesh source, string path)
        {
            var mesh = Copy(source, path); var vertices = mesh.vertices; var normals = mesh.normals;
            var sums = new Dictionary<Vector3Int, Vector3>(); var keys = new Vector3Int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i] * 10000; keys[i] = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                sums.TryGetValue(keys[i], out var sum); sums[keys[i]] = sum + normals[i];
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = sums[keys[i]].normalized;
            mesh.normals = normals; EditorUtility.SetDirty(mesh); return mesh;
        }
        private static Bounds BoundsOf(GameObject root)
        {
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.name.StartsWith("obj_outline", StringComparison.Ordinal)).ToArray();
            var bounds = new Bounds(); bool first = true;
            var baked = new Mesh();
            try
            {
                foreach (var skin in skins)
                {
                    // Compensate renderer scale before TransformPoint applies it to world space.
                    skin.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        var point = skin.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); }
            if (first) throw new InvalidOperationException("No visible skinned geometry: " + root.name);
            return bounds;
        }

        private static void BuildStage(Scene scene)
        {
            Directory.CreateDirectory(stageArt + "/Materials"); AssetDatabase.Refresh();
            var systems = new GameObject("[Systems]");
            var showcase = systems.AddComponent<Sys_CartoonShowcase>();
            var exhibitsRoot = new GameObject("obj_monster_lineup");
            var stage = new GameObject("obj_stage");
            var floorMat = StageMaterial("Floor", new Color(.11f,.15f,.19f));
            var baseMat = StageMaterial("Plinth", new Color(.24f,.32f,.36f));
            var newMat = StageMaterial("NewPlinth", new Color(.26f,.40f,.39f));
            Primitive("bg_floor", stage.transform, new Vector3(0,-.22f,8), new Vector3(37,.3f,32), floorMat);
            var entries = new List<Data_CartoonExhibit>();
            for (int i = 0; i < ids.Length; i++)
            {
                const int columns = 6;
                int row = i / columns;
                int rowCount = Mathf.Min(columns, ids.Length - row * columns);
                float x = (i % columns - (rowCount - 1) * .5f) * 5.1f, z = row * 8;
                var pedestal = Primitive("obj_plinth_" + ids[i], stage.transform, new Vector3(x,.05f,z), new Vector3(4.65f,.22f,5.4f), newIds.Contains(ids[i]) ? newMat : baseMat);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabs + "/P_Runner_Cartoon_" + ids[i] + ".prefab");
                var wrapper = new GameObject("V_Cartoon_" + ids[i]); wrapper.transform.SetParent(exhibitsRoot.transform);
                wrapper.transform.position = new Vector3(x,.18f,z);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
                var view = instance.GetComponent<RaceRunnerView>(); view.enabled = false;
                foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
                foreach (var trail in instance.GetComponentsInChildren<TrailRenderer>(true)) trail.gameObject.SetActive(false);
                foreach (var look in instance.GetComponentsInChildren<Sys_MinorousLookDev>(true)) look.enabled = false;
                foreach (var look in instance.GetComponentsInChildren<Sys_MonsterLook>(true)) look.enabled = false;
                view.IdleClip.SampleAnimation(view.MountAnimator.gameObject, .15f);
                var bounds = BoundsOf(instance);
                // Gallery scale affects only the wrapper, never the saved runner or its animation.
                float scale = Mathf.Min(3.35f / Mathf.Max(bounds.size.x,bounds.size.y), 3.8f / bounds.size.z);
                instance.transform.localScale *= scale;
                bounds = BoundsOf(instance);
                instance.transform.position += new Vector3(x - bounds.center.x, .18f - bounds.min.y, z - bounds.center.z);
                wrapper.transform.rotation = Quaternion.Euler(0,160,0);
                wrapper.transform.localScale = Vector3.one * ExhibitScale(ids[i]);
                bounds = BoundsOf(instance);
                entries.Add(new Data_CartoonExhibit { Name = names[i], Root = wrapper.transform, Animator = view.MountAnimator, Idle = view.IdleClip, Run = view.RunClip, Focus = bounds.center, Radius = Mathf.Max(bounds.extents.y, bounds.extents.x, bounds.extents.z) });
                WorldLabel("txt_name_" + ids[i], names[i], new Vector3(x,.25f,z-2.42f), stage.transform, .055f, Color.white);
            }
            showcase.Exhibits = entries.ToArray();
            showcase.NameLabels = stage.GetComponentsInChildren<TextMesh>().Select(t => t.gameObject).ToArray();
            var camera = new GameObject("Camera_CartoonTest").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 12.3f; camera.nearClipPlane=.1f; camera.farClipPlane=120;
            camera.transform.position = new Vector3(0,25,-28); camera.transform.LookAt(new Vector3(0,1,8));
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.08f,.12f); camera.allowHDR=true; camera.depth=100;
            camera.gameObject.AddComponent<AudioListener>();
            var cameraData = camera.GetUniversalAdditionalCameraData(); cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality=AntialiasingQuality.High;
            showcase.PreviewCamera = camera; showcase.OverviewPosition=camera.transform.position; showcase.OverviewEuler=camera.transform.eulerAngles; showcase.OverviewSize=camera.orthographicSize;
            Light("Light_Key", new Vector3(5,7,-4),new Color(1,.965f,.91f),1.1f,true);
            Light("Light_Fill",new Vector3(3,2,7),new Color(.68f,.81f,1),.45f,false);
            Light("Light_Rim",new Vector3(-4,5,2),new Color(1,.87f,.67f),.7f,false);
            RenderSettings.skybox=null; RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.60f,.70f,.85f); RenderSettings.ambientEquatorColor=new Color(.46f,.49f,.57f); RenderSettings.ambientGroundColor=new Color(.30f,.25f,.22f); RenderSettings.fog=false;
            BuildControls(showcase);
            CartoonOriginalComparisonBuilder.Configure(showcase);
            Selection.activeGameObject=exhibitsRoot;
            if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(0,1.5f,8),new Vector3(35,7,28)),false);
        }
        public static float ExhibitScale(string id)
        {
            switch (id)
            {
                case "Smokie": return .5f;
                case "GiantPoring": return .8f;
                case "BlueDragon": return 1.3f;
                case "FlameBird": return 1.2f;
                case "Poring": return .7f;
                case "Pecopeco": return .7f;
                case "Lunatic": return .5f;
                default: return 1;
            }
        }

        [MenuItem("Tools/Project Run/Cartoon monsters/Apply exhibit sizes")]
        public static void ApplyExhibitSizes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded || scene.isDirty) throw new InvalidOperationException("Open the saved cartoon scene first.");
            var show = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Sys_CartoonShowcase>(true)).Single();
            foreach (var entry in show.Exhibits)
            {
                string id = entry.Root.name.Replace("V_Cartoon_", "");
                float size = ExhibitScale(id);
                float previous = entry.Root.localScale.x;
                if (Mathf.Approximately(size, previous)) continue;
                Vector3 focus = entry.Root.InverseTransformPoint(entry.Focus);
                entry.Root.localScale = Vector3.one * size;
                entry.Focus = entry.Root.TransformPoint(focus);
                entry.Radius *= size / previous;
            }
            EditorUtility.SetDirty(show);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Material StageMaterial(string name, Color color)
        {
            string path=stageArt+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(m!=null)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.12f); AssetDatabase.CreateAsset(m,path);return m;
        }
        private static GameObject Primitive(string name, Transform parent, Vector3 position, Vector3 scale,Material material)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent);g.transform.position=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        private static void Light(string name,Vector3 direction,Color color,float intensity,bool shadow)
        {
            var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;light.transform.rotation=Quaternion.LookRotation(-direction.normalized);light.shadows=shadow?LightShadows.Soft:LightShadows.None;light.shadowBias=.005f;light.shadowNormalBias=.06f;
        }
        private static Font Font() { return AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf"); }
        private static void WorldLabel(string name,string text,Vector3 position,Transform parent,float size,Color color)
        {
            var label=new GameObject(name).AddComponent<TextMesh>();label.transform.SetParent(parent);label.transform.position=position;label.transform.rotation=Quaternion.Euler(52,0,0);label.text=text;label.font=Font();label.fontSize=64;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;label.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=position;return r;
        }
        private static UnityEngine.UI.Text Label(string name,string text,Transform parent,Vector2 size,Vector2 position,int fontSize)
        {
            var r=Rect(name,parent,size,position);var t=r.gameObject.AddComponent<UnityEngine.UI.Text>();t.text=text;t.font=Font();t.fontSize=fontSize;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
        }
        private static void Button(string name,string text,Transform parent,Vector2 position,UnityAction action)
        {
            var r=Rect(name,parent,new Vector2(142,42),position);var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.13f,.26f,.30f,.97f);var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;UnityEventTools.AddPersistentListener(button.onClick,action);Label("txt_label",text,r,new Vector2(138,40),Vector2.zero,19);
        }
        private static void BuildControls(Sys_CartoonShowcase showcase)
        {
            var root=Rect("UICanvas_Root",null,Vector2.zero,Vector2.zero);
            var canvas=root.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
            var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var safe=Rect("obj_safe_area",root,Vector2.zero,Vector2.zero);safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=safe.offsetMax=Vector2.zero;showcase.SafeArea=safe;
            Label("txt_title","카툰 몬스터 테스트",safe,new Vector2(600,48),new Vector2(0,316),32);
            Label("txt_note","몬스터와 탈것 17종 · 신규 탈것 6종 · 카툰 외형 비교",safe,new Vector2(1000,35),new Vector2(0,277),18);
            showcase.Status=Label("txt_status","전체 17종 · 대기",safe,new Vector2(550,34),new Vector2(0,-272),21);
            Button("btn_all","전체 보기",safe,new Vector2(-480,-320),showcase.ShowAll);
            Button("btn_previous","이전 몬스터",safe,new Vector2(-320,-320),showcase.Previous);
            Button("btn_next","다음 몬스터",safe,new Vector2(-160,-320),showcase.Next);
            Button("btn_rotate","45° 회전",safe,new Vector2(0,-320),showcase.Rotate);
            Button("btn_idle","대기",safe,new Vector2(160,-320),showcase.PlayIdle);
            Button("btn_run","달리기",safe,new Vector2(320,-320),showcase.PlayRun);
            Button("btn_title","타이틀",safe,new Vector2(480,-320),showcase.ReturnToTitle);
            new GameObject("EventSystem",typeof(UnityEngine.EventSystems.EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private static void UpdateSceneLinks()
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(scene => scene.path == ScenePath)) list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
        public static string Validate()
        {
            var report=new List<object>();
            foreach(string id in newIds)
            {
                string path=prefabs+"/P_Runner_Cartoon_"+id+".prefab";var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(p==null||PrefabUtility.GetPrefabAssetType(p)!=PrefabAssetType.Regular)throw new InvalidOperationException("Not regular: "+id);
                var deps=AssetDatabase.GetDependencies(path,true);var old=deps.Where(d=>d.StartsWith("Assets/")&&!d.StartsWith(art+"/")&&!d.StartsWith("Assets/Scripts/")&&d!=path).ToArray();
                if(old.Length>0)throw new InvalidOperationException("Original dependencies in "+id+": "+string.Join(",",old));
                var view=p.GetComponent<RaceRunnerView>();if(view.HasRider||view.IdleClip==null||view.RunClip==null||view.MountAnimator.avatar==null||!view.MountAnimator.avatar.isValid)throw new InvalidOperationException("Invalid rig/clips: "+id);
                foreach(var r in p.GetComponentsInChildren<Renderer>(true))if(r.sharedMaterials.Any(m=>m==null||m.shader==null||!m.shader.isSupported))throw new InvalidOperationException("Invalid material: "+id);
                report.Add(new{Id=id,Prefab=path,Regular=true,Dependencies=deps});
            }
            return JsonConvert.SerializeObject(report,Formatting.Indented);
        }
    }
}
