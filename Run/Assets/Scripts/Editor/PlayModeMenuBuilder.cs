using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class PlayModeMenuBuilder
    {
        public static readonly string[] ScenePaths = { "Assets/Scenes/Scene_Title.unity", "Assets/Scenes/Scene_All3D.unity", "Assets/Scenes/Scene_MonsterRenderCompare.unity" };
        private static Font font;
        [MenuItem("Tools/Project Run/Build play mode menu")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play mode first.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            for (int i = 0; i < ScenePaths.Length; i++)
            {
                // The comparison panel owns its own return button.
                if (ScenePaths[i] == MonsterRenderCompareBuilder.ScenePath) continue;
                var scene = EditorSceneManager.OpenScene(ScenePaths[i]);
                var old = GameObject.Find("UICanvas_Root/Canvas_PlayMode");
                if (old != null) Object.DestroyImmediate(old);
                var root = GameObject.Find("UICanvas_Root") ?? new GameObject("UICanvas_Root", typeof(RectTransform));
                var canvasRect = Rect(root.transform, "Canvas_PlayMode", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
                var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(720,1280); scaler.matchWidthOrHeight = 0;
                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
                var panelRect = Rect(canvasRect, "UIPanel_PlayMode", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                panelRect.gameObject.SetActive(false);
                var panel = panelRect.gameObject.AddComponent<UIPanel_PlayMode>();
                if (i == 0)
                {
                    var bg = Rect(panelRect,"bg_title",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
                    bg.color = new Color(.035f,.06f,.11f); bg.raycastTarget = false;
                }
                panel.SafeArea = Rect(panelRect,"obj_safe_area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
                if (i == 0)
                {
                    Label(panel.SafeArea,"txt_title","PROJECT RUN",new Vector2(0,310),new Vector2(620,90),54);
                    Label(panel.SafeArea,"txt_description","게임을 시작하세요",new Vector2(0,230),new Vector2(620,60),28);
                    panel.Buttons = new[] { Button(panel.SafeArea,"btn_all3d","게임시작",30), Button(panel.SafeArea,"btn_render_compare","렌더링 3종 비교",-110) };
                    panel.Scenes = new[] { "Scene_All3D", "Scene_MonsterRenderCompare" };
                    panel.StatusLabel = Label(panel.SafeArea,"txt_status","",new Vector2(0,-310),new Vector2(620,80),21);
                }
                else
                {
                    var button = Button(panel.SafeArea,"btn_title","타이틀",0);
                    var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(1,1); rect.sizeDelta = new Vector2(120,48); rect.anchoredPosition = new Vector2(-72,-174);
                    button.GetComponentInChildren<Text>().fontSize = 22;
                    panel.Buttons = new[] { button }; panel.Scenes = new[] { "Scene_Title" };
                }
                panelRect.gameObject.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(panelRect.gameObject,"Assets/Resources/Prefabs/UI/Panel/" + (i == 0 ? "UIPanel_PlayMode" : "UIPanel_PlayModeReturn") + ".prefab");
                if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                if (Object.FindFirstObjectByType<Mgr_UI>() == null)
                {
                    var managers = GameObject.Find("[Managers]") ?? new GameObject("[Managers]");
                    var manager = new GameObject("Mgr_UI"); manager.transform.SetParent(managers.transform); manager.AddComponent<Mgr_UI>();
                }
                EditorSceneManager.SaveScene(scene);
            }
            EditorBuildSettings.scenes = ScenePaths.Select(path => new EditorBuildSettingsScene(path,true)).ToArray();
            EditorSceneManager.OpenScene(ScenePaths[0]);
            AssetDatabase.SaveAssets();
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); rect.anchorMin=min;rect.anchorMax=max;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static Text Label(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize)
        {
            var text=Rect(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),position,size).gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
        private static Button Button(Transform parent,string name,string value,float y)
        {
            var rect=Rect(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,y),new Vector2(540,110));
            var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.12f,.3f,.46f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var label=Label(rect,"txt_label",value,Vector2.zero,Vector2.zero,34);var labelRect=label.rectTransform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;return button;
        }
    }
}
