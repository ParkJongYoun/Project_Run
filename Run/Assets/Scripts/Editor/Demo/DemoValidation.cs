using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    public static class DemoValidation
    {
        private const string Output="DataBundles/검증/심사데모_구현검증_20261018";
        [Serializable] public class Report { public List<RaceResult> Races=new List<RaceResult>();public bool SaveValidation;public bool AssetsValid; }
        [Serializable] public class RaceResult { public string Scene;public string Hero;public int Rank;public float Time;public float SkillAt;public int Overtakes;public bool Deterministic; }
        public static string Simulations()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play first.");
            string active=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;var report=new Report();
            foreach(string scene in new[]{Define_Scene.OvalToon,Define_Scene.Sprite2D})
            {
                EditorSceneManager.OpenScene(Define_Scene.Path(scene));var race=UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
                var original=race.Entrants;int laps=race.LapCount;bool contests=race.EnableLaneContests;float countdown=race.CountdownSeconds;
                for(int id=0;id<3;id++)
                {
                    race.Entrants=original;Sys_DemoRace.ConfigureSetups(race,id);int hero=Mgr_Table.Demo.Monsters[id].RunnerIndex;
                    race.Entrants[hero].StaminaDrainMultiplier=.8f;
                    race.Entrants[hero].BaseSpeed=(race.Course!=null?race.Course.Length:Data_RaceSimulation.Length)/40f*(.91f+(Mgr_Table.Demo.Monsters[id].Stats[0]+Mgr_Table.Demo.TrainingSpeed-240)*.0018f);
                    var first=new Data_RaceSimulation(race.Entrants,race.Course,race.EnableLaneContests,race.LapCount);
                    var second=new Data_RaceSimulation(race.Entrants,race.Course,race.EnableLaneContests,race.LapCount);
                    float skillAt=-1;first.Changed+=e=>{if(e.Kind==E_RaceEvent.Skill&&e.RunnerId==hero)skillAt=e.Time;};
                    first.Begin();second.Begin();int steps=0;
                    while(first.Phase!=E_RacePhase.Result&&steps++<12000)first.Step();
                    // A two-step playback batch must produce byte-for-byte identical results.
                    for(int i=0;i<steps;i+=2){second.Step();if(i+1<steps)second.Step();}
                    bool equal=first.Order.SequenceEqual(second.Order)&&first.Runners.Zip(second.Runners,(a,b)=>a.FinishTime==b.FinishTime&&a.Distance==b.Distance).All(x=>x);
                    report.Races.Add(new RaceResult{Scene=scene,Hero=Mgr_Table.Demo.Monsters[id].Name,Rank=first.Runners[hero].Rank,Time=(float)first.Runners[hero].FinishTime,SkillAt=skillAt,Overtakes=first.Overtakes,Deterministic=equal});
                }
                race.Entrants=original;race.LapCount=laps;race.EnableLaneContests=contests;race.CountdownSeconds=countdown;
            }
            var valid=new Data_UserSave();report.SaveValidation=Mgr_User.IsValid(valid)&&!Mgr_User.IsValid(new Data_UserSave{Version=900})&&!Mgr_User.IsValid(new Data_UserSave{SelectedMonster=8})&&!Mgr_User.IsValid(new Data_UserSave{Monsters=null});
            report.AssetsValid=DemoUI.Art!=null&&DemoUI.Art.Images.All(i=>i.Sprite!=null)&&new[]{"WildRose","Minorous","Matyr"}.All(id=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Lobby/P_Lobby_"+id+".prefab")!=null);
            EditorSceneManager.OpenScene(active);Directory.CreateDirectory(Output);string text=JsonUtility.ToJson(report,true);File.WriteAllText(Output+"/경기계산_검증.json",text);return text;
        }
    }
}
