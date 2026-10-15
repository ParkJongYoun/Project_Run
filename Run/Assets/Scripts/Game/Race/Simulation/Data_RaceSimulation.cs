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
        public float SkillAge = -1;
        public bool SkillUsed;
        public bool Finished;
        public double FinishTime;
    }

    /// <summary>Fixed-step race model. Rendering, cameras and cutscene options never change its input.</summary>
    public sealed class Data_RaceSimulation
    {
        public const double StepSeconds = 1.0 / 60.0;
        public const float Radius = 68.5f;
        public static readonly float Length = 360 + 2 * Mathf.PI * Radius;
        public readonly Data_RunnerState[] Runners;
        public readonly int[] Order;
        public E_RacePhase Phase { get; private set; }
        public double Time { get; private set; }
        public int Overtakes { get; private set; }
        public event Action<Data_RaceEvent> Changed;
        private readonly Data_RunnerSetup[] setups;
        private readonly int[] previousRanks;
        private double firstFinish = -1;
        private double lastOvertakeNotice = -10;

        public Data_RaceSimulation(Data_RunnerSetup[] setups)
        {
            this.setups = setups;
            Runners = new Data_RunnerState[setups.Length];
            Order = new int[setups.Length];
            previousRanks = new int[setups.Length];
            for (int i = 0; i < setups.Length; i++)
            {
                Runners[i] = new Data_RunnerState { Id = i, Rank = i + 1, Lane = setups[i].Lane };
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
            for (int i = 0; i < Runners.Length; i++)
            {
                var runner = Runners[i];
                var setup = setups[i];
                runner.PreviousDistance = runner.Distance;
                if (!runner.SkillUsed && t >= setup.SurgeTime && !runner.Finished)
                {
                    runner.SkillUsed = true;
                    Changed?.Invoke(new Data_RaceEvent(E_RaceEvent.Skill, i, t));
                }
                runner.SkillAge = runner.SkillUsed ? t - setup.SurgeTime : -1;
                float boost = runner.SkillAge >= 0 && runner.SkillAge < setup.SurgeDuration
                    ? setup.SurgePower * Mathf.SmoothStep(0, 1, runner.SkillAge / 0.75f)
                      * (1 - Mathf.SmoothStep(0, 1, (runner.SkillAge - setup.SurgeDuration + 1) / 1f)) : 0;
                float early = setup.EarlyPower * (1 - Mathf.SmoothStep(0, 1, (t - 7) / 19));
                float late = setup.LatePower * Mathf.SmoothStep(0, 1, (t - 36) / 17);
                // Different endurance and cadence profiles produce real catches and passes.
                float cadence = 0.23f * Mathf.Sin(t * 0.39f + i * 1.8f);
                float target = setup.BaseSpeed + early + late + cadence + boost;
                if (runner.Finished) target = Mathf.Max(1.8f, runner.Speed - 4.5f * (float)StepSeconds);
                runner.Speed = Mathf.MoveTowards(runner.Speed, target, (float)StepSeconds * (t < 3 ? 6 : 2.4f));
                runner.Stamina = Mathf.Clamp01(1 - t * (0.0069f + i * 0.0003f));
                runner.Lane = setup.Lane + Mathf.Sin(t * 0.3f + i) * 0.07f;
                float metric = CourseMetric((float)runner.Distance, runner.Lane);
                runner.Distance += runner.Speed / metric * StepSeconds;
                if (!runner.Finished && runner.Distance >= Length)
                {
                    double fraction = (Length - runner.PreviousDistance) / (runner.Distance - runner.PreviousDistance);
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
