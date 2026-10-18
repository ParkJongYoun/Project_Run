using UnityEngine;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoRace : UIPanel_Base
    {
        private Sys_Race race;
        private RectTransform safe,comic,result;
        private UnityEngine.UI.Text rank,remaining,commentary,countdown,standings,speedLabel,pauseLabel;
        private UIItem_RaceCommentary commentaryFeed;
        private UnityEngine.UI.Image stamina;
        private RectTransform heroPortrait;
        private UnityEngine.UI.Text comicTitle,comicBeat;
        private UIItem_ComicBurst burst;
        private RectTransform[] cuts;
        private UnityEngine.UI.Image[] comicImages;
        private UnityEngine.UI.Image comicFlash;
        private float comicAge=-1;
        private UIItem_MinorousComic minorousComic;
        private UIItem_DemoHeroComic heroComic;
        private bool cutscenes=true;
        private int generation=-1;
        private string latest="선택한 친구의 첫 레이스를 응원해 주세요";
        private float latestUntil;
        public bool IsComicPlaying=>comicAge>=0;
        public bool CutscenesEnabled {get=>cutscenes;set {cutscenes=value;if(!value)CancelSkillPresentation();}}
        private int Hero=>Mgr_Game.Instance.HeroRunner;
        private Data_DemoMonster Monster=>Mgr_Table.Demo.Monsters[Mgr_User.Data.SelectedMonster];
        public void Configure(Sys_Race target)=>race=target;
        public override void OnOpen()
        {
            if(race==null)return;Build();Mgr_Event.RaceChanged+=Handle;Mgr_Event.Reset+=CancelComic;
        }
        public override void OnClose(){Mgr_Event.RaceChanged-=Handle;Mgr_Event.Reset-=CancelComic;CancelSkillPresentation();}
        private void Build()
        {
            FitRuntimePanel();
            DemoUI.Clear(transform);safe=DemoUI.Fill(transform,"obj_safe_area");
            var header=DemoUI.Image(safe,"bg_header",null,0,315,1216,66);header.color=new Color(.04f,.14f,.12f,.88f);
            DemoUI.Text(header.transform,"txt_cup","프론테라컵",-447,0,280,55,29,DemoUI.Paper,TextAnchor.MiddleLeft,true);
            remaining=DemoUI.Text(header.transform,"txt_distance","",340,0,470,55,26,DemoUI.Paper,TextAnchor.MiddleRight);
            var rankPanel=DemoUI.Image(safe,"bg_standings",null,-509,133,206,275);rankPanel.color=new Color(.04f,.14f,.12f,.78f);
            standings=DemoUI.Text(rankPanel.transform,"txt_standings","",0,0,183,252,21,DemoUI.Paper,TextAnchor.MiddleLeft);
            var footer=DemoUI.Image(safe,"bg_footer",null,0,-296,1216,106);footer.color=new Color(.04f,.14f,.12f,.9f);
            DemoUI.Image(footer.transform,"img_hero",Monster.Id+"_face",-550,0,84,84,true);
            rank=DemoUI.Text(footer.transform,"txt_rank","",-425,17,160,53,32,DemoUI.Paper);
            DemoUI.Text(footer.transform,"txt_name",Monster.Name,-425,-29,190,35,18,DemoUI.Paper);
            commentary=DemoUI.Text(footer.transform,"txt_commentary",latest,7,11,600,65,24,DemoUI.Paper);
            if (FindFirstObjectByType<Sys_RacePresentation>() != null)
                commentaryFeed = UIItem_RaceCommentary.Create(race, commentary);
            var track=DemoUI.Image(footer.transform,"bg_stamina","gauge_bg",2,-34,456,13);
            stamina=DemoUI.Image(footer.transform,"img_stamina","gauge_1",2,-34,456,11);stamina.type=UnityEngine.UI.Image.Type.Filled;stamina.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;
            speedLabel=DemoUI.Button(footer.transform,"btn_speed","1×",374,11,90,52,()=>race.PlaybackSpeed=race.PlaybackSpeed>1?1:2).GetComponentInChildren<UnityEngine.UI.Text>();
            pauseLabel=DemoUI.Button(footer.transform,"btn_pause","정지",478,11,80,52,()=>race.Paused=!race.Paused).GetComponentInChildren<UnityEngine.UI.Text>();
            DemoUI.Button(footer.transform,"btn_skip","결과",559,11,76,52,()=>{CancelComic();race.SkipToResult();});
            DemoUI.Button(safe,"btn_lobby","로비",535,230,136,45,()=>{CancelComic();Mgr_User.Data.RaceInProgress=false;Mgr_User.Save();Mgr_Game.Instance.Go(Define_Scene.Lobby);},false);
            DemoUI.Button(safe,"btn_cutscene","연출 ON",379,230,148,45,()=>{CutscenesEnabled=!CutscenesEnabled;safe.Find("btn_cutscene/txt_label").GetComponent<UnityEngine.UI.Text>().text=cutscenes?"연출 ON":"연출 OFF";},false);
            countdown=DemoUI.Text(safe,"txt_countdown","",0,20,800,150,88,DemoUI.Paper,TextAnchor.MiddleCenter,true);
            countdown.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectDistance=new Vector2(3,-4);
            comic=DemoUI.Fill(safe,"group_comic");comic.gameObject.SetActive(false);
            BuildVideo();
            result=DemoUI.Fill(safe,"group_result");result.gameObject.SetActive(false);
        }
        private void Handle(Data_RaceEvent value)
        {
            if(race==null||race.IsSkipping)return;
            if(value.Kind==E_RaceEvent.Skill&&value.RunnerId==Hero)
            {
                latest=Monster.Skill+"! 고유 능력 자동 발동";latestUntil=(float)race.Simulation.Time+5;
                if(cutscenes)
                {
                    bool second=race.Simulation.Runners[Hero].SkillActivationCount==2;
                    if(second&&skillVideo!=null&&skillVideo.TryPlay())return;
                    PlayComicFallback();
                }
            }
            else if(value.Kind==E_RaceEvent.Overtake&&value.RunnerId==Hero){latest=Monster.Name+"! 앞 대열을 돌파합니다!";latestUntil=value.Time+3;}
        }
        private void LateUpdate()
        {
            if(race==null||race.Simulation==null)return;FitRuntimePanel();DemoUI.SafeArea(safe);
            UpdateVideoControls();
            if(generation!=race.Generation){generation=race.Generation;CancelComic();result.gameObject.SetActive(false);}
            var sim=race.Simulation;var hero=sim.Runners[Hero];
            rank.text=hero.Rank+"위 / "+sim.Runners.Length;
            remaining.text="남은 거리  "+Mathf.CeilToInt(Mathf.Max(0,sim.CourseLength-(float)hero.Distance))+"m";
            speedLabel.text=race.PlaybackSpeed>1?"2×":"1×";
            pauseLabel.text=race.Paused?"재개":"정지";
            stamina.fillAmount=hero.Stamina;
            countdown.text=race.Countdown>0?Mathf.CeilToInt(race.Countdown).ToString():sim.Time<1?"START!":"";
            string ranks="";for(int i=0;i<sim.Order.Length;i++){int id=sim.Order[i];ranks+=(id==Hero?"<color=#FFE99A>":"")+(i+1)+"  "+race.Entrants[id].Name+(id==Hero?" ◀</color>":"")+"\n";}standings.text=ranks;
            if (commentaryFeed == null) commentary.text=race.Paused?"잠시 쉬어가기":(float)sim.Time<latestUntil?latest:hero.SkillUsed?"결승선까지, 한 걸음 더!":sim.Time<8?Monster.Tactic+" 전술로 대열을 잡습니다":hero.BlockedBy>=0?"앞 주자를 피해 빈틈을 찾고 있어요":"힘을 아끼며 기회를 기다립니다";
            if(comicAge>=0&&!race.Paused)
            {
                comicAge+=Mathf.Min(Time.unscaledDeltaTime,.1f);AnimateComic();
                if(comicAge>=(minorousComic!=null?UIItem_MinorousComic.Duration:heroComic!=null?UIItem_DemoHeroComic.Duration:6.4f))CancelComic();
            }
            if(sim.Phase==E_RacePhase.Result&&!result.gameObject.activeSelf)ShowResult();
        }
        private void FitRuntimePanel()
        {
            var canvas=GetComponentInParent<Canvas>();
            if(canvas==null)return;
            var rect=(RectTransform)transform;
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero;
            rect.sizeDelta=new Vector2(Screen.width,Screen.height)/Mathf.Max(.001f,canvas.scaleFactor);
        }
        private void StartComic()
        {
            DemoUI.Clear(comic);comic.gameObject.SetActive(true);comicAge=0;
            minorousComic=null;
            heroComic=null;
            if(Monster.Id=="Minorous")
                minorousComic=UIItem_MinorousComic.Create(comic);
            else if(Monster.Id=="Poring"||Monster.Id=="Eddga")
                heroComic=UIItem_DemoHeroComic.Create(comic,Monster.Id,Monster.Skill);
            if(minorousComic!=null||heroComic!=null)
            {
                var skip=DemoUI.Button(comic,"btn_skip_comic","연출 건너뛰기",-145,31,245,46,CancelComic,false);
                var skipRect=(RectTransform)skip.transform;skipRect.anchorMin=skipRect.anchorMax=new Vector2(1,0);
                Mgr_Sound.PlayAccent(true);
                return;
            }
            Color tint=Monster.Id=="Poring"?new Color(.44f,.12f,.33f):Monster.Id=="Eddga"?new Color(.03f,.12f,.23f):new Color(.27f,.13f,.06f);
            DemoUI.Shade(comic,"bg_comic",tint).raycastTarget=true;
            burst=DemoUI.Fill(comic,"img_speed_lines").gameObject.AddComponent<UIItem_ComicBurst>();burst.color=new Color(1,.87f,.55f,.5f);burst.raycastTarget=false;
            cuts=new RectTransform[3];comicImages=new UnityEngine.UI.Image[3];
            for(int i=0;i<3;i++)
            {
                var strip=DemoUI.Rect(comic,"group_cut_"+i,(i-1)*407,35,380,436);cuts[i]=strip;strip.localRotation=Quaternion.Euler(0,0,-7);
                var border=DemoUI.Shade(strip,"bg_edge",DemoUI.Gold);var area=DemoUI.Rect(strip,"obj_mask",0,0,365,418);
                var maskImage=area.gameObject.AddComponent<UnityEngine.UI.Image>();maskImage.color=new Color(.96f,.93f,.80f);area.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=true;
                var portrait=DemoUI.Image(area,"img_portrait",Monster.Id+"_comic_"+i,0,0,418,418);
                comicImages[i]=portrait;
                var sound=DemoUI.Text(strip,"txt_onomatopoeia",Monster.Id=="Poring"?(i==0?"두근":i==1?"꾹!":"폴짝!"):Monster.Id=="Eddga"?(i==0?"고요":i==1?"파직!":"번쩍!"):(i==0?"후우…":i==1?"꽉!":"콰앙!"),0,-145,350,105,49,DemoUI.Paper,TextAnchor.MiddleCenter,true);
                var soundShadow=sound.gameObject.AddComponent<UnityEngine.UI.Outline>();soundShadow.effectColor=tint;soundShadow.effectDistance=new Vector2(3,-3);
                strip.gameObject.SetActive(false);
            }
            comicFlash=DemoUI.Shade(comic,"img_impact_flash",new Color(1,1,1,0));
            comicTitle=DemoUI.Text(comic,"txt_skill",Monster.Skill,0,286,1100,100,52,DemoUI.Paper,TextAnchor.MiddleCenter,true);
            comicTitle.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectDistance=new Vector2(3,-4);
            comicBeat=DemoUI.Text(comic,"txt_beat","",0,-256,1000,90,32,DemoUI.Paper);
            DemoUI.Button(comic,"btn_skip_comic","연출 건너뛰기",489,-309,245,46,CancelComic,false);
            Mgr_Sound.PlayAccent(true);
        }
        private void AnimateComic()
        {
            if(minorousComic!=null){minorousComic.Evaluate(comicAge);return;}
            if(heroComic!=null){heroComic.Evaluate(comicAge);return;}
            burst.Animate(comicAge);
            for(int i=0;i<cuts.Length;i++)
            {
                bool visible=comicAge>i*1.25f;cuts[i].gameObject.SetActive(visible);
                if(visible){float reveal=Mathf.Clamp01((comicAge-i*1.25f)/.3f);cuts[i].localScale=Vector3.one*Mathf.Lerp(1.2f,1,reveal);cuts[i].anchoredPosition=new Vector2((i-1)*407+Mathf.Sin(comicAge*20+i)*(comicAge>4?4:1.5f),35);comicImages[i].rectTransform.localScale=Vector3.one*(1+Mathf.Clamp01((comicAge-i*1.25f)/5)*.11f);}
            }
            comicImages[2].sprite=DemoUI.Art.Get(Monster.Id+"_comic_"+(comicAge>=4.2f?3:2));
            comicFlash.color=new Color(1,1,1,comicAge>=4.2f&&comicAge<4.5f?.72f*(1-(comicAge-4.2f)/.3f):0);
            string[] beats=Monster.Id=="Poring"?new[]{"겁이 나도…","꾹 참고, 용기를 모아서!","빈틈을 향해 뛰어올라!"}:Monster.Id=="Eddga"?new[]{"고요 속에 감춘 힘","한순간, 어둠을 가르고","맹호의 질주가 시작된다!"}:new[]{"숨을 고르고…","발굽 끝까지 힘을 모아!","길을 열어라! 대열 돌파!"};
            comicBeat.text=beats[Mathf.Min(2,(int)(comicAge/2))];
            if(heroPortrait!=null&&Monster.Id=="Poring")heroPortrait.localScale=new Vector3(1+Mathf.Sin(comicAge)*.1f,1-Mathf.Sin(comicAge)*.15f,1);
        }
        public void CancelComic()
        {
            comicAge=-1;if(comic!=null)comic.gameObject.SetActive(false);if(race!=null)race.ReleasePresentationHold(this);
        }
        public void PlayComicFallback()
        {
            if(cutscenes&&isActiveAndEnabled&&!race.IsSkipping&&race.Simulation.Phase!=E_RacePhase.Result
                &&race.AcquirePresentationHold(this))StartComic();
        }
        private void ShowResult()
        {
            CancelComic();DemoUI.Clear(result);result.gameObject.SetActive(true);
            DemoUI.Shade(result,"bg_result_dim",new Color(.03f,.10f,.08f,.78f)).raycastTarget=true;
            var panel=DemoUI.Panel(result,"group_result_card",1060,580);var hero=race.Simulation.Runners[Hero];
            DemoUI.Text(panel,"txt_title",hero.Rank==1?"프론테라컵 우승!":"프론테라컵 완주!",0,224,940,75,45,DemoUI.Green,TextAnchor.MiddleCenter,true);
            DemoUI.Image(panel,"img_winner",Monster.Id+"_body",-316,-5,305,350,true);
            DemoUI.Text(panel,"txt_name",Monster.Name,164,123,550,65,38);
            DemoUI.Text(panel,"txt_summary",hero.Rank+"위  ·  "+hero.FinishTime.ToString("0.00")+"초\nLv.5  ·  스피드 "+(Monster.Stats[0]+Mgr_User.Data.Monsters[Mgr_User.Data.SelectedMonster].SpeedBonus)+"\n전술  "+Monster.Tactic+"\n고유  "+Monster.Skill+"\n후천  지치지 않는 발",164,-26,560,224,25,DemoUI.Ink,TextAnchor.MiddleLeft);
            DemoUI.Button(panel,"btn_new_session","처음부터 다시 체험",-197,-226,390,64,()=>Mgr_Game.Instance.Replay());
            DemoUI.Button(panel,"btn_end_demo","데모 종료",248,-226,310,64,()=>Mgr_Game.Instance.Go(Define_Scene.Title),false);
        }
    }
}
