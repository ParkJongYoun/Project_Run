using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        private const string fantasyArt = "Assets/ArtBundles/UI/RaceFantasy";
        private static readonly Dictionary<string, Sprite> fantasySprites = new Dictionary<string, Sprite>();
        private static Color ink => Hex("12356D");
        private static Color goldInk => Hex("A67226");
        private static readonly Vector2 center = new Vector2(.5f, .5f);
        [Serializable] private sealed class SpriteRegion { public string Name, File; public int X, Y, Width, Height; }
        [Serializable] private sealed class SpriteRegionList { public SpriteRegion[] Items; }

        [MenuItem("Tools/Project Run/Race/Apply fantasy HUD")]
        public static void ApplyFantasyHUD()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            BuildFantasyHUD(Object.FindFirstObjectByType<Sys_Race>(), Object.FindFirstObjectByType<Sys_RacePresentation>(), Object.FindFirstObjectByType<Sys_RaceCamera>());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }
        private static void ImportFantasySprites()
        {
            Ensure(fantasyArt + "/Sprites"); fantasySprites.Clear();
            foreach (string file in new[] { "UI_FantasyAtlas.png", "UI_FantasyFrame.png" })
            {
                string path = fantasyArt + "/" + file; AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
            }
            var regions = JsonUtility.FromJson<SpriteRegionList>(File.ReadAllText(fantasyArt + "/sprite-regions.json"));
            foreach (var region in regions.Items)
            {
                string path = fantasyArt + "/Sprites/" + region.Name + ".asset";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fantasyArt + "/" + region.File);
                    Vector4 border = region.Name == "FrameClean" ? new Vector4(180, 145, 180, 145)
                        : region.Name == "Parchment" ? Vector4.one * 20
                        : region.Name == "ProgressFrame" ? new Vector4(48, 20, 48, 20) : Vector4.zero;
                    sprite = Sprite.Create(texture, new Rect(region.X, region.Y, region.Width, region.Height), center, 100, 0, SpriteMeshType.FullRect, border);
                    sprite.name = region.Name; AssetDatabase.CreateAsset(sprite, path);
                }
                fantasySprites.Add(region.Name, sprite);
            }
        }
        private static RectTransform Fixed(Transform parent, string name, float x, float y, float width, float height, Vector2 anchor)
            => Rect(parent, name, anchor, anchor, new Vector2(x, y), new Vector2(width, height));
        private static UnityEngine.UI.Image Art(Transform parent, string name, string sprite, float x, float y, float width, float height)
        {
            var image = Image(parent, name, center, center, new Vector2(x, y), new Vector2(width, height), Color.white);
            image.sprite = fantasySprites[sprite]; image.preserveAspect = true; return image;
        }
        private static UnityEngine.UI.Text Caption(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
            => Text(parent, name, value, center, center, new Vector2(x, y), new Vector2(width, height), size, color, alignment);
        private static void PanelSurface(Transform parent, Color tint, float frameScale = 6)
        {
            var bg = Image(parent, "bg_surface", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-10, -10), tint);
            bg.sprite = fantasySprites["Parchment"]; bg.type = UnityEngine.UI.Image.Type.Sliced; bg.pixelsPerUnitMultiplier = 1;
            var frame = Image(parent, "img_frame", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            frame.sprite = fantasySprites["FrameClean"]; frame.type = UnityEngine.UI.Image.Type.Sliced; frame.pixelsPerUnitMultiplier = frameScale;
        }
        private static UnityEngine.UI.Button FantasyButton(Transform parent, string name, string label, float x, float y, float width, float height, out UnityEngine.UI.Text text)
        {
            var rect = Fixed(parent,name,x,y,width,height,center); PanelSurface(rect,Color.white,7);
            var button = Get<UnityEngine.UI.Button>(rect.gameObject); var surface = rect.Find("bg_surface").GetComponent<UnityEngine.UI.Image>();
            surface.raycastTarget = true; button.targetGraphic = surface;
            var state = button.colors; state.highlightedColor = new Color(1,.96f,.82f); state.pressedColor = new Color(.8f,.86f,.95f); button.colors = state;
            text = Caption(rect,"txt_label",label,0,0,width-30,height-16,24,ink);
            return button;
        }
        private static void BuildFantasyHUD(Sys_Race race, Sys_RacePresentation presentation, Sys_RaceCamera director)
        {
            if (race == null) throw new InvalidOperationException("Race scene missing.");
            ImportFantasySprites();
            font = AssetDatabase.LoadAssetAtPath<Font>(fantasyArt + "/Fonts/NanumGothic-ExtraBold.ttf");
            var canvasRoot = GetRoot("UICanvas_Root"); var canvasObject = GetChild(canvasRoot.transform, "Canvas_Game");
            var canvas = Get<Canvas>(canvasObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = (int)E_CanvasLayer.Game;
            var scaler = Get<UnityEngine.UI.CanvasScaler>(canvasObject); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720,1280); scaler.matchWidthOrHeight = 0;
            Get<UnityEngine.UI.GraphicRaycaster>(canvasObject);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); events.transform.SetParent(canvasRoot.transform);
            }
            var panel = Rect(canvasObject.transform,"UIPanel_RaceHUD",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var safe = Rect(panel,"obj_safe_area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var hud = Get<UIPanel_RaceHUD>(panel.gameObject); hud.SafeArea = safe;
            // Keep existing objects/references available; the authored replacement layout has its own group.
            foreach (Transform child in safe) child.gameObject.SetActive(child.name == "group_fantasy");
            var root = Rect(safe,"group_fantasy",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            Vector2 top = new Vector2(.5f,1), bottom = new Vector2(.5f,0);
            var title = Fixed(root,"group_course",-151,-61,320,76,top); PanelSurface(title,Color.white);
            Caption(title,"txt_title","프론테라 초원컵",25,10,260,36,27,ink);
            Caption(title,"txt_course","잔디 "+Mathf.RoundToInt(Data_RaceSimulation.Length)+" m · "+race.Entrants.Length+"인",25,-19,250,26,18,Hex("80562D"),TextAnchor.MiddleLeft);
            var flag = Fixed(root,"obj_pennant",-305,-63,82,105,top); Art(flag,"img_pennant","Pennant",0,0,82,105);
            var distance = Fixed(root,"group_distance",195,-61,262,76,top); PanelSurface(distance,Color.white);
            Art(distance,"img_wing_left","WingLeft",-140,0,39,60); Art(distance,"img_wing_right","WingRight",140,0,39,60);
            Caption(distance,"txt_remaining_caption","남은 거리",-86,3,81,48,17,ink);
            hud.DistanceLabel = Caption(distance,"txt_remaining","790",18,0,132,67,54,ink);
            Caption(distance,"txt_m","m",100,-10,30,38,27,ink);
            var progress = Fixed(root,"group_progress",0,-133,642,24,top);
            var track = Image(progress,"bg_track",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-16,-9),Hex("B9B7A5")); track.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art+"/UI/RoundPanel.png");track.type=UnityEngine.UI.Image.Type.Sliced;
            hud.ProgressFill = Image(progress,"img_fill",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-17,-10),Hex("39D8E8"));
            hud.ProgressFill.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art+"/UI/RoundPanel.png");hud.ProgressFill.type=UnityEngine.UI.Image.Type.Filled;hud.ProgressFill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
            hud.ProgressFill.fillAmount=0;
            var rail = Image(progress,"img_frame",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.white);rail.sprite=fantasySprites["ProgressFrame"];rail.type=UnityEngine.UI.Image.Type.Sliced;rail.pixelsPerUnitMultiplier=3;rail.fillCenter=false;
            Art(progress,"img_start","StartFlag",-327,0,40,46);Art(progress,"img_finish","FinishFlag",327,0,40,46);
            hud.ProgressHandle=Art(progress,"img_handle","Gem",0,0,25,29).rectTransform;
            hud.ProgressHandle.anchorMin=hud.ProgressHandle.anchorMax=new Vector2(0,.5f);
            var ranking = Fixed(root,"group_ranking",0,-190,420,50,top);
            int count=race.Entrants.Length;
            hud.RankBadges=new RectTransform[count];hud.RankLabels=new UnityEngine.UI.Text[count];hud.RankBackgrounds=new UnityEngine.UI.Image[count];hud.MyBadges=new GameObject[count];hud.RunnerButtons=new UnityEngine.UI.Button[count];
            for(int i=0;i<count;i++)
            {
                var badge=Fixed(ranking,"btn_runner_"+i,(i-(count-1)*.5f)*55,0,43,43,center);hud.RankBadges[i]=badge;
                var bg=Image(badge,"bg_rank",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-6,-6),colors[i]);bg.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art+"/UI/Dot.png");bg.raycastTarget=true;hud.RankBackgrounds[i]=bg;
                Art(badge,"img_rank_frame","RankRing",0,0,43,43);
                hud.RankLabels[i]=Caption(badge,"txt_number",(i+1).ToString(),0,0,35,36,27,Color.white);
                hud.RunnerButtons[i]=Get<UnityEngine.UI.Button>(badge.gameObject);hud.RunnerButtons[i].targetGraphic=bg;
                var my=Fixed(badge,"obj_my",0,31,43,24,center);PanelSurface(my,Hex("FFDC72"),15);Caption(my,"txt_my","MY",0,0,37,24,16,Hex("80501C"));hud.MyBadges[i]=my.gameObject;my.gameObject.SetActive(i==2);
            }
            var phase=Fixed(root,"group_phase",-259,-234,157,36,top);PanelSurface(phase,Hex("234B82"),12);
            hud.PhaseLabel=Caption(phase,"txt_phase","초원 레이스",0,0,136,28,20,Color.white);
            hud.ShotLabel=Text(root,"txt_shot","자동 중계",top,top,new Vector2(274,-211),new Vector2(124,24),16,Color.white,TextAnchor.MiddleRight);
            Get<UnityEngine.UI.Shadow>(hud.ShotLabel.gameObject).effectDistance=new Vector2(1,-1);
            var skill=Fixed(root,"group_skill",137,-267,414,87,top);PanelSurface(skill,Color.white);hud.SkillRoot=skill.gameObject;
            Art(skill,"img_skill","SkillFeather",-162,0,63,63);
            hud.SkillRunner=Caption(skill,"txt_runner","03 · 아리",18,20,278,25,17,goldInk,TextAnchor.MiddleLeft);
            hud.SkillName=Caption(skill,"txt_skill","여우의 잔상",18,-12,278,38,28,ink,TextAnchor.MiddleLeft);
            skill.gameObject.SetActive(false);

            var status=Fixed(root,"group_selected",0,267,680,128,bottom);PanelSurface(status,Color.white);
            var portrait=Fixed(status,"obj_portrait",-267,8,116,116,center);
            var circle=Image(portrait,"bg_portrait",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Hex("7AC5EA"));circle.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art+"/UI/Dot.png");
            Get<UnityEngine.UI.Mask>(portrait.gameObject).showMaskGraphic=true;
            var maskImage=Get<UnityEngine.UI.Image>(portrait.gameObject);maskImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art+"/UI/Dot.png");maskImage.color=Hex("7AC5EA");maskImage.raycastTarget=false;
            hud.FocusPortrait=Image(portrait,"img_portrait",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(6,6),Color.white);hud.FocusPortrait.preserveAspect=true;
            hud.FocusPortrait.sprite=race.Entrants[Mathf.Clamp(hud.SelectedRunnerId,0,count-1)].FacePortrait;
            Art(status,"img_portrait_frame","PortraitFrame",-267,8,153,157);
            var plate=Fixed(status,"obj_number",-267,-60,65,26,center);PanelSurface(plate,Hex("224C8B"),15);hud.FocusNumber=Caption(plate,"txt_number","03",0,0,56,23,19,Color.white);
            Art(status,"img_feather","SkillFeather",-163,21,25,25);
            hud.FocusName=Caption(status,"txt_name","아리 · 구미호",11,26,290,43,28,ink,TextAnchor.MiddleLeft);hud.FocusName.resizeTextForBestFit=true;hud.FocusName.resizeTextMinSize=21;hud.FocusName.resizeTextMaxSize=28;
            hud.FocusGap=Caption(status,"txt_gap","선두와 0.0 m",11,-22,290,31,23,Hex("866239"),TextAnchor.MiddleLeft);
            hud.FocusRank=Caption(status,"txt_rank","3",216,8,84,97,76,Hex("BE922E"));var rankShadow=Get<UnityEngine.UI.Shadow>(hud.FocusRank.gameObject);rankShadow.effectColor=Hex("77501C");rankShadow.effectDistance=new Vector2(1,-2);
            Caption(status,"txt_rank_unit","위",279,29,42,42,32,ink);hud.FocusCount=Caption(status,"txt_count","/ 6",279,-7,58,30,25,Hex("866239"));hud.FocusChange=Caption(status,"txt_change","",279,-38,76,26,19,Hex("45802E"));
            var announcement=Fixed(root,"group_announcement",0,162,680,47,bottom);PanelSurface(announcement,new Color(.10f,.20f,.20f,.94f),12);hud.AnnouncementRoot=announcement.gameObject;
            Art(announcement,"img_trumpet","Trumpet",-300,0,34,34);Caption(announcement,"txt_caption","중계",-245,0,63,32,23,Hex("F4D26E"));
            hud.Announcement=Caption(announcement,"txt_announcement","여섯 주자의 초원컵 레이스",62,0,476,34,22,Color.white,TextAnchor.MiddleLeft);
            var controls=Fixed(root,"group_controls",0,67,680,85,bottom);
            UnityEngine.UI.Text skipLabel,menuLabel;
            hud.CameraButton=FantasyButton(controls,"btn_camera","카메라",-255,0,161,83,out hud.CameraButtonLabel);
            hud.SpeedButton=FantasyButton(controls,"btn_speed","1배",-85,0,161,83,out hud.SpeedButtonLabel);
            hud.SkipButton=FantasyButton(controls,"btn_skip","건너뛰기",85,0,161,83,out skipLabel);
            hud.MenuButton=FantasyButton(controls,"btn_menu","메뉴",255,0,161,83,out menuLabel);
            var mainButtons=new[]{hud.CameraButton,hud.SpeedButton,hud.SkipButton,hud.MenuButton};
            for(int i=0;i<mainButtons.Length;i++)Art(mainButtons[i].transform,"img_gem","Gem",0,-42,22,24);
            Art(hud.CameraButton.transform,"img_icon","CameraIcon",-42,0,39,39);
            hud.CameraButtonLabel.rectTransform.anchoredPosition=new Vector2(23,0);hud.CameraButtonLabel.rectTransform.sizeDelta=new Vector2(92,45);
            Caption(hud.SpeedButton.transform,"img_play","▶▶",-43,0,48,40,25,ink);hud.SpeedButtonLabel.rectTransform.anchoredPosition=new Vector2(28,0);
            Caption(hud.SkipButton.transform,"img_skip","▶|",-51,0,35,40,24,ink);skipLabel.fontSize=21;skipLabel.rectTransform.anchoredPosition=new Vector2(20,0);skipLabel.rectTransform.sizeDelta=new Vector2(109,44);
            Caption(hud.MenuButton.transform,"img_menu","≡",-42,1,42,48,42,ink);menuLabel.rectTransform.anchoredPosition=new Vector2(23,0);
            hud.CountdownLabel=Caption(root.Find("obj_countdown_plate") != null ? root.Find("obj_countdown_plate") : root,"txt_countdown","3",0,70,420,155,96,Hex("FFE398"));Get<UnityEngine.UI.Shadow>(hud.CountdownLabel.gameObject).effectDistance=new Vector2(2,-3);
            hud.WorldMarker=Fixed(safe,"obj_world_marker",0,0,79,39,center);PanelSurface(hud.WorldMarker,Hex("FFE6A3"),13);hud.WorldMarkerText=Caption(hud.WorldMarker,"txt_my","MY 03",0,2,71,30,22,Hex("684219"));Caption(hud.WorldMarker,"img_pointer","▼",0,-27,24,24,22,Hex("F8CA60"));
            var result=Fixed(root,"popup_result",0,25,548,525,center);PanelSurface(result,Color.white);hud.ResultRoot=result.gameObject;
            Caption(result,"txt_title","초원컵 경기 결과",0,208,460,40,28,goldInk);hud.ResultName=Caption(result.Find("group_result_sheet") != null ? result.Find("group_result_sheet") : result,"txt_winner","우승!",0,151,460,64,43,ink);
            hud.ResultDetails=Caption(result,"txt_results","",0,-6,330,235,24,ink,TextAnchor.UpperLeft);hud.ResultDetails.lineSpacing=1.2f;
            UnityEngine.UI.Text replayLabel;hud.ResultReplay=FantasyButton(result.Find("group_result_sheet") != null ? result.Find("group_result_sheet") : result,"btn_replay","다시 달리기",0,-196,270,65,out replayLabel);result.gameObject.SetActive(false);
            var menu=Rect(root,"popup_menu",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);hud.MenuRoot=menu.gameObject;
            var dim=Image(menu,"bg_dim",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.02f,.06f,.12f,.65f));dim.raycastTarget=true;
            var menuPanel=Fixed(menu,"group_panel",0,0,430,454,center);PanelSurface(menuPanel,Color.white);Caption(menuPanel,"txt_title","레이스 메뉴",0,171,370,48,32,ink);
            UnityEngine.UI.Text resumeLabel,restartLabel,closeLabel;
            hud.PauseButton=FantasyButton(menuPanel,"btn_resume","계속 보기",0,89,332,64,out resumeLabel);
            hud.CutsceneButton=FantasyButton(menuPanel,"btn_cutscene","컷씬 연출  ON",0,13,332,64,out hud.CutsceneButtonLabel);
            hud.RestartButton=FantasyButton(menuPanel,"btn_restart","다시 달리기",0,-63,332,64,out restartLabel);
            hud.MenuCloseButton=FantasyButton(menuPanel,"btn_close","닫기",0,-144,218,58,out closeLabel);menu.gameObject.SetActive(false);
            RefineFantasyHUD(hud, race, root);
            hud.Race=null;hud.Director=null;hud.Presentation=null;
            PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject,resources+"/Prefabs/UI/Panel/UIPanel_RaceHUD.prefab",InteractionMode.AutomatedAction);
            hud.Race=race;hud.Director=director;hud.Presentation=presentation;PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
        }
    }
}






