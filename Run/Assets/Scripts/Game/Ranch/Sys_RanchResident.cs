using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectRun.Ranch
{
    // Decorative village activity is independent of race simulation; residents steer and route around one another.
    public sealed class Sys_RanchResident : MonoBehaviour
    {
        public Sys_RanchPlacement Placement;
        public Animator TargetAnimator;
        public AnimationClip IdleClip;
        public AnimationClip LocomotionClip;
        public AnimationClip CelebrationClip;
        public AnimationClip AttackClip;
        public AnimationClip EatClip;
        public AnimationClip RestClip;
        public Transform FeedingHead;
        public float MoveSpeed = .85f;
        public float AnimationRate = .65f;
        public float HeadingOffset;
        public float Clearance = .38f;
        public int Seed = 41;
        private static readonly List<Sys_RanchResident> residents = new List<Sys_RanchResident>();
        private enum Phase { Roam, Approach, Enter, Activity, Lap, Exit }
        private Phase phase;
        private RanchFacilitySpot spot;
        private E_RanchActivity lastActivity = (E_RanchActivity)(-1);
        private readonly List<Vector3> route = new List<Vector3>();
        private System.Random random;
        private PlayableGraph graph;
        private AnimationClipPlayable idle, move, action;
        private AnimationMixerPlayable mixer;
        private AnimationClip activeClip;
        private double idleClock, moveClock, actionClock;
        private float moveBlend, actionBlend, pause, activityRemaining, visitDelay, groundY, blockedTime;
        private Vector3 animatorPosition;
        private Quaternion animatorRotation;
        private Quaternion feedingRotation;
        private bool feedingPoseApplied;
        private bool dirty = true;
        private int waypoint;
        private bool[] roads;
        private int[] previous;
        private readonly List<int> reachable = new List<int>();
        private readonly Queue<int> frontier = new Queue<int>();

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (!residents.Contains(this)) residents.Add(this);
            random = new System.Random(Seed); groundY = transform.position.y;
            phase = Phase.Roam; dirty = true; route.Clear(); waypoint = 0;
            pause = 1 + (float)random.NextDouble() * 3;
            visitDelay = 3 + (float)random.NextDouble() * 10;
            if (Placement != null) Placement.Changed += InvalidateRoute;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (TargetAnimator == null || IdleClip == null || LocomotionClip == null) return;
            animatorPosition = TargetAnimator.transform.localPosition;
            animatorRotation = TargetAnimator.transform.localRotation;
            TargetAnimator.applyRootMotion = false;
            TargetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("RanchResident_" + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            idle = AnimationClipPlayable.Create(graph, IdleClip);
            move = AnimationClipPlayable.Create(graph, LocomotionClip);
            mixer = AnimationMixerPlayable.Create(graph, 3);
            graph.Connect(idle, 0, mixer, 0); graph.Connect(move, 0, mixer, 1);
            AnimationPlayableOutput.Create(graph, "resident", TargetAnimator).SetSourcePlayable(mixer);
            graph.Play();
        }

        private void InvalidateRoute()
        {
            ReleaseVisit(); route.Clear(); waypoint = 0; dirty = true; phase = Phase.Roam;
        }

        private void Update()
        {
            if (Placement == null || Placement.Layout == null) return;
            float dt = Time.deltaTime;
            bool walking = false;
            bool blocked = Placement.IsBuilding || Placement.InputBlocked;
            if (!blocked)
            {
                visitDelay -= dt;
                if (phase != Phase.Roam && (spot == null || !spot.isActiveAndEnabled || spot.Occupant != this))
                    InvalidateRoute();
                if (dirty)
                {
                    BuildRoads(); BuildReachable();
                    int nearest = NearestRoad(transform.position, false, true);
                    if (nearest >= 0) transform.position = Center(nearest);
                    dirty = false; pause = Mathf.Min(pause, .5f);
                }
                if (phase == Phase.Activity)
                {
                    activityRemaining -= dt;
                    if (activityRemaining <= 0) BeginExit();
                }
                else if (pause > 0) pause -= dt;
                else if (waypoint < route.Count)
                {
                    Vector3 goal = route[waypoint];
                    Vector3 delta = goal - transform.position;
                    if (delta.sqrMagnitude < .008f && CanMoveTo(goal))
                    {
                        transform.position = goal; waypoint++;
                        if (waypoint >= route.Count) Arrived();
                    }
                    else
                    {
                        Vector3 before = transform.position;
                        float speed = MoveSpeed * (phase == Phase.Lap ? 1.7f : 1);
                        Vector3 next = Vector3.MoveTowards(before, goal, speed * dt);
                        Vector3 direction = new Vector3(delta.x, 0, delta.z).normalized;
                        bool direct = CanMoveTo(next) && CanMoveTo(before + direction * Mathf.Min(.42f, delta.magnitude));
                        bool canAdvance = direct;
                        if (!direct && (phase == Phase.Roam || phase == Phase.Approach || phase == Phase.Exit))
                        {
                            // Prefer the right-hand passing lane; keep detours on actual road cells.
                            foreach (float angle in new[] { 50f, -50f, 85f, -85f, 115f, -115f })
                            {
                                Vector3 steer = Quaternion.Euler(0, angle, 0) * direction;
                                Vector3 probe = before + steer * .45f;
                                Vector3 step = before + steer * speed * dt;
                                if (!OnRoad(probe) || !OnRoad(step) || !CanMoveTo(probe) || !CanMoveTo(step)) continue;
                                next = step; canAdvance = true; break;
                            }
                        }
                        if (canAdvance)
                        {
                            Vector3 flat = next - before; flat.y = 0;
                            if (flat.sqrMagnitude > .000001f)
                            {
                                Quaternion facing = Quaternion.LookRotation(flat) * Quaternion.Euler(0, HeadingOffset, 0);
                                transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, 260 * dt);
                            }
                            transform.position = next; walking = true;
                        }
                        blockedTime = direct ? 0 : blockedTime + dt;
                        if (blockedTime > 1.05f + (Seed % 7) * .08f && (phase == Phase.Roam || phase == Phase.Approach))
                            ReplanAroundResidents();
                    }
                }
                else ChooseDestination();
            }
            Animate(walking, !blocked && phase == Phase.Activity, dt);
        }

        private bool OnRoad(Vector3 point)
        {
            int x = Mathf.FloorToInt((point.x - Placement.GridOrigin.x) / Placement.CellSize);
            int y = Mathf.FloorToInt((point.z - Placement.GridOrigin.z) / Placement.CellSize);
            return roads != null && x >= 0 && y >= 0 && x < Placement.GridSize.x && y < Placement.GridSize.y && roads[y * Placement.GridSize.x + x];
        }

        private bool Occupied(Vector3 point)
        {
            foreach (var other in residents)
            {
                if (other == null || other == this || !other.isActiveAndEnabled || other.Placement != Placement) continue;
                Vector3 delta = point - other.transform.position; delta.y = 0;
                float distance = Clearance + other.Clearance + .10f;
                if (delta.sqrMagnitude < distance * distance) return true;
            }
            return false;
        }

        private bool CanMoveTo(Vector3 point)
        {
            Vector3 start = transform.position; start.y = 0; point.y = 0;
            Vector3 step = point - start;
            foreach (var other in residents)
            {
                if (other == null || other == this || !other.isActiveAndEnabled || other.Placement != Placement) continue;
                Vector3 obstacle = other.transform.position; obstacle.y = 0;
                float clearance = Clearance + other.Clearance;
                float initial = (start - obstacle).sqrMagnitude;
                // If a construction edit places two residents together, allow only separation.
                if (initial < clearance * clearance && (point - obstacle).sqrMagnitude > initial + .00001f) continue;
                float along = step.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(obstacle - start, step) / step.sqrMagnitude) : 0;
                if ((start + step * along - obstacle).sqrMagnitude < clearance * clearance) return false;
            }
            return true;
        }

        private void ReplanAroundResidents()
        {
            blockedTime = 0;
            BuildReachable();
            if (phase == Phase.Approach && spot != null)
            {
                Vector3 entry = spot.transform.TransformPoint(spot.Entry);
                int end = NearestRoad(entry, true);
                if (end >= 0 && (Center(end) - entry).sqrMagnitude < 7)
                { SetRoadRoute(end); route.Add(entry); return; }
                ReleaseVisit(); phase = Phase.Roam; visitDelay = 3;
            }
            route.Clear(); waypoint = 0;
            if (reachable.Count > 0) SetRoadRoute(reachable[random.Next(reachable.Count)]);
            if (route.Count == 0) pause = .3f + (float)random.NextDouble() * .4f;
        }

        private void Animate(bool walking, bool acting, float dt)
        {
            if (!graph.IsValid()) return;
            if (feedingPoseApplied && FeedingHead != null) FeedingHead.localRotation = feedingRotation;
            feedingPoseApplied = false;
            moveBlend = Mathf.MoveTowards(moveBlend, walking ? 1 : 0, dt * 5);
            actionBlend = Mathf.MoveTowards(actionBlend, acting ? 1 : 0, dt * 5);
            idleClock += dt; moveClock += dt * AnimationRate * (phase == Phase.Lap ? 1.6f : 1);
            idle.SetTime(idleClock % Math.Max(.01, IdleClip.length));
            move.SetTime(moveClock % Math.Max(.01, LocomotionClip.length));
            if (activeClip != null && action.IsValid())
            {
                if (acting) actionClock += dt;
                action.SetTime(actionClock % Math.Max(.01, activeClip.length));
            }
            mixer.SetInputWeight(0, (1 - actionBlend) * (1 - moveBlend));
            mixer.SetInputWeight(1, (1 - actionBlend) * moveBlend);
            mixer.SetInputWeight(2, actionBlend);
            graph.Evaluate(0);
            if (TargetAnimator.transform != transform)
            {
                TargetAnimator.transform.localPosition = animatorPosition;
                TargetAnimator.transform.localRotation = animatorRotation;
            }
            if (acting && spot != null && spot.Activity == E_RanchActivity.Feeding && EatClip == null && FeedingHead != null)
            {
                feedingRotation = FeedingHead.localRotation;
                float nod = 14 + Mathf.Sin((float)actionClock * 5) * 4;
                FeedingHead.Rotate(transform.right, nod, Space.World);
                feedingPoseApplied = true;
            }
        }

        private void ChooseDestination()
        {
            BuildReachable();
            if (reachable.Count == 0) { pause = 2; return; }
            if (visitDelay <= 0 && TryVisit()) return;
            int end = reachable[random.Next(reachable.Count)];
            SetRoadRoute(end); phase = Phase.Roam;
            if (route.Count == 0) pause = 2;
        }

        private bool TryVisit()
        {
            var candidates = new List<RanchFacilitySpot>();
            foreach (var entry in Placement.Layout.Facilities)
            {
                var candidate = Placement.FindActivitySpot(entry.InstanceId);
                if (candidate != null && candidate.isActiveAndEnabled && candidate.Occupant == null &&
                    candidate.Activity != lastActivity) candidates.Add(candidate);
            }
            while (candidates.Count > 0)
            {
                int index = random.Next(candidates.Count);
                var candidate = candidates[index]; candidates.RemoveAt(index);
                Vector3 entry = candidate.transform.TransformPoint(candidate.Entry);
                int cell = NearestRoad(entry, true);
                if (cell < 0 || (Center(cell) - entry).sqrMagnitude > 7 || !candidate.TryReserve(this)) continue;
                spot = candidate;
                SetRoadRoute(cell);
                route.Add(entry); phase = Phase.Approach; pause = 0;
                return true;
            }
            visitDelay = 3; return false;
        }

        private void Arrived()
        {
            route.Clear(); waypoint = 0;
            if (phase == Phase.Roam) { pause = 1 + (float)random.NextDouble() * 3; return; }
            if (spot == null) { InvalidateRoute(); return; }
            if (phase == Phase.Approach)
            {
                if (spot.Activity == E_RanchActivity.Track && spot.TrackPoints != null && spot.TrackPoints.Length > 1)
                {
                    foreach (var p in spot.TrackPoints) route.Add(spot.transform.TransformPoint(p));
                    phase = Phase.Lap;
                }
                else
                {
                    if (spot.EntryPoints != null)
                        foreach (var point in spot.EntryPoints) route.Add(spot.transform.TransformPoint(point));
                    route.Add(spot.transform.TransformPoint(spot.ActionPoint)); phase = Phase.Enter;
                }
            }
            else if (phase == Phase.Enter) BeginActivity();
            else if (phase == Phase.Lap) BeginExit();
            else if (phase == Phase.Exit)
            {
                lastActivity = spot.Activity;
                ReleaseVisit(); phase = Phase.Roam; pause = 1;
                visitDelay = 7 + (float)random.NextDouble() * 12;
            }
        }

        private void BeginActivity()
        {
            phase = Phase.Activity; activityRemaining = spot.Duration;
            Vector3 facing = spot.transform.TransformDirection(spot.Facing);
            if (facing.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(facing) * Quaternion.Euler(0, HeadingOffset, 0);
            AnimationClip clip = IdleClip;
            if (spot.Activity == E_RanchActivity.Training) clip = AttackClip;
            if (spot.Activity == E_RanchActivity.Feeding) clip = EatClip;
            if (spot.Activity == E_RanchActivity.Rest) clip = RestClip;
            clip = clip != null ? clip : IdleClip;
            if (!graph.IsValid() || clip == null) return;
            if (action.IsValid()) { graph.Disconnect(mixer, 2); graph.DestroyPlayable(action); }
            action = AnimationClipPlayable.Create(graph, clip);
            graph.Connect(action, 0, mixer, 2); activeClip = clip; actionClock = 0; actionBlend = 0;
        }

        private void BeginExit()
        {
            route.Clear(); waypoint = 0; phase = Phase.Exit;
            if (spot == null) { InvalidateRoute(); return; }
            if (spot.Activity != E_RanchActivity.Track && spot.EntryPoints != null)
                for (int i = spot.EntryPoints.Length - 1; i >= 0; i--)
                    route.Add(spot.transform.TransformPoint(spot.EntryPoints[i]));
            route.Add(spot.transform.TransformPoint(spot.Entry));
            int cell = NearestRoad(spot.transform.TransformPoint(spot.Entry));
            if (cell >= 0) route.Add(Center(cell));
        }

        private void ReleaseVisit()
        {
            if (spot != null) spot.Release(this);
            spot = null;
        }

        private void BuildRoads()
        {
            int count = Placement.GridSize.x * Placement.GridSize.y;
            roads = new bool[count]; previous = new int[count];
            foreach (var entry in Placement.Layout.Facilities)
            {
                var definition = Placement.Catalog.Find(entry.FacilityId);
                if (definition == null || !definition.IsRoad) continue;
                var size = definition.Size(entry.Rotation);
                for (int y = entry.Y; y < entry.Y + size.y; y++)
                    for (int x = entry.X; x < entry.X + size.x; x++)
                        if (x >= 0 && y >= 0 && x < Placement.GridSize.x && y < Placement.GridSize.y)
                            roads[y * Placement.GridSize.x + x] = true;
            }
        }

        private void BuildReachable()
        {
            if (roads == null) BuildRoads();
            reachable.Clear(); frontier.Clear();
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            int start = NearestRoad(transform.position);
            if (start < 0) return;
            previous[start] = start; frontier.Enqueue(start);
            int width = Placement.GridSize.x, height = Placement.GridSize.y;
            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue(); reachable.Add(current);
                int x = current % width, y = current / width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                    int ny = y + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    int next = ny * width + nx;
                    if (!roads[next] || previous[next] >= 0 || Occupied(Center(next))) continue;
                    previous[next] = current; frontier.Enqueue(next);
                }
            }
        }

        private int NearestRoad(Vector3 point, bool connected = false, bool avoidResidents = false)
        {
            if (roads == null) return -1;
            int nearest = -1; float best = float.PositiveInfinity;
            for (int i = 0; i < roads.Length; i++)
            {
                if (!roads[i] || (connected && previous[i] < 0)) continue;
                if (avoidResidents && Occupied(Center(i))) continue;
                float distance = (Center(i) - point).sqrMagnitude;
                if (distance < best) { best = distance; nearest = i; }
            }
            return nearest;
        }

        private void SetRoadRoute(int end)
        {
            route.Clear(); waypoint = 0;
            if (end < 0 || previous[end] < 0) return;
            int limit = previous.Length;
            while (limit-- > 0)
            {
                route.Add(Center(end));
                if (previous[end] == end) break;
                end = previous[end];
            }
            route.Reverse();
            if (route.Count > 1 && (route[0] - transform.position).sqrMagnitude < Placement.CellSize * Placement.CellSize * .8f)
                route.RemoveAt(0);
        }

        private Vector3 Center(int index) => Placement.GridOrigin +
            new Vector3((index % Placement.GridSize.x + .5f) * Placement.CellSize, groundY,
                (index / Placement.GridSize.x + .5f) * Placement.CellSize);

        private void OnDisable()
        {
            if (feedingPoseApplied && FeedingHead != null) FeedingHead.localRotation = feedingRotation;
            feedingPoseApplied = false;
            residents.Remove(this);
            ReleaseVisit();
            if (Placement != null) Placement.Changed -= InvalidateRoute;
            if (graph.IsValid()) graph.Destroy();
        }
    }
}
