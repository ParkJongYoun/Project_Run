using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Race;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static partial class RanchCollectionBuilder
    {
        private const string artRoot = "Assets/ArtBundles/UI/RanchCollection";
        private const string resourcePath = "Assets/Resources/UI/RanchCollection/Data_RanchCollectionArt.asset";
        private const string panelRoot = "Assets/Resources/Prefabs/UI/Panel/";
        private const string scenePath = "Assets/Scenes/Scene_LobbyTest.unity";
        private static Data_DemoArt art;
        private static readonly Color ink = new Color(.27f,.13f,.065f);
        private static readonly Color paper = new Color(1,.95f,.78f);

        [MenuItem("Tools/Project Run/Ranch/Build Monster and Training UI")]
        public static void BuildAndConnect()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building UI.");
            art = ImportArt();
            BuildPrefab(false); BuildPrefab(true);
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            bool temporary = !scene.isLoaded;
            if (temporary) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var bootstrap = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Sys_RanchUI>(true)).Single();
            var canvas = bootstrap.Panel.GetComponentInParent<Canvas>().transform;
            Undo.RecordObject(bootstrap,"Connect ranch collection panels");
            Undo.RecordObject(bootstrap.Panel,"Connect ranch collection manager");
            bootstrap.MonsterPanel = Connect(canvas,false);
            bootstrap.TrainingPanel = Connect(canvas,true);
            bootstrap.Panel.CollectionManager = bootstrap.Manager;
            EditorUtility.SetDirty(bootstrap); EditorUtility.SetDirty(bootstrap.Panel);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (temporary) EditorSceneManager.CloseScene(scene,true);
            Debug.Log("Ranch collection: two prefabs built and connected to Scene_LobbyTest.");
        }

        private static UIPanel_RanchCollection Connect(Transform canvas,bool training)
        {
            string name = training ? "UIPanel_RanchTraining" : "UIPanel_RanchMonsters";
            var existing = canvas.Find(name);
            if (existing != null) return existing.GetComponent<UIPanel_RanchCollection>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(panelRoot+name+".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab,canvas);
            instance.name=name;instance.SetActive(false);
            Undo.RegisterCreatedObjectUndo(instance,"Connect ranch collection UI");
            return instance.GetComponent<UIPanel_RanchCollection>();
        }

        private static Data_DemoArt ImportArt()
        {
            Directory.CreateDirectory("Assets/Resources/UI/RanchCollection");
            AssetDatabase.Refresh();
            var data = AssetDatabase.LoadAssetAtPath<Data_DemoArt>(resourcePath);
            if (data == null) {data=ScriptableObject.CreateInstance<Data_DemoArt>();AssetDatabase.CreateAsset(data,resourcePath);}
            data.BodyFont=DemoUI.Art.BodyFont;data.TitleFont=DemoUI.Art.TitleFont;
            var list=new List<Data_DemoSprite>();
            foreach(var path in Directory.GetFiles(artRoot,"*.png").OrderBy(p=>p))
            {
                string assetPath=path.Replace('\\','/'),key=Path.GetFileNameWithoutExtension(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(assetPath);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
                importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Bilinear;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                int border=0;
                if(key.EndsWith("_bg") || key.EndsWith("_frame") || key.EndsWith("_selected"))border=18;
                if(key=="screen_frame")border=70;
                if(key.StartsWith("scroll_")&&!key.StartsWith("scroll_button"))border=14;
                if(key.StartsWith("gauge_"))border=10;
                if(key.StartsWith("gauge_fill"))border=0;
                if(key=="preview_frame")border=32;
                if(key.StartsWith("portrait") || key.StartsWith("ticket"))border=30;
                if(key.StartsWith("nameplate"))border=38;
                if(key.StartsWith("gauge_")&&!key.StartsWith("gauge_fill"))border=48;
                if(key=="title_bg"||key=="title_frame") importer.spriteBorder=new Vector4(55,28,55,28);
                else importer.spriteBorder=new Vector4(border,border,border,border);
                importer.SaveAndReimport();
                list.Add(new Data_DemoSprite{Key=key,Sprite=AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)});
            }
            foreach(var monster in Mgr_Table.Demo.Monsters)
                foreach(string suffix in new[]{"_face","_body"})
                    if(!list.Any(e=>e.Key==monster.Id+suffix))
                        list.Add(new Data_DemoSprite{Key=monster.Id+suffix,Sprite=DemoUI.Art.Get(monster.Id+suffix)});
            AddReferenceArt(list);
            data.BodyFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/RaceFantasy/Fonts/NanumGothic-Bold.ttf");
            data.TitleFont=AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/RaceFantasy/Fonts/NanumGothic-ExtraBold.ttf");
            data.Images=list.ToArray();EditorUtility.SetDirty(data);return data;
        }

        private static void BuildPrefab(bool training)
        {
            string path=panelRoot+(training?"UIPanel_RanchTraining":"UIPanel_RanchMonsters")+".prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)
            {
                var existing=PrefabUtility.LoadPrefabContents(path);
                try { ApplyPresentation(existing.GetComponent<UIPanel_RanchCollection>()); PrefabUtility.SaveAsPrefabAsset(existing,path); }
                finally { PrefabUtility.UnloadPrefabContents(existing); }
                return;
            }
            var go=new GameObject(training?"UIPanel_RanchTraining":"UIPanel_RanchMonsters",typeof(RectTransform));
            go.SetActive(false);
            var root=(RectTransform)go.transform;root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            UIPanel_RanchCollection panel=training?(UIPanel_RanchCollection)go.AddComponent<UIPanel_RanchTraining>():go.AddComponent<UIPanel_RanchMonsters>();panel.TrainingMode=training;
            var cover=DemoUI.Fill(root,"bg_fullscreen");var image=cover.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=art.Get("screen_bg");image.raycastTarget=true;
            panel.SafeArea=DemoUI.Fill(root,"obj_safe_area");
            panel.Layout=DemoUI.Rect(panel.SafeArea,"group_layout",0,0,1280,720);
            var layout=panel.Layout;
            Pic(layout,"img_outer_frame","screen_frame",0,0,1280,720);
            Pair(layout,"group_title","title",430,12,420,78);
            Label(layout,"txt_title",training?"스피드 훈련":"몬스터 수첩",462,25,356,46,34,paper,TextAnchor.MiddleCenter,true);
            panel.BackButton=Button(layout,"btn_back","back_button",39,25,216,59);
            Pic(panel.BackButton.transform,"img_arrow","icon_back",16,17,31,25,true);
            Label(panel.BackButton.transform,"txt_label","목장으로",49,9,143,39,24,paper);
            Pic(layout,"img_lantern_left","icon_lantern",396,22,28,50,true);
            Pic(layout,"img_lantern_right","icon_lantern",855,22,28,50,true);
            BuildRoster(panel,layout,training);
            if(training) BuildTraining(panel,layout);else BuildMonsters(panel,layout);
            // Defaults are visible when inspecting the prefab without entering Play mode.
            PreviewDefaults(panel);
            ApplyPresentation(panel);
            PrefabUtility.SaveAsPrefabAsset(go,panelRoot+go.name+".prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        private static void BuildRoster(UIPanel_RanchCollection panel,Transform parent,bool horizontal)
        {
            var root=Rect(parent,"group_roster",horizontal?53:43,horizontal?104:106,horizontal?780:222,horizontal?125:554);
            var scrollRoot=Rect(root,"scroll_monsters",horizontal?32:0,0,horizontal?697:188,horizontal?94:530);
            var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();
            var viewport=Rect(scrollRoot,"obj_viewport",0,0,scrollRoot.sizeDelta.x,scrollRoot.sizeDelta.y);
            var hit=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;hit.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect(viewport,"group_cards",0,0,horizontal?770:188,horizontal?94:590);
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=horizontal;scroll.vertical=!horizontal;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;scroll.inertia=true;
            panel.Roster=scroll;panel.RosterButtons=new Button[3];panel.SelectionFrames=new UnityEngine.UI.Image[3];panel.RosterNames=new Text[3];panel.RosterLevels=new Text[3];
            for(int i=0;i<3;i++)
            {
                var def=Mgr_Table.Demo.Monsters[i];
                var button=Button(content,"btn_monster_"+i,horizontal?"ticket":"portrait",horizontal?i*258:0,horizontal?0:i*199,horizontal?247:188,horizontal?94:188);
                panel.RosterButtons[i]=button;
                var face=Pic(button.transform,"img_face",null,horizontal?9:25,horizontal?10:9,horizontal?78:137,horizontal?74:128,true);face.sprite=art.Get(def.Id+"_face");
                panel.RosterNames[i]=Label(button.transform,"txt_name",def.Name,horizontal?92:6,horizontal?16:129,horizontal?148:176,27,horizontal?19:20,ink);
                panel.RosterLevels[i]=Label(button.transform,"txt_level","Lv.4",horizontal?98:12,horizontal?49:154,horizontal?130:164,24,18,ink);
                panel.SelectionFrames[i]=Pic(button.transform,"img_selected",horizontal?"ticket_selected":"portrait_selected",0,0,horizontal?247:188,horizontal?94:188);
                panel.SelectionFrames[i].gameObject.SetActive(i==0);
            }
            if(horizontal)
            {
                panel.PreviousButton=Arrow(root,"btn_previous","left",0,28,31);
                panel.NextButton=Arrow(root,"btn_next","right",741,28,31);
                var bar=MakeScrollbar(root,"scrollbar_horizontal",true,33,101,696,21);
                scroll.horizontalScrollbar=bar;scroll.horizontalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            }
            else
            {
                panel.PreviousButton=Arrow(root,"btn_previous","up",195,0,27);
                panel.NextButton=Arrow(root,"btn_next","down",195,527,27);
                var bar=MakeScrollbar(root,"scrollbar_vertical",false,198,33,22,486);
                scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            }
        }

        private static void BuildMonsters(UIPanel_RanchCollection panel,Transform parent)
        {
            var display=Rect(parent,"group_character",286,106,435,439);
            var mask=Rect(display,"obj_clip",10,10,415,419);mask.gameObject.AddComponent<RectMask2D>();
            var backdrop=Pic(mask,"bg_illustration",null,0,0,415,419);backdrop.sprite=DemoUI.Art.Get("portrait_bg");
            panel.Character=Pic(mask,"img_character",null,22,37,372,358,true);
            panel.CharacterSprites=Mgr_Table.Demo.Monsters.Select(m=>art.Get(m.Id+"_body")).ToArray();
            Pic(display,"img_frame","preview_frame",0,0,435,439);
            var stats=Pair(parent,"group_stats","info",737,106,495,267);
            panel.MonsterName=Label(stats,"txt_name","포링  Lv.4",23,12,446,44,29,ink,TextAnchor.MiddleLeft);
            panel.StatFills=new UnityEngine.UI.Image[5];panel.StatValues=new Text[5];
            string[] labels={"스피드","체력","근력","지력","정신력"};string[] keys={"speed","stamina","strength","intelligence","spirit"};
            for(int i=0;i<5;i++)
            {
                float y=67+i*36;
                Pic(stats,"img_stat_"+i,"icon_"+keys[i],23,y,29,29,true);
                Label(stats,"txt_stat_"+i,labels[i],61,y,83,30,20,ink,TextAnchor.MiddleLeft);
                Pic(stats,"bg_gauge_"+i,"gauge_bg",151,y+5,251,22);
                var fill=Pic(stats,"img_gauge_"+i,"gauge_fill_"+keys[i],158,y+9,237,14);
                fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillOrigin=0;panel.StatFills[i]=fill;
                Pic(stats,"img_gauge_frame_"+i,"gauge_frame",151,y+5,251,22);
                panel.StatValues[i]=Label(stats,"txt_value_"+i,"240",408,y,67,30,22,ink);
            }
            var story=Pair(parent,"group_story","info",737,384,495,161);
            Pic(story,"img_book","icon_encyclopedia",22,14,34,31,true);
            Label(story,"txt_heading","도감 기록",67,11,210,37,24,ink,TextAnchor.MiddleLeft);
            panel.Story=Label(story,"txt_story","",26,56,443,89,20,ink,TextAnchor.UpperLeft);
            panel.SkillLabels=new Text[3];panel.SkillButtons=new Button[3];
            string[] titles={"고유 스킬","선천 스킬","후천 스킬"};string[] kinds={"unique","innate","passive"};string[] icons={"icon_poring_skill","icon_lock","icon_passive"};
            for(int i=0;i<3;i++)
            {
                var card=Button(parent,"btn_skill_"+i,"skill",286+i*317,558,310,109);
                Pair(card.transform,"group_heading","tab_"+kinds[i],0,0,310,37);
                Label(card.transform,"txt_heading",titles[i],18,2,274,31,23,paper);
                Pic(card.transform,"img_icon",icons[i],13,46,48,48,true);
                panel.SkillLabels[i]=Label(card.transform,"txt_skill","",67,42,230,55,21,ink);
                panel.SkillButtons[i]=card;
            }
            BuildSkillPopup(panel,parent);
        }

        private static void BuildTraining(UIPanel_RanchCollection panel,Transform parent)
        {
            var preview=Rect(parent,"group_training_preview",48,244,784,426);
            var clipRect=Rect(preview,"obj_clip",34,31,716,364);clipRect.gameObject.AddComponent<RectMask2D>();
            var image=Pic(clipRect,"img_training",null,0,0,716,364);
            panel.TrainingFilm=image.gameObject.AddComponent<UIItem_LobbyAnimation>();
            panel.TrainingClips=Mgr_Table.Demo.Monsters.Select(m=>Resources.Load<Data_LobbyAnimation>("UI/Demo/Training/"+m.Id)).ToArray();
            panel.TrainingFilm.ConfigureStates(panel.TrainingClips[0],null);panel.TrainingFilm.SetPlaying(false);
            Pic(preview,"img_frame","preview_frame",0,0,784,426);
            var effects=Pair(parent,"group_growth","info",851,106,381,272);
            Header(effects,"훈련 효과","icon_growth",381);
            panel.GrowthLevel=Label(effects,"txt_level","",21,75,339,44,28,ink);
            Pic(effects,"img_speed","icon_speed",23,139,35,36,true);
            panel.GrowthSpeed=Label(effects,"txt_speed","",67,131,292,52,24,ink);
            Pic(effects,"img_passive","icon_passive",23,208,35,37,true);
            panel.GrowthPassive=Label(effects,"txt_passive","",66,199,293,53,20,new Color(.12f,.38f,.10f));
            var condition=Pair(parent,"group_requirements","info",851,391,381,115);
            Header(condition,"출전 조건","icon_entry",381);
            panel.Requirements=Label(condition,"txt_conditions","",18,55,345,44,23,ink);
            var note=Pair(parent,"group_note","note",851,518,381,60);
            Pic(note,"img_notice","icon_notice",18,17,26,26,true);
            panel.TrainingStatus=Label(note,"txt_note","",49,9,312,42,18,ink);
            panel.TrainButton=Button(parent,"btn_train","train_button",851,591,381,80);
            Pic(panel.TrainButton.transform,"img_speed","icon_speed",38,20,47,40,true);
            panel.TrainLabel=Label(panel.TrainButton.transform,"txt_label","훈련 시작",94,14,243,50,30,Color.white);
            var bar=Rect(preview,"group_progress",28,376,728,20);
            Pic(bar,"bg_gauge","gauge_bg",0,0,728,20);
            panel.TrainingProgress=Pic(bar,"img_fill","gauge_fill_speed",8,5,712,10);
            panel.TrainingProgress.type=UnityEngine.UI.Image.Type.Filled;panel.TrainingProgress.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;panel.TrainingProgress.fillOrigin=0;
            Pic(bar,"img_frame","gauge_frame",0,0,728,20);bar.gameObject.SetActive(false);
        }

        private static void BuildSkillPopup(UIPanel_RanchCollection panel,Transform parent)
        {
            var root=Rect(parent,"popup_skill",0,0,1280,720);panel.SkillPopup=root.gameObject;
            var shade=Pic(root,"bg_dim",null,0,0,1280,720);shade.color=new Color(0,0,0,.64f);shade.raycastTarget=true;
            var content=Pair(root,"group_content","info",290,150,700,420);
            panel.SkillTitle=Label(content,"txt_title","",35,27,630,64,30,ink);
            panel.SkillDescription=Label(content,"txt_body","",45,107,610,200,23,ink,TextAnchor.UpperLeft);
            panel.SkillClose=Button(content,"btn_close","back_button",240,330,220,62);
            Label(panel.SkillClose.transform,"txt_label","돌아가기",15,10,190,40,23,paper);
            root.gameObject.SetActive(false);
        }

        private static void PreviewDefaults(UIPanel_RanchCollection panel)
        {
            // Authoring a prefab must not depend on the developer's saved training progress.
            var table=Mgr_Table.Demo;var monster=table.Monsters[0];
            for(int i=0;i<3;i++) {panel.RosterNames[i].text=table.Monsters[i].Name;panel.RosterLevels[i].text="Lv.4";}
            if(panel.TrainingMode)
            {
                panel.GrowthLevel.text="Lv.4  →  Lv."+table.RequiredLevel;
                panel.GrowthSpeed.text="스피드  "+monster.Stats[0]+"  →  "+(monster.Stats[0]+table.TrainingSpeed);
                panel.GrowthPassive.text="후천 패시브 습득";
                panel.Requirements.text="훈련 없이도 출전 가능";
                panel.TrainingStatus.text="선택한 몬스터만 성장해요";
            }
            else
            {
                panel.MonsterName.text=monster.Name+"  Lv.4";panel.Character.sprite=panel.CharacterSprites[0];panel.Story.text=monster.Story;
                for(int i=0;i<5;i++){panel.StatValues[i].text=monster.Stats[i].ToString();panel.StatFills[i].fillAmount=monster.Stats[i]/400f;}
                panel.SkillLabels[0].text=monster.Skill;panel.SkillLabels[1].text="미보유";panel.SkillLabels[2].text="훈련으로 습득";
            }
        }

        private static void Header(Transform parent,string title,string icon,float width)
        {
            Pair(parent,"group_heading","tab_innate",0,0,width,45);
            Pic(parent,"img_heading",icon,22,8,29,30,true);
            Label(parent,"txt_heading",title,63,6,width-80,33,25,paper,TextAnchor.MiddleLeft);
        }
        private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        private static UnityEngine.UI.Image Pic(Transform parent,string name,string key,float x,float y,float w,float h,bool preserve=false)
        {
            var image=Rect(parent,name,x,y,w,h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite=key==null?null:art.Get(key);image.raycastTarget=false;image.preserveAspect=preserve;
            // The approved parts include painted highlights and inset silhouettes. At this
            // fixed reference layout, Simple preserves them without undersized slice corners.
            image.type=UnityEngine.UI.Image.Type.Simple;
            return image;
        }
        private static RectTransform Pair(Transform parent,string name,string key,float x,float y,float w,float h)
        {
            var r=Rect(parent,name,x,y,w,h);Pic(r,"bg_surface",key+"_bg",0,0,w,h);Pic(r,"img_frame",key+"_frame",0,0,w,h);return r;
        }
        private static Button Button(Transform parent,string name,string key,float x,float y,float w,float h)
        {
            var r=Pair(parent,name,key,x,y,w,h);var b=r.gameObject.AddComponent<Button>();var image=r.Find("bg_surface").GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;b.targetGraphic=image;
            b.navigation=new Navigation{mode=Navigation.Mode.None};return b;
        }
        private static Button Arrow(Transform parent,string name,string direction,float x,float y,float size)
        {
            var image=Pic(parent,name,"scroll_button_"+direction,x,y,size,size,true);image.raycastTarget=true;var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
            button.navigation=new Navigation{mode=Navigation.Mode.None};return button;
        }
        private static Scrollbar MakeScrollbar(Transform parent,string name,bool horizontal,float x,float y,float w,float h)
        {
            var rail=Pic(parent,name,horizontal?"scroll_horizontal_rail":"scroll_vertical_rail",x,y,w,h);rail.raycastTarget=true;
            var area=Rect(rail.transform,"obj_sliding_area",horizontal?8:3,horizontal?3:8,horizontal?w-16:w-6,horizontal?h-6:h-16);
            var handle=Pic(area,"img_handle",horizontal?"scroll_horizontal_thumb":"scroll_vertical_thumb",0,0,area.sizeDelta.x,area.sizeDelta.y);handle.raycastTarget=true;
            handle.rectTransform.anchorMin=Vector2.zero;handle.rectTransform.anchorMax=Vector2.one;
            handle.rectTransform.offsetMin=handle.rectTransform.offsetMax=Vector2.zero;
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;
            bar.direction=horizontal?Scrollbar.Direction.LeftToRight:Scrollbar.Direction.BottomToTop;bar.navigation=new Navigation{mode=Navigation.Mode.None};return bar;
        }
        private static Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size,Color color,TextAnchor align=TextAnchor.MiddleCenter,bool title=false)
        {
            var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=title?art.TitleFont:art.BodyFont;t.fontSize=size;t.text=value;t.color=color;t.alignment=align;
            t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
    }
}
