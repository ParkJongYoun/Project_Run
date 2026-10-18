using UnityEngine;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoLobby
    {
        private static readonly string[] statNames={"스피드","체력","근력","지력","정신력"};
        public void ShowTraining() => ShowRoster(RosterPurpose.Training);
        private void ShowTrainingTarget(int id)
        {
            if(training)return;
            ClearPage("training");trainingId=id;
            var monster=Mgr_Table.Demo.Monsters[id];var progress=Mgr_User.Data.Monsters[id];
            var panel=PagePanel("group_training");
            BackButton(panel,"훈련 대상 목록",ShowTraining);
            DemoUI.Text(panel,"txt_title","스피드 훈련",30,224,700,55,32,null,TextAnchor.MiddleCenter,true);
            Portrait(panel,id,-345,0,260,340);
            DemoUI.Text(panel,"txt_name",monster.Name+"  Lv."+progress.Level,-345,-194,320,42,25);
            Stats(panel,id,90,132,420);
            DemoUI.Text(panel,"txt_requirements","프론테라컵 출전 조건\nLv.5 이상  /  스피드 260 이상",295,-118,450,70,22);
            DemoUI.Button(panel,"btn_train",progress.Trained?"출전 준비 완료":"훈련 1회  ·  스피드 +30",215,-221,385,60,()=>BeginTraining(),!progress.Trained).interactable=!progress.Trained;
            DemoUI.Text(panel,"txt_training_note",progress.Trained?"이번 체험의 훈련을 마쳤어요":"선택한 친구만 성장해요",-315,-247,340,32,19,DemoUI.Green);
            SetObjective(progress.Trained?"훈련 완료 · 몬스터 능력 확인":"스피드 훈련 1회 진행");
        }
        private void Portrait(Transform parent,int id,float x,float y,float width,float height)
        {
            DemoUI.Image(parent,"bg_portrait","portrait_bg",x,y,width,height);
            DemoUI.Image(parent,"img_monster",Mgr_Table.Demo.Monsters[id].Id+"_body",x,y,width-25,height-20,true);
            DemoUI.Image(parent,"img_portrait_frame","portrait_frame",x,y,width,height);
        }
        private void Stats(Transform parent,int id,float x,float y,float width)
        {
            var monster=Mgr_Table.Demo.Monsters[id];var progress=Mgr_User.Data.Monsters[id];
            for(int i=0;i<5;i++)
            {
                float row=y-i*43;int value=monster.Stats[i]+(i==0?progress.SpeedBonus:0);
                DemoUI.Text(parent,"txt_stat_"+i,statNames[i],x-width*.5f+38,row,100,34,20,DemoUI.Ink,TextAnchor.MiddleLeft);
                DemoUI.Image(parent,"bg_stat_"+i,"gauge_bg",x+55,row,width-150,19);
                var fill=DemoUI.Image(parent,"img_stat_"+i,"gauge_"+i,x+55,row,width-150,17);fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;fill.fillAmount=value/400f;
                DemoUI.Image(parent,"img_stat_frame_"+i,"gauge_frame",x+55,row,width-150,21);
                DemoUI.Text(parent,"txt_value_"+i,value.ToString(),x+width*.5f+65,row,88,35,21,DemoUI.Green);
            }
        }
        public void BeginTraining()
        {
            int id=trainingId;if(training||page!="training"||id<0||Mgr_User.Data.Monsters[id].Trained)return;
            ClearOverlay();training=true;trainingElapsed=0;
            var dim=DemoUI.Shade(Overlay,"bg_training_dim",new Color(0,0,0,.58f));dim.raycastTarget=true;
            var window=DemoUI.Rect(Overlay,"popup_training",0,12,920,540);
            DemoUI.Image(window,"bg_training","training_bg",0,0,900,506);
            trainingActor=DemoUI.Rect(window,"obj_actor",-170,-50,330,330);
            trainingImage=DemoUI.Image(trainingActor,"img_action",Mgr_Table.Demo.Monsters[id].Id+"_run",0,0,330,330,true);
            var clip=Resources.Load<Data_LobbyAnimation>("UI/Demo/Training/"+Mgr_Table.Demo.Monsters[id].Id);
            if(clip!=null)
            {
                trainingImage.gameObject.SetActive(false);
                var film=DemoUI.Image(window,"img_training_film",null,0,0,900,506);
                film.gameObject.AddComponent<UIItem_LobbyAnimation>().ConfigureStates(clip,null,3,6,0);
            }
            DemoUI.Image(window,"img_training_frame","training_frame",0,0,920,526);
            trainingCaption=DemoUI.Text(window,"txt_training_caption","스피드 훈련",0,216,700,60,30,DemoUI.Paper);
            var shadow=trainingCaption.gameObject.AddComponent<UnityEngine.UI.Shadow>();shadow.effectColor=DemoUI.Green;shadow.effectDistance=new Vector2(2,-2);
            DemoUI.Image(window,"bg_progress","gauge_bg",0,-225,760,18);
            trainingProgress=DemoUI.Image(window,"img_progress","gauge_0",-380,-225,0,14).rectTransform;trainingProgress.pivot=new Vector2(0,.5f);
        }
        private void AnimateTraining()
        {
            trainingElapsed+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            float progress=Mathf.Clamp01(trainingElapsed/Mgr_Table.Demo.TrainingSeconds);
            if(trainingProgress!=null)trainingProgress.sizeDelta=new Vector2(760*progress,14);
            bool jump=progress>.45f&&progress<.74f;
            if(trainingActor!=null)
            {
                float bounce=jump?Mathf.Sin((progress-.45f)/.29f*Mathf.PI)*95:Mathf.Abs(Mathf.Sin(trainingElapsed*9))*16;
                trainingActor.anchoredPosition=new Vector2(Mathf.Lerp(-190,190,progress),-66+bounce);
                trainingActor.localRotation=Quaternion.Euler(0,0,Mathf.Sin(trainingElapsed*9)*2);
                trainingActor.localScale=new Vector3(1+Mathf.Sin(trainingElapsed*14)*.025f,1-Mathf.Sin(trainingElapsed*14)*.035f,1);
                trainingImage.sprite=DemoUI.Art.Get(Mgr_Table.Demo.Monsters[trainingId].Id+(jump?"_jump":"_run"));
            }
            if(trainingCaption!=null)trainingCaption.text=progress<.2f?"준비, 출발!":jump?"장애물을 넘어!":progress>.8f?"마지막 스퍼트!":"리듬에 맞춰 달려요";
            if(progress>=1) {training=false;Mgr_User.Train(trainingId);ShowTrainingResult();}
        }
        private void ShowTrainingResult()
        {
            ClearPage("training_result");var monster=Mgr_Table.Demo.Monsters[trainingId];
            var panel=PagePanel("group_training_result");
            DemoUI.Text(panel,"txt_title","LEVEL UP!",0,216,800,65,45,DemoUI.Green,TextAnchor.MiddleCenter,true);
            Portrait(panel,trainingId,-337,-12,250,320);
            DemoUI.Text(panel,"txt_name",monster.Name+"  Lv.5",140,140,560,60,32);
            DemoUI.Text(panel,"txt_growth","스피드   "+monster.Stats[0]+"  →  "+(monster.Stats[0]+30)+"  <color=#24784C>+30</color>\n경험치   +20     ·     Lv.4 → Lv.5",140,48,580,115,27);
            DemoUI.Text(panel,"txt_unlock","출전 조건 충족",140,-51,550,45,28,DemoUI.Green);
            DemoUI.Text(panel,"txt_passive","후천 스킬 습득  ·  지치지 않는 발\n레이스 전 구간에서 체력 소모 감소",140,-129,600,85,23);
            DemoUI.Button(panel,"btn_result_confirm","훈련 목록으로",180,-226,330,58,ShowTraining);
            SetObjective(monster.Name+" 훈련 완료 · 출전 가능");
        }
        public void ShowDetails(int id)
        {
            if(training)return;ClearPage("details");detailId=id;
            var monster=Mgr_Table.Demo.Monsters[id];var progress=Mgr_User.Data.Monsters[id];
            var panel=PagePanel("group_details");
            BackButton(panel,"몬스터 목록",ShowMonsters);
            DemoUI.Text(panel,"txt_title",monster.Name+"  Lv."+progress.Level,30,229,700,55,32,null,TextAnchor.MiddleCenter,true);
            Portrait(panel,id,-379,8,228,320);
            Stats(panel,id,-64,141,300);
            DemoUI.Text(panel,"txt_story_title","도감 기록",342,151,300,38,24,DemoUI.Green);
            DemoUI.Text(panel,"txt_story",monster.Story,342,55,300,145,21,DemoUI.Ink,TextAnchor.UpperLeft);
            DemoUI.Button(panel,"btn_unique","고유  ·  "+monster.Skill,-341,-196,320,76,()=>ShowSkill(false));
            var empty=DemoUI.Button(panel,"btn_innate","선천  ·  미보유",0,-196,300,76,null,false);empty.interactable=false;
            DemoUI.Button(panel,"btn_passive",progress.EndurancePassive?"후천  ·  지치지 않는 발":"후천  ·  훈련으로 습득",334,-196,320,76,()=>ShowSkill(true),progress.EndurancePassive);
            SetObjective("몬스터 정보 · "+monster.Name);
        }
        private void ShowSkill(bool passive)
        {
            ClearPage(passive?"passive":"unique");var monster=Mgr_Table.Demo.Monsters[detailId];var progress=Mgr_User.Data.Monsters[detailId];
            var panel=PagePanel("group_skill");
            DemoUI.Text(panel,"txt_title",passive?"지치지 않는 발":monster.Skill,0,214,900,65,37,DemoUI.Green,TextAnchor.MiddleCenter,true);
            DemoUI.Image(panel,"img_face",monster.Id+"_face",-350,8,230,230,true);
            DemoUI.Text(panel,"txt_category",passive?"후천 패시브  ·  "+(progress.EndurancePassive?"습득 완료":"미습득"):monster.Name+"의 고유 능력",110,130,650,45,26);
            DemoUI.Text(panel,"txt_description",passive?"적용 방식\n출발부터 상시 적용\n\n인게임 효과\n레이스 전 구간의 체력 소모 20% 감소": "발동 조건\n"+monster.SkillCondition+"\n\n인게임 효과\n"+monster.SkillDescription,110,-4,650,205,24,DemoUI.Ink,TextAnchor.MiddleLeft);
            DemoUI.Text(panel,"txt_note",passive?"다음 성장의 후보: 빠른 출발 · 곡선의 감각 · 마지막 한 발\n위 후보는 이번 체험에서 미습득입니다.":"조건을 확인한 AI가 스스로 사용합니다. 별도 조작은 필요 없어요.",0,-151,970,62,19);
            DemoUI.Button(panel,"btn_skill_back","몬스터 정보로",0,-226,300,56,()=>ShowDetails(detailId));
        }
    }
}
