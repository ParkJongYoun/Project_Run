using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    public static class RaceBroadcastValidation
    {
        [Serializable] private sealed class Summary
        {
            public List<Sample> Monsters=new List<Sample>();
            public bool AllSkillCounts=true;
            public bool Deterministic=true;
            public bool CosmeticOnly=true;
            public float EarliestPlayerUnique=1;
            public bool Trained;
        }
        [Serializable] private sealed class Sample
        {
            public string Name;
            public int Races,Wins,Assisted,AssistedWins,LateComebacks;
            public float AverageWinningPassProgress;
        }
        public static string Run(int count=36,bool trained=false)
        {
            var report=new Summary{Trained=trained};
            string savedUser=JsonUtility.ToJson(Mgr_User.Data);
            var scene=EditorSceneManager.OpenPreviewScene(Define_Scene.Path(Define_Scene.OvalToon));
            try
            {
                var race=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Sys_Race>(true)).Single();
                Data_RaceSimulation.StraightExtension=race.StraightExtension;
                Data_RaceSimulation.StartLaunchSpeed=race.StartLaunchSpeed;
                Data_RaceSimulation.StartAcceleration=race.StartAcceleration;
                var table=Table_RaceSkills.Load();
                for(int selected=0;selected<3;selected++)
                {
                    Mgr_User.Data.Monsters[selected].SpeedBonus=trained?30:0;
                    Mgr_User.Data.Monsters[selected].EndurancePassive=trained;
                    Sys_DemoRace.ConfigureSetups(race,selected);
                    var sample=new Sample{Name=table.Runners[selected].Name,Races=count};
                    report.Monsters.Add(sample);
                    for(int seed=0;seed<count;seed++)
                    {
                        var entries=race.Entrants.Select(x=>x.Copy()).ToArray();
                        var random=new System.Random(seed*7919+31);
                        bool assisted=random.NextDouble()<table.ComebackChance;
                        entries[selected].DemoComeback=assisted;if(assisted)sample.Assisted++;
                        for(int i=entries.Length-1;i>0;i--)
                        {int j=random.Next(i+1);float lane=entries[i].Lane;entries[i].Lane=entries[j].Lane;entries[j].Lane=lane;}
                        for(int i=0;i<entries.Length;i++)
                            if(i!=selected)entries[i].SkillProgress=entries[i].RaceSkills.UniqueProgress+(float)random.NextDouble()*.025f;
                        var sim=new Data_RaceSimulation(entries,race.Course,true,1);
                        float winningPass=0;
                        int rankAt80=1;
                        bool sampled=false;
                        sim.Changed+=e=>{
                            if(e.Kind==E_RaceEvent.Skill&&e.RunnerId==selected)
                                report.EarliestPlayerUnique=Mathf.Min(report.EarliestPlayerUnique,(float)(sim.Runners[selected].Distance/sim.CourseLength));
                        };
                        sim.Begin();
                        for(int step=0;step<18000&&sim.Phase!=E_RacePhase.Result;step++)
                        {
                            int oldRank=sim.Runners[selected].Rank;
                            sim.Step();
                            float progress=(float)(sim.Runners[selected].Distance/sim.CourseLength);
                            if(!sampled&&progress>=.8f){rankAt80=sim.Runners[selected].Rank;sampled=true;}
                            if(oldRank>1&&sim.Runners[selected].Rank==1)winningPass=progress;
                        }
                        if(sim.Runners[selected].Rank==1)
                        {
                            sample.Wins++;if(assisted)sample.AssistedWins++;
                            if(rankAt80>1)sample.LateComebacks++;
                            sample.AverageWinningPassProgress+=winningPass;
                        }
                        report.AllSkillCounts&=sim.Phase==E_RacePhase.Result&&sim.Runners.All(x=>x.SkillActivationCount==1&&x.NormalSkillCount==3&&x.RareSkillCount==2);
                        if(seed==0)
                        {
                            var fast=new Data_RaceSimulation(entries,race.Course,true,1);fast.Begin();
                            for(int step=0;step<9000&&fast.Phase!=E_RacePhase.Result;step++){fast.Step();fast.Step();}
                            report.Deterministic&=Equal(sim,fast);
                            var quietEntries=entries.Select(x=>x.Copy()).ToArray();foreach(var entry in quietEntries)entry.RaceSkills=null;
                            var quiet=new Data_RaceSimulation(quietEntries,race.Course,true,1);quiet.Begin();
                            for(int step=0;step<18000&&quiet.Phase!=E_RacePhase.Result;step++)quiet.Step();
                            report.CosmeticOnly&=Equal(sim,quiet);
                        }
                    }
                    sample.AverageWinningPassProgress/=Mathf.Max(1,sample.Wins);
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);JsonUtility.FromJsonOverwrite(savedUser,Mgr_User.Data);}
            var json=JsonUtility.ToJson(report,true);
            Directory.CreateDirectory("DataBundles/검증/경기스킬과HUD_20261020");
            File.WriteAllText("DataBundles/검증/경기스킬과HUD_20261020/"+(trained?"경기계산_훈련완료_검증.json":"경기계산_검증.json"),json);
            if(!report.AllSkillCounts||!report.Deterministic||!report.CosmeticOnly||report.EarliestPlayerUnique<.60f)
                throw new InvalidOperationException(json);
            return json;
        }
        private static bool Equal(Data_RaceSimulation a,Data_RaceSimulation b)=>
            a.Order.SequenceEqual(b.Order)&&a.Runners.Zip(b.Runners,(x,y)=>x.FinishTime==y.FinishTime&&x.SkillActivationCount==y.SkillActivationCount).All(x=>x);
    }
}
