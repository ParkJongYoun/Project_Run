using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static partial class RanchCollectionBuilder
    {
        private static readonly Color referenceInk = new Color(.24f,.045f,.035f);

        // Re-layout serialized objects in place so existing scene references remain intact.
        private static void ApplyPresentation(UIPanel_RanchCollection panel)
        {
            panel.CollectionArt=art;
            var layout=panel.Layout;
            foreach(var label in panel.GetComponentsInChildren<Text>(true)) {label.font=art.BodyFont;label.fontStyle=FontStyle.Normal;}
            var shade=panel.transform.Find("bg_fullscreen").GetComponent<Image>();
            shade.sprite=null;shade.color=new Color(0,0,0,.48f);shade.raycastTarget=true;
            panel.LayoutScale=.94f;layout.localScale=Vector3.one*panel.LayoutScale;
            var page=EnsurePic(layout,"bg_page",art.Get("ui_paper"),42,72,1196,614);page.type=Image.Type.Sliced;page.transform.SetAsFirstSibling();
            SetRect(layout.Find("img_outer_frame"),16,26,1248,688);
            var titleGroup=layout.Find("group_title");SetRect(titleGroup,450,14,380,84);ResizePair(titleGroup,380,84);
            var titleBg=titleGroup.Find("bg_surface").GetComponent<Image>();titleBg.sprite=art.Get("ui_title");titleBg.type=Image.Type.Sliced;
            titleGroup.Find("img_frame").gameObject.SetActive(false);
            SetRect(layout.Find("txt_title"),494,22,292,62);
            Style(layout.Find("txt_title").GetComponent<Text>(),39,paper,TextAnchor.MiddleCenter,true);
            AddTextShadow(layout.Find("txt_title").GetComponent<Text>(),new Vector2(0,-2));
            EnsurePic(layout,"img_paw_left",art.Get("icon_paw"),459,37,34,35,true);
            EnsurePic(layout,"img_paw_right",art.Get("icon_paw"),787,37,34,35,true);
            EnsurePic(layout,"img_lantern_left",art.Get("title_post_left"),412,10,43,94);
            EnsurePic(layout,"img_lantern_right",art.Get("title_post_right"),827,10,45,94);
            for(int i=0;i<4;i++) EnsurePic(titleGroup,"img_rivet_"+i,art.Get("ui_rivet"),i%2==0?10:362,i<2?8:66,8,8,true);
            SetRect(panel.BackButton.transform,66,29,182,64);ResizePair(panel.BackButton.transform,182,64);
            ApplyReferenceButton(panel.BackButton,"reference_back_bg","reference_BackFrame",182,64,6);
            SetRect(panel.BackButton.transform.Find("img_arrow"),17,16,33,32);
            var backText=panel.BackButton.GetComponentInChildren<Text>();SetRect(backText.transform,52,10,115,44);Style(backText,24,paper,TextAnchor.MiddleCenter,true);
            RepairRoster(panel);
            panel.CharacterSprites=Mgr_Table.Demo.Monsters.Select(m=>art.Get(m.Id+"_body")).ToArray();
            if(panel.TrainingMode) RepairTraining(panel);else RepairMonsterDetails(panel);
            PreviewDefaults(panel);
            if(panel.TrainingMode) {panel.TrainingFilm.gameObject.SetActive(false);panel.TrainingActor.gameObject.SetActive(false);panel.TrainingShadow.gameObject.SetActive(false);panel.TrainingScene.sprite=art.Get("Poring_training");}
            else
            {
                panel.Story.text=UIPanel_RanchCollection.StorySummary(Mgr_Table.Demo.Monsters[0].Story);
                for(int i=0;i<5;i++) panel.StatFills[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,234*Mgr_Table.Demo.Monsters[0].Stats[i]/400f);
            }
        }

        private static void RepairRoster(UIPanel_RanchCollection panel)
        {
            bool horizontal=panel.TrainingMode;int count=panel.RosterButtons.Length;
            var root=panel.Layout.Find("group_roster");SetRect(root,horizontal?73:66,111,horizontal?770:210,horizontal?128:565);
            var roster=panel.Roster;float cw=horizontal?250:166,ch=horizontal?92:182,gap=horizontal?10:9;
            float vw=horizontal?770:166,vh=horizontal?92:564;
            SetRect(roster.transform,0,0,vw,vh);SetRect(roster.viewport,0,0,vw,vh);
            SetRect(roster.content,0,0,horizontal?count*(cw+gap)-gap:cw,horizontal?ch:count*(ch+gap)-gap);
            panel.RosterPortraits=new Image[count];
            for(int i=0;i<count;i++)
            {
                var card=panel.RosterButtons[i].transform;SetRect(card,horizontal?i*(cw+gap):0,horizontal?0:i*(ch+gap),cw,ch);
                ApplyPaper(card,cw,ch);
                var face=card.Find("img_face")??card.Find("obj_portrait/img_face");
                var viewport=card.Find("obj_portrait") as RectTransform;
                if(viewport==null) {viewport=Rect(card,"obj_portrait",0,0,1,1);var maskImage=viewport.gameObject.AddComponent<Image>();maskImage.sprite=art.Get("ui_paper");maskImage.type=Image.Type.Sliced;maskImage.raycastTarget=false;viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;}
                SetRect(viewport,6,6,horizontal?91:154,horizontal?80:170);viewport.SetSiblingIndex(1);
                face.SetParent(viewport,false);var faceImage=face.GetComponent<Image>();faceImage.sprite=art.Get(Mgr_Table.Demo.Monsters[i].Id+"_face");faceImage.preserveAspect=true;
                panel.RosterPortraits[i]=faceImage;
                var scenery=EnsurePic(viewport,"bg_scene",art.Get("village"),0,0,horizontal?91:154,horizontal?91:154);scenery.transform.SetAsFirstSibling();
                SetRect(face,horizontal?-6:-12,horizontal?-4:-5,horizontal?105:178,horizontal?96:160);
                if(!horizontal && i==1)SetRect(face,-16,-7,188,188);
                var oldScene=card.Find("bg_portrait_scene");if(oldScene!=null)oldScene.gameObject.SetActive(false);
                var oldPlate=card.Find("group_nameplate");if(oldPlate!=null)oldPlate.gameObject.SetActive(false);
                var strip=EnsurePic(viewport,"bg_name_strip",art.Get("ui_plate"),0,127,154,43);strip.type=Image.Type.Sliced;strip.gameObject.SetActive(!horizontal);strip.transform.SetAsLastSibling();
                var selected=panel.SelectionFrames[i];selected.sprite=art.Get("ui_selected");selected.type=Image.Type.Sliced;SetRect(selected.transform,0,0,cw,ch);selected.transform.SetAsLastSibling();
                SetRect(panel.RosterNames[i].transform,horizontal?103:7,horizontal?13:135,horizontal?136:152,25);
                SetRect(panel.RosterLevels[i].transform,horizontal?103:7,horizontal?48:158,horizontal?136:152,23);
                Style(panel.RosterNames[i],horizontal?20:19,horizontal?referenceInk:paper,TextAnchor.MiddleCenter,true);
                Style(panel.RosterLevels[i],horizontal?20:18,horizontal?referenceInk:paper,TextAnchor.MiddleCenter,true);
                panel.RosterNames[i].transform.SetAsLastSibling();panel.RosterLevels[i].transform.SetAsLastSibling();
            }
            SetRect(panel.PreviousButton.transform,horizontal?0:173,horizontal?26:0,32,40);
            SetRect(panel.NextButton.transform,horizontal?750:173,horizontal?26:532,32,40);
            panel.PreviousButton.gameObject.SetActive(!horizontal);panel.NextButton.gameObject.SetActive(!horizontal);
            var bar=root.Find(horizontal?"scrollbar_horizontal":"scrollbar_vertical").GetComponent<Scrollbar>();
            panel.RosterScrollbar=bar;roster.horizontalScrollbar=null;roster.verticalScrollbar=null;
            SetRect(bar.transform,horizontal?0:177,horizontal?100:40,horizontal?770:23,horizontal?24:486);
            var rail=bar.GetComponent<Image>();rail.sprite=art.Get("ui_rail");rail.type=Image.Type.Sliced;
            var area=bar.transform.Find("obj_sliding_area") as RectTransform;SetRect(area,4,4,horizontal?762:15,horizontal?16:478);
            var handle=bar.handleRect.GetComponent<Image>();handle.sprite=art.Get("ui_thumb");handle.type=Image.Type.Sliced;
            bar.handleRect.anchorMin=Vector2.zero;bar.handleRect.anchorMax=Vector2.one;bar.handleRect.offsetMin=bar.handleRect.offsetMax=Vector2.zero;
            bar.size=horizontal?.10f:.14f;bar.value=horizontal?0:1;
            roster.horizontalNormalizedPosition=0;roster.verticalNormalizedPosition=1;
        }

        private static void RepairMonsterDetails(UIPanel_RanchCollection panel)
        {
            var layout=panel.Layout;var display=layout.Find("group_character");SetRect(display,298,113,458,433);
            SetRect(display.Find("obj_clip"),4,4,450,425);
            var backdrop=display.Find("obj_clip/bg_illustration").GetComponent<Image>();backdrop.sprite=art.Get("village");backdrop.type=Image.Type.Simple;SetRect(backdrop.transform,0,0,450,425);
            var shadow=EnsurePic(display.Find("obj_clip"),"img_ground_shadow",art.Get("ui_shadow"),69,361,318,45);shadow.transform.SetSiblingIndex(1);
            SetRect(panel.Character.transform,75,149,300,242);panel.Character.preserveAspect=true;
            PreviewFrame(display,458,433);
            var stats=layout.Find("group_stats");SetRect(stats,772,113,447,271);ApplyPaper(stats,447,271);
            panel.HeaderPortrait=EnsurePic(stats,"img_monster",art.Get("Poring_face"),22,13,69,55,true);
            SetRect(panel.MonsterName.transform,112,15,321,48);Style(panel.MonsterName,31,referenceInk,TextAnchor.MiddleLeft,true);
            Rule(stats,"img_divider",21,68,405);
            string[] keys={"speed","stamina","strength","intelligence","spirit"};
            panel.StatFillWidth=234;
            for(int i=0;i<5;i++)
            {
                float y=78+i*36;
                var icon=stats.Find("img_stat_"+i).GetComponent<Image>();icon.sprite=art.Get("icon_"+keys[i]);SetRect(icon.transform,20,y,31,31);
                var label=stats.Find("txt_stat_"+i).GetComponent<Text>();SetRect(label.transform,61,y,66,31);Style(label,19,referenceInk,TextAnchor.MiddleLeft,true);
                var track=stats.Find("bg_gauge_"+i).GetComponent<Image>();track.sprite=art.Get("ui_gauge");track.type=Image.Type.Sliced;SetRect(track.transform,127,y+6,238,22);
                var fill=panel.StatFills[i];fill.sprite=art.Get("ui_fill_"+keys[i]);fill.type=Image.Type.Sliced;SetRect(fill.transform,129,y+8,234,18);
                stats.Find("img_gauge_frame_"+i).gameObject.SetActive(false);
                SetRect(panel.StatValues[i].transform,375,y,57,31);Style(panel.StatValues[i],22,referenceInk,TextAnchor.MiddleCenter,true);
            }
            var story=layout.Find("group_story");SetRect(story,772,394,447,152);ApplyPaper(story,447,152);
            SetRect(story.Find("img_book"),17,10,48,38);SetRect(story.Find("txt_heading"),79,11,326,39);Style(story.Find("txt_heading").GetComponent<Text>(),26,referenceInk,TextAnchor.MiddleLeft,true);
            Rule(story,"img_divider",19,51,409);SetRect(panel.Story.transform,29,72,391,63);Style(panel.Story,20,referenceInk,TextAnchor.UpperLeft);panel.Story.lineSpacing=1.12f;
            for(int i=0;i<3;i++)
            {
                var card=panel.SkillButtons[i].transform;SetRect(card,302+i*311,554,295,124);ApplyPaper(card,295,124);
                Heading(card,295,45,i==0?"ui_red":i==1?"ui_blue":"ui_brown");
                SetRect(card.Find("txt_heading"),12,4,271,36);Style(card.Find("txt_heading").GetComponent<Text>(),25,paper,TextAnchor.MiddleCenter,true);
                SetRect(card.Find("img_icon"),37,52,63,63);SetRect(panel.SkillLabels[i].transform,112,52,170,62);Style(panel.SkillLabels[i],21,referenceInk,TextAnchor.MiddleCenter,true);
            }
            panel.UniqueSkillIcon=panel.SkillButtons[0].transform.Find("img_icon").GetComponent<Image>();
        }

        private static void RepairTraining(UIPanel_RanchCollection panel)
        {
            var layout=panel.Layout;var preview=layout.Find("group_training_preview");SetRect(preview,73,247,770,431);
            var clip=preview.Find("obj_clip");SetRect(clip,4,4,762,423);
            var backdrop=EnsurePic(clip,"bg_training",art.Get("Poring_training"),0,0,762,429);backdrop.transform.SetAsFirstSibling();panel.TrainingScene=backdrop;
            SetRect(panel.TrainingFilm.transform,0,0,762,423);panel.TrainingFilm.ConfigureStates(null,null);panel.TrainingFilm.SetPlaying(false);panel.TrainingFilm.gameObject.SetActive(false);
            if(panel.TrainingClips==null || panel.TrainingClips.Length!=3)panel.TrainingClips=new Data_LobbyAnimation[3];
            panel.TrainingFilm.UseIllustrationLayout=false;
            panel.TrainingPosters=Mgr_Table.Demo.Monsters.Select(m=>art.Get(m.Id+"_training")).ToArray();
            panel.TrainingStartFrames=Mgr_Table.Demo.Monsters.Select(m=>art.Get(m.Id+"_training_start")).ToArray();
            panel.TrainingShadow=EnsurePic(clip,"img_training_shadow",art.Get("ui_shadow"),340,310,212,34);
            panel.TrainingActor=EnsurePic(clip,"img_training_actor",art.Get("Poring_body"),316,77,218,175,true);panel.TrainingActor.gameObject.SetActive(true);
            PreviewFrame(preview,770,431);
            var growth=layout.Find("group_growth");SetRect(growth,863,111,350,243);ApplyPaper(growth,350,243);Heading(growth,350,57,"ui_blue");
            SetRect(growth.Find("img_heading"),21,13,37,34);SetRect(growth.Find("txt_heading"),75,10,252,39);Style(growth.Find("txt_heading").GetComponent<Text>(),27,paper,TextAnchor.MiddleLeft,true);
            SetRect(panel.GrowthLevel.transform,18,70,314,43);Style(panel.GrowthLevel,31,referenceInk,TextAnchor.MiddleCenter,true);
            var wing=growth.Find("img_speed").GetComponent<Image>();wing.sprite=art.Get("icon_speed");SetRect(wing.transform,23,124,37,37);
            SetRect(panel.GrowthSpeed.transform,72,121,261,44);Style(panel.GrowthSpeed,24,referenceInk,TextAnchor.MiddleCenter,true);
            SetRect(growth.Find("img_passive"),23,183,43,45);SetRect(panel.GrowthPassive.transform,80,181,255,47);Style(panel.GrowthPassive,21,new Color(.04f,.38f,.10f),TextAnchor.MiddleLeft,true);
            var condition=layout.Find("group_requirements");SetRect(condition,863,365,350,116);ApplyPaper(condition,350,116);Heading(condition,350,52,"ui_blue");
            SetRect(condition.Find("img_heading"),22,10,34,32);SetRect(condition.Find("txt_heading"),75,8,253,37);Style(condition.Find("txt_heading").GetComponent<Text>(),27,paper,TextAnchor.MiddleLeft,true);
            SetRect(panel.Requirements.transform,15,61,320,42);Style(panel.Requirements,24,referenceInk,TextAnchor.MiddleCenter,true);
            var note=layout.Find("group_note");SetRect(note,863,493,350,54);ApplyPaper(note,350,54);SetRect(note.Find("img_notice"),28,12,31,31);
            SetRect(panel.TrainingStatus.transform,69,10,264,35);Style(panel.TrainingStatus,17,referenceInk,TextAnchor.MiddleLeft);
            SetRect(panel.TrainButton.transform,863,568,350,98);ResizePair(panel.TrainButton.transform,350,98);
            ApplyReferenceButton(panel.TrainButton,"reference_train_bg","reference_TrainFrame",350,98,8);
            var colors=panel.TrainButton.colors;colors.disabledColor=new Color(.65f,.85f,.65f,1);panel.TrainButton.colors=colors;
            var trainWing=panel.TrainButton.transform.Find("img_speed").GetComponent<Image>();trainWing.sprite=art.Get("reference_TrainWing");SetRect(trainWing.transform,55,23,59,55);
            SetRect(panel.TrainLabel.transform,124,17,207,65);Style(panel.TrainLabel,38,Color.white,TextAnchor.MiddleCenter,true);AddTextShadow(panel.TrainLabel,new Vector2(0,-2));
            var outline=panel.TrainLabel.GetComponent<Outline>()??panel.TrainLabel.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.03f,.18f,.04f,1);outline.effectDistance=new Vector2(1,-1);
            SetRect(preview.Find("group_progress"),24,396,722,19);
            foreach(RectTransform child in preview.Find("group_progress")) SetRect(child,0,0,722,19);
            var progressTrack=preview.Find("group_progress/bg_gauge").GetComponent<Image>();progressTrack.sprite=art.Get("ui_gauge");progressTrack.type=Image.Type.Sliced;
            panel.TrainingProgress.sprite=art.Get("ui_fill_speed");SetRect(panel.TrainingProgress.transform,2,2,718,15);
            preview.Find("group_progress/img_frame").gameObject.SetActive(false);
            panel.GrowthLevel.supportRichText=panel.GrowthSpeed.supportRichText=true;
        }

        private static void ApplyPaper(Transform group,float width,float height)
        {
            ResizePair(group,width,height);
            var bg=group.Find("bg_surface").GetComponent<Image>();bg.sprite=art.Get("ui_paper");bg.type=Image.Type.Sliced;
            var frame=group.Find("img_frame").GetComponent<Image>();frame.sprite=art.Get("ui_frame");frame.type=Image.Type.Sliced;
            if(group.name=="group_stats"||group.name=="group_story")
                for(int i=0;i<4;i++) EnsurePic(group,"img_corner_"+i,art.Get("ui_corner"),i%2==0?11:width-21,i<2?10:height-20,10,10,true);
        }
        private static void ApplyReferenceButton(Button button,string background,string border,float width,float height,float inset)
        {
            var surface=button.transform.Find("bg_surface").GetComponent<Image>();surface.sprite=art.Get(background);surface.type=Image.Type.Sliced;surface.color=Color.white;
            SetRect(surface.transform,inset,inset,width-inset*2,height-inset*2);
            var frame=button.transform.Find("img_frame").GetComponent<Image>();frame.sprite=art.Get(border);frame.type=Image.Type.Simple;frame.color=Color.white;
            frame.raycastTarget=true;SetRect(frame.transform,0,0,width,height);
        }
        private static void ResizePair(Transform group,float width,float height)
        {SetRect(group.Find("bg_surface"),0,0,width,height);SetRect(group.Find("img_frame"),0,0,width,height);}
        private static void Heading(Transform card,float width,float height,string key)
        {
            var heading=card.Find("group_heading");SetRect(heading,3,3,width-6,height);ResizePair(heading,width-6,height);
            var bg=heading.Find("bg_surface").GetComponent<Image>();bg.sprite=art.Get(key);bg.type=Image.Type.Sliced;heading.Find("img_frame").gameObject.SetActive(false);
        }
        private static void PreviewFrame(Transform group,float width,float height)
        {var image=group.Find("img_frame").GetComponent<Image>();image.sprite=art.Get("ui_preview");image.type=Image.Type.Sliced;SetRect(image.transform,0,0,width,height);}
        private static void Rule(Transform group,string name,float x,float y,float width)
        {var image=EnsurePic(group,name,null,x,y,width,1);image.color=new Color(.75f,.57f,.36f,.6f);}
        private static void Style(Text label,int size,Color color,TextAnchor alignment,bool bold=false)
        {label.font=bold?art.TitleFont:art.BodyFont;label.fontSize=size;label.color=color;label.alignment=alignment;label.fontStyle=FontStyle.Normal;label.resizeTextForBestFit=false;}
        private static void AddTextShadow(Text label,Vector2 offset)
        {var shadow=label.GetComponent<Shadow>()??label.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.19f,.055f,.01f,.85f);shadow.effectDistance=offset;}
        private static Image EnsurePic(Transform parent,string name,Sprite sprite,float x,float y,float width,float height,bool preserve=false)
        {
            var existing=parent.Find(name);var image=existing!=null?existing.GetComponent<Image>():Pic(parent,name,null,x,y,width,height,preserve);
            image.sprite=sprite;image.color=Color.white;image.preserveAspect=preserve;image.raycastTarget=false;SetRect(image.transform,x,y,width,height);return image;
        }
        private static void SetRect(Transform transform,float x,float y,float width,float height)
        {var rect=(RectTransform)transform;rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    }
}
