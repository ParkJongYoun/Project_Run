using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        [MenuItem("Tools/Project Run/Race/Apply fox skill video")]
        public static void ApplyFoxSkillVideo()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            if (!File.Exists("Assets/StreamingAssets/Video/FoxUltimate1024.mp4")) throw new FileNotFoundException("Generate and copy the final fox movie first.");
            var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            var hud = UnityEngine.Object.FindFirstObjectByType<UIPanel_RaceHUD>(FindObjectsInactive.Include);
            if (race == null || hud == null) throw new InvalidOperationException("Open Scene_Oval first.");
            font = AssetDatabase.LoadAssetAtPath<Font>(fantasyArt + "/Fonts/NanumGothic-ExtraBold.ttf");
            var canvasRoot = GameObject.Find("UICanvas_Root");
            var canvasObject = GetChild(canvasRoot.transform, "Canvas_Cutscene");
            var canvas = Get<Canvas>(canvasObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = (int)E_CanvasLayer.Cutscene;
            var scaler = Get<UnityEngine.UI.CanvasScaler>(canvasObject);
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            PlayModeLandscapeLayout.ConfigureCanvas(scaler);
            Get<UnityEngine.UI.GraphicRaycaster>(canvasObject);
            var panelRoot = Rect(canvasObject.transform,"UIPanel_RaceSkillVideo",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var panel = Get<UIPanel_RaceSkillVideo>(panelRoot.gameObject);
            panel.Opacity = Get<CanvasGroup>(panelRoot.gameObject);
            var backdrop = Image(panelRoot,"bg_dim",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.018f,.035f,.07f,1));
            backdrop.raycastTarget = true;
            panel.SafeArea = Rect(panelRoot,"obj_safe_area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var zone = Rect(panel.SafeArea,"group_video",new Vector2(0,.16f),new Vector2(1,.84f),Vector2.zero,Vector2.zero);
            var video = Fixed(zone,"img_video",0,0,720,720,center);
            var fit = Get<UnityEngine.UI.AspectRatioFitter>(video.gameObject);
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
            panel.VideoImage = Get<UnityEngine.UI.RawImage>(video.gameObject); panel.VideoImage.raycastTarget = false;
            var titlePlate = Fixed(panel.SafeArea,"group_title",0,-106,410,79,new Vector2(.5f,1));
            VideoSurface(titlePlate, false);
            Caption(titlePlate,"txt_skill","여우의 잔상",0,5,350,46,34,ink).fontStyle = FontStyle.Normal;
            Caption(titlePlate,"txt_subtitle","여우불 · 천공 질주",0,-23,330,25,16,goldInk).fontStyle = FontStyle.Normal;
            panel.MenuButton = VideoButton(panel.SafeArea,"btn_menu","메뉴","IconMenu",-123,119,164);
            panel.SkipButton = VideoButton(panel.SafeArea,"btn_skip","연출 생략","IconSkip",98,119,244);
            panel.SetOpacity(0);
            PlayModeLandscapeLayout.ApplyVideo(panel);
            Ensure(resources + "/Prefabs/UI/Panel");
            PrefabUtility.SaveAsPrefabAssetAndConnect(panelRoot.gameObject,resources+"/Prefabs/UI/Panel/UIPanel_RaceSkillVideo.prefab",InteractionMode.AutomatedAction);
            var systemObject = GetChild(race.transform.parent,"Sys_RaceSkillVideo");
            var system = Get<Sys_RaceSkillVideo>(systemObject);
            system.Race = race; system.HUD = hud; system.Panel = panel;
            system.Presentation = UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>();
            system.Director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            system.Player = Get<VideoPlayer>(systemObject); system.Player.playOnAwake = false;
            system.Player.audioOutputMode = VideoAudioOutputMode.None;
            system.RunnerId = 2; system.VideoPath = "Video/FoxUltimate1024.mp4";
            // The existing menu remains above the movie and continues to pause both race and video.
            const string hudPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_RaceHUD.prefab";
            var contents = PrefabUtility.LoadPrefabContents(hudPath);
            try
            {
                var menu = contents.GetComponent<UIPanel_RaceHUD>().MenuRoot;
                var menuCanvas = Get<Canvas>(menu); menuCanvas.overrideSorting = true; menuCanvas.sortingOrder = (int)E_CanvasLayer.Popup;
                Get<UnityEngine.UI.GraphicRaycaster>(menu);
                PrefabUtility.SaveAsPrefabAsset(contents,hudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            EditorUtility.SetDirty(system);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(race.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(race.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        private static void VideoSurface(Transform parent, bool button)
        {
            var background = Image(parent,"bg_surface",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-14,-14),Color.white);
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fantasyArt+"/Sprites/Parchment.asset");
            var frame = Image(parent,"img_frame",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.white);
            frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fantasyArt+"/V2/Sprites/"+(button?"FrameButtonV2":"FramePanelV2")+".asset");
            frame.type = UnityEngine.UI.Image.Type.Sliced; frame.pixelsPerUnitMultiplier = frame.sprite.border.x / 20;
        }
        private static UnityEngine.UI.Button VideoButton(Transform parent,string name,string label,string icon,float x,float y,float width)
        {
            var rect = Fixed(parent,name,x,y,width,65,new Vector2(.5f,0));
            VideoSurface(rect,true);
            var button = Get<UnityEngine.UI.Button>(rect.gameObject);
            button.targetGraphic = rect.Find("bg_surface").GetComponent<UnityEngine.UI.Image>(); button.targetGraphic.raycastTarget = true;
            var graphic = Image(rect,"img_action",center,center,new Vector2(-width*.5f+35,0),new Vector2(28,32),Color.white);
            graphic.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(fantasyArt+"/V2/Sprites/"+icon+".asset"); graphic.preserveAspect = true;
            Caption(rect,"txt_label",label,19,0,width-65,44,24,ink).fontStyle = FontStyle.Normal;
            return button;
        }
    }
}
