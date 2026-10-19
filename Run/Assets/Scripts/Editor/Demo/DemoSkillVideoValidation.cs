using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    public static class DemoSkillVideoValidation
    {
        private const string Folder="DataBundles/아트/필살기영상/인게임연결_작업기록";
        private static IEnumerator<float> sequence;
        private static double next,deadline;
        private static string savedUser;
        private static readonly List<string> notes=new List<string>();
        private static Sys_Race Race=>UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
        private static UIPanel_DemoRace Panel=>UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();
        private static Sys_DemoSkillVideo Video=>UnityEngine.Object.FindFirstObjectByType<Sys_DemoSkillVideo>();

        [Serializable] private sealed class SimulationReport { public List<SimulationResult> Races=new List<SimulationResult>();public bool LegacySingleSkill; }
        [Serializable] private sealed class SimulationResult
        {
            public string Scene,Monster;
            public int Count;
            public float[] ActivationTimes;
            public float FinishTime;
            public bool Deterministic;
        }
        public static string Simulations()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before simulation validation.");
            var report=new SimulationReport();
            foreach(string sceneName in new[]{Define_Scene.OvalToon,Define_Scene.Sprite2D})
            {
                var scene=EditorSceneManager.OpenPreviewScene(Define_Scene.Path(sceneName));
                try
                {
                    var race=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Sys_Race>(true)).Single();
                    var original=race.Entrants;
                    for(int selected=0;selected<3;selected++)
                    {
                        race.Entrants=original;Sys_DemoRace.ConfigureSetups(race,selected);
                        int hero=Mgr_Table.Demo.Monsters[selected].RunnerIndex;
                        race.Entrants[hero].BaseSpeed=(race.Course!=null?race.Course.Length:Data_RaceSimulation.Length)/Sys_DemoRace.RaceSeconds*(.91f+(Mgr_Table.Demo.Monsters[selected].Stats[0]+30-240)*.0018f);
                        race.Entrants[hero].StaminaDrainMultiplier=.8f;
                        var first=new Data_RaceSimulation(race.Entrants,race.Course,true,1);
                        var second=new Data_RaceSimulation(race.Entrants,race.Course,true,1);
                        var times=new List<float>();first.Changed+=e=>{if(e.Kind==E_RaceEvent.Skill&&e.RunnerId==hero)times.Add(e.Time);};
                        first.Begin();second.Begin();
                        for(int step=0;step<12000&&first.Phase!=E_RacePhase.Result;step++)first.Step();
                        for(int batch=0;batch<6000&&second.Phase!=E_RacePhase.Result;batch++){second.Step();second.Step();}
                        bool equal=first.Order.SequenceEqual(second.Order)&&first.Runners.Zip(second.Runners,(a,b)=>a.FinishTime==b.FinishTime&&a.SkillActivationCount==b.SkillActivationCount).All(value=>value);
                        if(times.Count!=2||!equal||times[1]>=first.Runners[hero].FinishTime)throw new InvalidOperationException("Skill count/determinism failed: "+sceneName+" / "+selected);
                        report.Races.Add(new SimulationResult{Scene=sceneName,Monster=Mgr_Table.Demo.Monsters[selected].Id,Count=times.Count,ActivationTimes=times.ToArray(),FinishTime=(float)first.Runners[hero].FinishTime,Deterministic=equal});
                    }
                }
                finally {EditorSceneManager.ClosePreviewScene(scene);}
            }
            var legacy=new Data_RaceSimulation(new[]{new Data_RunnerSetup{SurgeTime=1}});legacy.Begin();
            for(int i=0;i<12000&&legacy.Phase!=E_RacePhase.Result;i++)legacy.Step();
            report.LegacySingleSkill=legacy.Runners[0].SkillActivationCount==1;
            if(!report.LegacySingleSkill)throw new InvalidOperationException("Legacy activation count changed.");
            Directory.CreateDirectory(Folder);string json=JsonUtility.ToJson(report,true);File.WriteAllText(Folder+"/경기계산_검증.json",json);return json;
        }
        public static void RunPlaythrough()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play first.");
            if(sequence!=null)throw new InvalidOperationException("Validation already running.");
            Directory.CreateDirectory(Folder);notes.Clear();
            savedUser=JsonUtility.ToJson(Mgr_User.Data);
            File.WriteAllText(Folder+"/검증전_사용자데이터.json",savedUser);
            sequence=Steps();next=0;deadline=EditorApplication.timeSinceStartup+600;
            EditorApplication.update+=Tick;WriteStatus("RUNNING");
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup>deadline)throw new InvalidOperationException("Play stopped or QA timed out.");
                if(!sequence.MoveNext()){Finish("COMPLETED");return;}
                next=EditorApplication.timeSinceStartup+sequence.Current;
            }
            catch(Exception exception){notes.Add(exception.ToString());Finish("FAILED");Debug.LogException(exception);}
        }
        private static void Finish(string status)
        {
            EditorApplication.update-=Tick;sequence?.Dispose();sequence=null;
            if(savedUser!=null){JsonUtility.FromJsonOverwrite(savedUser,Mgr_User.Data);Mgr_User.Save();savedUser=null;}
            if(EditorApplication.isPlaying&&Mgr_Game.Instance!=null)Mgr_Game.Instance.Go(Define_Scene.Title);
            WriteStatus(status);
        }
        private static void WriteStatus(string state)=>File.WriteAllLines(Folder+"/실제플레이_검증.txt",new[]{state}.Concat(notes));
        private static void Check(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException(message);notes.Add("PASS "+message);WriteStatus("RUNNING");
        }
        private static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+name+".png"));
        private static IEnumerator<float> Steps()
        {
            foreach(string scene in new[]{Define_Scene.Sprite2D,Define_Scene.OvalToon})
            for(int id=0;id<3;id++)
            {
                string label=scene+"_"+Mgr_Table.Demo.Monsters[id].Id;
                Mgr_User.Data.SelectedMonster=id;
                var growth=Mgr_User.Data.Monsters[id];growth.Level=5;growth.SpeedBonus=30;growth.Trained=true;growth.EndurancePassive=true;
                Mgr_Game.Instance.EnterRace(scene);yield return 1;
                while(Race==null||Panel==null||Mgr_Game.Instance.IsTransitioning)yield return .1f;
                var race=Race;var panel=Panel;var video=Video;int hero=Mgr_Game.Instance.HeroRunner;
                Check(video!=null&&video.VideoPath==Sys_DemoSkillVideo.PathFor(Mgr_Table.Demo.Monsters[id].Id,scene==Define_Scene.Sprite2D),label+" correct movie mapping");
                var expected=new Data_RaceSimulation(race.Entrants,race.Course,race.EnableLaneContests,race.LapCount);expected.Begin();
                for(int step=0;step<12000&&expected.Phase!=E_RacePhase.Result;step++)expected.Step();
                race.PlaybackSpeed=2;
                while(!panel.IsComicPlaying&&race.Simulation.Phase!=E_RacePhase.Result)yield return .05f;
                Check(panel.IsComicPlaying&&!video.IsActive&&race.Simulation.Runners[hero].SkillActivationCount==1,label+" first activation comic only");
                double held=race.Simulation.Time;yield return .35f;
                Check(race.Simulation.Time==held,label+" comic holds all runners");
                Capture(label+"_1차_만화");yield return .15f;
                if(id==0&&scene==Define_Scene.Sprite2D){while(panel.IsComicPlaying)yield return .1f;}
                else {GameObject.Find("btn_skip_comic").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return .1f;}
                Check(!panel.IsComicPlaying&&!race.PresentationHeld,label+" comic completion/skip releases hold");
                while(!video.IsActive&&race.Simulation.Phase!=E_RacePhase.Result)yield return .05f;
                Check(video.IsActive&&!panel.IsComicPlaying&&race.Simulation.Runners[hero].SkillActivationCount==2,label+" second activation movie only");
                while(video.IsActive&&!video.HasFirstFrame)yield return .05f;
                Check(video.IsActive&&video.HasFirstFrame,label+" actual decoded first frame");
                held=race.Simulation.Time;yield return .7f;
                Check(race.Simulation.Time==held&&video.MediaTime>.1,label+" movie advances while race holds");
                if(id==0)
                {
                    GameObject.Find("btn_pause_video").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return .2f;
                    double media=video.MediaTime;yield return .35f;
                    Check(race.Paused&&race.Simulation.Time==held&&Math.Abs(media-video.MediaTime)<.09,label+" pause freezes movie and race");
                    GameObject.Find("btn_pause_video").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return .3f;
                }
                yield return 1.4f;Capture(label+"_2차_영상");yield return .1f;
                if(id==1){GameObject.Find("btn_skip_video").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return .4f;}
                else while(video.IsActive)yield return .1f;
                Check(!video.IsActive&&!race.PresentationHeld,label+" movie completion/skip releases hold");
                while(race.Simulation.Phase!=E_RacePhase.Result)yield return .1f;
                Check(race.Simulation.Runners[hero].SkillActivationCount==2&&race.Simulation.Order.SequenceEqual(expected.Order)
                    &&race.Simulation.Runners.Zip(expected.Runners,(a,b)=>a.FinishTime==b.FinishTime).All(value=>value),label+" exactly two skills and deterministic result");
                yield return .2f;
                // Restart resets the activation sequence; result skip must not start a movie.
                race.RestartRace();yield return .1f;
                Check(race.Simulation.Runners[hero].SkillActivationCount==0&&!video.IsActive&&!panel.IsComicPlaying,label+" restart clears presentation");
                race.SkipToResult();yield return .2f;
                Check(race.Simulation.Runners[hero].SkillActivationCount==2&&!race.PresentationHeld&&!video.IsActive&&!panel.IsComicPlaying,label+" result skip suppresses both presentations");
                if(id==2)
                {
                    panel.CutscenesEnabled=false;race.RestartRace();race.PlaybackSpeed=20;yield return .1f;
                    while(race.Simulation.Phase!=E_RacePhase.Result)
                    {
                        Check(!race.PresentationHeld&&!video.IsActive&&!panel.IsComicPlaying,label+" cutscenes off remains live");yield return .4f;
                    }
                    Check(race.Simulation.Runners[hero].SkillActivationCount==2&&race.Simulation.Runners[hero].FinishTime==expected.Runners[hero].FinishTime,label+" cutscenes off preserves skills/result");
                }
            }
        }
    }
}
