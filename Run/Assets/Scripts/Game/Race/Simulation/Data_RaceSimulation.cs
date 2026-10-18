using System;
using UnityEngine;

namespace ProjectRun.Race
{
    public enum E_RacePhase { Preparing, Running, Finishing, Result }
    public enum E_RaceEvent { Start, Skill, Overtake, Finish, Result }

    public readonly struct Data_RaceEvent
    {
        public readonly E_RaceEvent Kind;
        public readonly int RunnerId;
        public readonly float Time;
        public Data_RaceEvent(E_RaceEvent kind, int runnerId, float time) { Kind = kind; RunnerId = runnerId; Time = time; }
    }

    [Serializable]
    public sealed class Data_RunnerSetup
    {
        public string Name;
        public string Mount;
        public string SkillName;
        public Color Color = Color.white;
        public GameObject Prefab;
        public Sprite Portrait;
        public Sprite FacePortrait;
        public float BaseSpeed = 12.5f;
        public float EarlyPower;
        public float LatePower;
        public float SurgeTime;
        public float SurgePower = 2.7f;
        public float SurgeDuration = 5.5f;
        public float Lane;
        public bool ConditionalSkill;
        public int DemoSkillType;
        public float SkillProgress = .62f;
        public int SkillActivationLimit = 1;
        public float RepeatSkillProgress = .72f;
        public float StaminaDrainMultiplier = 1;
        public bool UseStamina;
        public Data_RunnerSetup Copy() => (Data_RunnerSetup)MemberwiseClone();
    }

    public sealed class Data_RunnerState
    {
        public int Id;
        public int Rank;
        public double Distance;
        public double PreviousDistance;
        public float Speed;
        public float Lane;
        public float Stamina = 1;
        public float PreviousLane;
        public float TargetLane;
        public float LaneVelocity;
        public int BlockedBy = -1;
        public bool IsContesting;
        public float SkillAge = -1;
        public bool SkillUsed;
        public int SkillActivationCount;
        public float SkillStarted = -1;
        public bool Finished;
        public double FinishTime;
    }

    /// <summary>Fixed-step race model. Rendering, cameras and cutscene options never change its input.</summary>
    public sealed class Data_RaceSimulation
    {
        public const double StepSeconds = 1.0 / 60.0;
        public const float Radius = 68.5f;
        public static readonly float Length = 360 + 2 * Mathf.PI * Radius;
        public readonly Data_RaceCourse Course;
        public readonly int LapCount;
        public float LapLength => Course != null ? Course.Length : Length;
        /// <summary>Total distance to the finish; positions wrap at LapLength.</summary>
        public float CourseLength => LapLength * LapCount;
        public int CurrentLap(double distance) => Mathf.Clamp((int)(System.Math.Max(0, distance) / LapLength) + 1, 1, LapCount);
        public Vector3 Point(float distance, float lane = 0) => Course != null ? Course.Point(distance, lane) : CoursePoint(distance, lane);
        public Vector3 Forward(float distance) => Course != null ? Course.Forward(distance) : CourseForward(distance);
        public float Metric(float distance, float lane) => Course != null ? Course.Metric(distance, lane) : CourseMetric(distance, lane);
        public readonly Data_RunnerState[] Runners;
        public readonly int[] Order;
        public E_RacePhase Phase { get; private set; }
        public double Time { get; private set; }
        public int Overtakes { get; private set; }
        public event Action<Data_RaceEvent> Changed;
        private readonly Data_RunnerSetup[] setups;
        private readonly int[] previousRanks;
        private readonly Data_RaceLaneContest laneContest;
        public bool UsesLaneContests => laneContest != null;
        public bool IsAirborne(int id) => setups[id].ConditionalSkill && setups[id].DemoSkillType==1 && Runners[id].SkillAge>=.7f && Runners[id].SkillAge<2.2f;
        public bool IsBursting(int id) => setups[id].ConditionalSkill && setups[id].DemoSkillType>0 && Runners[id].SkillAge>=0 && Runners[id].SkillAge<setups[id].SurgeDuration;
        private double firstFinish = -1;
        private double lastOvertakeNotice = -10;

        public Data_RaceSimulation(Data_RunnerSetup[] setups, Data_RaceCourse course = null, bool enableLaneContests = false, int lapCount = 1)
        {
            if (course != null && !course.IsValid) throw new ArgumentException("Race course must contain a valid closed path.", nameof(course));
            Course = course;
            LapCount = Mathf.Max(1, lapCount);
            this.setups = setups;
            Runners = new Data_RunnerState[setups.Length];
            Order = new int[setups.Length];
            previousRanks = new int[setups.Length];
            if (enableLaneContests) laneContest = new Data_RaceLaneContest(setups.Length, course != null ? course.RoadWidth : 20);
            for (int i = 0; i < setups.Length; i++)
            {
                Runners[i] = new Data_RunnerState { Id = i, Rank = i + 1, Lane = setups[i].Lane,
                    PreviousLane = setups[i].Lane, TargetLane = setups[i].Lane };
                Order[i] = i;
                previousRanks[i] = i + 1;
            }
            Phase = E_RacePhase.Preparing;
        }

        public void Begin()
        {
            if (Phase != E_RacePhase.Preparing) return;
            Phase = E_RacePhase.Running;
            Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.Start, 0, 0));
        }

        public void Step()
        {
            if (Phase == E_RacePhase.Preparing || Phase == E_RacePhase.Result) return;
            double previousTime = Time;
            Time += StepSeconds;
            float t = (float)Time;
            laneContest?.Plan(this,setups);
            for (int i = 0; i < Runners.Length; i++)
            {
                var runner = Runners[i];
                var setup = setups[i];
                runner.PreviousDistance = runner.Distance;
                runner.PreviousLane = runner.Lane;
                if (!runner.Finished && ShouldActivate(i))
                {
                    runner.SkillUsed = true;
                    runner.SkillActivationCount++;
                    runner.SkillStarted = t;
                    runner.SkillAge = 0;
                    Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.Skill, i, t));
                }
                runner.SkillAge = runner.SkillUsed ? t - runner.SkillStarted : -1;
                float target = DesiredSpeed(i);
                if (runner.Finished) target = Mathf.Max(1.8f, runner.Speed - 4.5f * (float)StepSeconds);
                if (laneContest != null) target = laneContest.AdvanceLaneAndLimitSpeed(i,this,target);
                runner.Speed = Mathf.MoveTowards(runner.Speed, target, (float)StepSeconds * (t < 3 ? 6 : 2.4f));
                runner.Stamina = Mathf.Clamp01(1 - t * (0.0069f + i * 0.0003f) * Mathf.Max(.1f, setup.StaminaDrainMultiplier));
                if (laneContest == null) runner.Lane = setup.Lane + Mathf.Sin(t * 0.3f + i) * 0.07f;
                float metric = Metric((float)runner.Distance, runner.Lane);
                double advance = runner.Speed / metric * StepSeconds;
                if (laneContest != null)
                {
                    advance = laneContest.LimitAdvance(i,this,advance);
                    runner.Speed = Mathf.Min(runner.Speed,(float)(advance * metric / StepSeconds));
                }
                runner.Distance += advance;
                if (!runner.Finished && runner.Distance >= CourseLength)
                {
                    double fraction = (CourseLength - runner.PreviousDistance) / (runner.Distance - runner.PreviousDistance);
                    runner.FinishTime = previousTime + fraction * StepSeconds;
                    runner.Finished = true;
                    if (firstFinish < 0) { firstFinish = runner.FinishTime; Phase = E_RacePhase.Finishing; }
                    Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.Finish, i, t));
                }
            }
            // Small race fields: allocation-free insertion sort and stable ties.
            for (int i = 1; i < Order.Length; i++)
            {
                int value = Order[i], j = i - 1;
                while (j >= 0 && IsAhead(Runners[value], Runners[Order[j]])) { Order[j + 1] = Order[j]; j--; }
                Order[j + 1] = value;
            }
            for (int i = 0; i < Order.Length; i++) Runners[Order[i]].Rank = i + 1;
            for (int i = 0; i < Runners.Length; i++)
            {
                if (t > 4 && Runners[i].Rank < previousRanks[i] && !Runners[i].Finished)
                {
                    Overtakes++;
                    if (Time - lastOvertakeNotice > 2.5) { Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.Overtake, i, t)); lastOvertakeNotice = Time; }
                }
                previousRanks[i] = Runners[i].Rank;
            }
            bool allFinished = true;
            foreach (var runner in Runners) allFinished &= runner.Finished;
            if (allFinished && Time - firstFinish > 4)
            {
                Phase = E_RacePhase.Result;
                Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.Result, Order[0], t));
            }
        }

        public float DesiredSpeed(int id)
        {
            var setup = setups[id]; float t = (float)Time;
            float age = setup.ConditionalSkill ? (Runners[id].SkillUsed ? t-Runners[id].SkillStarted : -1) : t-setup.SurgeTime;
            float boost = age >= 0 && age < setup.SurgeDuration
                ? setup.SurgePower * Mathf.SmoothStep(0,1,age/.75f)
                    * (1-Mathf.SmoothStep(0,1,(age-setup.SurgeDuration+1)/1f)) : 0;
            float early = setup.EarlyPower * (1-Mathf.SmoothStep(0,1,(t-7)/19));
            float late = setup.LatePower * Mathf.SmoothStep(0,1,(t-36)/17);
            float cadence = .23f * Mathf.Sin(t*.39f+id*1.8f);
            float fatigue = setup.UseStamina ? Mathf.Lerp(.86f,1,Mathf.Clamp01(Runners[id].Stamina/.5f)) : 1;
            return (setup.BaseSpeed + early + late + cadence + boost) * fatigue;
        }

        private bool ShouldActivate(int id)
        {
            var setup=setups[id];var runner=Runners[id];
            if(runner.SkillActivationCount>=Mathf.Max(1,setup.SkillActivationLimit))return false;
            if(runner.SkillActivationCount>0)
                return setup.ConditionalSkill && runner.Distance/CourseLength>=setup.RepeatSkillProgress
                    && Time-runner.SkillStarted>=setup.SurgeDuration;
            if(!setup.ConditionalSkill) return Time>=setup.SurgeTime;
            float progress=(float)(runner.Distance/CourseLength);
            if(progress<setup.SkillProgress)return false;
            // Tactical conditions use simulation data only. A late-course opportunity also
            // permits a clear leader to use the skill, without depending on camera visibility.
            bool nearby=false;
            for(int i=0;i<Runners.Length;i++) if(i!=id && Runners[i].Distance>runner.Distance && Runners[i].Distance-runner.Distance<55)nearby=true;
            float guaranteedProgress=setup.SkillActivationLimit>1?Mathf.Min(.74f,setup.SkillProgress+.06f):.74f;
            return nearby || progress>=guaranteedProgress;
        }

        private static bool IsAhead(Data_RunnerState a, Data_RunnerState b)
        {
            if (a.Finished && b.Finished) return a.FinishTime == b.FinishTime ? a.Id < b.Id : a.FinishTime < b.FinishTime;
            if (a.Finished != b.Finished) return a.Finished;
            return a.Distance == b.Distance ? a.Id < b.Id : a.Distance > b.Distance;
        }

        public static float CourseMetric(float distance, float lane)
        {
            float s = Mathf.Repeat(distance + 139, Length);
            bool curved = (s >= 180 && s < 180 + Mathf.PI * Radius) || s >= 360 + Mathf.PI * Radius;
            return curved ? (Radius + lane) / Radius : 1;
        }

        public static Vector3 CoursePoint(float distance, float lane = 0)
        {
            float s = Mathf.Repeat(distance + 139, Length);
            float r = Radius + lane;
            if (s < 180) return new Vector3(-90 + s, 0.14f, -r);
            s -= 180;
            if (s < Mathf.PI * Radius) { float a = s / Radius - Mathf.PI * 0.5f; return new Vector3(90 + Mathf.Cos(a) * r, 0.14f, Mathf.Sin(a) * r); }
            s -= Mathf.PI * Radius;
            if (s < 180) return new Vector3(90 - s, 0.14f, r);
            s -= 180;
            float angle = s / Radius + Mathf.PI * 0.5f;
            return new Vector3(-90 + Mathf.Cos(angle) * r, 0.14f, Mathf.Sin(angle) * r);
        }

        public static Vector3 CourseForward(float distance) => (CoursePoint(distance + 0.15f) - CoursePoint(distance - 0.15f)).normalized;
    }
}
