using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ProjectRun.Race.Editor
{
    public static class RaceBroadcastPlaythrough
    {
        private const string folder="DataBundles/검증/경기스킬과HUD_20261020";
        private static IEnumerator<float> sequence;
        private static double next,deadline;
        private static string savedUser;
        private static string reportName="실제플레이_검증.txt";
        private static readonly List<string> notes=new List<string>();
        public static string Status {get;private set;}
        private static Sys_Race Race=>Object.FindFirstObjectByType<Sys_Race>();
        private static UIPanel_DemoRace Panel=>Object.FindFirstObjectByType<UIPanel_DemoRace>();
        private static Sys_DemoSkillVideo Video=>Object.FindFirstObjectByType<Sys_DemoSkillVideo>();

        public static void Run(int first=0,int count=3)
        {
            if(!EditorApplication.isPlaying||sequence!=null)throw new InvalidOperationException("Enter Play first.");
            if(first<0||count<1||first+count>3)throw new ArgumentOutOfRangeException("first/count");
            reportName=count==3?"실제플레이_검증.txt":"실제플레이_최종HUD_검증.txt";
            savedUser=JsonUtility.ToJson(Mgr_User.Data);notes.Clear();Directory.CreateDirectory(folder);
            sequence=Steps(first,count);deadline=EditorApplication.timeSinceStartup+360;next=0;
            EditorApplication.update+=Tick;Write("RUNNING");
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup>deadline)throw new Exception("Play verification timed out/stopped.");
                if(!sequence.MoveNext()){Finish("PASSED");return;}
                next=EditorApplication.timeSinceStartup+sequence.Current;
            }
            catch(Exception e){notes.Add(e.ToString());Finish("FAILED");Debug.LogException(e);}
        }
        private static void Finish(string state)
        {
            EditorApplication.update-=Tick;sequence?.Dispose();sequence=null;
            if(savedUser!=null){JsonUtility.FromJsonOverwrite(savedUser,Mgr_User.Data);Mgr_User.Save();savedUser=null;}
            if(Race!=null)Race.Paused=true;
            Write(state);
        }
        private static void Write(string state){Status=state;File.WriteAllLines(folder+"/"+reportName,new[]{state}.Concat(notes));}
        private static void Check(bool value,string message)
        {if(!value)throw new Exception(message);notes.Add("PASS "+message);Write("RUNNING");}
        private static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.GetFullPath(folder+"/"+name+".png"));
        private static bool Equal(Data_RaceSimulation a,Data_RaceSimulation b)=>
            a.Order.SequenceEqual(b.Order)&&a.Runners.Zip(b.Runners,(x,y)=>x.FinishTime==y.FinishTime&&x.SkillActivationCount==y.SkillActivationCount).All(x=>x);
        private static Data_RaceSimulation Expected(Sys_Race race)
        {
            var expected=new Data_RaceSimulation(race.Entrants,race.Course,race.EnableLaneContests,race.LapCount);
            expected.Begin();for(int i=0;i<18000&&expected.Phase!=E_RacePhase.Result;i++)expected.Step();return expected;
        }
        private static IEnumerator<float> Steps(int first,int count)
        {
            for(int hero=first;hero<first+count;hero++)
            {
                Mgr_User.Data.SelectedMonster=hero;
                Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon);yield return 1;
                while(Race==null||Panel==null||Mgr_Game.Instance.IsTransitioning)yield return .05f;
                var race=Race;var panel=Panel;var video=Video;var safe=panel.transform.Find("obj_safe_area");
                string label=Mgr_Table.Demo.Monsters[hero].Name;
                var expected=Expected(race);
                Check(race.Entrants.Length==6,label+" six runners");
                Check(safe.Find("btn_cutscene")==null,label+" cutscene toggle removed");
                Check(panel.GetComponentsInChildren<Image>(true).First(x=>x.name=="img_hero").sprite==race.Entrants[hero].FacePortrait,label+" correct hero face");
                var standings=safe.Find("group_standings").gameObject;
                if(race.Simulation.Phase==E_RacePhase.Preparing)Check(standings.activeSelf,label+" standings visible before start");
                race.PlaybackSpeed=2;race.Paused=false;
                while(race.Simulation.Time<13)yield return .05f;
                Check(!standings.activeSelf,label+" standings collapse after START");
                var toggle=safe.Find("btn_standings").GetComponent<Button>();
                toggle.onClick.Invoke();Check(standings.activeSelf,label+" standings reopen");
                Capture(label+"_목록펼침");yield return .1f;
                toggle.onClick.Invoke();Check(!standings.activeSelf,label+" standings close again");
                var markers=safe.Find("group_progress/group_markers");
                Check(markers!=null&&Enumerable.Range(1,6).All(i=>markers.Find("marker_"+i).gameObject.activeSelf),label+" all six progress numbers visible");
                Check(Enumerable.Range(1,6).Select(i=>markers.Find("marker_"+i+"/img_dot").GetComponent<Image>().color).Distinct().Count()==6,label+" six distinct marker colors");
                Check(Enumerable.Range(1,6).All(i=>markers.Find("marker_"+i+"/img_dot").GetSiblingIndex()>markers.Find("marker_"+i+"/img_ring").GetSiblingIndex()),label+" color discs visible above rings");
                var feed=safe.Find("bg_footer/group_commentary_lines");
                Check(feed.GetComponent<RectMask2D>()!=null,label+" commentary clipped inside its area");
                var feedRect=(RectTransform)feed;var speed=(RectTransform)safe.Find("bg_footer/btn_speed");
                Check(feedRect.anchoredPosition.x+feedRect.rect.width*.5f<speed.anchoredPosition.x-speed.rect.width*.5f,label+" commentary does not overlap speed");
                bool markerSeen=false;
                for(int wait=0;wait<30;wait++){markerSeen|=safe.Find("img_player_marker").gameObject.activeSelf;if(markerSeen)break;yield return .1f;}
                Check(markerSeen,label+" selected runner marker tracks on screen");
                Capture(label+"_주행HUD");
                while(!video.IsActive&&race.Simulation.Phase!=E_RacePhase.Result)yield return .05f;
                Check(video.IsActive&&race.Simulation.Runners[hero].SkillActivationCount==1,label+" only player unique starts movie");
                while(video.IsActive&&!video.HasFirstFrame)yield return .05f;
                Check(video.HasFirstFrame&&!video.HasFailed,label+" movie decoded first frame");
                double held=race.Simulation.Time;yield return .4f;
                Check(race.Simulation.Time==held,label+" movie holds simulation");
                Capture(label+"_유니크영상");
                if(hero==1){video.Skip();yield return .6f;}
                else while(video.IsActive)yield return .1f;
                Check(!race.PresentationHeld,label+" movie return releases simulation");
                yield return .2f;Capture(label+"_유니크띠");
                while(race.Simulation.Runners[hero].Distance/race.Simulation.CourseLength<.83&&race.Simulation.Phase!=E_RacePhase.Result)yield return .05f;
                Capture(label+"_후반스킬띠");
                while(race.Simulation.Phase!=E_RacePhase.Result)yield return .1f;
                Check(Equal(race.Simulation,expected),label+" actual playback/skip gives identical result");
                Check(race.Simulation.Runners.All(x=>x.SkillActivationCount==1&&x.NormalSkillCount==2&&x.RareSkillCount>=1&&x.RareSkillCount<=2),label+" every runner uses normal 2 rare 1-2 unique 1");
                Check(panel.GetComponentsInChildren<UIItem_RaceSkillRibbon>(true).All(x=>!x.Active),label+" finish clears ribbons");
                race.RestartRace();yield return .1f;
                Check(standings.activeSelf,label+" replay restores expanded list");
                var replayExpected=Expected(race);race.SkipToResult();yield return .1f;
                Check(Equal(race.Simulation,replayExpected),label+" result skip uses same new race inputs");
                Check(!race.PresentationHeld&&!video.IsActive,label+" result skip clears presentation");
            }
        }
    }
}
