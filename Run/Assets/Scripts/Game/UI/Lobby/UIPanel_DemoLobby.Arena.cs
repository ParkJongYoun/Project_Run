using UnityEngine;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoLobby
    {
        public void ShowArena()
        {
            if(training)return;ClearPage("arena");
            var image=DemoUI.Image(Body,"bg_arena","arena",0,0,1280,720);
            var fit=image.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=image.sprite.rect.width/image.sprite.rect.height;
            // Share the image's coordinate space, including aspect-ratio fitting and cropping.
            var crowd=DemoUI.Fill(image.transform,"group_exhibition");crowd.gameObject.AddComponent<UIItem_ArenaExhibition>();
            var info=DemoUI.Panel(Body,"group_race_info",430,170);info.anchoredPosition=new Vector2(-370,163);
            DemoUI.Text(info,"txt_cup","프론테라컵",0,48,380,50,31,DemoUI.Green,TextAnchor.MiddleCenter,true);
            DemoUI.Text(info,"txt_course","몬스터 레이스 · 흙과 잔디의 순환 코스\n참가 조건  Lv.5 / 스피드 260",0,-24,390,83,21);
            Icon(Body,"btn_notebook","notebook","",477,318,60,ShowNotebook);
            DemoUI.Button(Body,"btn_select_arena","출전 준비",475,-293,255,65,ShowEntrants);
            SetObjective("경기 정보 확인 · 출전 준비");
            GuideOnce("arena","먼저 오른쪽 위 수첩을 눌러 코스를 살펴봐요.\n확인한 뒤 아래 출전 준비에서 달릴 친구 1마리를 골라 주세요.",()=>HighlightMenu("btn_notebook"));
        }
        public void ShowNotebook()
        {
            ClearOverlay();var dim=DemoUI.Shade(Overlay,"bg_notebook_dim",new Color(0,0,0,.58f));dim.raycastTarget=true;
            var book=DemoUI.Rect(Overlay,"popup_notebook",0,0,1120,594);
            DemoUI.Image(book,"bg_paper","book_bg",0,0,1120,594);DemoUI.Image(book,"img_frame","book_frame",0,0,1120,594);DemoUI.Image(book,"img_binding","book_binding",0,0,19,570);
            DemoUI.Text(book,"txt_heading","모험가의 수첩",-278,237,440,57,33,null,TextAnchor.MiddleCenter,true);
            DemoUI.Image(book,"img_course","arena",-279,82,452,250);
            DemoUI.Image(book,"img_photo_frame","photo_frame",-279,82,460,258);
            DemoUI.Image(book,"bg_memo","memo_bg",-279,-146,460,152);DemoUI.Image(book,"img_memo_frame","memo_frame",-279,-146,460,152);
            DemoUI.Text(book,"txt_memo","먼 곳에서도 흙먼지가 보인다.\n마지막 직선에서 한 번 더 힘을 낼\n여유를 남겨 두는 편이 좋겠다.",-279,-146,410,122,22,DemoUI.Ink,TextAnchor.MiddleLeft);
            BookSection(book,"course","코스의 성격","순환 코스 · 완만한 커브\n최종 직선에서 벌어지는 접전",181);
            BookSection(book,"stats","잘 맞는 능력","스피드: 직선에서의 최고 속도\n근력: 코너를 빠져나오는 재가속",21);
            BookSection(book,"skill","잘 맞는 스킬","체력 유지: 막판 여력 확보\n가속과 돌파: 최종 직선 공략",-139);
            DemoUI.Button(book,"btn_close_notebook","수첩 닫기",0,-259,250,48,ClearOverlay,false);
        }
        private void BookSection(Transform parent,string key,string title,string body,float y)
        {
            DemoUI.Image(parent,"img_"+key,"book_"+key,90,y,55,55,true);
            DemoUI.Image(parent,"bg_"+key,"book_heading_bg",302,y,340,42);DemoUI.Image(parent,"img_frame_"+key,"book_heading_frame",302,y,340,42);
            DemoUI.Text(parent,"txt_heading_"+key,title,302,y,320,40,23,DemoUI.Paper);
            DemoUI.Text(parent,"txt_body_"+key,body,285,y-63,420,83,21,DemoUI.Ink,TextAnchor.MiddleLeft);
        }
        private void ShowTactics()
        {
            ClearPage("tactics");tacticSelected=false;var monster=Mgr_Table.Demo.Monsters[Mgr_User.Data.SelectedMonster];
            var panel=PagePanel("group_tactics");
            BackButton(panel,"출전 다시 선택",ShowEntrants);
            DemoUI.Text(panel,"txt_title",monster.Name+"의 전술",30,218,700,60,32,null,TextAnchor.MiddleCenter,true);
            string[] names={"도주","선행","선입","추입"};
            string[] descriptions={"초반부터 앞서기\n처음부터 속도를 높여\n선두를 유지합니다.","앞 대열 유지\n선두권을 따라가며\n빈틈을 노립니다.","중반부터 가속\n힘을 모았다가\n앞 대열을 추격합니다.","후반까지 힘 비축\n체력을 아껴 두었다\n막판에 추격합니다."};
            for(int i=0;i<4;i++)
            {
                bool recommended=i==monster.TacticIndex;var card=DemoUI.Image(panel,"btn_tactic_"+i,"card_bg",(i-1.5f)*257,-8,238,320);card.raycastTarget=true;
                DemoUI.Image(card.transform,"img_frame",recommended?"card_selected":"card_frame",0,0,238,320);
                DemoUI.Text(card.transform,"txt_name",names[i],0,105,210,60,32,DemoUI.Green);
                DemoUI.Text(card.transform,"txt_description",descriptions[i],0,3,204,130,21);
                DemoUI.Text(card.transform,"txt_status",recommended?"추천 전술":"이번 체험은 정보만 표시",0,-116,218,56,18,recommended?DemoUI.Green:new Color(.58f,.54f,.45f));
                var button=card.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=card;button.interactable=recommended;
                button.onClick.AddListener(()=>{tacticSelected=true;ClearOverlay();Body.Find("group_tactics/btn_tactic_confirm").GetComponent<UnityEngine.UI.Button>().interactable=true;});
            }
            DemoUI.Button(panel,"btn_tactic_confirm","전술 확정 · 출전 확인",0,-224,460,60,()=>{if(tacticSelected)ShowModes();}).interactable=false;
            SetObjective("출전 몬스터 · "+monster.Name+" / 전술 선택");
            GuideOnce("tactics",monster.Strength+"에 강한 "+monster.Name+"에게는\n‘"+monster.Tactic+"’ 전술을 추천해요. 선택 후 확정해 주세요.");
        }
        private void ShowModes()
        {
            ClearPage("modes");var panel=PagePanel("group_modes");
            BackButton(panel,"전술로 돌아가기",ShowTactics);
            DemoUI.Text(panel,"txt_title","카툰 경기장 출전",30,218,700,60,34,null,TextAnchor.MiddleCenter,true);
            DemoUI.Text(panel,"txt_subtitle","포링 · 미노타우르스 · 페코페코 + 무작위 주자 3종",0,161,980,40,21);
            DemoUI.Image(panel,"img_oval_preview","mode_3d",0,0,470,240);DemoUI.Image(panel,"img_oval_frame","photo_frame",0,0,478,248);
            DemoUI.Button(panel,"btn_enter_oval","카툰 경기장 · 레이스 시작",0,-183,470,68,()=>Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon));
            SetObjective("선택한 몬스터와 훈련 결과로 카툰 레이스 시작");
        }
    }
}
