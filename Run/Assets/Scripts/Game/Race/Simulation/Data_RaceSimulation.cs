using System;
using UnityEngine;

namespace ProjectRun.Race
{
    public enum E_RacePhase { Preparing, Running, Finishing, Result }
    public enum E_RaceEvent { Start, Skill, Overtake, Finish, Result, CosmeticSkill }

    public readonly struct Data_RaceEvent
    {
        public readonly E_RaceEvent Kind;
        public readonly int RunnerId;
        public readonly float Time;
        public readonly E_RaceSkillRarity Rarity;
        public Data_RaceEvent(E_RaceEvent kind, int runnerId, float time, E_RaceSkillRarity rarity = E_RaceSkillRarity.Unique)
        { Kind = kind; RunnerId = runnerId; Time = time; Rarity = rarity; }
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
        [NonSerialized] public Data_RaceSkillSet RaceSkills;
        [NonSerialized] public bool DemoComeback;
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
        public int NormalSkillCount;
        public int RareSkillCount;
        public bool Finished;
        public double FinishTime;
    }

    /// <summary>Fixed-step race model. Rendering, cameras and cutscene options never change its input.</summary>
    public sealed class Data_RaceSimulation
    {
        public const double StepSeconds = 1.0 / 60.0;
        public const float Radius = 68.5f;
        /// <summary>Half length of each straight of the original oval (straights span x -90..90).</summary>
        public const float BaseHalfStraight = 90;
        /// <summary>Start/finish line X on the home straight. Kept fixed when straights are extended.</summary>
        public const float FinishX = 49;
        /// <summary>Meters added to each end of both straights (set per scene by Sys_Race). 0 = original course.</summary>
        public static float StraightExtension { get; set; }
        public static float HalfStraight => BaseHalfStraight + Mathf.Max(0, StraightExtension);
        /// <summary>Speed (m/s) every runner already has the instant the race begins (set per scene by Sys_Race). 0 = standing start.</summary>
        public static float StartLaunchSpeed { get; set; }
        /// <summary>Acceleration (m/s²) during the first <see cref="StartBoostSeconds"/> (set per scene by Sys_Race).</summary>
        public static float StartAcceleration { get; set; } = 6f;
        public const float StartBoostSeconds = 3f;
        public static float Length => 4 * HalfStraight + 2 * Mathf.PI * Radius;
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
        public bool IsBursting(int id) => setups[id].ConditionalSkill && setups[id].DemoSkillType>0 && Runners[id].SkillAge>=0 &&
            (Runners[id].SkillAge<setups[id].SurgeDuration || setups[id].DemoComeback&&Runners[id].Distance/CourseLength>.70f);
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
            for (int i = 0; i < Runners.Length; i++)
                Runners[i].Speed = Mathf.Min(Mathf.Max(0f, StartLaunchSpeed), Mathf.Max(0f, setups[i].BaseSpeed));
            Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.Start, 0, 0));
        }

        /// <summary>True after Result while runners are still coasting (see <see cref="CoolDownStep"/>).</summary>
        public bool CoolingDown { get; private set; }

        /// <summary>
        /// Presentation only: after Result, runners keep running past the line and slow to a stop.
        /// Ranks, finish times and events are not touched.
        /// </summary>
        public void CoolDownStep(float deceleration)
        {
            if (Phase != E_RacePhase.Result || !CoolingDown) return;
            Time += StepSeconds;
            bool moving = false;
            foreach (var runner in Runners)
            {
                runner.PreviousDistance = runner.Distance;
                runner.PreviousLane = runner.Lane;
                runner.LaneVelocity = 0;
                runner.Speed = Mathf.MoveTowards(runner.Speed, 0, Mathf.Max(0.1f, deceleration) * (float)StepSeconds);
                if (runner.Speed <= 0) continue;
                moving = true;
                runner.Distance += runner.Speed / Metric((float)runner.Distance, runner.Lane) * StepSeconds;
            }
            CoolingDown = moving;
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
                if (!runner.Finished) PublishCosmeticSkills(i);
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
                runner.Speed = Mathf.MoveTowards(runner.Speed, target, (float)StepSeconds * (t < StartBoostSeconds ? Mathf.Max(0.1f, StartAcceleration) : 2.4f));
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
                CoolingDown = true;
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
            // Demo assistance belongs to the unique skill and is fixed in the race inputs.
            // It changes speed, never positions, ranks or finish times directly.
            if (setup.DemoComeback && Runners[id].SkillUsed && !Runners[id].Finished)
            {
                float progress = (float)(Runners[id].Distance / CourseLength);
                double leadingDistance = double.NegativeInfinity;
                float leadingSpeed = setup.BaseSpeed;
                int leaderId=id;
                for (int i = 0; i < Runners.Length; i++)
                    if (i != id && Runners[i].Distance > leadingDistance)
                    { leadingDistance = Runners[i].Distance; leadingSpeed = Runners[i].Speed;leaderId=i; }
                if(double.IsNegativeInfinity(leadingDistance))leadingDistance=Runners[id].Distance;
                float gap = Mathf.Max(-4,(float)(leadingDistance - Runners[id].Distance));
                float secondsLeft = Mathf.Max(2, (CourseLength - (float)leadingDistance) / Mathf.Max(1, leadingSpeed));
                float metric=Metric((float)Runners[id].Distance,Runners[id].Lane);
                float leadMetric=Metric((float)leadingDistance,Runners[leaderId].Lane);
                float catchupTarget=(leadingSpeed/leadMetric+(gap+6)/Mathf.Max(1.5f,secondsLeft*.60f))*metric/fatigue;
                float baseTarget=setup.BaseSpeed+early+late+cadence;
                float catchup=Mathf.Clamp(catchupTarget-baseTarget,setup.BaseSpeed*.045f,setup.BaseSpeed);
                boost=Mathf.Max(boost,catchup*Mathf.SmoothStep(0,1,(progress-.70f)/.07f));
            }
            return (setup.BaseSpeed + early + late + cadence + boost) * fatigue;
        }

        private void PublishCosmeticSkills(int id)
        {
            var skills = setups[id].RaceSkills;
            if (skills == null) return;
            var runner = Runners[id];
            float progress = (float)(runner.Distance / CourseLength);
            if (runner.NormalSkillCount < 3 && progress >= skills.NormalProgress + runner.NormalSkillCount * .28f)
            {
                runner.NormalSkillCount++;
                Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.CosmeticSkill, id, (float)Time, E_RaceSkillRarity.Normal));
            }
            if (runner.RareSkillCount < 2 && progress >= skills.RareProgress + runner.RareSkillCount * .36f)
            {
                runner.RareSkillCount++;
                Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.CosmeticSkill, id, (float)Time, E_RaceSkillRarity.Rare));
            }
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
            float h = HalfStraight, straight = 2 * h;
            float s = Mathf.Repeat(distance + h + FinishX, Length);
            bool curved = (s >= straight && s < straight + Mathf.PI * Radius) || s >= 2 * straight + Mathf.PI * Radius;
            return curved ? (Radius + lane) / Radius : 1;
        }

        public static Vector3 CoursePoint(float distance, float lane = 0)
        {
            float h = HalfStraight, straight = 2 * h;
            // distance 0 = start/finish line at FinishX on the home straight
            float s = Mathf.Repeat(distance + h + FinishX, Length);
            float r = Radius + lane;
            if (s < straight) return new Vector3(-h + s, 0.14f, -r);
            s -= straight;
            if (s < Mathf.PI * Radius) { float a = s / Radius - Mathf.PI * 0.5f; return new Vector3(h + Mathf.Cos(a) * r, 0.14f, Mathf.Sin(a) * r); }
            s -= Mathf.PI * Radius;
            if (s < straight) return new Vector3(h - s, 0.14f, r);
            s -= straight;
            float angle = s / Radius + Mathf.PI * 0.5f;
            return new Vector3(-h + Mathf.Cos(angle) * r, 0.14f, Mathf.Sin(angle) * r);
        }

        public static Vector3 CourseForward(float distance) => (CoursePoint(distance + 0.15f) - CoursePoint(distance - 0.15f)).normalized;
    }
}
