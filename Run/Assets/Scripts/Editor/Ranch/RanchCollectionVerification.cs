using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class RanchCollectionVerification
    {
        private const string folder="DataBundles/UI/목장/시설목록_몬스터삽화_훈련개선_20261020/작업기록/Unity검증";
        private static readonly StringBuilder report=new StringBuilder();
        public static string Status {get;private set;}
        public static void Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter the lobby Play mode first.");
            Directory.CreateDirectory(folder);report.Clear();Status="RUNNING";
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).Focus();
            Mgr_Game.Instance.StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            string previous=JsonUtility.ToJson(Mgr_User.Data);
            var ranch=UnityEngine.Object.FindFirstObjectByType<UIPanel_Ranch>();
            var monsters=UnityEngine.Object.FindObjectsByType<UIPanel_RanchMonsters>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
            var training=UnityEngine.Object.FindObjectsByType<UIPanel_RanchTraining>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
            var buttons=ranch.GetComponentsInChildren<Button>(true);
            var showMonsters=buttons.Single(b=>b.name=="btn_monsters");var showTraining=buttons.Single(b=>b.name=="btn_training");
            try
            {
                Mgr_User.Data.Monsters=new[]{new Data_MonsterProgress(),new Data_MonsterProgress(),new Data_MonsterProgress()};
                Mgr_User.Data.SelectedMonster=-1;
                ranch.Guide.Clear();
                ranch.Placement.SetBuildMode(true);yield return null;yield return new WaitForEndOfFrame();
                Check(ranch.Drawer.sizeDelta==new Vector2(1200,126),"시설 목록 로비 공통 목재 받침 적용");
                Check(ranch.Drawer.Find("group_catalog/group_cards").childCount==6,"시설 목록 6칸 표시");
                Capture("시설목록_첫페이지.png");
                ranch.Drawer.GetComponentsInChildren<Button>().Single(b=>b.name=="btn_next").onClick.Invoke();
                yield return null;yield return new WaitForEndOfFrame();Capture("시설목록_둘째페이지.png");
                Check(ranch.Drawer.Find("group_catalog/group_cards").childCount==6,"시설 목록 다음 페이지");
                ranch.Drawer.GetComponentsInChildren<Button>().Single(b=>b.name=="btn_previous").onClick.Invoke();
                ranch.Drawer.GetComponentsInChildren<Button>().Single(b=>b.name=="btn_facility_feed_store").onClick.Invoke();
                Check(ranch.Placement.Preview!=null,"시설 카드 클릭으로 배치 시작");
                ranch.Placement.Cancel();ranch.Placement.SetBuildMode(false);
                ranch.CloseCollection();showMonsters.onClick.Invoke();
                yield return null;yield return new WaitForEndOfFrame();
                Check(ranch.Placement.InputBlocked && ranch.GetComponent<CanvasGroup>().alpha==0,"전체화면에서 목장 조작 및 하단 메뉴 차단");
                foreach(var panel in new UIPanel_RanchCollection[]{monsters,training})
                {
                    var shade=panel.transform.Find("bg_fullscreen").GetComponent<Image>();
                    Check(shade.sprite==null && shade.color.r==0 && shade.color.g==0 && shade.color.b==0 && Mathf.Approximately(shade.color.a,.48f),panel.name+" 검은 반투명 배경");
                }
                for(int i=0;i<3;i++)
                {
                    monsters.RosterButtons[i].onClick.Invoke();yield return null;yield return new WaitForEndOfFrame();
                    string id=Mgr_Table.Demo.Monsters[i].Id;
                    Check(monsters.Character.sprite.name==id+"_body" && monsters.RosterPortraits[i].sprite.name==id+"_face",id+" 수첩 전신·얼굴 연결");
                    Check(monsters.MonsterName.text.Contains(Mgr_Table.Demo.Monsters[i].Name) && monsters.StatValues[0].text==Mgr_Table.Demo.Monsters[i].Stats[0].ToString(),id+" 수첩 이름·능력치");
                    Capture("수첩_"+id+"_1280.png");
                    for(int category=0;category<3;category++) {monsters.SkillButtons[category].onClick.Invoke();Check(monsters.SkillPopup.activeSelf,"스킬 정보 열기 "+id+"/"+category);monsters.SkillClose.onClick.Invoke();}
                }
                Check(monsters.PreviousButton==null && monsters.NextButton==null,"수첩 이전 화살표 오브젝트 제거");
                Check(monsters.RosterScrollbar.GetComponent<RectTransform>().rect.height==540,"수첩 스크롤바 상하 잔여 영역까지 확장");
                monsters.SelectMonster(0);
                monsters.RosterScrollbar.value=0;Check(monsters.SelectedId==2,"수첩 스크롤 손잡이 선택");
                monsters.ReturnToRanch();Check(!ranch.Placement.InputBlocked && ranch.GetComponent<CanvasGroup>().alpha==1,"목장으로 돌아가기 복구");
                showTraining.onClick.Invoke();yield return null;
                Check(training.SelectedId==0 && monsters.SelectedId==2 && Mgr_User.Data.SelectedMonster==-1,"수첩·훈련·경기 선택 독립");
                Check(training.PreviousButton==null && training.NextButton==null,"훈련 카드 양옆 화살표 제거");
                training.RosterButtons[1].onClick.Invoke();Check(training.SelectedId==1,"훈련 카드 직접 선택");
                training.RosterScrollbar.value=1;Check(training.SelectedId==2,"훈련 스크롤 손잡이 선택");
                Check(training.TrainButton.transform.Find("img_frame").GetComponent<Image>().sprite.name=="TrainFrame" && training.TrainButton.transform.Find("img_speed").GetComponent<Image>().sprite.name=="TrainWing","시안형 녹색 버튼 테두리·흰 날개");
                training.BeginTraining();
                float startDeadline=Time.realtimeSinceStartup+15;
                while(!training.TrainingVideo.HasStarted && !training.TrainingVideo.HasFailed && Time.realtimeSinceStartup<startDeadline)yield return null;
                Check(training.TrainingVideo.HasStarted && !training.TrainingVideo.HasFailed,"실제 MP4 훈련 영상 재생 연결");
                training.ReturnToRanch();Check(!training.TrainingVideo.HasStarted,"훈련 복귀 시 영상 정지");
                showTraining.onClick.Invoke();
                for(int i=0;i<3;i++)
                {
                    training.RosterButtons[i].onClick.Invoke();yield return null;yield return new WaitForEndOfFrame();
                    string id=Mgr_Table.Demo.Monsters[i].Id;
                    Check(training.TrainingScene.sprite.name==id+"_Display" && !training.TrainingActor.gameObject.activeSelf && !training.TrainingFilm.gameObject.activeSelf,id+" 전용 훈련 장면 연결");
                    Check(training.TrainingStartFrames[i]!=null && training.TrainingStartFrames[i].name==id+"_Start",id+" 전용 Comfy 시작 프레임 연결");
                    Capture("훈련_"+id+"_1280.png");
                    training.TrainButton.onClick.Invoke();
                    startDeadline=Time.realtimeSinceStartup+15;
                    while(!training.TrainingVideo.HasStarted && !training.TrainingVideo.HasFailed && Time.realtimeSinceStartup<startDeadline)yield return null;
                    yield return new WaitForSecondsRealtime(.4f);
                    Check(training.IsTraining && training.TrainingVideo.HasStarted && training.TrainingProgress.fillAmount>0,id+" 전용 영상 재생·훈련 진행도");
                    Check(training.TrainingOverlay.activeInHierarchy && training.TrainingOverlayLabel.text=="훈련중....",id+" 중앙 훈련중 문구");
                    Check(training.TrainingProgress.type==Image.Type.Sliced && Mathf.Abs(training.TrainingProgress.rectTransform.rect.width-training.TrainingProgressWidth*training.TrainingProgress.fillAmount)<1,id+" 9-slice 진행바 너비");
                    var decoder=training.GetComponent<UnityEngine.Video.VideoPlayer>();
                    Check(Math.Abs(decoder.length-2.8)<.08,id+" 원본 7초 대비 2.5배속");
                    yield return new WaitForEndOfFrame();Capture("훈련중_"+id+".png");
                    training.SelectMonster((i+1)%3);Check(training.SelectedId==i && !training.RosterScrollbar.interactable,id+" 훈련 중 대상 변경 차단");
                    training.ReturnToRanch();yield return null;
                    Check(!training.IsTraining && !Mgr_User.Data.Monsters[i].Trained,id+" 중도 복귀 시 성장 미지급");
                    Check(!training.TrainingOverlay.activeSelf,id+" 중도 복귀 시 중앙 문구 제거");
                    showTraining.onClick.Invoke();training.SelectMonster(i);training.TrainButton.onClick.Invoke();
                    float timeout=Time.realtimeSinceStartup+15;
                    while(training.IsTraining && Time.realtimeSinceStartup<timeout)yield return null;
                    var progress=Mgr_User.Data.Monsters[i];
                    Check(!training.IsTraining && progress.Level==5 && progress.SpeedBonus==30 && progress.Experience==100 && progress.EndurancePassive,id+" 훈련 완료 Lv.5·스피드+30·경험치+20·패시브");
                    Check(!training.TrainButton.interactable && Mgr_User.CanEnter(i),id+" 중복 훈련 차단·출전 조건");
                    int experience=progress.Experience;training.BeginTraining();Check(!training.IsTraining && progress.Experience==experience,id+" 재호출 중복 성장 방지");
                    Check(Mgr_User.Data.SelectedMonster==-1,"훈련이 경기 확정 대상을 변경하지 않음");
                    for(int j=i+1;j<3;j++)Check(!Mgr_User.Data.Monsters[j].Trained,"다른 몬스터 성장 미변경 "+j);
                    yield return new WaitForEndOfFrame();Capture("훈련완료_"+id+".png");
                }
                training.ReturnToRanch();showMonsters.onClick.Invoke();monsters.SelectMonster(2);yield return null;
                Check(monsters.StatValues[0].text=="280" && monsters.MonsterName.text.Contains("Lv.5") && monsters.SkillLabels[2].text=="지치지 않는 발","페코페코 성장 결과 수첩 반영");
                Status="PASSED";
            }
            finally
            {
                ranch.CloseCollection();JsonUtility.FromJsonOverwrite(previous,Mgr_User.Data);Mgr_User.Save();
                if(Status=="RUNNING")Status="FAILED";
                report.AppendLine("결과: "+Status);File.WriteAllText(folder+"/동작검증.txt",report.ToString());
            }
        }
        private static void Check(bool success,string description)
        {report.AppendLine((success?"PASS ":"FAIL ")+description);if(!success)throw new InvalidOperationException(description);}
        private static void Capture(string name)
        {var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(folder+"/"+name,image.EncodeToPNG());}finally{UnityEngine.Object.Destroy(image);}}
    }
}
