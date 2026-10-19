using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Trigger volume on the track. While live, the linked virtual camera gets <see cref="ActivePriority"/>;
    /// otherwise it falls back to <see cref="InactivePriority"/>.
    /// Trigger = Protagonist: live while the protagonist probe is inside (or for LiveSeconds after it enters). Original behavior.
    /// Trigger = Leader: live for LiveSeconds after whoever is 1st place enters the zone.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaceCameraZone : MonoBehaviour
    {
        public enum E_Trigger { Protagonist, Leader }

        [Header("Camera")]
        [Tooltip("Virtual camera shown while this zone is live. Accepts CinemachineCamera or camera groups (Sequencer / ClearShot).")]
        public Unity.Cinemachine.CinemachineVirtualCameraBase ZoneCamera;
        [Tooltip("HUD shot label shown while this zone is live.")]
        public string ShotLabel = "중계";

        [Header("Trigger")]
        [Tooltip("Protagonist: the protagonist entering turns the camera on (original).\nLeader: whoever is 1st place entering turns the camera on.")]
        public E_Trigger Trigger = E_Trigger.Protagonist;
        [Tooltip("Seconds (race time, follows playback speed / pause) the camera keeps priority after the trigger enters.\n" +
                 "Protagonist: 0 = while inside (original). Leader: 0 = 3 s.")]
        [Min(0f)] public float LiveSeconds = 0f;
        [Tooltip("Leader trigger only: aim the zone camera (Follow / LookAt) at the leader while live, then restore its previous targets.")]
        public bool FrameLeader = true;
        [Tooltip("Race rig (runner views / race). Found automatically if empty.")]
        public Sys_RaceCinemachine Rig;

        [Header("Priority")]
        public int ActivePriority = 20;
        public int InactivePriority = 0;

        public bool IsLive { get; private set; }
        /// <summary>Runner that triggered the current live period (-1 = protagonist trigger / none).</summary>
        public int TriggerRunner { get; private set; } = -1;

        private const float DefaultLeaderSeconds = 3f;
        private int inside;
        private double enteredAt = double.NegativeInfinity;
        private int generation = -1;
        private int leaderId = -1;
        private bool leaderInside;
        private bool retargeted;
        private Transform savedFollow, savedLookAt;
        private BoxCollider box;

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            if (Rig == null) Rig = FindFirstObjectByType<Sys_RaceCinemachine>();
            Refresh(true);
        }

        private void OnDisable()
        {
            inside = 0; enteredAt = double.NegativeInfinity; leaderId = -1; leaderInside = false;
            Refresh(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Trigger != E_Trigger.Protagonist || other.GetComponent<RaceCameraProbe>() == null) return;
            if (inside++ == 0) { enteredAt = RaceTime(); TriggerRunner = -1; }
            Refresh(false);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<RaceCameraProbe>() == null) return;
            inside = Mathf.Max(0, inside - 1);
            Refresh(false);
        }

        private void Update()
        {
            var race = Rig != null ? Rig.Race : null;
            if (race != null && race.Generation != generation)
            {
                // Race restarted: race time is back to 0, drop the old timer.
                generation = race.Generation;
                enteredAt = double.NegativeInfinity; leaderId = -1; leaderInside = false;
            }
            if (Trigger == E_Trigger.Leader) TrackLeader(race);
            Refresh(false);
        }

        private void TrackLeader(Sys_Race race)
        {
            var sim = race != null ? race.Simulation : null;
            var views = Rig != null && Rig.Presentation != null ? Rig.Presentation.Views : null;
            if (sim == null || views == null || sim.Order == null || sim.Order.Length == 0) return;
            int id = sim.Order[0];
            bool now = id >= 0 && id < views.Length && views[id] != null && Contains(views[id].transform.position);
            bool running = (sim.Phase == E_RacePhase.Running || sim.Phase == E_RacePhase.Finishing) && !race.IsSkipping;
            // Only a real entry counts: the same leader going outside -> inside (an overtake inside the zone does not retrigger).
            if (running && id == leaderId && now && !leaderInside) { enteredAt = sim.Time; TriggerRunner = id; }
            leaderId = id; leaderInside = now;
        }

        private bool Contains(Vector3 world)
        {
            if (box == null) return false;
            Vector3 p = transform.InverseTransformPoint(world) - box.center, half = box.size * 0.5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }

        private double RaceTime()
        {
            var sim = Rig != null && Rig.Race != null ? Rig.Race.Simulation : null;
            return sim != null ? sim.Time : Time.timeAsDouble;
        }

        private bool TimedLive(float seconds)
        {
            double elapsed = RaceTime() - enteredAt;
            return elapsed >= 0 && elapsed < seconds;
        }

        private void Refresh(bool force)
        {
            bool live = Trigger == E_Trigger.Leader
                ? TimedLive(LiveSeconds > 0f ? LiveSeconds : DefaultLeaderSeconds)
                : LiveSeconds > 0f ? TimedLive(LiveSeconds) : inside > 0;
            if (live == IsLive && !force) return;
            IsLive = live;
            Apply();
        }

        private void Apply()
        {
            if (ZoneCamera == null) return;
            if (IsLive && Trigger == E_Trigger.Leader && FrameLeader) FrameRunner(TriggerRunner);
            else if (!IsLive) RestoreTargets();
            ZoneCamera.Priority = IsLive ? ActivePriority : InactivePriority;
        }

        private void FrameRunner(int id)
        {
            var views = Rig != null && Rig.Presentation != null ? Rig.Presentation.Views : null;
            if (views == null || id < 0 || id >= views.Length || views[id] == null) return;
            if (!retargeted) { savedFollow = ZoneCamera.Follow; savedLookAt = ZoneCamera.LookAt; retargeted = true; }
            var view = views[id];
            ZoneCamera.Follow = view.transform;
            ZoneCamera.LookAt = view.FaceTarget != null ? view.FaceTarget : view.transform;
            ZoneCamera.PreviousStateIsValid = false; // cut onto the leader
        }

        private void RestoreTargets()
        {
            if (!retargeted) return;
            ZoneCamera.Follow = savedFollow; ZoneCamera.LookAt = savedLookAt;
            retargeted = false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = IsLive ? new Color(1f, .55f, .1f, .25f) : new Color(.1f, .8f, 1f, .12f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(.1f, .8f, 1f, .8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
#endif
    }
}
