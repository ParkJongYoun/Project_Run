using ProjectRun.Race;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ProjectRun.Editor
{
    public static partial class SpriteRaceSceneBuilder
    {
        private static Font font;
        private static Sprite fillSprite;
        private static Sprite frameSprite;
        private static readonly Color ink = new Color(.08f,.18f,.16f,.94f);
        private static readonly Color cream = new Color(.96f,.93f,.8f);

        private static void BuildHUD(Sys_Race race, Sys_SpriteRaceCamera director, Mgr_UI manager, Color[] colors)
        {
            font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/RaceFantasy/Fonts/NanumGothic-Bold.ttf");
            SpriteRaceCapture.Ensure(art+"/UI");
            fillSprite=UIShape(false);frameSprite=UIShape(true);
            var root=new GameObject("UICanvas_Root",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder=(int)E_CanvasLayer.Game;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var layer=Rect(root.transform,"Canvas_Game",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var panel=Rect(layer,"UIPanel_SpriteRaceHUD",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            panel.gameObject.SetActive(false);
            var hud=panel.gameObject.AddComponent<UIPanel_SpriteRaceHUD>();
            var safe=Rect(panel,"group_safe_area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            hud.SafeRoot=safe;
            var header=Frame(safe,"group_race_header",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-37),new Vector2(1248,52),ink);
            Label(header,"txt_club","MEADOW CLUB",new Vector2(14,0),new Vector2(200,44),22,cream,TextAnchor.MiddleLeft);
            hud.Status=Label(header,"txt_status","MONSTER CUP / 초원 경기",new Vector2(235,0),new Vector2(600,44),21,cream,TextAnchor.MiddleLeft);
            hud.Clock=Label(header,"txt_clock","00:00.0",new Vector2(1050,0),new Vector2(170,44),26,cream,TextAnchor.MiddleRight);
            hud.TitleButton=Button(header,"btn_title","타이틀",new Vector2(930,0),new Vector2(96,34));
            var progress=Rect(safe,"group_progress",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-78),new Vector2(1190,8));
            var bar=Image(progress,"bg_progress",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.1f,.25f,.19f,.55f));
            hud.ProgressDots=new UnityEngine.UI.Image[6];
            for(int i=0;i<6;i++)hud.ProgressDots[i]=Image(progress,"img_runner_"+i,Vector2.zero,Vector2.zero,new Vector2(0,(i-2.5f)*2),new Vector2(12,12),colors[i]);
            var ranking=Frame(safe,"group_rankings",new Vector2(0,1),new Vector2(0,1),new Vector2(109,-218),new Vector2(186,238),new Color(.08f,.18f,.16f,.83f));
            Label(ranking,"txt_title","LIVE RANKING",new Vector2(12,94),new Vector2(164,30),16,new Color(.68f,.8f,.7f),TextAnchor.MiddleLeft);
            hud.RankLabels=new UnityEngine.UI.Text[6];
            for(int i=0;i<6;i++)hud.RankLabels[i]=Label(ranking,"txt_rank_"+i,(i+1)+"   "+SpriteRaceCapture.Names[i],new Vector2(12,62-i*29),new Vector2(168,27),17,Color.white,TextAnchor.MiddleLeft);
            var bottom=Frame(safe,"group_controls",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,63),new Vector2(1248,102),ink);
            hud.FocusName=Label(bottom,"txt_focus_name","미노타우로스",new Vector2(18,25),new Vector2(255,34),26,cream,TextAnchor.MiddleLeft);
            hud.FocusStats=Label(bottom,"txt_focus_stats","1위",new Vector2(18,-8),new Vector2(345,28),17,new Color(.75f,.85f,.78f),TextAnchor.MiddleLeft);
            var stamina=Image(bottom,"bg_stamina",new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(170,-33),new Vector2(305,6),new Color(.17f,.3f,.25f));
            hud.Stamina=Image(stamina.transform,"img_stamina",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.89f,.71f,.37f));
            hud.Stamina.type=UnityEngine.UI.Image.Type.Filled;hud.Stamina.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
            hud.Commentary=Label(bottom,"txt_commentary","잠시 후 출발합니다",new Vector2(380,25),new Vector2(830,30),20,cream,TextAnchor.MiddleLeft);
            hud.PauseButton=Button(bottom,"btn_pause","정지",new Vector2(433,-20),new Vector2(102,38));
            hud.SpeedButton=Button(bottom,"btn_speed","1× 배속",new Vector2(545,-20),new Vector2(102,38));
            hud.CameraButton=Button(bottom,"btn_camera","전체 보기",new Vector2(657,-20),new Vector2(116,38));
            hud.CameraLabel=hud.CameraButton.GetComponentInChildren<UnityEngine.UI.Text>();
            hud.FollowButton=Button(bottom,"btn_follow","다음 주자",new Vector2(783,-20),new Vector2(116,38));
            hud.SkipButton=Button(bottom,"btn_skip","결과 보기",new Vector2(909,-20),new Vector2(116,38));
            hud.RestartButton=Button(bottom,"btn_restart","다시 경기",new Vector2(1035,-20),new Vector2(162,38));
            hud.SpeedLabel=hud.SpeedButton.GetComponentInChildren<UnityEngine.UI.Text>();
            hud.PauseLabel=hud.PauseButton.GetComponentInChildren<UnityEngine.UI.Text>();
            var result=Frame(safe,"popup_result",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(55,10),new Vector2(640,370),new Color(.07f,.17f,.15f,.97f));
            Label(result,"txt_result_title","MONSTER CUP  /  경기 결과",new Vector2(36,142),new Vector2(568,40),27,new Color(1,.82f,.45f),TextAnchor.MiddleLeft);
            hud.ResultText=Label(result,"txt_result_rows","",new Vector2(36,-14),new Vector2(360,240),23,cream,TextAnchor.UpperLeft);
            hud.ResultTimes=Label(result,"txt_result_times","",new Vector2(430,-14),new Vector2(174,240),23,cream,TextAnchor.UpperRight);
            hud.ResultText.lineSpacing=1.6f;hud.ResultTimes.lineSpacing=1.6f;
            Label(result,"txt_result_hint","아래 ‘다시 경기’로 재출전",new Vector2(36,-149),new Vector2(568,30),17,new Color(.7f,.8f,.72f),TextAnchor.MiddleLeft);
            hud.Result=result.gameObject;result.gameObject.SetActive(false);
            SpriteRaceCapture.Ensure("Assets/Resources/Prefabs/UI/Panel");
            if(director.FlatScene) StyleBroadcastHUD(hud);
            // Save the reusable panel without scene references, then bind its connected scene instance.
            PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject,"Assets/Resources/Prefabs/UI/Panel/UIPanel_SpriteRaceHUD.prefab",InteractionMode.AutomatedAction);
            hud.Race=race;hud.Director=director;
            if(director.FlatScene) AttachBroadcast(race,director,hud);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            panel.gameObject.SetActive(true);
            if(Object.FindFirstObjectByType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        }

        private static Sprite UIShape(bool border)
        {
            string path=art+"/UI/"+(border?"UI_Frame.png":"UI_Fill.png");
            var texture=new Texture2D(12,12,TextureFormat.RGBA32,false);
            var pixels=new Color[144];
            for(int y=0;y<12;y++)for(int x=0;x<12;x++)pixels[y*12+x]=border&&x>=2&&x<10&&y>=2&&y<10?Color.clear:Color.white;
            texture.SetPixels(pixels);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            // Explicit sprite assets keep the border independent of any old Multiple importer slices.
            var source=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var sprite=Sprite.Create(source,new Rect(0,0,12,12),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(3,3,3,3));
            string spritePath=art+"/UI/"+(border?"S_Frame.asset":"S_Fill.asset");
            var existing=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if(existing==null){AssetDatabase.CreateAsset(sprite,spritePath);return sprite;}
            EditorUtility.CopySerialized(sprite,existing);Object.DestroyImmediate(sprite);EditorUtility.SetDirty(existing);return existing;
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 p,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=min;r.anchorMax=max;r.anchoredPosition=p;r.sizeDelta=size;return r;
        }
        private static RectTransform Frame(Transform parent,string name,Vector2 min,Vector2 max,Vector2 p,Vector2 size,Color color)
        {
            var root=Rect(parent,name,min,max,p,size);
            Image(root,"bg_panel",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,color);
            var border=Image(root,"img_frame",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.73f,.77f,.58f,.8f));
            border.sprite=frameSprite;border.type=UnityEngine.UI.Image.Type.Sliced;
            return root;
        }
        private static UnityEngine.UI.Image Image(Transform parent,string name,Vector2 min,Vector2 max,Vector2 p,Vector2 size,Color color)
        {
            var rect=Rect(parent,name,min,max,p,size);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=fillSprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=color;image.raycastTarget=false;return image;
        }
        private static UnityEngine.UI.Text Label(Transform parent,string name,string value,Vector2 p,Vector2 size,int fontSize,Color color,TextAnchor alignment)
        {
            var rect=Rect(parent,name,new Vector2(0,.5f),new Vector2(0,.5f),p,size);rect.pivot=new Vector2(0,.5f);
            var text=rect.gameObject.AddComponent<UnityEngine.UI.Text>();text.font=font;text.fontSize=fontSize;text.text=value;text.color=color;text.alignment=alignment;text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        private static UnityEngine.UI.Button Button(Transform parent,string name,string label,Vector2 p,Vector2 size)
        {
            var r=Frame(parent,name,new Vector2(0,.5f),new Vector2(0,.5f),p+new Vector2(size.x/2,0),size,new Color(.18f,.34f,.28f));
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();var image=r.Find("bg_panel").GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;button.targetGraphic=image;
            Label(r,"txt_label",label,new Vector2(0,0),size,16,cream,TextAnchor.MiddleCenter);
            return button;
        }
    }
}
