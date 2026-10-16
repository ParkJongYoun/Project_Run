using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class MonsterRenderCompareBuilder
    {
        public const string ScenePath = "Assets/Scenes/Scene_MonsterRenderCompare.unity";
        private static Font font;

        [MenuItem("Tools/Project Run/Race/Build three-monster comparison")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            // Save a separate copy so race results and the six-monster lineup stay independent.
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_All3D.unity");
            EditorSceneManager.SaveScene(scene, ScenePath, true);
            scene = EditorSceneManager.OpenScene(ScenePath);
            var race = Object.FindFirstObjectByType<Sys_Race>();
            var foxPrefab = race.Entrants[2].Prefab;
            var presentation = Object.FindFirstObjectByType<Sys_RacePresentation>();
            foreach (var view in presentation.Views) Object.DestroyImmediate(view.gameObject);
            foreach (var panel in Object.FindObjectsByType<UIPanel_Base>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(panel.gameObject);
            foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (component is Sys_Race || component is Sys_RacePresentation || component is Sys_RaceCamera || component is Sys_RaceRenderStyle || component is Sys_RaceSkillVideo)
                    Object.DestroyImmediate(component);

            var systems = GameObject.Find("[Systems]");
            var host = new GameObject("Sys_MonsterRenderCompare"); host.transform.SetParent(systems.transform, false);
            var comparison = host.AddComponent<Sys_MonsterRenderCompare>();
            comparison.ViewCamera = Camera.main;
            comparison.ViewCamera.fieldOfView = 32;
            comparison.ViewCamera.nearClipPlane = .1f;
            comparison.Views = new RaceRunnerView[3];
            var runners = new GameObject("obj_comparison_runners");
            for (int i = 0; i < 3; i++)
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(foxPrefab, runners.transform);
                root.name = "P_FoxCompare_" + (E_MonsterRenderStyle)i;
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var view = root.GetComponent<RaceRunnerView>();
                view.enabled = false;
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>()) particles.gameObject.SetActive(false);
                foreach (var trail in root.GetComponentsInChildren<TrailRenderer>()) trail.gameObject.SetActive(false);
                var sprite = root.GetComponent<RaceFoxSpriteView>();
                if (sprite != null) Object.DestroyImmediate(sprite);
                view.RunClip.SampleAnimation(view.MountRoot.gameObject, .2f);
                view.MountRoot.localPosition = Vector3.zero;
                view.MountRoot.localRotation = Quaternion.identity;
                root.GetComponent<Sys_MonsterLook>().Apply((E_MonsterRenderStyle)i);
                comparison.Views[i] = view;
            }
            comparison.ApplyLayout();
            CreateUi(comparison);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void CreateUi(Sys_MonsterRenderCompare comparison)
        {
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            var root = GameObject.Find("UICanvas_Root");
            foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            var canvasRect = Rect(root.transform, "Canvas_Panel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = (int)E_CanvasLayer.Panel;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720,1280);
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            var panelRect = Rect(canvasRect,"UIPanel_MonsterRenderCompare",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            panelRect.gameObject.SetActive(false);
            var panel = panelRect.gameObject.AddComponent<UIPanel_MonsterRenderCompare>();
            panel.Comparison = comparison;
            panel.SafeArea = Rect(panelRect,"obj_safe_area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var header = Rect(panel.SafeArea,"group_header",new Vector2(0,1),Vector2.one,new Vector2(0,-90),new Vector2(0,180));
            Background(header, "bg_header", new Color(.035f,.06f,.09f,.94f));
            Label(header,"txt_title","구미호 · 렌더링 3종 비교",new Vector2(0,35),new Vector2(680,50),30);
            Label(header,"txt_description","같은 모델 · 같은 동작 · 같은 조명",new Vector2(0,-18),new Vector2(680,40),23);
            panel.Markers = new RectTransform[3];
            string[] names = {"1  기존", "2  툰", "3  툰+외곽선"};
            for (int i = 0; i < 3; i++)
            {
                var marker = Rect(panelRect,"group_style_"+i,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(184,50));
                Background(marker,"bg_label",new Color(.035f,.06f,.09f,.88f));
                Label(marker,"txt_style",names[i],Vector2.zero,new Vector2(182,48),22);
                panel.Markers[i] = marker;
            }
            var controls = Rect(panel.SafeArea,"group_controls",Vector2.zero,new Vector2(1,0),new Vector2(0,112),new Vector2(0,224));
            Background(controls,"bg_controls",new Color(.035f,.06f,.09f,.94f));
            Label(controls,"txt_help","달리는 모습에서 음영과 윤곽을 비교하세요",new Vector2(0,72),new Vector2(690,40),22);
            panel.PauseButton = Button(controls,"btn_pause","일시정지",new Vector2(-220,14),202);
            panel.SpeedButton = Button(controls,"btn_speed","속도 1×",new Vector2(0,14),202);
            panel.AngleButton = Button(controls,"btn_angle","사선 보기",new Vector2(220,14),202);
            panel.PauseLabel = panel.PauseButton.GetComponentInChildren<Text>();
            panel.SpeedLabel = panel.SpeedButton.GetComponentInChildren<Text>();
            panel.AngleLabel = panel.AngleButton.GetComponentInChildren<Text>();
            panel.ResetButton = Button(controls,"btn_reset","처음부터",new Vector2(-165,-57),310);
            panel.ReturnButton = Button(controls,"btn_title","타이틀",new Vector2(165,-57),310);
            // Scene-only references are rebound by OnOpen when this prefab is instantiated elsewhere.
            panel.Comparison = null;
            PrefabUtility.SaveAsPrefabAsset(panelRect.gameObject,"Assets/Resources/Prefabs/UI/Panel/UIPanel_MonsterRenderCompare.prefab");
            panel.Comparison = comparison;
            panelRect.gameObject.SetActive(true);
        }

        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 pos,Vector2 size)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=min; rect.anchorMax=max; rect.anchoredPosition=pos; rect.sizeDelta=size; return rect;
        }
        private static void Background(Transform parent,string name,Color color)
        {
            var image=Rect(parent,name,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>(); image.color=color; image.raycastTarget=false;
        }
        private static Text Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,int fontSize)
        {
            var label=Rect(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,size).gameObject.AddComponent<Text>();
            label.font=font; label.text=text; label.fontSize=fontSize; label.alignment=TextAnchor.MiddleCenter; label.color=Color.white; label.raycastTarget=false; return label;
        }
        private static Button Button(Transform parent,string name,string text,Vector2 pos,float width)
        {
            var rect=Rect(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),pos,new Vector2(width,56));
            var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.12f,.29f,.34f);
            var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
            Label(rect,"txt_label",text,Vector2.zero,new Vector2(width-10,54),23); return button;
        }
    }
}
