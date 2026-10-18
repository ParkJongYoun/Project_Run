using System;
using System.IO;
using System.Linq;
using ProjectRun.Race;
using UnityEngine;

namespace ProjectRun.Editor
{
    public static class SpriteRacePacingValidation
    {
        public const string Folder = "DataBundles/검증/2D경기_개선검수_20261018";
        [Serializable] public sealed class Metrics
        {
            public int Laps, Passes, ConfirmedPasses, LaneReversals;
            public int[] FinishOrder;
            public double[] FinishTimes;
            public double Duration;
            public float MeanPackSpan, MeanNearestGap, ContestRunnerSeconds, BlockedRunnerSeconds, LateralTravel;
            public float SecondLapContestSeconds, SecondLapBlockedSeconds, MinimumSeparation = 1000, MaximumLaneStep;
            public bool Finished, FirstLapContinues = true, BoundsValid = true;
        }

        public static Metrics Measure(Data_RunnerSetup[] setups, Data_RaceCourse course, int laps, int batch = 1, bool contests = true)
        {
            var sim = new Data_RaceSimulation(setups, course, contests, laps); sim.Begin();
            var result = new Metrics { Laps = laps };
            var signs = new int[setups.Length]; var pairSigns = new int[setups.Length,setups.Length]; int samples = 0;
            for (int frame = 0; frame < 18000 && sim.Phase != E_RacePhase.Result; frame++)
            for (int step = 0; step < batch && sim.Phase != E_RacePhase.Result; step++)
            {
                sim.Step();
                float leader = (float)sim.Runners[sim.Order[0]].Distance;
                if (laps > 1 && leader >= sim.LapLength && leader < sim.CourseLength)
                    result.FirstLapContinues &= sim.Phase == E_RacePhase.Running && sim.Runners.All(r => !r.Finished);
                bool racing = sim.Runners.All(r => !r.Finished);
                if (racing) { result.MeanPackSpan += leader - (float)sim.Runners[sim.Order[sim.Order.Length - 1]].Distance; samples++; }
                for (int i = 0; i < setups.Length; i++)
                {
                    var runner = sim.Runners[i]; if (runner.Finished) continue;
                    bool second = runner.Distance >= sim.LapLength;
                    if (runner.IsContesting) { result.ContestRunnerSeconds += 1f/60; if (second) result.SecondLapContestSeconds += 1f/60; }
                    if (runner.BlockedBy >= 0) { result.BlockedRunnerSeconds += 1f/60; if (second) result.SecondLapBlockedSeconds += 1f/60; }
                    float movement = Mathf.Abs(runner.Lane-runner.PreviousLane);
                    result.LateralTravel += movement; result.MaximumLaneStep = Mathf.Max(result.MaximumLaneStep,movement);
                    result.BoundsValid &= Mathf.Abs(runner.Lane) <= course.RoadWidth*.5f-2.249f;
                    int sign = runner.LaneVelocity > .3f ? 1 : runner.LaneVelocity < -.3f ? -1 : 0;
                    if (sign != 0) { if (signs[i] != 0 && signs[i] != sign) result.LaneReversals++; signs[i] = sign; }
                    float nearest = 1000;
                    for (int j = 0; j < setups.Length; j++)
                    {
                        if (i==j || sim.Runners[j].Finished) continue;
                        if (i<j && sim.Time>4)
                        {
                            double lead=runner.Distance-sim.Runners[j].Distance;
                            int ahead=lead>1.5 ? 1 : lead< -1.5 ? -1 : 0;
                            if(ahead!=0) { if(pairSigns[i,j]!=0 && pairSigns[i,j]!=ahead)result.ConfirmedPasses++; pairSigns[i,j]=ahead; }
                        }
                        float gap = Vector3.Distance(sim.Point((float)runner.Distance,runner.Lane),sim.Point((float)sim.Runners[j].Distance,sim.Runners[j].Lane));
                        nearest = Mathf.Min(nearest,gap); result.MinimumSeparation = Mathf.Min(result.MinimumSeparation,gap);
                    }
                    if (racing) result.MeanNearestGap += nearest / setups.Length;
                }
            }
            result.MeanPackSpan /= Mathf.Max(1,samples); result.MeanNearestGap /= Mathf.Max(1,samples);
            result.Passes = sim.Overtakes; result.Duration = sim.Time; result.FinishOrder = sim.Order;
            result.FinishTimes = sim.Runners.Select(r=>r.FinishTime).ToArray();
            result.Finished = sim.Phase == E_RacePhase.Result && sim.Runners.All(r=>r.Finished);
            return result;
        }

        public static string Record(string label, int laps)
        {
            var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            string json = JsonUtility.ToJson(Measure(race.Entrants,race.Course,laps),true);
            Directory.CreateDirectory(Folder); File.WriteAllText(Folder+"/"+label+".json",json); return json;
        }

        public static string Validate()
        {
            var race=UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
            var baseline=Measure(race.Entrants,race.Course,2);
            bool batching=true, scenarios=true; float minimum=1000;
            foreach(int batch in new[]{2,5,11,240})
            {
                var repeated=Measure(race.Entrants,race.Course,2,batch);
                for(int i=0;i<6;i++) batching &= repeated.FinishTimes[i]==baseline.FinishTimes[i];
            }
            var random=new System.Random(47119);
            for(int scenario=0;scenario<12;scenario++)
            {
                var setups=race.Entrants.Select(s=>JsonUtility.FromJson<Data_RunnerSetup>(JsonUtility.ToJson(s))).ToArray();
                if(scenario>0) foreach(var setup in setups)
                {
                    setup.BaseSpeed=9+(float)random.NextDouble()*6;
                    setup.EarlyPower=(float)random.NextDouble()*2;
                    setup.SurgeTime=6+(float)random.NextDouble()*42;
                    setup.SurgePower=2+(float)random.NextDouble()*3;
                }
                var result=Measure(setups,race.Course,2);
                minimum=Mathf.Min(minimum,result.MinimumSeparation);
                scenarios &= result.Finished && result.BoundsValid && result.FirstLapContinues
                    && result.MinimumSeparation>=2.2f && result.MaximumLaneStep<=1.65f/60+.001f;
            }
            var one=new Data_RaceSimulation(race.Entrants,race.Course,false); one.Begin();
            for(int i=0;i<18000 && one.Phase!=E_RacePhase.Result;i++) one.Step();
            bool oneLap=one.LapCount==1 && one.CourseLength==race.Course.Length && one.Phase==E_RacePhase.Result;
            var pair=new[]{new Data_RunnerSetup{BaseSpeed=12,Lane=0,SurgeTime=100},new Data_RunnerSetup{BaseSpeed=5,Lane=0,SurgeTime=100}};
            var wrap=new Data_RaceSimulation(pair,race.Course,true,2);wrap.Begin();
            wrap.Runners[0].Distance=wrap.LapLength-4;wrap.Runners[1].Distance=1;
            wrap.Runners[0].Speed=12;wrap.Runners[1].Speed=5;
            bool blocked=false;float separation=1000;
            for(int i=0;i<30;i++)
            {
                wrap.Step(); blocked |= wrap.Runners[0].BlockedBy==1;
                separation=Mathf.Min(separation,Vector3.Distance(wrap.Point((float)wrap.Runners[0].Distance,wrap.Runners[0].Lane),wrap.Point((float)wrap.Runners[1].Distance,wrap.Runners[1].Lane)));
            }
            string report="Batching finish times exact="+batching+"\nTwelve two-lap profiles safe="+scenarios
                +"\nMinimum separation="+minimum+"\nSingle-lap default retained="+oneLap
                +"\nLapped runner across line blocked="+blocked+"; separation="+separation;
            Directory.CreateDirectory(Folder); File.WriteAllText(Folder+"/경기계산_회귀검증.txt",report);
            if(!batching || !scenarios || !oneLap || !blocked || separation<2.2f) throw new InvalidOperationException(report);
            return report;
        }
    }
}
