using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    public static class RaceTacticsValidation
    {
        public const string Output="DataBundles/작업기록/경기스킬분산과_추월AI_20261021";
        [Serializable] private sealed class Report
        {
            public int Races, RareOnce, RareTwice, PecoOutsidePasses, PecoSamples, PecoOutsideSamples;
            public bool Counts=true, Deterministic=true, MinorBoost=true, LaneBounds=true;
            public float SmallestSkillGap=100, SmallestUniqueGap=100, MinimumLane=100, MaximumLane=-100;
            public float[] UniqueMin={1,1,1,1,1,1}, UniqueMax=new float[6];
            public int[] Wins=new int[3], Assisted=new int[3], AssistedWins=new int[3];
            public bool RareYield;
            public List<string> ExampleEvents=new List<string>();
        }
        public static string Run(int count=36,bool trained=false)
        {
            Directory.CreateDirectory(Output);
            string savedUser=JsonUtility.ToJson(Mgr_User.Data);
            var scene=EditorSceneManager.OpenPreviewScene(Define_Scene.Path(Define_Scene.OvalToon));
            var report=new Report();
            try
            {
                var race=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Sys_Race>(true)).Single();
                Data_RaceSimulation.StraightExtension=race.StraightExtension;
                Data_RaceSimulation.StartLaunchSpeed=race.StartLaunchSpeed;
                Data_RaceSimulation.StartAcceleration=race.StartAcceleration;
                for(int selected=0;selected<3;selected++)
                {
                    Mgr_User.Data.Monsters[selected].SpeedBonus=trained?30:0;
                    Mgr_User.Data.Monsters[selected].EndurancePassive=trained;
                    Sys_DemoRace.ConfigureSetups(race,selected);
                    for(int sample=0;sample<count;sample++)
                    {
                        int seed=sample*7919+31;
                        var entries=race.Entrants.Select(x=>x.Copy()).ToArray();
                        var random=new System.Random(seed);
                        for(int i=entries.Length-1;i>0;i--)
                        {int j=random.Next(i+1);float lane=entries[i].Lane;entries[i].Lane=entries[j].Lane;entries[j].Lane=lane;}
                        Sys_DemoRace.ConfigureRaceVariation(entries,selected,seed);
                        var sim=new Data_RaceSimulation(entries,race.Course,true,1);
                        float lastSkill=-100,lastUnique=-100,maxMinor=0;
                        sim.Changed+=e=>{
                            if(e.Kind!=E_RaceEvent.Skill&&e.Kind!=E_RaceEvent.MinorSkill)return;
                            float progress=(float)(sim.Runners[e.RunnerId].Distance/sim.CourseLength);
                            report.SmallestSkillGap=Mathf.Min(report.SmallestSkillGap,e.Time-lastSkill);lastSkill=e.Time;
                            if(e.Kind==E_RaceEvent.Skill)
                            {
                                report.SmallestUniqueGap=Mathf.Min(report.SmallestUniqueGap,e.Time-lastUnique);lastUnique=e.Time;
                                report.UniqueMin[e.RunnerId]=Mathf.Min(report.UniqueMin[e.RunnerId],progress);
                                report.UniqueMax[e.RunnerId]=Mathf.Max(report.UniqueMax[e.RunnerId],progress);
                            }
                            if(sample==0&&selected==2)
                                report.ExampleEvents.Add($"{e.Time:F2}s {entries[e.RunnerId].Name} {e.Rarity} {progress:P1}");
                        };
                        sim.Begin();
                        for(int step=0;step<18000&&sim.Phase!=E_RacePhase.Result;step++)
                        {
                            int rank=sim.Runners[2].Rank;
                            sim.Step();
                            foreach(var runner in sim.Runners)
                            {
                                report.MinimumLane=Mathf.Min(report.MinimumLane,runner.Lane);
                                report.MaximumLane=Mathf.Max(report.MaximumLane,runner.Lane);
                                report.LaneBounds&=runner.Lane>=-6.251f&&runner.Lane<=7.751f;
                                maxMinor=Mathf.Max(maxMinor,sim.MinorSkillBoost(runner.Id)/entries[runner.Id].BaseSpeed);
                            }
                            var peco=sim.Runners[2];
                            if(peco.SkillAge>=0&&peco.SkillAge<entries[2].SurgeDuration&&!peco.Finished)
                            {
                                report.PecoSamples++;
                                if(peco.Lane>3)report.PecoOutsideSamples++;
                                if(peco.Rank<rank&&peco.Lane>3)report.PecoOutsidePasses++;
                            }
                        }
                        report.Races++;
                        if(entries[selected].DemoComeback)report.Assisted[selected]++;
                        if(sim.Runners[selected].Rank==1)
                        {report.Wins[selected]++;if(entries[selected].DemoComeback)report.AssistedWins[selected]++;}
                        report.Counts&=sim.Phase==E_RacePhase.Result&&sim.Runners.All(x=>
                            x.SkillActivationCount==1&&x.NormalSkillCount==2&&x.RareSkillCount>=1&&x.RareSkillCount<=2);
                        report.RareOnce+=sim.Runners.Count(x=>x.RareSkillCount==1);
                        report.RareTwice+=sim.Runners.Count(x=>x.RareSkillCount==2);
                        report.MinorBoost&=maxMinor>.024f&&maxMinor<=.03501f;
                        if(sample<3)
                        {
                            var fast=new Data_RaceSimulation(entries,race.Course,true,1);fast.Begin();
                            while(fast.Phase!=E_RacePhase.Result){fast.Step();fast.Step();}
                            report.Deterministic&=sim.Order.SequenceEqual(fast.Order)&&sim.Runners.Zip(fast.Runners,
                                (a,b)=>a.FinishTime==b.FinishTime&&a.SkillActivationCount==b.SkillActivationCount&&
                                a.NormalSkillCount==b.NormalSkillCount&&a.RareSkillCount==b.RareSkillCount).All(x=>x);
                        }
                    }
                }
                report.RareYield=CheckYield();
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);JsonUtility.FromJsonOverwrite(savedUser,Mgr_User.Data);}
            string json=JsonUtility.ToJson(report,true);
            File.WriteAllText(Output+(trained?"/훈련후_반복검증.json":"/경기계산_반복검증.json"),json);
            if(!report.Counts||!report.Deterministic||!report.MinorBoost||!report.LaneBounds||!report.RareYield||
                report.SmallestSkillGap<1.599f||report.SmallestUniqueGap<4.999f||report.PecoOutsidePasses==0)
                throw new InvalidOperationException(json);
            return json;
        }

        private static bool CheckYield()
        {
            var entries=new[]{
                new Data_RunnerSetup{BaseSpeed=15,Lane=.75f,SurgeTime=999,InnerRailPadding=1.5f},
                new Data_RunnerSetup{BaseSpeed=15,Lane=.75f,SurgeTime=999,InnerRailPadding=1.5f,
                    RaceSkills=new Data_RaceSkillSet{NormalCount=0,RareCountMin=0,RareCountMax=0,RareLaneIntent=E_RaceLaneIntent.Middle}}
            };
            var sim=new Data_RaceSimulation(entries,null,true,1);
            var quiet=new Data_RaceSimulation(entries,null,true,1);
            foreach(var run in new[]{sim,quiet})
            {
                run.Begin();
                // Far straight: no curvature incentive that could masquerade as a yield.
                run.Runners[0].Distance=360;run.Runners[1].Distance=350;
                run.Runners[0].Speed=run.Runners[1].Speed=15;
            }
            sim.Runners[1].RareSkillStarted=0;
            for(int i=0;i<150;i++){sim.Step();quiet.Step();}
            return Mathf.Abs(sim.Runners[0].Lane-.75f)>1 && Mathf.Abs(quiet.Runners[0].Lane-.75f)<.2f;
        }
    }
}
