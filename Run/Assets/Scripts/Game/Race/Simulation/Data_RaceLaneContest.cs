using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>Deterministic lane planning from one shared fixed-step snapshot; no camera input.</summary>
    public sealed class Data_RaceLaneContest
    {
        public const float Clearance = 2.45f;
        public const float FollowingGap = 3.65f;
        private readonly float[] lanes, speeds, targets, decisions, reservations;
        private readonly double[] distances;
        private readonly float limit;

        public Data_RaceLaneContest(int count, float roadWidth)
        {
            lanes = new float[count]; speeds = new float[count]; targets = new float[count];
            decisions = new float[count]; reservations = new float[count]; distances = new double[count];
            limit = Mathf.Max(1, roadWidth * .5f - 2.25f);
        }

        public void Plan(Data_RaceSimulation simulation, Data_RunnerSetup[] setups)
        {
            float time = (float)simulation.Time;
            for (int i = 0; i < lanes.Length; i++)
            {
                var runner = simulation.Runners[i];
                lanes[i] = runner.Lane; distances[i] = runner.Distance; speeds[i] = runner.Speed;
                reservations[i] = runner.TargetLane;
                runner.BlockedBy = -1; runner.IsContesting = false;
            }
            // Front-to-back reservations prevent two side-by-side runners selecting the same gap.
            foreach (int id in simulation.Order)
            {
                var runner = simulation.Runners[id];
                if (time < .65f || runner.Finished) { targets[id] = lanes[id]; reservations[id] = lanes[id]; continue; }
                if (time >= decisions[id])
                {
                    float best = Score(id, lanes[id], simulation, setups);
                    float choice = lanes[id];
                    for (int slot = 0; slot <= 8; slot++)
                    {
                        float candidate = slot<=6 ? Mathf.Lerp(-limit, limit, slot / 6f)
                            : Mathf.Clamp(lanes[id] + (slot==7 ? -1 : 1)*(Clearance+.5f),-limit,limit);
                        if (Mathf.Abs(candidate - lanes[id]) > (simulation.IsBursting(id)?6.8f:4.4f) || !CanSweep(id, lanes[id], candidate, simulation)) continue;
                        float score = Score(id, candidate, simulation, setups);
                        if (score < best - .08f) { best = score; choice = candidate; }
                    }
                    targets[id] = choice;
                    decisions[id] = time + (simulation.IsBursting(id)?.16f:.45f) + id * .027f;
                }
                reservations[id] = targets[id];
                runner.TargetLane = targets[id];
            }
        }

        private float Score(int id, float lane, Data_RaceSimulation simulation, Data_RunnerSetup[] setups)
        {
            float desired = simulation.DesiredSpeed(id);
            float metric = simulation.Metric((float)distances[id] + desired, lane);
            // Pay for the actual longer path around a bend, not a permanent preference for the rail.
            float score = (metric-1) * desired * 2.2f + (lane + limit) * .018f
                + Mathf.Abs(lane - lanes[id]) * .18f;
            for (int other = 0; other < lanes.Length; other++)
            {
                if (other == id || simulation.IsAirborne(other)) continue;
                float gap = Gap(id,other,simulation) * simulation.Metric((float)distances[id],lane);
                float lateral = Mathf.Abs(lane - lanes[other]);
                float closing = Mathf.Max(0,desired-speeds[other]);
                float lookAhead = FollowingGap + desired * .45f + closing * .8f;
                if(setups[id].DemoComeback&&simulation.IsBursting(id))lookAhead+=desired*.75f;
                if (gap > 0 && gap < lookAhead && lateral < Clearance + .6f)
                    score += (lookAhead - gap) * (.05f + closing * .65f)
                        * (1 - Mathf.Clamp01((lateral - Clearance) / .6f));
                if (Mathf.Abs(gap) < 5 && Mathf.Abs(lane - reservations[other]) < Clearance)
                    score += 18;
            }
            return score;
        }

        private bool CanSweep(int id, float from, float to, Data_RaceSimulation simulation)
        {
            if(simulation.IsAirborne(id))return true;
            float low = Mathf.Min(from,to) - Clearance;
            float high = Mathf.Max(from,to) + Clearance;
            for (int other = 0; other < lanes.Length; other++)
            {
                if (other == id || simulation.IsAirborne(other)) continue;
                float gap = Mathf.Abs(Gap(id,other,simulation)) * simulation.Metric((float)distances[id],from);
                if (gap >= FollowingGap + .7f) continue;
                float before = Mathf.Abs(from - lanes[other]);
                float after = Mathf.Abs(to - lanes[other]);
                bool escaping = before < Clearance && after > before && (from - lanes[other]) * (to - lanes[other]) >= 0;
                if (escaping) continue;
                if (lanes[other] > low && lanes[other] < high) return false;
                if (reservations[other] > low && reservations[other] < high) return false;
            }
            return true;
        }

        public float AdvanceLaneAndLimitSpeed(int id, Data_RaceSimulation simulation, float desiredSpeed)
        {
            var runner = simulation.Runners[id];
            float dt = (float)Data_RaceSimulation.StepSeconds;
            float lateralSpeed=simulation.IsBursting(id)?3.4f:1.65f;
            float velocity = Mathf.Clamp((targets[id] - runner.Lane) * 1.8f,-lateralSpeed,lateralSpeed);
            runner.LaneVelocity = Mathf.MoveTowards(runner.LaneVelocity,velocity,(simulation.IsBursting(id)?7.6f:3.8f) * dt);
            float next = Mathf.Clamp(runner.Lane + runner.LaneVelocity * dt,-limit,limit);
            if (!CanSweep(id,runner.Lane,next,simulation)) { next = runner.Lane; runner.LaneVelocity = 0; }
            runner.Lane = next;
            if(simulation.IsAirborne(id))return desiredSpeed;
            float speedLimit = desiredSpeed;
            float metric = simulation.Metric((float)runner.Distance,runner.Lane);
            for (int other = 0; other < lanes.Length; other++)
            {
                if (other == id || simulation.IsAirborne(other)) continue;
                float gap = Gap(id,other,simulation) * metric;
                float lateral = Mathf.Abs(lanes[other] - next);
                if (gap > 0 && gap < 14 && lateral < Clearance)
                {
                    float cap = Mathf.Max(0,speeds[other] + (gap - FollowingGap) * 1.8f);
                    if (cap < speedLimit) { speedLimit = cap; runner.BlockedBy = other; }
                    runner.IsContesting = true;
                }
                if (Mathf.Abs(gap) < 7 && lateral < 5.2f) runner.IsContesting = true;
            }
            return speedLimit;
        }

        public double LimitAdvance(int id, Data_RaceSimulation simulation, double advance)
        {
            if(simulation.IsAirborne(id))return advance;
            var runner = simulation.Runners[id];
            float metric = simulation.Metric((float)runner.Distance,runner.Lane);
            for (int other = 0; other < lanes.Length; other++)
            {
                if (other == id || simulation.IsAirborne(other) || Mathf.Abs(lanes[other] - runner.Lane) >= Clearance) continue;
                float gap = Gap(id,other,simulation);
                if (gap <= 0) continue;
                double available = gap - FollowingGap / metric + speeds[other] / metric * Data_RaceSimulation.StepSeconds;
                advance = System.Math.Min(advance,System.Math.Max(0,available));
            }
            return advance;
        }

        private float Gap(int from, int to, Data_RaceSimulation simulation)
        {
            // Physical neighbours can straddle the lap line or be on different laps.
            float length = simulation.LapLength;
            return Mathf.Repeat((float)(distances[to]-distances[from]) + length*.5f,length)-length*.5f;
        }
    }
}
