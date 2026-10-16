using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class FoxDirectionalImpostorBuilder
    {
        public const string Art = "Assets/ArtBundles/FoxDirectionalImpostor";
        public const string DataPath = "Assets/Resources/FoxImpostor/FoxDirectionalImpostor.asset";
        public const string ScenePath = "Assets/Scenes/Scene_FoxImpostorTest.unity";
        public const string ReferencePath = Art + "/V_FoxFrozenReference.prefab";
        public const string Output = "DataBundles/FoxDirectionalImpostor";
        public static string Status { get; private set; } = "Idle";
        private static Data_FoxDirectionalImpostor data;
        private static Camera captureCamera;
        private static GameObject reference;
        private static MeshRenderer[] renderers;
        private static Material[][] originalMaterials;
        private static Material[][] depthMaterials;
        private static Texture2D colorAtlas, depthAtlas;
        private static int frame;
        private static Font font;

        public static string FreezeLivePose()
        {
            var source = Object.FindObjectsByType<RaceRunnerView>(FindObjectsSortMode.None).First(v => v.RunnerId == 2);
            // Pose a private clone so capture never advances or pauses the running race.
            var clone = Object.Instantiate(source.MountRoot.gameObject);
            clone.name = "FoxCapturePose";
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var scale = source.MountRoot.lossyScale;
            clone.transform.localScale = scale;
            foreach (var animator in clone.GetComponentsInChildren<Animator>()) animator.enabled = false;
            source.RunClip.SampleAnimation(clone, source.RunClip.length * .18f / source.GallopsPerClip);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            clone.transform.localScale = scale;
            Directory.CreateDirectory(Art + "/Meshes"); Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            var root = new GameObject("V_FoxFrozenReference");
            var bounds = new Bounds(); bool first = true;
            try
            {
                int count = 0;
                foreach (var skin in clone.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => !r.name.Contains("outline")))
                {
                    var mesh = new Mesh { name = "FoxFrozen_" + skin.name };
                    skin.BakeMesh(mesh);
                    Matrix4x4 matrix = skin.transform.localToWorldMatrix;
                    var vertices = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
                    var normalMatrix = matrix.inverse.transpose;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
                        if (i < normals.Length) normals[i] = normalMatrix.MultiplyVector(normals[i]).normalized;
                        if (i < tangents.Length)
                        {
                            Vector3 t = matrix.MultiplyVector(tangents[i]).normalized;
                            tangents[i] = new Vector4(t.x,t.y,t.z,tangents[i].w);
                        }
                    }
                    mesh.vertices=vertices; mesh.normals=normals; mesh.tangents=tangents; mesh.RecalculateBounds();
                    string path=Art+"/Meshes/"+mesh.name+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing!=null) { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; }
                    else AssetDatabase.CreateAsset(mesh,path);
                    var child=new GameObject(skin.name); child.transform.SetParent(root.transform,false);
                    child.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=child.AddComponent<MeshRenderer>(); renderer.sharedMaterials=skin.sharedMaterials;
                    renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
                    if(first) { bounds=mesh.bounds; first=false; } else bounds.Encapsulate(mesh.bounds);
                    count++;
                }
                PrefabUtility.SaveAsPrefabAsset(root,ReferencePath);
                data=AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(DataPath);
                if(data==null) { data=ScriptableObject.CreateInstance<Data_FoxDirectionalImpostor>(); AssetDatabase.CreateAsset(data,DataPath); }
                data.Center=bounds.center; data.Radius=bounds.extents.magnitude*1.06f;
                data.AzimuthCount=16; data.ElevationCount=9; data.TileSize=384;
                data.MinimumElevation=-90; data.MaximumElevation=90;
                data.PoseDescription="Scene_All3D P_Runner_03 / fox RunClip gait phase 0.18 / mount-only private clone; one identical mesh pose in every view";
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
                return $"Frozen {count} meshes; center={bounds.center}, radius={data.Radius}; one shared pose for all 144 views.";
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(clone); }
        }

        [MenuItem("Project Run/Fox Impostor/Open orbit test")]
        public static void OpenTest()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before opening the test scene.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static string CreateTestScene()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Current scene has unsaved edits; save before creating the test.");
            data=AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(DataPath);
            if(data==null) throw new InvalidOperationException("Freeze a live pose first.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.65f,.69f,.78f);
            RenderSettings.ambientEquatorColor=new Color(.40f,.43f,.48f);
            RenderSettings.ambientGroundColor=new Color(.30f,.27f,.25f);
            RenderSettings.fog=false;
            var camera=new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.075f,.10f);
            camera.nearClipPlane=.1f; camera.farClipPlane=100; camera.fieldOfView=32;
            camera.rect=new Rect(0,.25f,1,.64f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            camera.gameObject.AddComponent<AudioListener>();
            Light("Directional Light",new Vector3(40,-35,0),1.55f,new Color(1,.94f,.86f));
            Light("Fill Light",new Vector3(-20,145,0),.65f,new Color(.74f,.83f,1));
            reference=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePath));
            reference.name="obj_original_3d";
            var root=new GameObject("obj_fox_impostor");
            var imp=root.AddComponent<FoxDirectionalImpostor>(); imp.Data=data; imp.ViewCamera=camera;
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name="obj_image_plane"; quad.transform.SetParent(root.transform,false);
            Object.DestroyImmediate(quad.GetComponent<Collider>()); imp.Display=quad.GetComponent<MeshRenderer>();
            var systems=new GameObject("[Systems]");
            var testObject=new GameObject("Sys_FoxImpostorTest"); testObject.transform.SetParent(systems.transform);
            var test=testObject.AddComponent<Sys_FoxImpostorTest>(); test.Impostor=imp; test.ReferenceModel=reference; test.OrbitCamera=camera;
            CreateGrid(data);
            CreateUi(test);
            test.ApplyView();
            EditorSceneManager.SaveScene(scene,ScenePath);
            return "Created separate portrait orbit test scene, with drag/zoom, vertical orbit, automatic orbit and original comparison.";
        }

        private static void Light(string name,Vector3 angles,float intensity,Color color)
        {
            var light=new GameObject(name).AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.color=color;
            light.shadows=LightShadows.None;light.transform.rotation=Quaternion.Euler(angles);
        }

        private static void CreateGrid(Data_FoxDirectionalImpostor set)
        {
            string path=Art+"/Mat_OrbitGrid.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material,path); }
            material.SetColor("_BaseColor",new Color(.12f,.18f,.23f));
            var grid=new GameObject("obj_floor_grid");
            float y=reference.GetComponentsInChildren<MeshRenderer>().Min(r=>r.bounds.min.y)-.1f;
            // Only thin lines: the underside remains visible when orbiting below the fox.
            for(int i=-4;i<=4;i++) for(int axis=0;axis<2;axis++)
            {
                var line=new GameObject("line_"+axis+"_"+i).AddComponent<LineRenderer>();line.transform.SetParent(grid.transform,false);
                line.sharedMaterial=material;line.startWidth=line.endWidth=.012f;line.positionCount=2;
                line.SetPosition(0,axis==0?new Vector3(i,y,-4):new Vector3(-4,y,i));
                line.SetPosition(1,axis==0?new Vector3(i,y,4):new Vector3(4,y,i));
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            }
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 pos,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=min;rect.anchorMax=max;rect.anchoredPosition=pos;rect.sizeDelta=size;return rect;
        }
        private static Text Label(Transform parent,string name,string value,Vector2 pos,Vector2 size,int fontSize,TextAnchor anchor=TextAnchor.MiddleCenter)
        {
            var label=Rect(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,size).gameObject.AddComponent<Text>();
            label.font=font;label.text=value;label.fontSize=fontSize;label.color=new Color(.88f,.92f,.95f);label.alignment=anchor;label.raycastTarget=false;return label;
        }
        private static Button Button(Transform parent,string name,string value,float x,float y,float width)
        {
            var rect=Rect(parent,name,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(x,y),new Vector2(width,54));
            var bg=rect.gameObject.AddComponent<Image>();bg.color=new Color(.13f,.23f,.28f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=bg;
            var label=Label(rect,"txt_label",value,Vector2.zero,new Vector2(width-8,50),22);
            return button;
        }
        private static Slider Slider(Transform parent,string name,string caption,float y,float min,float max)
        {
            var row=Rect(parent,name,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,y),new Vector2(640,46));
            Label(row,"txt_label",caption,new Vector2(-255,0),new Vector2(120,40),21,TextAnchor.MiddleLeft);
            var track=Rect(row,"bg_track",new Vector2(.24f,.5f),new Vector2(1,.5f),Vector2.zero,new Vector2(0,8));
            track.gameObject.AddComponent<Image>().color=new Color(.19f,.27f,.32f);
            var area=Rect(row,"obj_handle_area",new Vector2(.24f,0),new Vector2(1,1),Vector2.zero,new Vector2(-20,0));
            var handle=Rect(area,"img_handle",Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(24,-14));
            var image=handle.gameObject.AddComponent<Image>();image.color=new Color(.4f,.85f,.75f);
            var slider=row.gameObject.AddComponent<Slider>();slider.minValue=min;slider.maxValue=max;slider.handleRect=handle;slider.targetGraphic=image;
            return slider;
        }
        private static void CreateUi(Sys_FoxImpostorTest test)
        {
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            var root=new GameObject("UICanvas_Root",typeof(RectTransform));
            var canvasRect=Rect(root.transform,"Canvas_System",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var canvas=canvasRect.gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=(int)E_CanvasLayer.System;
            var scaler=canvasRect.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(720,1280);scaler.matchWidthOrHeight=0;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            var panelRect=Rect(canvasRect,"UIPanel_FoxImpostorTest",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);panelRect.gameObject.SetActive(false);
            var panel=panelRect.gameObject.AddComponent<UIPanel_FoxImpostorTest>();panel.Test=test;
            panel.SafeArea=Rect(panelRect,"obj_safe_area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var header=Rect(panel.SafeArea,"group_header",new Vector2(0,1),Vector2.one,new Vector2(0,-64),new Vector2(0,128));
            var bg=header.gameObject.AddComponent<Image>();bg.color=new Color(.035f,.05f,.065f);bg.raycastTarget=false;
            Label(header,"txt_title","FOX / VIEW STUDY",new Vector2(0,28),new Vector2(640,44),27);
            panel.StatusLabel=Label(header,"txt_status","2D 임포스터 · 고정 자세",new Vector2(0,-18),new Vector2(640,40),23);
            var controls=Rect(panel.SafeArea,"group_controls",Vector2.zero,new Vector2(1,0),new Vector2(0,154),new Vector2(0,308));
            controls.gameObject.AddComponent<Image>().color=new Color(.035f,.05f,.065f);
            panel.AngleLabel=Label(controls,"txt_angles","",new Vector2(0,133),new Vector2(640,35),20);
            panel.YawSlider=Slider(controls,"slider_yaw","좌우",234,0,360);
            panel.ElevationSlider=Slider(controls,"slider_elevation","위아래",185,-89.9f,89.9f);
            panel.ZoomSlider=Slider(controls,"slider_zoom","거리",136,2.5f,7);
            panel.CompareButton=Button(controls,"btn_compare","원본 3D 보기",-215,77,205);
            panel.AutoButton=Button(controls,"btn_auto","자동 회전",0,77,205);
            panel.ResetButton=Button(controls,"btn_reset","시점 초기화",215,77,205);
            panel.CompareLabel=panel.CompareButton.GetComponentInChildren<Text>();panel.AutoLabel=panel.AutoButton.GetComponentInChildren<Text>();
            Label(controls,"txt_help","화면 드래그: 회전  ·  휠: 거리  ·  Space: 2D / 3D",new Vector2(0,-125),new Vector2(670,35),18);
            panelRect.gameObject.SetActive(true);
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        }

        public static void InstallPresetControls(UIPanel_FoxImpostorTest panel)
        {
            font=panel.StatusLabel.font;
            var header=(RectTransform)panel.SafeArea.Find("group_header");
            header.anchoredPosition=new Vector2(0,-107);header.sizeDelta=new Vector2(0,214);
            ((RectTransform)header.Find("txt_title")).anchoredPosition=new Vector2(0,75);
            panel.StatusLabel.rectTransform.anchoredPosition=new Vector2(0,34);
            var old=header.Find("group_presets");
            if(old!=null) Object.DestroyImmediate(old.gameObject);
            var group=Rect(header,"group_presets",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            panel.PresetLabel=Label(group,"txt_view_count","",new Vector2(0,-3),new Vector2(640,35),20);
            var presets=panel.Test.Presets;
            var counts=presets.Select(p=>p.AzimuthCount*p.ElevationCount).ToArray();panel.PresetButtons=new Button[counts.Length];
            float spacing=636f/counts.Length;
            for(int i=0;i<counts.Length;i++)
                panel.PresetButtons[i]=Button(group,"btn_views_"+counts[i],counts[i]+"장",(i-(counts.Length-1)*.5f)*spacing,38,spacing-10);
            panel.Test.OrbitCamera.rect=new Rect(0,.25f,1,.58f);
            EditorUtility.SetDirty(panel);
        }

        public static string BeginBake()
        {
            if(EditorApplication.isPlaying || captureCamera!=null) throw new InvalidOperationException("Bake requires idle Edit Mode.");
            data=AssetDatabase.LoadAssetAtPath<Data_FoxDirectionalImpostor>(DataPath);
            var test=Object.FindFirstObjectByType<Sys_FoxImpostorTest>();
            if(test==null) throw new InvalidOperationException("Open the orbit test scene.");
            reference=test.ReferenceModel; reference.SetActive(true);test.Impostor.Display.enabled=false;
            renderers=reference.GetComponentsInChildren<MeshRenderer>();
            // Keep this comparison independent of later changes to the game's toon material/shader.
            Directory.CreateDirectory(Art+"/Materials");
            foreach(var renderer in renderers)
            {
                var materials=renderer.sharedMaterials;
                for(int m=0;m<materials.Length;m++)
                {
                    var source=materials[m];
                    if(AssetDatabase.GetAssetPath(source).StartsWith(Art+"/Materials/")) continue;
                    string shaderPath=AssetDatabase.GetAssetPath(source.shader);
                    string shaderCopy=Art+"/Shaders/FoxReference.shader";
                    string shaderName="ProjectRun/FoxFrozenReference";
                    File.WriteAllText(shaderCopy,File.ReadAllText(shaderPath).Replace("Shader \""+source.shader.name+"\"","Shader \""+shaderName+"\""));
                    AssetDatabase.ImportAsset(shaderCopy,ImportAssetOptions.ForceSynchronousImport);
                    string path=Art+"/Materials/Mat_FoxFrozenReference_"+m+".mat";
                    var copy=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(copy==null) {copy=new Material(source);AssetDatabase.CreateAsset(copy,path);} else copy.CopyPropertiesFromMaterial(source);
                    copy.shader=Shader.Find(shaderName);EditorUtility.SetDirty(copy);materials[m]=copy;
                }
                renderer.sharedMaterials=materials;
            }
            string referenceName=reference.name;
            reference.name="V_FoxFrozenReference";
            PrefabUtility.SaveAsPrefabAsset(reference,ReferencePath);
            reference.name=referenceName;
            originalMaterials=renderers.Select(r=>r.sharedMaterials).ToArray();
            var shader=Shader.Find("Hidden/ProjectRun/FoxCaptureDepth");
            depthMaterials=originalMaterials.Select(list=>list.Select(source=>
            {
                var material=new Material(shader);
                material.SetTexture("_BaseMap",source.GetTexture("_BaseMap"));
                material.SetColor("_BaseColor",source.GetColor("_BaseColor"));
                material.SetFloat("_Cutoff",source.GetFloat("_Cutoff"));
                material.SetFloat("_AlphaClip",source.HasProperty("_AlphaClip")?source.GetFloat("_AlphaClip"):0);
                material.SetFloat("_Cull",source.HasProperty("_Cull")?source.GetFloat("_Cull"):2);
                material.SetVector("_CaptureCenter",data.Center); material.SetFloat("_Radius",data.Radius);
                return material;
            }).ToArray()).ToArray();
            foreach(var r in renderers) r.gameObject.layer=30;
            captureCamera=new GameObject("FoxImpostorCaptureCamera").AddComponent<Camera>();captureCamera.enabled=false;
            captureCamera.orthographic=true;captureCamera.orthographicSize=data.Radius;captureCamera.aspect=1;
            captureCamera.nearClipPlane=.1f;captureCamera.farClipPlane=data.Radius*8;
            captureCamera.clearFlags=CameraClearFlags.SolidColor;captureCamera.backgroundColor=Color.clear;captureCamera.cullingMask=1<<30;
            var extra=captureCamera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=false;extra.antialiasing=AntialiasingMode.None;
            captureCamera.allowHDR=false;captureCamera.allowMSAA=false;
            colorAtlas=new Texture2D(data.AzimuthCount*data.TileSize,data.ElevationCount*data.TileSize,TextureFormat.RGBA32,false);
            depthAtlas=new Texture2D(colorAtlas.width,colorAtlas.height,TextureFormat.RGBA32,false,true);
            Directory.CreateDirectory(Output+"/Views");
            frame=0;Status="Baking 0 / 144";
            EditorApplication.update+=BakeTick;
            return Status;
        }
        private static void BakeTick()
        {
            try
            {
                BakeFrame(frame++);Status=$"Baking {frame} / {data.AzimuthCount*data.ElevationCount}";
                if(frame>=data.AzimuthCount*data.ElevationCount) { EditorApplication.update-=BakeTick; FinishBake(); }
            }
            catch(Exception exception) { EditorApplication.update-=BakeTick;Status="Failed: "+exception.Message;Cleanup();Debug.LogException(exception); }
        }
        private static void BakeFrame(int index)
        {
            int column=index%data.AzimuthCount,row=index/data.AzimuthCount;
            Data_FoxDirectionalImpostor.Basis(column*360f/data.AzimuthCount,-90+row*180f/(data.ElevationCount-1),out var d,out var r,out var u);
            captureCamera.transform.SetPositionAndRotation(data.Center+d*(data.Radius*3),Quaternion.LookRotation(-d,u));
            for(int i=0;i<renderers.Length;i++) renderers[i].sharedMaterials=originalMaterials[i];
            if(index==0) Object.DestroyImmediate(Render(captureCamera,data.TileSize,false));
            var color=Render(captureCamera,data.TileSize*2,false);
            var pixels=color.GetPixels32();var reduced=new Color32[data.TileSize*data.TileSize];
            int width=data.TileSize*2;
            for(int y=0;y<data.TileSize;y++) for(int x=0;x<data.TileSize;x++)
            {
                Color sum=Color.clear;
                for(int dy=0;dy<2;dy++) for(int dx=0;dx<2;dx++) sum+=(Color)pixels[(y*2+dy)*width+x*2+dx];
                sum*=.25f;
                if(sum.a>0) { sum.r/=sum.a;sum.g/=sum.a;sum.b/=sum.a; }
                reduced[y*data.TileSize+x]=sum;
            }
            var single=new Texture2D(data.TileSize,data.TileSize,TextureFormat.RGBA32,false);single.SetPixels32(reduced);single.Apply();
            File.WriteAllBytes(Output+$"/Views/view_{row:00}_{column:00}.png",single.EncodeToPNG());
            colorAtlas.SetPixels32(column*data.TileSize,row*data.TileSize,data.TileSize,data.TileSize,reduced);
            Object.DestroyImmediate(color);Object.DestroyImmediate(single);
            for(int i=0;i<renderers.Length;i++)
            {
                foreach(var m in depthMaterials[i]) m.SetVector("_CaptureDirection",d);
                renderers[i].sharedMaterials=depthMaterials[i];
            }
            if(index==0) Object.DestroyImmediate(Render(captureCamera,data.TileSize,true));
            var depth=Render(captureCamera,data.TileSize,true);
            depthAtlas.SetPixels32(column*data.TileSize,row*data.TileSize,data.TileSize,data.TileSize,depth.GetPixels32());Object.DestroyImmediate(depth);
            for(int i=0;i<renderers.Length;i++) renderers[i].sharedMaterials=originalMaterials[i];
        }
        private static Texture2D Render(Camera camera,int size,bool linear)
        {
            var target=RenderTexture.GetTemporary(size,size,24,RenderTextureFormat.ARGB32,linear?RenderTextureReadWrite.Linear:RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
                RenderTexture.active=target;
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,linear);texture.ReadPixels(new Rect(0,0,size,size),0,0);texture.Apply();return texture;
            }
            finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target); }
        }
        private static void FinishBake()
        {
            colorAtlas.Apply();depthAtlas.Apply();
            Directory.CreateDirectory(Art+"/Textures");
            string colorPath=Art+"/Textures/Fox_Color.png",depthPath=Art+"/Textures/Fox_Depth.png";
            File.WriteAllBytes(colorPath,colorAtlas.EncodeToPNG());File.WriteAllBytes(depthPath,depthAtlas.EncodeToPNG());
            Cleanup();
            Import(colorPath,false);Import(depthPath,true);
            data.ColorAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);data.DepthAtlas=AssetDatabase.LoadAssetAtPath<Texture2D>(depthPath);
            string materialPath=Art+"/Mat_FoxDirectionalImpostor.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null) { material=new Material(Shader.Find("ProjectRun/FoxDirectionalImpostor"));AssetDatabase.CreateAsset(material,materialPath); }
            material.SetTexture("_ColorAtlas",data.ColorAtlas);material.SetTexture("_DepthAtlas",data.DepthAtlas);data.Material=material;
            EditorUtility.SetDirty(material);EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            var test=Object.FindFirstObjectByType<Sys_FoxImpostorTest>();test.ShowOriginal=false;test.ApplyView();
            EditorSceneManager.SaveScene(test.gameObject.scene);
            File.WriteAllText(Output+"/capture.json",JsonUtility.ToJson(data,true));
            Status="Complete: 144 color/depth views, one frozen pose, 6144 x 3456 atlases.";Debug.Log(Status);
        }
        private static void Import(string path,bool linear)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;
            importer.sRGBTexture=!linear;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=!linear;
            importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=linear?FilterMode.Point:FilterMode.Bilinear;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=8192;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        private static void Cleanup()
        {
            if(renderers!=null) for(int i=0;i<renderers.Length;i++) if(renderers[i]!=null) {renderers[i].sharedMaterials=originalMaterials[i];renderers[i].gameObject.layer=0;}
            if(depthMaterials!=null) foreach(var list in depthMaterials) foreach(var m in list) Object.DestroyImmediate(m);
            if(captureCamera!=null) Object.DestroyImmediate(captureCamera.gameObject);
            if(colorAtlas!=null) Object.DestroyImmediate(colorAtlas);if(depthAtlas!=null) Object.DestroyImmediate(depthAtlas);
            captureCamera=null;
        }
    }
}
