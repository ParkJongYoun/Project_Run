using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using ProjectRun.Race;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static class RanchSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Scene_LobbyTest.unity";
        private const string artRoot = "Assets/ArtBundles/Ranch";
        private const string prefabRoot = "Assets/Resources/Prefabs/Ranch";
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static Mesh bevel;
        private static Mesh cone;
        private static Mesh foliage;
        private static System.Random random;

        [MenuItem("Tools/Project Run/Ranch/Build Lobby Test")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding the ranch.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Scene_LobbyTest first.");
            string backup = "DataBundles/아트/목장_시설물_20261019/작업기록/씬_원본";
            Directory.CreateDirectory(backup);
            if (!File.Exists(backup + "/Scene_LobbyTest.unity")) File.Copy(ScenePath, backup + "/Scene_LobbyTest.unity");
            Directory.CreateDirectory(artRoot + "/Materials"); Directory.CreateDirectory(artRoot + "/Meshes");
            Directory.CreateDirectory(prefabRoot); Directory.CreateDirectory("Assets/Resources/UI/Ranch");
            AssetDatabase.Refresh(); materials.Clear(); random = new System.Random(40819);
            bevel = SaveMesh(BeveledCube(), "BeveledBlock"); cone = SaveMesh(ConeMesh(), "PineCrown"); foliage = SaveMesh(FoliageMesh(), "Foliage");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "RanchWorld" || root.name == "UICanvas_Root" || root.name == "[Systems]" || root.name == "[Managers]" || root.name == "EventSystem")
                    UnityEngine.Object.DestroyImmediate(root);
            var art = BuildArt();
            var catalog = BuildCatalog();
            var world = new GameObject("RanchWorld");
            var ground = Ground(world.transform);
            Environment(world.transform);
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>()).FirstOrDefault();
            if (camera == null) camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 7;
            camera.transform.rotation = Quaternion.Euler(41.84146f, 359.9193f, 0);
            camera.transform.position = new Vector3(1.08389819f, 23.3474884f, -28.5533619f);
            var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderShadows = true;
            camera.nearClipPlane = .3f; camera.farClipPlane = 150; camera.backgroundColor = new Color(.56f,.70f,.58f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var light = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>()).FirstOrDefault();
            if (light == null) light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(47,-115,0);
            light.color = new Color(1,.94f,.80f); light.intensity = 1.12f; light.shadows = LightShadows.Soft;
            light.shadowStrength = .65f; light.shadowBias = .04f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.63f,.72f,.82f);
            RenderSettings.ambientEquatorColor = new Color(.52f,.57f,.42f);
            RenderSettings.ambientGroundColor = new Color(.32f,.36f,.26f);
            RenderSettings.fog = false; RenderSettings.sun = light;
            var systems = new GameObject("[Systems]");
            var placement = new GameObject("Sys_RanchPlacement").AddComponent<Sys_RanchPlacement>(); placement.transform.SetParent(systems.transform);
            placement.Catalog = catalog; placement.WorldCamera = camera;
            placement.FacilitiesRoot = new GameObject("group_facilities").transform; placement.FacilitiesRoot.SetParent(world.transform);
            placement.InitialLayout = InitialLayout();
            placement.Grid = Grid(world.transform, placement.GridSize, placement.CellSize, placement.GridOrigin);
            placement.Grid.SetActive(false);
            placement.FootprintMarker = Box(world.transform,"obj_footprint",Vector3.zero,Vector3.one,Mat("FootprintValid",new Color(.3f,.9f,.20f))).transform;
            placement.ValidMaterial = Mat("FootprintValid",new Color(.3f,.9f,.20f));
            placement.InvalidMaterial = Mat("FootprintInvalid",new Color(.95f,.18f,.10f)); placement.FootprintMarker.gameObject.SetActive(false);
            foreach (var entry in placement.InitialLayout.Facilities)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(catalog.Find(entry.FacilityId).Prefab, placement.FacilitiesRoot);
                instance.name = "obj_" + entry.FacilityId + "_" + entry.InstanceId;
                instance.transform.SetPositionAndRotation(placement.Position(entry),Quaternion.Euler(0,90 * entry.Rotation,0));
            }
            var cameraSystem = camera.GetComponent<Sys_RanchCamera>() ?? camera.gameObject.AddComponent<Sys_RanchCamera>();
            cameraSystem.WorldCamera = camera; cameraSystem.Placement = placement;
            cameraSystem.HomePosition = camera.transform.position; cameraSystem.HomeSize = camera.orthographicSize;
            cameraSystem.HomeAngles = camera.transform.eulerAngles; cameraSystem.WheelZoomStep = .4f;
            var eventObject = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            eventObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var ui = new GameObject("UICanvas_Root",typeof(RectTransform));
            var canvas = DemoUI.Canvas(ui.transform,"Canvas_Panel",(int)E_CanvasLayer.Panel);
            var panelRoot = DemoUI.Fill(canvas.transform,"UIPanel_Ranch");
            var panel = panelRoot.gameObject.AddComponent<UIPanel_Ranch>();
            panel.Art = art; panel.Placement = placement; panel.RanchCamera = cameraSystem;
            panel.LockedPositions = new[] { new Vector3(21,.2f,4),new Vector3(-23,.2f,-1) }; panel.Build();
            PrefabUtility.SaveAsPrefabAsset(panelRoot.gameObject,"Assets/Resources/Prefabs/UI/Panel/UIPanel_Ranch.prefab");
            var managers = new GameObject("[Managers]");
            var manager = new GameObject("Mgr_UI").AddComponent<Mgr_UI>(); manager.transform.SetParent(managers.transform);
            // Registration is scoped to this scene and is performed again on Play by the bootstrap.
            var bootstrap = systems.AddComponent<Sys_RanchUI>(); bootstrap.Manager = manager; bootstrap.Panel = panel;
            AddResidents(world.transform);
            if (RanchPaintedLook.IsAvailable) RanchPaintedLook.ApplyScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); Selection.activeGameObject = world;
            RanchDemoSetup.Apply();
            Debug.Log("Ranch ready: temporary demo layout, placeable roads, locked expansion plots.");
        }

        private static Data_DemoArt BuildArt()
        {
            var art = AssetDatabase.LoadAssetAtPath<Data_DemoArt>("Assets/Resources/UI/Ranch/Data_RanchArt.asset");
            if (art == null) { art = ScriptableObject.CreateInstance<Data_DemoArt>(); AssetDatabase.CreateAsset(art,"Assets/Resources/UI/Ranch/Data_RanchArt.asset"); }
            var entries = new List<Data_DemoSprite>();
            foreach (string file in Directory.GetFiles("Assets/ArtBundles/UI/Ranch","*.png"))
            {
                var path = file.Replace('\\','/'); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                string key = Path.GetFileNameWithoutExtension(path);
                importer.spriteBorder = Vector4.zero;
                importer.SaveAndReimport();
                entries.Add(new Data_DemoSprite { Key = key, Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
            }
            var demo = Resources.Load<Data_DemoArt>("UI/Demo/Data_DemoArt");
            art.BodyFont = demo.BodyFont; art.TitleFont = demo.TitleFont;
            entries.Add(new Data_DemoSprite { Key = "panel_bg", Sprite = demo.Get("panel_bg") });
            entries.Add(new Data_DemoSprite { Key = "panel_frame", Sprite = demo.Get("panel_frame") });
            art.Images = entries.ToArray(); EditorUtility.SetDirty(art); return art;
        }

        private static Data_RanchCatalog BuildCatalog()
        {
            ImportPortraits();
            var list = new List<Data_RanchFacility>();
            Add(list,"feed_store","먹이 창고","먹이와 육성 물품을 보관하는 중심 시설",new Vector2Int(3,3),Warehouse(),"시설");
            Add(list,"shelter","몬스터 축사","몬스터들이 쉬고 생활하는 지붕 있는 축사",new Vector2Int(4,3),Shelter(),"시설");
            Add(list,"training","훈련장","달리기·장애물·근력 훈련을 위한 야외 운동장",new Vector2Int(4,4),Training(),"시설");
            Add(list,"house","목장 관리동","목장주와 관리인이 사용하는 작은 집",new Vector2Int(3,3),House(),"시설");
            Add(list,"broadleaf","활엽수","둥근 수관의 목장 나무",new Vector2Int(2,2),Tree(false),"환경");
            Add(list,"pine","침엽수","목장 주변 숲을 만드는 침엽수",new Vector2Int(2,2),Tree(true),"환경");
            Add(list,"fence","나무 울타리","구역을 나누는 한 칸 울타리",new Vector2Int(1,1),Fence(),"장식");
            Add(list,"gate","목장 출입문","목장 입구를 꾸미는 두 칸 문",new Vector2Int(2,1),Gate(),"장식");
            Add(list,"lamp","가로등","따뜻한 랜턴이 달린 목재 가로등",new Vector2Int(1,1),Lamp(),"장식");
            Add(list,"flowerbed","꽃 화단","목장길 옆의 작은 나무 화단",new Vector2Int(2,1),Flowerbed(),"장식");
            Add(list,"rock","바위","목장과 잠긴 지형을 꾸미는 바위",new Vector2Int(1,1),Rock(),"환경");
            var catalog = AssetDatabase.LoadAssetAtPath<Data_RanchCatalog>("Assets/Resources/Prefabs/Ranch/Data_RanchCatalog.asset");
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<Data_RanchCatalog>(); AssetDatabase.CreateAsset(catalog,"Assets/Resources/Prefabs/Ranch/Data_RanchCatalog.asset"); }
            catalog.Facilities = list.ToArray(); RanchDemoSetup.ConfigureCatalog(catalog); EditorUtility.SetDirty(catalog); return catalog;
        }

        private static void ImportPortraits()
        {
            string folder="Assets/ArtBundles/UI/Ranch/Portraits";
            if(!Directory.Exists(folder))return;
            foreach(var path in Directory.GetFiles(folder,"*.png"))
            {
                var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;if(importer==null)continue;
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
        }
        [MenuItem("Tools/Project Run/Ranch/Refresh Facility Portraits")]
        public static void RefreshPortraits()
        {
            AssetDatabase.Refresh();ImportPortraits();
            var catalog=Resources.Load<Data_RanchCatalog>("Prefabs/Ranch/Data_RanchCatalog");
            foreach(var item in catalog.Facilities)item.Portrait=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ArtBundles/UI/Ranch/Portraits/"+item.Id+".png");
            EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
        }
        private static void Add(List<Data_RanchFacility> list,string id,string name,string description,Vector2Int size,GameObject root,string category)
        {
            root.name = "P_Ranch_" + id;
            string path = prefabRoot + "/" + root.name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.transform.Find("model_generated") == null)
            {
                if (id != "feed_store") Combine(root.transform, id);
                root.AddComponent<BoxCollider>();
                FitSelectionCollider(root, size);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            UnityEngine.Object.DestroyImmediate(root);
            var portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ArtBundles/UI/Ranch/Portraits/" + id + ".png");
            list.Add(new Data_RanchFacility { Id=id,Name=name,Description=description,Footprint=size,Prefab=prefab,Portrait=portrait,Category=category });
        }

        public static void FitSelectionCollider(GameObject root, Vector2Int size)
        {
            float height = 1;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                height = Mathf.Max(height, renderer.bounds.max.y - root.transform.position.y);
            var collider = root.GetComponent<BoxCollider>();
            collider.center = new Vector3(0, height * .5f, 0);
            collider.size = new Vector3(size.x * 1.35f - .08f, height, size.y * 1.35f - .08f);
        }

        private static GameObject Warehouse()
        {
            var root = new GameObject("warehouse");
            string texturePath = artRoot + "/FeedWarehouse/FeedWarehouse_BaseColor.jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath); importer.maxTextureSize = 4096; importer.SaveAndReimport();
            var material = Mat("FeedWarehouse",Color.white);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_ShadeColor",new Color(.68f,.72f,.78f)); material.SetFloat("_Softness",.10f); material.SetFloat("_Specular",.01f);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(artRoot + "/FeedWarehouse/FeedWarehouse.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
            model.transform.localRotation = Quaternion.Euler(0,180,0);
            var renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers) renderer.sharedMaterials = Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float scale = 3.6f / Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale *= scale;
            bounds = renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            model.transform.localPosition -= new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            return root;
        }

        private static GameObject Shelter()
        {
            var root=new GameObject("shelter"); var wood=Wood(); var plaster=Mat("Plaster",new Color(.91f,.81f,.59f));
            Box(root.transform,"foundation",new Vector3(0,.08f,.5f),new Vector3(4.5f,.16f,2.7f),Stone());
            Box(root.transform,"back_wall",new Vector3(0,1.05f,1.6f),new Vector3(4.3f,1.9f,.18f),plaster);
            Box(root.transform,"side_wall",new Vector3(-2.05f,1.05f,.45f),new Vector3(.18f,1.9f,2.4f),plaster);
            foreach(float x in new[]{-2.12f,0,2.12f}) Beam(root.transform,"post",new Vector3(x,0,-.6f),new Vector3(x,2.15f,-.6f),.21f,wood);
            Beam(root.transform,"front_header",new Vector3(-2.3f,2,-.6f),new Vector3(2.3f,2,-.6f),.23f,wood);
            Roof(root.transform,4.7f,2.9f,2.03f,1.2f,.5f,Mat("RoofRed",new Color(.72f,.19f,.12f)));
            for(int i=0;i<3;i++)
            {
                var hay=Box(root.transform,"hay_bale",new Vector3(-1.4f+i*1.3f,.3f,.7f),new Vector3(1,.5f,.8f),Mat("Hay",new Color(.87f,.64f,.20f)));
                Box(hay.transform,"strap",new Vector3(0,.02f,0),new Vector3(.1f,1.06f,1.06f),wood);
            }
            FenceLine(root.transform,new Vector3(-2.4f,0,-1.7f),new Vector3(2.4f,0,-1.7f),4,.72f);
            FenceLine(root.transform,new Vector3(2.4f,0,-1.7f),new Vector3(2.4f,0,1.7f),3,.72f);
            Trough(root.transform,new Vector3(1.4f,.0f,-1.15f));
            var medallion=Primitive(root.transform,"poring_medallion",PrimitiveType.Sphere,new Vector3(0,2.4f,-1.05f),new Vector3(.65f,.65f,.15f),Mat("PoringPink",new Color(1,.55f,.61f)));
            foreach(float x in new[]{-.13f,.13f}) Primitive(medallion.transform,"eye",PrimitiveType.Sphere,new Vector3(x,-.03f,-.52f),new Vector3(.09f,.13f,.09f),Mat("Dark",new Color(.17f,.12f,.10f)));
            return root;
        }

        private static GameObject Training()
        {
            var root=new GameObject("training");
            Box(root.transform,"sand_court",new Vector3(0,.025f,0),new Vector3(5.15f,.05f,5.15f),Mat("Sand",new Color(.81f,.64f,.35f)));
            FenceLine(root.transform,new Vector3(-2.5f,0,-2.5f),new Vector3(-2.5f,0,2.5f),4,.85f);
            FenceLine(root.transform,new Vector3(-2.5f,0,2.5f),new Vector3(2.5f,0,2.5f),4,.85f);
            FenceLine(root.transform,new Vector3(2.5f,0,2.5f),new Vector3(2.5f,0,-2.5f),4,.85f);
            for(int i=0;i<2;i++)
            {
                float x=-1.2f+i*2.4f;
                Beam(root.transform,"hurdle_post",new Vector3(x-.5f,0,-.5f),new Vector3(x-.5f,1,-.5f),.13f,Wood());
                Beam(root.transform,"hurdle_post",new Vector3(x+.5f,0,-.5f),new Vector3(x+.5f,1,-.5f),.13f,Wood());
                Beam(root.transform,"hurdle",new Vector3(x-.65f,.75f,-.5f),new Vector3(x+.65f,.75f,-.5f),.16f,Mat("RoofRed",new Color(.72f,.19f,.12f)));
                var dummy=Primitive(root.transform,"training_dummy",PrimitiveType.Cylinder,new Vector3(x,.8f,1.25f),new Vector3(.6f,.8f,.6f),Mat("Hay",new Color(.87f,.64f,.20f)));
                Beam(root.transform,"dummy_arm",new Vector3(x-.8f,1.1f,1.25f),new Vector3(x+.8f,1.1f,1.25f),.15f,Wood());
                for(int band=0;band<3;band++) Primitive(root.transform,"dummy_band",PrimitiveType.Cylinder,new Vector3(x,.35f+band*.45f,1.25f),new Vector3(.65f,.05f,.65f),Wood());
            }
            for(int i=0;i<7;i++)
            {
                var flag=Box(root.transform,"bunting",new Vector3(-2.2f+i*.72f,.7f,2.48f),new Vector3(.38f,.35f,.06f),i%2==0?Mat("RoofRed",new Color(.72f,.19f,.12f)):Mat("Plaster",new Color(.91f,.81f,.59f)));
                flag.transform.localRotation=Quaternion.Euler(0,0,45);
            }
            return root;
        }

        private static GameObject House()
        {
            var root=new GameObject("house"); var plaster=Mat("Plaster",new Color(.91f,.81f,.59f));
            Box(root.transform,"foundation",new Vector3(0,.14f,0),new Vector3(3.25f,.28f,3.1f),Stone());
            Box(root.transform,"walls",new Vector3(0,1.25f,0),new Vector3(3,2.2f,2.8f),plaster);
            foreach(float x in new[]{-1.5f,1.5f}) foreach(float z in new[]{-1.4f,1.4f}) Beam(root.transform,"timber",new Vector3(x,.2f,z),new Vector3(x,2.4f,z),.20f,Wood());
            Roof(root.transform,3.7f,3.5f,2.35f,1.25f,0,Mat("RoofTeal",new Color(.14f,.48f,.48f)));
            Box(root.transform,"door",new Vector3(-.35f,.91f,-1.46f),new Vector3(.95f,1.6f,.17f),Wood());
            Box(root.transform,"door_step",new Vector3(-.35f,.13f,-1.7f),new Vector3(1.35f,.25f,.6f),Stone());
            Box(root.transform,"window_frame",new Vector3(.95f,1.35f,-1.45f),new Vector3(.77f,.85f,.16f),Wood());
            Box(root.transform,"window",new Vector3(.95f,1.35f,-1.55f),new Vector3(.57f,.64f,.04f),Mat("GlassBlue",new Color(.27f,.53f,.63f)));
            Box(root.transform,"mullion",new Vector3(.95f,1.35f,-1.58f),new Vector3(.07f,.64f,.05f),Wood());
            Box(root.transform,"chimney",new Vector3(.9f,3.1f,.8f),new Vector3(.48f,1.6f,.5f),Stone());
            return root;
        }

        private static void Roof(Transform root,float width,float depth,float bottom,float height,float z,Material red)
        {
            float slope=Mathf.Atan2(height,width/2)*Mathf.Rad2Deg;
            float length=Mathf.Sqrt(width*width/4+height*height);
            for(int side=-1;side<=1;side+=2)
            {
                var roof=Box(root,"roof",new Vector3(side*width/4,bottom+height/2,z),new Vector3(length,.19f,depth),red);
                roof.transform.localRotation=Quaternion.Euler(0,0,-side*slope);
                for(int row=0;row<4;row++) for(int col=0;col<7;col++)
                {
                    var tile=Box(roof.transform,"tile",new Vector3(-.375f+row*.25f,.65f,-.428f+col*.143f),new Vector3(.255f,.4f,.15f),red);
                }
                Beam(root,"fascia",new Vector3(0,bottom+height,z-depth/2-.04f),new Vector3(side*width/2,bottom,z-depth/2-.04f),.2f,Wood());
            }
            Beam(root,"ridge",new Vector3(0,bottom+height+.08f,z-depth/2-.18f),new Vector3(0,bottom+height+.08f,z+depth/2+.18f),.25f,Wood());
        }

        private static GameObject Tree(bool pine)
        {
            var generated = GeneratedVisual(pine ? "pine" : "broadleaf");
            if (generated != null) return generated;
            var root=new GameObject(pine?"pine":"broadleaf");
            Beam(root.transform,"trunk",Vector3.zero,new Vector3(.12f,2.7f,.1f),.38f,Wood());
            if(pine)
            {
                for(int i=0;i<4;i++)
                {
                    var crown=new GameObject("crown",typeof(MeshFilter),typeof(MeshRenderer)); crown.transform.SetParent(root.transform,false);
                    crown.transform.localPosition=new Vector3(.05f,1+i*.65f,0); crown.transform.localScale=new Vector3(2.5f-i*.43f,1.55f,2.5f-i*.43f);
                    crown.transform.localRotation=Quaternion.Euler(0,i*23,0); crown.GetComponent<MeshFilter>().sharedMesh=cone;
                    crown.GetComponent<MeshRenderer>().sharedMaterial=Mat("Pine"+i,Color.Lerp(new Color(.12f,.33f,.17f),new Color(.36f,.54f,.20f),i/3f));
                }
            }
            else
            {
                for(int i=0;i<7;i++)
                {
                    float angle=i*2.4f; var p=new Vector3(Mathf.Cos(angle)*.65f,2.3f+(i%3)*.38f,Mathf.Sin(angle)*.65f);
                    var leaf=Box(root.transform,"leaf_cluster",p,new Vector3(1.8f,1.55f,1.7f),Mat("Leaf"+(i%3),Color.Lerp(new Color(.24f,.43f,.13f),new Color(.50f,.65f,.21f),(i%3)/2f)));
                    leaf.GetComponent<MeshFilter>().sharedMesh=foliage;
                }
            }
            return root;
        }

        private static GameObject Fence()
        {
            var root=new GameObject("fence"); FenceLine(root.transform,new Vector3(-.62f,0,0),new Vector3(.62f,0,0),1,.9f); return root;
        }
        private static GameObject Gate()
        {
            var root=new GameObject("gate");
            foreach(float x in new[]{-1.15f,1.15f})
            {
                Beam(root.transform,"post",new Vector3(x,0,0),new Vector3(x,1.7f,0),.24f,Wood());
                Primitive(root.transform,"cap",PrimitiveType.Sphere,new Vector3(x,1.73f,0),new Vector3(.35f,.20f,.35f),Mat("Gold",new Color(.87f,.61f,.17f)));
            }
            for(int i=0;i<7;i++) Box(root.transform,"picket",new Vector3(-.9f+i*.3f,.65f,0),new Vector3(.18f,1.05f,.14f),Wood());
            Beam(root.transform,"crossbeam",new Vector3(-1.1f,.3f,-.1f),new Vector3(1.1f,1.05f,-.1f),.15f,Wood());
            return root;
        }
        private static GameObject Lamp()
        {
            var root=new GameObject("lamp");
            Box(root.transform,"base",new Vector3(0,.12f,0),new Vector3(.55f,.25f,.55f),Stone());
            Beam(root.transform,"post",Vector3.zero,new Vector3(0,2.6f,0),.18f,Wood());
            Box(root.transform,"arm",new Vector3(.22f,2.45f,0),new Vector3(.75f,.15f,.18f),Wood());
            Box(root.transform,"lantern",new Vector3(.5f,2.03f,0),new Vector3(.43f,.57f,.43f),Mat("Lantern",new Color(1,.79f,.30f)));
            foreach(float y in new[]{1.72f,2.35f}) Box(root.transform,"lantern_cap",new Vector3(.5f,y,0),new Vector3(.58f,.12f,.58f),Mat("Dark",new Color(.17f,.12f,.10f)));
            return root;
        }
        private static GameObject Flowerbed()
        {
            var root=new GameObject("flowerbed");
            Box(root.transform,"planter",new Vector3(0,.22f,0),new Vector3(2.35f,.44f,1.04f),Wood());
            Box(root.transform,"earth",new Vector3(0,.45f,0),new Vector3(2.11f,.05f,.8f),Mat("Soil",new Color(.32f,.23f,.13f)));
            for(int i=0;i<12;i++)
            {
                float x=-.88f+(i%6)*.35f;float z=-.23f+(i/6)*.46f;
                Primitive(root.transform,"leaves",PrimitiveType.Sphere,new Vector3(x,.57f,z),new Vector3(.45f,.28f,.4f),Mat("Leaf1",new Color(.35f,.53f,.17f)));
                var blossom=Primitive(root.transform,"flower",PrimitiveType.Sphere,new Vector3(x,.76f,z),new Vector3(.23f,.12f,.23f),Mat("Flower"+(i%3),i%3==0?new Color(.95f,.54f,.64f):i%3==1?new Color(1,.88f,.48f):new Color(.86f,.85f,1)));
            }
            return root;
        }
        private static GameObject Rock()
        {
            var generated = GeneratedVisual("rock");
            if (generated != null) return generated;
            var root=new GameObject("rock");
            var rock=Box(root.transform,"stone",new Vector3(0,.4f,0),new Vector3(1.12f,.85f,1),Stone());rock.transform.localRotation=Quaternion.Euler(8,24,-11);
            Box(root.transform,"moss",new Vector3(.1f,.79f,-.04f),new Vector3(.56f,.08f,.5f),Mat("Moss",new Color(.40f,.48f,.17f)));return root;
        }
        private static void Trough(Transform root,Vector3 position)
        {
            Box(root,"trough",position+new Vector3(0,.24f,0),new Vector3(1.35f,.48f,.60f),Wood());
            Box(root,"water",position+new Vector3(0,.49f,0),new Vector3(1.1f,.03f,.38f),Mat("GlassBlue",new Color(.27f,.53f,.63f)));
        }

        private static GameObject Ground(Transform parent)
        {
            var root=new GameObject("group_ground");root.transform.SetParent(parent,false);
            var grass=Mat("Grass",Color.white); var grassTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(artRoot+"/Grass.png");
            grass.SetTexture("_BaseMap",grassTexture);grass.SetTextureScale("_BaseMap",new Vector2(10,10));grass.SetColor("_BaseColor",new Color(.72f,.85f,.69f));
            Box(root.transform,"earth_base",new Vector3(0,-.6f,0),new Vector3(110,1.1f,100),Mat("Earth",new Color(.48f,.36f,.18f)));
            var ground=Primitive(root.transform,"meadow",PrimitiveType.Plane,new Vector3(0,-.02f,0),new Vector3(11,1,10),grass);
            var sand=Mat("Path",new Color(.76f,.62f,.39f));
            Box(root.transform,"main_path",new Vector3(0,-.003f,-2.3f),new Vector3(29,.025f,1.65f),sand);
            Box(root.transform,"entry_path",new Vector3(.5f,.002f,-6.1f),new Vector3(1.65f,.03f,9.6f),sand);
            FenceLine(root.transform,new Vector3(-13.7f,0,10.95f),new Vector3(13.7f,0,10.95f),20,1.05f);
            FenceLine(root.transform,new Vector3(-13.7f,0,-10.95f),new Vector3(-1.6f,0,-10.95f),9,1.05f);
            FenceLine(root.transform,new Vector3(2.6f,0,-10.95f),new Vector3(13.7f,0,-10.95f),8,1.05f);
            FenceLine(root.transform,new Vector3(-13.7f,0,-10.95f),new Vector3(-13.7f,0,10.95f),16,1.05f);
            FenceLine(root.transform,new Vector3(13.7f,0,-10.95f),new Vector3(13.7f,0,10.95f),16,1.05f);
            DashedBoundary(root.transform,new Vector3(14.3f,.08f,-8),new Vector2(13,17));
            DashedBoundary(root.transform,new Vector3(-27.5f,.08f,-8),new Vector2(13,17));
            Combine(root.transform,"Ground");
            return root;
        }
        private static void DashedBoundary(Transform root,Vector3 start,Vector2 size)
        {
            var color=Mat("LockedLine",new Color(.92f,.88f,.65f));
            for(float x=0;x<size.x;x+=1.5f) foreach(float z in new[]{0f,size.y}) Box(root,"locked_dash",start+new Vector3(x+.45f,0,z),new Vector3(.85f,.03f,.11f),color);
            for(float z=0;z<size.y;z+=1.5f) foreach(float x in new[]{0f,size.x}) Box(root,"locked_dash",start+new Vector3(x,0,z+.45f),new Vector3(.11f,.03f,.85f),color);
        }
        private static void Environment(Transform parent)
        {
            var root=new GameObject("group_forest");root.transform.SetParent(parent,false);
            for(int i=0;i<130;i++)
            {
                float x=(float)random.NextDouble()*90-45;float z=(float)random.NextDouble()*64-32;
                if(Mathf.Abs(x)<15 && Mathf.Abs(z)<13) continue;
                if(Mathf.Abs(x)<30 && Mathf.Abs(z)<11 && random.NextDouble()>.18) continue;
                var tree=Tree(i%3!=0);tree.transform.SetParent(root.transform,false);tree.transform.localPosition=new Vector3(x,0,z);
                tree.transform.localScale=Vector3.one*(.75f+(float)random.NextDouble()*.55f);tree.transform.localRotation=Quaternion.Euler(0,random.Next(360),0);
            }
            for(int i=0;i<36;i++)
            {
                float x=(float)random.NextDouble()*67-33;float z=(float)random.NextDouble()*44-22;
                if(Mathf.Abs(x)<14 && Mathf.Abs(z)<12) continue;
                var rock=Rock();rock.transform.SetParent(root.transform,false);rock.transform.localPosition=new Vector3(x,0,z);rock.transform.localScale=Vector3.one*(.8f+(float)random.NextDouble()*1.3f);
            }
            var chunks=new Dictionary<Vector2Int,Transform>();
            foreach(var child in root.transform.Cast<Transform>().ToArray())
            {
                var key=new Vector2Int(Mathf.FloorToInt(child.position.x/12),Mathf.FloorToInt(child.position.z/12));
                if(!chunks.TryGetValue(key,out var chunk)){chunk=new GameObject("group_forest_"+key.x+"_"+key.y).transform;chunk.SetParent(root.transform,false);chunks[key]=chunk;}
                child.SetParent(chunk,true);
            }
            foreach(var pair in chunks)Combine(pair.Value,"Forest_"+pair.Key.x+"_"+pair.Key.y);
        }

        private static GameObject GeneratedVisual(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabRoot + "/P_Ranch_" + id + ".prefab");
            var visual = prefab == null ? null : prefab.transform.Find("model_generated");
            if (visual == null) return null;
            var root = new GameObject("obj_generated_" + id);
            UnityEngine.Object.Instantiate(visual.gameObject, root.transform, false);
            return root;
        }

        [MenuItem("Tools/Project Run/Ranch/Refresh Generated Environment")]
        public static void RefreshGeneratedEnvironment()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open Scene_LobbyTest in Edit mode first.");
            var world = GameObject.Find("RanchWorld");
            if (world == null) throw new InvalidOperationException("Missing ranch world.");
            materials.Clear(); random = new System.Random(40819);
            bevel = SaveMesh(BeveledCube(), "BeveledBlock"); cone = SaveMesh(ConeMesh(), "PineCrown"); foliage = SaveMesh(FoliageMesh(), "Foliage");
            foreach (string name in new[] { "group_ground", "group_forest" })
            {
                var child = world.transform.Find(name);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            Ground(world.transform); Environment(world.transform);
            if (RanchPaintedLook.IsAvailable) RanchPaintedLook.ApplyScene();
            EditorSceneManager.MarkSceneDirty(world.scene); EditorSceneManager.SaveScene(world.scene); AssetDatabase.SaveAssets();
        }

        private static void Combine(Transform root,string prefix)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>(); if(renderer==null||filter.sharedMesh==null)continue;
                for(int i=0;i<filter.sharedMesh.subMeshCount;i++)
                {
                    var material=renderer.sharedMaterials[Mathf.Min(i,renderer.sharedMaterials.Length-1)];
                    if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups[material]=list;}
                    list.Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=i,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
            }
            foreach(var child in root.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach(var pair in groups)
            {
                var mesh=new Mesh{name=prefix+"_"+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                var go=new GameObject("obj_"+pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,prefix+"_"+pair.Key.name);go.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
        }
        private static void FenceLine(Transform root,Vector3 start,Vector3 end,int segments,float height)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabRoot + "/P_Ranch_fence.prefab");
            if (source != null && source.transform.Find("model_generated") != null)
            {
                var direction = (end - start).normalized;
                float segmentLength = Vector3.Distance(start, end) / segments;
                for (int i = 0; i < segments; i++)
                {
                    var piece = GeneratedVisual("fence");
                    var renderers = piece.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    piece.transform.SetParent(root, false);
                    piece.transform.localScale = new Vector3(segmentLength / bounds.size.x, height / bounds.size.y, 1);
                    piece.transform.localRotation = Quaternion.Euler(0, -Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg, 0);
                    piece.transform.localPosition = Vector3.Lerp(start, end, (i + .5f) / segments);
                }
                return;
            }
            var wood=Wood();
            for(int i=0;i<=segments;i++)
            {
                var p=Vector3.Lerp(start,end,i/(float)segments);
                Box(root,"fence_post",p+Vector3.up*height*.5f,new Vector3(.20f,height,.20f),wood);
                Box(root,"post_cap",p+Vector3.up*(height+.015f),new Vector3(.25f,.08f,.25f),Mat("WoodLight",new Color(.68f,.45f,.20f)));
                if(i==segments) continue;
                var next=Vector3.Lerp(start,end,(i+1)/(float)segments);
                foreach(float y in new[]{height*.33f,height*.72f}) Beam(root,"fence_rail",p+Vector3.up*y,next+Vector3.up*y,.11f,wood);
            }
        }
        private static GameObject Grid(Transform root,Vector2Int size,float cell,Vector3 origin)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();
            Action<Vector3,Vector3> line=(a,b)=>
            {
                var cross=Vector3.Cross((b-a).normalized,Vector3.up)*.012f;
                Face(vertices,indices,a-cross,a+cross,b+cross,b-cross);
            };
            for(int x=0;x<=size.x;x++)line(origin+new Vector3(x*cell,.025f,0),origin+new Vector3(x*cell,.025f,size.y*cell));
            for(int y=0;y<=size.y;y++)line(origin+new Vector3(0,.025f,y*cell),origin+new Vector3(size.x*cell,.025f,y*cell));
            var mesh=new Mesh { name="RanchGrid",vertices=vertices.ToArray(),triangles=indices.ToArray() };mesh.RecalculateNormals();
            var go=new GameObject("group_placement_grid",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,"PlacementGrid");go.GetComponent<MeshRenderer>().sharedMaterial=Mat("Grid",new Color(.76f,.83f,.48f));
            return go;
        }

        private static Data_RanchSave InitialLayout()
        {
            return RanchDemoLayout.Create();
        }

        private static void AddResidents(Transform parent)
        {
            var root=new GameObject("group_monsters");root.transform.SetParent(parent,false);
            string[] ids={"GiantPoring","Minorous","Edgar"};
            Vector3[] positions={new Vector3(-9,.05f,3),new Vector3(6.75f,.05f,.6f),new Vector3(-2,.05f,3)};
            for(int i=0;i<ids.Length;i++)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Race/CartoonMonsters/P_Runner_Cartoon_"+ids[i]+".prefab");
                if(source==null) continue;
                var monster=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);monster.name="obj_resident_"+ids[i];
                foreach(var component in monster.GetComponentsInChildren<MonoBehaviour>()) component.enabled=false;
                foreach(var collider in monster.GetComponentsInChildren<Collider>())collider.enabled=false;
                var renderers=monster.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                monster.transform.localScale*=2.2f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                monster.transform.localPosition=positions[i]; monster.transform.localRotation=Quaternion.Euler(0,160+i*30,0);
                RanchSurfaceRepair.ConfigureResident(monster, ids[i], UnityEngine.Object.FindFirstObjectByType<Sys_RanchPlacement>(), i);
            }
        }

        private static Material Wood()=>Mat("Wood",new Color(.49f,.28f,.11f));
        private static Material Stone()=>Mat("Stone",new Color(.55f,.55f,.48f));
        private static Material Mat(string name,Color color)
        {
            if(materials.TryGetValue(name,out var found))return found;
            string path=artRoot+"/Materials/Mat_"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.shader=Shader.Find("Universal Render Pipeline/Lit");material.SetFloat("_Smoothness",.08f);material.SetFloat("_Metallic",0);
            material.SetColor("_BaseColor",color);material.SetColor("_ShadeColor",new Color(.62f,.69f,.73f));material.SetFloat("_AmbientStrength",.18f);
            material.SetFloat("_Softness",.10f);material.SetFloat("_Specular",.025f);material.SetFloat("_FillStrength",.2f);material.enableInstancing=true;
            EditorUtility.SetDirty(material);materials[name]=material;return material;
        }
        private static GameObject Box(Transform root,string name,Vector3 position,Vector3 scale,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<MeshFilter>().sharedMesh=bevel;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        private static GameObject Primitive(Transform root,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=position;go.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private static void Beam(Transform root,string name,Vector3 a,Vector3 b,float width,Material material)
        {
            var go=Box(root,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),material);go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=artRoot+"/Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);return existing;
        }
        private static void Face(List<Vector3> v,List<int> t,params Vector3[] points)
        {
            int start=v.Count;v.AddRange(points);for(int i=1;i<points.Length-1;i++){t.Add(start);t.Add(start+i);t.Add(start+i+1);}
        }
        private static Mesh BeveledCube()
        {
            var v=new List<Vector3>();var t=new List<int>();float a=.5f,b=.43f;
            // Generate each face in a local right-handed basis, then the twelve chamfers and eight corners.
            for(int axis=0;axis<3;axis++)foreach(int sign in new[]{-1,1})
            {
                var n=Vector3.zero;n[axis]=sign;var u=Vector3.zero;u[(axis+1)%3]=1;var w=Vector3.Cross(n,u);
                Face(v,t,n*a-u*b-w*b,n*a+u*b-w*b,n*a+u*b+w*b,n*a-u*b+w*b);
            }
            for(int axis=0;axis<3;axis++)foreach(int s in new[]{-1,1})foreach(int r in new[]{-1,1})
            {
                int j=(axis+1)%3,k=(axis+2)%3;
                var p=Vector3.zero;p[axis]=-b;p[j]=s*a;p[k]=r*b;
                var q=p;q[j]=s*b;q[k]=r*a;
                var p2=p;p2[axis]=b;var q2=q;q2[axis]=b;
                var points=new[]{p,p2,q2,q};var normal=Vector3.Cross(points[1]-points[0],points[2]-points[0]);
                if(Vector3.Dot(normal,(p+q+p2+q2))<0)Array.Reverse(points);Face(v,t,points);
            }
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var points=new[]{new Vector3(x*a,y*b,z*b),new Vector3(x*b,y*a,z*b),new Vector3(x*b,y*b,z*a)};
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),new Vector3(x,y,z))<0)Array.Reverse(points);Face(v,t,points);
            }
            var mesh=new Mesh { name="BeveledBlock",vertices=v.ToArray(),triangles=t.ToArray() };mesh.uv=v.Select(p=>new Vector2(p.x+.5f,p.z+.5f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static Mesh ConeMesh()
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*.2f,b=(i+1)*Mathf.PI*.2f;
                var p=new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f);var q=new Vector3(Mathf.Cos(b)*.5f,0,Mathf.Sin(b)*.5f);
                Face(v,t,p,Vector3.up,q);Face(v,t,Vector3.zero,p,q);
            }
            var mesh=new Mesh { name="PineCrown",vertices=v.ToArray(),triangles=t.ToArray() };mesh.RecalculateNormals();return mesh;
        }
        private static Mesh FoliageMesh()
        {
            const int rings=6,segments=10;
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int y=0;y<=rings;y++)for(int x=0;x<=segments;x++)
            {
                float phi=y*Mathf.PI/rings,theta=x*2*Mathf.PI/segments;
                vertices.Add(new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta))*.5f);
            }
            for(int y=0;y<rings;y++)for(int x=0;x<segments;x++)
            {int a=y*(segments+1)+x,b=a+segments+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=new Mesh{name="Foliage",vertices=vertices.ToArray(),triangles=triangles.ToArray()};mesh.normals=vertices.Select(v=>v.normalized).ToArray();mesh.RecalculateBounds();return mesh;
        }
    }
}
