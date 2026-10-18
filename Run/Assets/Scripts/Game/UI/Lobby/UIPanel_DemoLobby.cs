using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoLobby : UIPanel_Base
    {
        [SerializeField] private RectTransform SafeArea;
        [SerializeField] private RectTransform Body;
        [SerializeField] private RectTransform Overlay;
        [SerializeField] private UIItem_LobbyDissolve Background;
        private UnityEngine.UI.Text objective;
        private string page = "home";
        private int selectedCard = -1;
        private int detailId;
        private int trainingId = -1;
        private RectTransform pageBackground;
        private UnityEngine.UI.Button backgroundButton;
        private RectTransform lobbyNavigation;
        private System.Action afterGuide;
        private UnityEngine.UI.Image targetFrame;
        private bool training;
        private bool tacticSelected;
        private bool guideOpen;
        private bool firstGuide;
        private float trainingElapsed;
        private RectTransform trainingActor;
        private UnityEngine.UI.Image trainingImage;
        private UnityEngine.UI.Text trainingCaption;
        private RectTransform trainingProgress;
        public string CurrentPage => page;
        public bool IsTraining => training;
        public bool IsPresentationReady => Background != null && Background.IsReady;
        public bool HasPreparationFailed => Background == null || Background.HasPreparationFailed;

        public void Build()
        {
            DemoUI.Clear(transform);
            var art = DemoUI.Fill(transform,"group_illustration");
            Background=art.gameObject.AddComponent<UIItem_LobbyDissolve>();
            // The page owns its cover; the animated lobby remains underneath.
            // Keep the cover outside Safe Area so it reaches every screen edge.
            pageBackground=DemoUI.Fill(transform,"group_page_background");
            var paper=DemoUI.Shade(pageBackground,"bg_paper",Color.white);
            paper.sprite=DemoUI.Art.Get("panel_bg");paper.type=UnityEngine.UI.Image.Type.Sliced;paper.raycastTarget=true;
            var frame=DemoUI.Shade(pageBackground,"img_frame",Color.white);
            frame.sprite=DemoUI.Art.Get("panel_frame");frame.type=UnityEngine.UI.Image.Type.Sliced;
            pageBackground.gameObject.SetActive(false);
            SafeArea=DemoUI.Fill(transform,"obj_safe_area");
            Body=DemoUI.Fill(SafeArea,"group_content");
            lobbyNavigation=DemoUI.Fill(SafeArea,"group_lobby_navigation");
            Icon(lobbyNavigation,"btn_lobby","nav_lobby","로비",-551,-290,100,()=>ShowHome());
            Icon(lobbyNavigation,"btn_monsters","nav_monsters","몬스터",-431,-290,100,ShowMonsters);
            Icon(lobbyNavigation,"btn_training","nav_training","훈련",-311,-290,100,()=>ShowTraining());
            Icon(lobbyNavigation,"btn_arena","nav_arena","경기장",475,-293,255,()=>ShowArena());
            var nav=DemoUI.Fill(SafeArea,"group_page_header");
            backgroundButton=Icon(nav,"btn_background","nav_background","",477,318,52,()=>Background.Next());
            Icon(nav,"btn_settings","nav_settings","",565,318,52,ShowSettings);
            DemoUI.Image(nav,"bg_objective","panel_bg",-360,318,470,50);
            DemoUI.Image(nav,"img_objective_frame","panel_frame",-360,318,470,50);
            objective=DemoUI.Text(nav,"txt_objective","첫 레이스 준비",-360,318,435,42,19);
            Overlay=DemoUI.Fill(SafeArea,"group_overlay");
        }
        private UnityEngine.UI.Button Icon(Transform parent,string name,string key,string label,float x,float y,float width, UnityEngine.Events.UnityAction action)
        {
            float height=key=="nav_arena"?92:width;
            var image=DemoUI.Image(parent,name,key,x,y,width,height,true);image.raycastTarget=true;
            var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.onClick.AddListener(action);
            if(!string.IsNullOrEmpty(label)) DemoUI.Text(image.transform,"txt_label",label,key=="nav_arena"?23:0,key=="nav_arena"?0:-width*.30f,key=="nav_arena"?145:92,30,20);
            return button;
        }
        public override void OnOpen()
        {
            // Rebuild callbacks when loading a serialized prefab; UnityEvents are runtime bindings.
            if (Mgr_Game.Instance != null) Mgr_Game.Instance.EnsureDemoSession();
            firstGuide=!Mgr_Guide.HasShownAny;
            Build(); ShowHome(false); Welcome();
        }
        public override void OnClose() { StopAllCoroutines(); training=false; trainingId=-1; }
        private void Welcome()
        {
            string message="프론테라에 온 걸 환영해요!\n먼저 아래의 몬스터 버튼을 눌러 친구들의 정보를 살펴봐요.";
            if(!string.IsNullOrEmpty(Mgr_User.Notice)) message=Mgr_User.Notice+"\n"+message;
            if(Mgr_Guide.HasShown("welcome")) GuideLobbyMenu();
            else GuideOnce("welcome",message,()=>HighlightMenu("btn_monsters"));
        }
        private void Update()
        {
            DemoUI.SafeArea(SafeArea);
            if(targetFrame!=null)targetFrame.color=new Color(1,1,1,.7f+.3f*Mathf.Abs(Mathf.Sin(Time.unscaledTime*2.4f)));
            if(training) AnimateTraining();
        }
        private void ClearPage(string value)
        {
            page=value; DemoUI.Clear(Body); ClearOverlay();
            pageBackground.gameObject.SetActive(value!="home"&&value!="arena");
            lobbyNavigation.gameObject.SetActive(value=="home"||value=="arena");
            lobbyNavigation.Find("btn_arena").gameObject.SetActive(value=="home");
            backgroundButton.gameObject.SetActive(value=="home");
        }
        private void ClearOverlay() { DemoUI.Clear(Overlay); guideOpen=false; afterGuide=null; targetFrame=null; }
        private RectTransform PagePanel(string name) => DemoUI.Rect(Body,name,0,5,1120,554);
        private void BackButton(Transform panel,string label,UnityEngine.Events.UnityAction action)
            => DemoUI.Button(panel,"btn_page_back",label,-458,236,174,44,action,false);
        private void SetObjective(string text) { if(objective!=null) objective.text=text; }
        public void ShowHome(bool guide=true)
        {
            if(training) return;
            ClearPage("home");
            bool ready=false;
            for(int i=0;i<3;i++)ready|=Mgr_User.CanEnter(i);
            SetObjective(ready?"경기장에서 출전할 몬스터를 고르세요":"몬스터 정보 확인 · 훈련으로 성장");
            if(guide)GuideLobbyMenu();
        }
        private void GuideLobbyMenu()
        {
            if(!Mgr_Guide.Enabled)return;
            if(!Mgr_Guide.HasShown("monsters"))HighlightMenu("btn_monsters");
            else if(!Mgr_Guide.HasShown("training_select"))
            {
                if(Mgr_Guide.HasShown("open_training"))HighlightMenu("btn_training");
                else GuideOnce("open_training","친구들의 정보를 확인했나요?\n이번에는 아래의 훈련 버튼을 눌러 성장시킬 대상을 골라 봐요.",()=>HighlightMenu("btn_training"));
            }
        }
        private enum RosterPurpose { Information, Training, Entry }
        public void ShowMonsters() => ShowRoster(RosterPurpose.Information);
        private void ShowEntrants() => ShowRoster(RosterPurpose.Entry);
        private void ShowRoster(RosterPurpose purpose)
        {
            if(training) return;
            bool entry=purpose==RosterPurpose.Entry, train=purpose==RosterPurpose.Training;
            ClearPage(entry?"entry":train?"training_select":"monsters");
            if(entry)selectedCard=-1;
            var panel=PagePanel("group_monsters");
            BackButton(panel,entry?"경기장으로":"로비로",()=>{if(entry)ShowArena();else ShowHome();});
            DemoUI.Text(panel,"txt_title",entry?"프론테라컵 · 출전 선택":train?"훈련할 몬스터":"보유 몬스터",30,227,700,55,32,null,TextAnchor.MiddleCenter,true);
            DemoUI.Text(panel,"txt_subtitle",entry?"Lv.5 이상 · 스피드 260 이상인 몬스터 1마리를 선택하세요":train?"훈련할 친구의 초상을 누르세요":"초상을 누르면 능력치, 도감, 스킬을 볼 수 있어요",0,180,1000,35,19);
            for(int i=0;i<3;i++)
            {
                int id=i;var monster=Mgr_Table.Demo.Monsters[i];var progress=Mgr_User.Data.Monsters[i];float x=(i-1)*335;
                var card=DemoUI.Image(panel,"btn_card_"+i,"card_bg",x,-9,300,325);card.raycastTarget=true;
                var button=card.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=card;
                DemoUI.Image(card.transform,"img_frame","card_frame",0,0,300,325);
                DemoUI.Image(card.transform,"img_portrait",monster.Id+"_face",0,65,155,155,true);
                DemoUI.Text(card.transform,"txt_name",monster.Name,0,-33,280,42,25);
                DemoUI.Text(card.transform,"txt_strength",monster.Strength,0,-70,270,30,18);
                DemoUI.Text(card.transform,"txt_skill",monster.Skill,0,-101,275,33,20,DemoUI.Green);
                DemoUI.Text(card.transform,"txt_level","Lv."+progress.Level+"   "+(Mgr_User.CanEnter(i)?"출전 가능":"훈련 전"),0,-135,270,30,18,Mgr_User.CanEnter(i)?DemoUI.Green:new Color(.55f,.42f,.27f));
                button.interactable=!entry||Mgr_User.CanEnter(i);
                if(!button.interactable) {var dim=DemoUI.Shade(card.transform,"bg_locked",new Color(.15f,.18f,.16f,.28f));DemoUI.Text(dim.transform,"txt_locked","조건 미달",0,0,200,60,25,Color.white);}
                button.onClick.AddListener(()=>{if(entry)SelectEntrant(id);else if(train)ShowTrainingTarget(id);else ShowDetails(id);});
            }
            if(entry)
            {
                DemoUI.Button(panel,"btn_confirm","출전 확정",0,-223,320,56,ConfirmEntrant).interactable=false;
                DemoUI.Button(panel,"btn_entry_training","훈련하러 가기",405,-223,240,56,ShowTraining,false);
            }
            else DemoUI.Text(panel,"txt_roster_note",train?"훈련 1회로 Lv.5 · 스피드 +30 · 후천 패시브 습득":"포링 · 마이너 우로스 · 에드가   /   보유 3마리",0,-223,1000,50,22,DemoUI.Green);
            GuideOnce(page,entry?"출전 조건을 갖춘 친구 중 1마리를 골라 확정해 주세요.\n아직 준비가 안 됐다면 훈련 메뉴를 이용해요.":train?"여기서 훈련할 친구를 직접 고를 수 있어요.\n훈련 한 번으로 레벨과 스피드가 오르고 후천 능력을 배워요.":"보유한 세 친구의 정보를 자유롭게 살펴보세요.\n능력치와 고유·선천·후천 스킬을 확인할 수 있어요.");
            SetObjective(entry?"이번 경기에 출전할 1마리 선택":train?"훈련 대상 선택":"보유 몬스터의 성장과 능력 확인");
        }
        private void SelectEntrant(int id)
        {
            if(!Mgr_User.CanEnter(id))return;
            selectedCard=id;ClearOverlay();
            for(int i=0;i<3;i++)
            {
                var card=Body.Find("group_monsters/btn_card_"+i);if(card==null)continue;
                card.Find("img_frame").GetComponent<UnityEngine.UI.Image>().sprite=DemoUI.Art.Get(i==id?"card_selected":"card_frame");
            }
            Body.Find("group_monsters/btn_confirm").GetComponent<UnityEngine.UI.Button>().interactable=true;
        }
        private void ConfirmEntrant()
        {
            if(!Mgr_User.CanEnter(selectedCard))return;
            Mgr_User.Select(selectedCard);ShowTactics();
        }
        private void GuideOnce(string screen,string message,System.Action completed=null)
        {
            if(Mgr_Guide.TryShow(screen)){Guide(message);afterGuide=completed;}
        }
        private void Guide(string message)
        {
            ClearOverlay();guideOpen=true;
            var dim=DemoUI.Shade(Overlay,"bg_guide_dim",new Color(0,0,0,.58f));dim.raycastTarget=true;
            DemoUI.Image(Overlay,"img_kafra","kafra_guide",-385,-28,300,530,true);
            var box=DemoUI.Image(Overlay,"btn_dialogue","dialog_bg",100,-172,820,205);box.raycastTarget=true;
            DemoUI.Image(box.transform,"img_frame","dialog_frame",0,0,820,205);
            DemoUI.Text(box.transform,"txt_name","카프라",0,62,740,35,24,DemoUI.Green,TextAnchor.MiddleLeft);
            DemoUI.Text(box.transform,"txt_dialogue",message,0,-1,740,100,25,DemoUI.Ink,TextAnchor.MiddleLeft);
            DemoUI.Text(box.transform,"txt_continue","대화창을 눌러 계속",235,-76,270,30,17,new Color(.5f,.46f,.32f));
            box.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(DismissGuide);
            if(firstGuide)
            {
                var skip=DemoUI.Button(box.transform,"btn_skip_all","전부 스킵",285,62,170,42,SkipAllGuides,false);
                skip.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=20;
                firstGuide=false;
            }
        }
        public void SkipAllGuides() { Mgr_Guide.SetEnabled(false); ClearOverlay(); }
        public void DismissGuide() { if(!guideOpen)return;var completed=afterGuide;ClearOverlay();completed?.Invoke(); }
        private void HighlightMenu(string name)
        {
            bool notebook=name=="btn_notebook";
            if(!Mgr_Guide.Enabled||page!=(notebook?"arena":"home"))return;
            ClearOverlay();Canvas.ForceUpdateCanvases();
            var target=(notebook?Body:lobbyNavigation).Find(name) as RectTransform;if(target==null)return;
            var corners=new Vector3[4];target.GetWorldCorners(corners);
            var min=Overlay.InverseTransformPoint(corners[0]);var max=Overlay.InverseTransformPoint(corners[2]);var bounds=Overlay.rect;
            float left=min.x-8,right=max.x+8,bottom=min.y-8,top=max.y+8;
            MenuShade("left",bounds.xMin,bounds.yMin,left,bounds.yMax);
            MenuShade("right",right,bounds.yMin,bounds.xMax,bounds.yMax);
            MenuShade("bottom",left,bounds.yMin,right,bottom);
            MenuShade("top",left,top,right,bounds.yMax);
            var hole=DemoUI.Rect(Overlay,"obj_menu_highlight",(min.x+max.x)*.5f,(min.y+max.y)*.5f,right-left,top-bottom);
            targetFrame=DemoUI.Image(hole,"img_target_frame","card_selected",0,0,right-left,top-bottom);
            float hintY=((top-bottom)*.5f+29)*(notebook?-1:1);
            var hint=DemoUI.Image(hole,"bg_target_hint",null,0,hintY,250,40);hint.color=DemoUI.Green;
            string caption=notebook?"수첩 버튼을 눌러 주세요":name=="btn_monsters"?"몬스터 버튼을 눌러 주세요":"훈련 버튼을 눌러 주세요";
            DemoUI.Text(hint.transform,"txt_target_hint",caption,0,0,242,36,18,DemoUI.Paper);
        }
        private void MenuShade(string name,float left,float bottom,float right,float top)
        {
            if(right<=left||top<=bottom)return;
            var dim=DemoUI.Image(Overlay,"bg_menu_dim_"+name,null,(left+right)*.5f,(bottom+top)*.5f,right-left,top-bottom);
            dim.color=new Color(0,0,0,.58f);dim.raycastTarget=true;
        }
        private void ShowSettings()
        {
            if(training)return;ClearOverlay();var dim=DemoUI.Shade(Overlay,"bg_settings_dim",new Color(0,0,0,.58f));dim.raycastTarget=true;
            var panel=DemoUI.Panel(Overlay,"popup_settings",640,420);
            DemoUI.Text(panel,"txt_title","설정",0,152,500,55,32,null,TextAnchor.MiddleCenter,true);
            DemoUI.Button(panel,"btn_sound","소리  "+(Mgr_User.Data.Volume>0?"켜짐":"꺼짐"),0,65,470,55,()=>{Mgr_User.Data.Volume=Mgr_User.Data.Volume>0?0:.65f;AudioListener.volume=Mgr_User.Data.Volume;Mgr_User.Save();ShowSettings();});
            DemoUI.Button(panel,"btn_guides","카프라 안내  "+(Mgr_Guide.Enabled?"켜짐":"꺼짐"),0,-5,470,55,()=>{Mgr_Guide.SetEnabled(!Mgr_Guide.Enabled);firstGuide=true;ShowSettings();});
            DemoUI.Button(panel,"btn_motion","배경 자동 교체  "+(Mgr_User.Data.MotionEnabled?"켜짐":"꺼짐"),0,-75,470,55,()=>{Mgr_User.Data.MotionEnabled=!Mgr_User.Data.MotionEnabled;Mgr_User.Save();ShowSettings();});
            DemoUI.Button(panel,"btn_close","닫기",0,-157,210,52,ClearOverlay,false);
        }
    }
}
