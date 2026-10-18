using System;
using System.Linq;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class SpriteRaceSceneBuilder
    {
        private const string hudPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_SpriteRaceHUD.prefab";
        private static readonly Color broadcastInk = new Color(.20f,.28f,.23f,1);

        [MenuItem("Tools/Project Run/2D race/Upgrade race broadcast")]
        public static string UpgradeBroadcast()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path!=ScenePath)
                throw new InvalidOperationException("Open Scene_2D in Edit Mode first.");
            var race=Object.FindFirstObjectByType<Sys_Race>();
            var director=Object.FindFirstObjectByType<Sys_SpriteRaceCamera>();
            var prefab=PrefabUtility.LoadPrefabContents(hudPath);
            try { StyleBroadcastHUD(prefab.GetComponent<UIPanel_SpriteRaceHUD>()); PrefabUtility.SaveAsPrefabAsset(prefab,hudPath); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var hud=Object.FindFirstObjectByType<UIPanel_SpriteRaceHUD>(FindObjectsInactive.Include);
            AttachBroadcast(race,director,hud);
            var root=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name.StartsWith("[Environment]"));
            ApplyPaintedMeadow(root.transform);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            return "Scene_2D: ground start boxes, real lane contests, fixed-scale tracking, fantasy HUD and six surge effects saved.";
        }

        private static void AttachBroadcast(Sys_Race race, Sys_SpriteRaceCamera director, UIPanel_SpriteRaceHUD hud)
        {
            ConfigureTwoLapRace(race);
            var presentation=director.GetComponent<Sys_SpriteRacePresentation>();
            if(presentation==null) presentation=director.gameObject.AddComponent<Sys_SpriteRacePresentation>();
            presentation.Race=race;
            director.Overview=false;
            hud.Race=race; hud.Director=director; hud.Presentation=presentation;
            var effects=ImportSkillEffects();
            var material=AssetDatabase.LoadAssetAtPath<Material>(flatArt+"/M_FlatSprite.mat");
            foreach(var view in Object.FindObjectsByType<RaceDirectionalSpriteView>(FindObjectsSortMode.None))
            {
                if(view.Race!=race) continue;
                var effect=view.GetComponent<RaceSpriteSkillEffects>();
                if(effect==null) effect=view.gameObject.AddComponent<RaceSpriteSkillEffects>();
                effect.View=view; effect.Swirl=effects[view.RunnerId]; effect.SpriteMaterial=material;
                EditorUtility.SetDirty(effect);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
            EditorUtility.SetDirty(race); EditorUtility.SetDirty(director); EditorUtility.SetDirty(presentation);
        }

        public static void ConfigureTwoLapRace(Sys_Race race)
        {
            if (EditorApplication.isPlaying || race.gameObject.scene.name != Define_Scene.Sprite2D)
                throw new InvalidOperationException("Configure Scene_2D in Edit Mode.");
            race.EnableLaneContests=true; race.LapCount=2;
            string[] skillNames={"황소의 돌진","통통 가속","그림자 질주","야생의 돌파","달빛 도약","호랑이 포효"};
            float[] skillTimes={9,18,28,36,42,47};
            for(int i=0;i<race.Entrants.Length;i++)
            {
                race.Entrants[i].BaseSpeed=10.8f+i*.55f;
                race.Entrants[i].EarlyPower=i*.22f;
                race.Entrants[i].LatePower=i==0 ? 1.1f : .25f;
                race.Entrants[i].SurgePower=3.2f;
                race.Entrants[i].SurgeTime=skillTimes[i];
                race.Entrants[i].SurgeDuration=4;
                race.Entrants[i].SkillName=skillNames[i];
            }
            EditorUtility.SetDirty(race);
        }

        private static Sprite[] ImportSkillEffects()
        {
            string folder=art+"/SkillEffects", path=folder+"/SkillSwirls.png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default; importer.alphaIsTransparency=true;
            importer.sRGBTexture=true; importer.mipmapEnabled=true; importer.filterMode=FilterMode.Trilinear;
            importer.wrapMode=TextureWrapMode.Clamp; importer.maxTextureSize=2048;
            importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path); var sprites=new Sprite[6];
            for(int i=0;i<6;i++)
            {
                var sprite=Sprite.Create(texture,new Rect(i%3*512,(1-i/3)*512,512,512),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                sprite.name="Skill_"+SpriteRaceCapture.Ids[i]; string spritePath=folder+"/S_"+sprite.name+".asset";
                var previous=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if(previous==null) AssetDatabase.CreateAsset(sprite,spritePath);
                else { EditorUtility.CopySerialized(sprite,previous); Object.DestroyImmediate(sprite); sprite=previous; EditorUtility.SetDirty(sprite); }
                sprites[i]=sprite;
            }
            return sprites;
        }

        private static Sprite BroadcastSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ArtBundles/UI/RaceFantasy/Sprites/"+name+".asset");
        private static RectTransform BroadcastRect(Transform parent,string name,Vector2 anchor,Vector2 point,Vector2 size)
        {
            var child=parent.Find(name);
            var rect=child!=null ? (RectTransform)child : new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false); PlaceBroadcast(rect,anchor,point,size); return rect;
        }
        private static void PlaceBroadcast(RectTransform rect,Vector2 anchor,Vector2 point,Vector2 size)
        { rect.anchorMin=rect.anchorMax=anchor; rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=point; rect.sizeDelta=size; }
        private static UnityEngine.UI.Image BroadcastImage(Transform parent,string name,Sprite sprite,Vector2 point,Vector2 size)
        {
            var rect=BroadcastRect(parent,name,new Vector2(.5f,.5f),point,size);
            var image=rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite=sprite; image.color=Color.white; image.type=UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect=true; image.raycastTarget=false; return image;
        }
        private static void BroadcastCard(Transform root)
        {
            foreach(string name in new[]{"bg_panel","img_frame"})
            {
                var image=BroadcastImage(root,name,BroadcastSprite(name=="bg_panel"?"Parchment":"FrameClean"),Vector2.zero,Vector2.zero);
                image.rectTransform.anchorMin=Vector2.zero; image.rectTransform.anchorMax=Vector2.one;
                image.rectTransform.sizeDelta=Vector2.zero; image.type=UnityEngine.UI.Image.Type.Sliced;
                image.pixelsPerUnitMultiplier=5; image.preserveAspect=false;
                image.transform.SetSiblingIndex(name=="bg_panel"?0:1);
            }
        }
        private static UnityEngine.UI.Text BroadcastText(Transform parent,string name,string value,Vector2 point,Vector2 size,int fontSize)
        {
            var rect=BroadcastRect(parent,name,new Vector2(.5f,.5f),point,size);
            var text=rect.GetComponent<UnityEngine.UI.Text>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/RaceFantasy/Fonts/NanumGothic-Bold.ttf");
            text.fontSize=fontSize; text.text=value; text.color=broadcastInk; text.alignment=TextAnchor.MiddleLeft;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate; text.raycastTarget=false;
            return text;
        }
        private static void PlaceText(UnityEngine.UI.Text text,Transform parent,Vector2 point,Vector2 size,int fontSize,TextAnchor align=TextAnchor.MiddleLeft)
        {
            text.transform.SetParent(parent,false); PlaceBroadcast(text.rectTransform,new Vector2(.5f,.5f),point,size);
            text.color=broadcastInk; text.fontSize=fontSize; text.alignment=align;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate;
        }
        private static void StyleBroadcastHUD(UIPanel_SpriteRaceHUD hud)
        {
            var safe=hud.SafeRoot; var topLeft=new Vector2(0,1); var topRight=Vector2.one;
            var header=safe.Find("group_race_header"); PlaceBroadcast((RectTransform)header,topLeft,new Vector2(188,-56),new Vector2(340,82)); BroadcastCard(header);
            var club=header.Find("txt_club").GetComponent<UnityEngine.UI.Text>();
            club.text="MEADOW MONSTER CUP"; PlaceText(club,header,new Vector2(0,18),new Vector2(292,24),15);
            PlaceText(hud.Status,header,new Vector2(0,-12),new Vector2(292,32),23);
            var timing=BroadcastRect(safe,"group_distance",topRight,new Vector2(-150,-54),new Vector2(264,78)); BroadcastCard(timing);
            PlaceText(hud.Clock,timing,new Vector2(0,15),new Vector2(218,28),24,TextAnchor.MiddleRight);
            hud.Remaining=BroadcastText(timing,"txt_remaining","남은 거리 336 m",new Vector2(0,-16),new Vector2(218,25),17); hud.Remaining.alignment=TextAnchor.MiddleRight;
            hud.TitleButton.transform.SetParent(safe,false); PlaceBroadcast((RectTransform)hud.TitleButton.transform,topRight,new Vector2(-65,-117),new Vector2(94,34));
            BroadcastCard(hud.TitleButton.transform); hud.TitleButton.targetGraphic=hud.TitleButton.transform.Find("bg_panel").GetComponent<UnityEngine.UI.Image>(); hud.TitleButton.targetGraphic.raycastTarget=true;
            PlaceText(hud.TitleButton.GetComponentInChildren<UnityEngine.UI.Text>(),hud.TitleButton.transform,Vector2.zero,new Vector2(88,32),15,TextAnchor.MiddleCenter);

            var progress=(RectTransform)safe.Find("group_progress"); PlaceBroadcast(progress,new Vector2(.5f,1),new Vector2(0,-40),new Vector2(450,18));
            var progressBack=progress.Find("bg_progress").GetComponent<UnityEngine.UI.Image>(); progressBack.color=new Color(.22f,.39f,.29f,.9f);
            var progressFrame=BroadcastImage(progress,"img_frame",BroadcastSprite("ProgressFrame"),Vector2.zero,new Vector2(480,36)); progressFrame.type=UnityEngine.UI.Image.Type.Sliced; progressFrame.pixelsPerUnitMultiplier=3; progressFrame.preserveAspect=false;
            BroadcastImage(progress,"img_start",BroadcastSprite("StartFlag"),new Vector2(-250,0),new Vector2(28,32));
            BroadcastImage(progress,"img_finish",BroadcastSprite("FinishFlag"),new Vector2(250,0),new Vector2(28,32));
            for(int i=0;i<hud.ProgressDots.Length;i++)
            { hud.ProgressDots[i].rectTransform.sizeDelta=new Vector2(11,11); hud.ProgressDots[i].rectTransform.anchoredPosition=new Vector2(0,(i-2.5f)*1.8f); hud.ProgressDots[i].transform.SetAsLastSibling(); }

            hud.Portraits=new Sprite[6]; hud.RankPortraits=new UnityEngine.UI.Image[6]; hud.RankButtons=new UnityEngine.UI.Button[6];
            var ranks=(RectTransform)safe.Find("group_rankings"); PlaceBroadcast(ranks,new Vector2(.5f,1),new Vector2(0,-100),new Vector2(456,74));
            foreach(string name in new[]{"bg_panel","img_frame","txt_title"}) { var child=ranks.Find(name); if(child!=null) child.gameObject.SetActive(false); }
            for(int i=0;i<6;i++)
            {
                hud.Portraits[i]=AutoSpriteRaceImporter.Load(SpriteRaceCapture.Ids[i]).Frame(4,0);
                var slot=BroadcastRect(ranks,"btn_rank_"+i,new Vector2(.5f,.5f),new Vector2((i-2.5f)*76,0),new Vector2(68,74));
                var bg=BroadcastImage(slot,"bg_badge",BroadcastSprite("Parchment"),new Vector2(0,9),new Vector2(54,54));
                hud.RankPortraits[i]=BroadcastImage(slot,"img_portrait",hud.Portraits[i],new Vector2(0,11),new Vector2(48,48));
                BroadcastImage(slot,"img_frame",BroadcastSprite("RankRing"),new Vector2(0,9),new Vector2(62,62));
                hud.RankButtons[i]=slot.GetComponent<UnityEngine.UI.Button>() ?? slot.gameObject.AddComponent<UnityEngine.UI.Button>();
                hud.RankButtons[i].targetGraphic=bg; bg.raycastTarget=true;
                PlaceText(hud.RankLabels[i],slot,new Vector2(0,-28),new Vector2(60,22),16,TextAnchor.MiddleCenter);
            }
            var controls=safe.Find("group_controls"); PlaceBroadcast((RectTransform)controls,new Vector2(1,0),new Vector2(-318,57),new Vector2(600,82)); BroadcastCard(controls);
            var selected=BroadcastRect(safe,"group_selected",Vector2.zero,new Vector2(249,73),new Vector2(462,114)); BroadcastCard(selected);
            hud.FocusPortrait=BroadcastImage(selected,"img_portrait",hud.Portraits[0],new Vector2(-171,0),new Vector2(86,86));
            BroadcastImage(selected,"img_portrait_frame",BroadcastSprite("PortraitFrame"),new Vector2(-171,0),new Vector2(96,96));
            PlaceText(hud.FocusName,selected,new Vector2(37,24),new Vector2(274,36),25);
            PlaceText(hud.FocusStats,selected,new Vector2(37,-7),new Vector2(274,26),15);
            hud.FocusRank=BroadcastText(selected,"txt_rank","1위",new Vector2(187,24),new Vector2(50,36),24); hud.FocusRank.alignment=TextAnchor.MiddleRight;
            var stamina=(RectTransform)hud.Stamina.transform.parent; stamina.SetParent(selected,false); PlaceBroadcast(stamina,new Vector2(.5f,.5f),new Vector2(37,-31),new Vector2(274,7));
            var commentary=BroadcastRect(safe,"group_commentary",new Vector2(1,0),new Vector2(-318,123),new Vector2(600,40)); BroadcastCard(commentary);
            PlaceText(hud.Commentary,commentary,Vector2.zero,new Vector2(554,30),16);
            var buttons=new[]{hud.PauseButton,hud.SpeedButton,hud.CameraButton,hud.FollowButton,hud.SkipButton,hud.RestartButton};
            for(int i=0;i<buttons.Length;i++)
            {
                var button=buttons[i]; PlaceBroadcast((RectTransform)button.transform,new Vector2(.5f,.5f),new Vector2((i-2.5f)*96,0),new Vector2(90,48));
                BroadcastCard(button.transform); button.targetGraphic=button.transform.Find("bg_panel").GetComponent<UnityEngine.UI.Image>(); button.targetGraphic.raycastTarget=true;
                PlaceText(button.GetComponentInChildren<UnityEngine.UI.Text>(),button.transform,Vector2.zero,new Vector2(84,40),15,TextAnchor.MiddleCenter);
            }
            var skill=BroadcastRect(safe,"group_skill",topRight,new Vector2(-227,-210),new Vector2(418,90)); BroadcastCard(skill);
            BroadcastImage(skill,"img_feather",BroadcastSprite("SkillFeather"),new Vector2(175,0),new Vector2(38,54));
            hud.SkillPortrait=BroadcastImage(skill,"img_portrait",hud.Portraits[0],new Vector2(-157,0),new Vector2(72,72));
            hud.SkillName=BroadcastText(skill,"txt_skill_name","황소의 돌진",new Vector2(24,15),new Vector2(266,32),25);
            hud.SkillRunner=BroadcastText(skill,"txt_runner","미노타우로스 · 가속 스킬",new Vector2(24,-17),new Vector2(266,28),16);
            hud.SkillCard=skill.gameObject; skill.gameObject.SetActive(false);
            hud.CountdownText=BroadcastText(safe,"txt_countdown","",new Vector2(0,92),new Vector2(300,120),68); hud.CountdownText.alignment=TextAnchor.MiddleCenter;
            hud.CountdownText.color=new Color(1,.94f,.71f);
            var outline=hud.CountdownText.GetComponent<UnityEngine.UI.Outline>() ?? hud.CountdownText.gameObject.AddComponent<UnityEngine.UI.Outline>(); outline.effectColor=new Color(.17f,.31f,.21f,.85f); outline.effectDistance=new Vector2(2,-2);
            BroadcastCard(hud.Result.transform);
            foreach(var text in hud.Result.GetComponentsInChildren<UnityEngine.UI.Text>(true)) text.color=broadcastInk;
            EditorUtility.SetDirty(hud);
        }
    }
}
