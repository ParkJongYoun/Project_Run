using System;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEngine;

namespace ProjectRun.Editor
{
    public static class SpriteRaceContestValidation
    {
        [Serializable] private sealed class Report
        {
            public bool Passed, LegacyExact, BatchingExact, AllFinished, BoundsValid, MovementContinuous;
            public int ScenarioCount, Passes, BlockedSteps;
            public float MinimumGroundSeparation = float.MaxValue, MaximumLaneStep;
            public int[] FinishOrder;
            public double[] FinishTimes;
        }
        public static string Run()
        {
            var race=UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            var report=new Report { AllFinished=true,BoundsValid=true,MovementContinuous=true,BatchingExact=true };
            var baseline=Finish(race.Entrants,race.Course,true,1);
            report.FinishOrder=baseline.Order;
            report.FinishTimes=baseline.Runners.Select(r=>r.FinishTime).ToArray();
            report.Passes=baseline.Overtakes;
            foreach(int batch in new[]{2,5,11,240})
            {
                var repeated=Finish(race.Entrants,race.Course,true,batch);
                for(int i=0;i<6;i++) report.BatchingExact &= repeated.Runners[i].FinishTime==report.FinishTimes[i];
            }
            // Reconstruct the exact pre-contest scene inputs and compare the saved six finish times.
            var legacy=Copy(race.Entrants); float[] oldSpeeds={12.5f,12.25f,12.4f,12.2f,12.55f,12.35f};
            for(int i=0;i<6;i++) { legacy[i].BaseSpeed=oldSpeeds[i];legacy[i].EarlyPower=i==5?.85f:i==0?-.1f:.1f;legacy[i].SurgePower=1.7f; }
            var legacySim=Finish(legacy,race.Course,false,1);
            string actual=string.Join("\n",legacySim.Runners.Select(r=>r.Id+":"+r.FinishTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).ToArray());
            report.LegacyExact=actual==File.ReadAllText("DataBundles/검증/Race2D_Contests_20261018/legacy_baseline.txt").Trim().Replace("\r","");
            var random=new System.Random(47119);
            for(int scenario=0;scenario<12;scenario++)
            {
                var setups=Copy(race.Entrants);
                if(scenario>0) for(int i=0;i<6;i++)
                {
                    setups[i].BaseSpeed=9+(float)random.NextDouble()*6;
                    setups[i].EarlyPower=(float)random.NextDouble()*2;
                    setups[i].SurgeTime=6+(float)random.NextDouble()*14;
                    setups[i].SurgePower=2+(float)random.NextDouble()*3;
                }
                var sim=new Data_RaceSimulation(setups,race.Course,true);sim.Begin();
                for(int tick=0;tick<18000 && sim.Phase!=E_RacePhase.Result;tick++)
                {
                    sim.Step();
                    for(int i=0;i<6;i++)
                    {
                        var a=sim.Runners[i];
                        if(scenario==0 && a.BlockedBy>=0) report.BlockedSteps++;
                        report.BoundsValid &= Mathf.Abs(a.Lane)<=7.751f;
                        report.MaximumLaneStep=Mathf.Max(report.MaximumLaneStep,Mathf.Abs(a.Lane-a.PreviousLane));
                        for(int j=i+1;j<6;j++)
                        {
                            var b=sim.Runners[j];if(a.Finished||b.Finished)continue;
                            float separation=Vector3.Distance(sim.Point((float)a.Distance,a.Lane),sim.Point((float)b.Distance,b.Lane));
                            report.MinimumGroundSeparation=Mathf.Min(report.MinimumGroundSeparation,separation);
                        }
                    }
                }
                report.AllFinished &= sim.Runners.All(r=>r.Finished) && sim.Phase==E_RacePhase.Result;
                report.ScenarioCount++;
            }
            report.MovementContinuous=report.MaximumLaneStep<=1.65f/60+.001f;
            report.Passed=report.LegacyExact && report.BatchingExact && report.AllFinished && report.BoundsValid
                && report.MovementContinuous && report.MinimumGroundSeparation>=2.2f && report.Passes>0 && report.BlockedSteps>0;
            string json=JsonUtility.ToJson(report,true);
            File.WriteAllText("DataBundles/검증/Race2D_Contests_20261018/simulation_validation.json",json);
            if(!report.Passed) throw new InvalidOperationException(json);
            return json;
        }
        private static Data_RunnerSetup[] Copy(Data_RunnerSetup[] source)
        { return source.Select(s=>JsonUtility.FromJson<Data_RunnerSetup>(JsonUtility.ToJson(s))).ToArray(); }
        private static Data_RaceSimulation Finish(Data_RunnerSetup[] setups,Data_RaceCourse course,bool contests,int batch)
        {
            var sim=new Data_RaceSimulation(setups,course,contests);sim.Begin();
            for(int frame=0;frame<18000 && sim.Phase!=E_RacePhase.Result;frame++)
                for(int step=0;step<batch && sim.Phase!=E_RacePhase.Result;step++)sim.Step();
            return sim;
        }
    }
}
