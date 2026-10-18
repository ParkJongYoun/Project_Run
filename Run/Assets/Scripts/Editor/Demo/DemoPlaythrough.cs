using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace ProjectRun.Race.Editor
{
    public static class DemoPlaythrough
    {
        private const string Folder="DataBundles/검증/심사데모_구현검증_20261018";
        private static IEnumerator<float> sequence;
        private static double next;
        private static readonly List<string> notes=new List<string>();
        private static UIPanel_DemoLobby Lobby=>UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoLobby>();
        private static Sys_Race Race=>UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play first.");
            Directory.CreateDirectory(Folder);
            notes.Clear();sequence=Steps();next=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            File.WriteAllText(Folder+"/실제플레이_검증.txt","RUNNING");
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play stopped during QA.");
                if(!sequence.MoveNext()){EditorApplication.update-=Tick;File.WriteAllLines(Folder+"/실제플레이_검증.txt",notes);Debug.Log("DEMO_PLAYTHROUGH_OK");return;}
                next=EditorApplication.timeSinceStartup+sequence.Current;
            }
            catch(Exception e){EditorApplication.update-=Tick;notes.Add("FAILED "+e);File.WriteAllLines(Folder+"/실제플레이_검증.txt",notes);Debug.LogException(e);}
        }
        private static void Click(string name)
        {
            if((name=="btn_monsters"||name=="btn_training"||name=="btn_arena")&&GameObject.Find(name)==null)
            {
                for(int guard=0;guard<8&&Lobby.CurrentPage!="home";guard++)
                {
                    Lobby.DismissGuide();
                    if(GameObject.Find("btn_page_back")!=null)Click("btn_page_back");
                    else if(GameObject.Find("btn_skill_back")!=null)Click("btn_skill_back");
                    else if(GameObject.Find("btn_result_confirm")!=null)Click("btn_result_confirm");
                    else Click("btn_lobby");
                }
                Lobby.DismissGuide();
            }
            var root=GameObject.Find(name);if(root==null)throw new InvalidOperationException("Missing button "+name);
            var button=root.GetComponent<Button>();if(button==null||!button.interactable)throw new InvalidOperationException("Disabled button "+name);
            button.onClick.Invoke();
        }
        private static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+name+".png"));
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);notes.Add("PASS "+message);}
        private static IEnumerator<float> Steps()
        {
            for(int id=0;id<3;id++)
            {
                Mgr_Game.Instance.StartDemo();yield return 2;
                while(Lobby==null||Mgr_Game.Instance.IsTransitioning)yield return .5f;
                Lobby.DismissGuide();Click("btn_monsters");yield return .3f;Lobby.DismissGuide();
                Click("btn_card_"+id);yield return .3f;
                Check(Lobby.CurrentPage=="details"&&Mgr_User.Data.SelectedMonster==-1,"Information does not select entrant "+id);
                Click("btn_training");yield return .3f;Lobby.DismissGuide();Click("btn_card_"+id);yield return .3f;Click("btn_train");yield return 1.5f;
                Capture("최종_훈련_"+id);yield return 1;
                while(Lobby.IsTraining)yield return .5f;
                Check(Mgr_User.Data.Monsters[id].Level==5&&Mgr_User.CanEnter(id)&&Mgr_User.Data.Monsters[id].EndurancePassive,"Training growth "+id);
                Check(!Mgr_User.Train(id),"Training duplicate prevented "+id);
                Check(Mgr_User.Data.SelectedMonster==-1,"Training does not select entrant "+id);
                Click("btn_result_confirm");yield return .3f;Lobby.DismissGuide();Click("btn_monsters");yield return .3f;Lobby.DismissGuide();
                Click("btn_card_"+id);yield return .3f;Lobby.DismissGuide();Click("btn_unique");yield return .3f;
                Click("btn_skill_back");yield return .3f;Lobby.DismissGuide();Click("btn_passive");yield return .3f;
                Click("btn_skill_back");yield return .3f;Lobby.DismissGuide();Click("btn_arena");yield return .3f;Lobby.DismissGuide();
                Click("btn_notebook");yield return .3f;if(id==0)Capture("최종_수첩");yield return .5f;
                Click("btn_close_notebook");yield return .3f;Lobby.DismissGuide();Click("btn_select_arena");yield return .3f;Lobby.DismissGuide();
                for(int i=0;i<3;i++)Check(GameObject.Find("btn_card_"+i).GetComponent<Button>().interactable==(i==id),"Entry eligibility "+id+" / "+i);
                Click("btn_card_"+id);yield return .3f;Click("btn_confirm");yield return .3f;Lobby.DismissGuide();
                Click("btn_tactic_"+Mgr_Table.Demo.Monsters[id].TacticIndex);yield return .3f;Click("btn_tactic_confirm");yield return .4f;
                if(id==0)Capture("최종_모드선택");yield return .5f;
                Click(id==2?"btn_enter_2d":"btn_enter_oval");yield return 2;
                while(Race==null||Mgr_Game.Instance.IsTransitioning)yield return .5f;
                yield return 4;Race.PlaybackSpeed=2;Capture("최종_경기_"+id);yield return 1;
                var panel=UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();
                while(!panel.IsComicPlaying&&Race.Simulation.Phase!=E_RacePhase.Result)yield return .15f;
                Check(panel.IsComicPlaying,"Conditional comic starts "+id);
                double holdTime=Race.Simulation.Time;yield return 2.8f;
                Check(Race.Simulation.Time==holdTime,"Comic holds simulation "+id);Capture("최종_만화_"+id);yield return 1;
                while(panel.IsComicPlaying)yield return .25f;
                Race.Paused=true;double pauseTime=Race.Simulation.Time;yield return .4f;
                Check(Race.Simulation.Time==pauseTime,"Pause stable "+id);Race.Paused=false;
                while(Race.Simulation.Phase!=E_RacePhase.Result)yield return .3f;
                int hero=Mgr_Game.Instance.HeroRunner;Check(Race.Simulation.Runners[hero].Rank==1,"Race win "+id);
                yield return 1;Capture("최종_결과_"+id);yield return .5f;
                Mgr_User.Load();Check(Mgr_User.Data.SelectedMonster==id&&Mgr_User.CanEnter(id)&&!Mgr_User.Data.RaceInProgress,"Saved progression reload "+id);
            }
            // Final branch exercises cutscene off and instant result against the same model.
            Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon);yield return 2;
            while(Race==null||Mgr_Game.Instance.IsTransitioning)yield return .5f;
            var racePanel=UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();racePanel.CutscenesEnabled=false;Race.SkipToResult();yield return .5f;
            Check(Race.Simulation.Runners[Mgr_Game.Instance.HeroRunner].Rank==1&&!racePanel.IsComicPlaying,"Cutscene off and skip produce a valid result");
            Mgr_User.NewSession();Mgr_Game.Instance.Go(Define_Scene.Title);yield return 2;
            Check(Mgr_User.Data.SelectedMonster==-1,"Replay starts a fresh session");
            notes.Add("COMPLETED "+DateTime.Now.ToString("O"));
        }
    }
}
