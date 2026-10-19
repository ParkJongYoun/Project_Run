using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Race start intro using three hand-placed virtual cameras (no procedural camera movement):
    /// 1) In camera is shown (cut).
    /// 2) Out camera takes over; the in->out move is the Brain blend (time/curve set in the Custom Blends asset).
    /// 3) Hold on the out camera for <see cref="OutHoldSeconds"/>.
    /// 4) Cut to the center camera, release the race: countdown 3,2,1, start.
    /// 5) Center camera is then owned by <see cref="StartZone"/>: it stays until the protagonist leaves the start zone.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class RaceStartIntro : MonoBehaviour
    {
        public static bool Playing { get; private set; }

        [Header("Timeline (Optional)")]
        [Tooltip("If assigned, playback is driven by this Timeline (PlayableDirector).")]
        public UnityEngine.Playables.PlayableDirector Director;

        [Header("Refs")]
        public Sys_Race Race;
        public Unity.Cinemachine.CinemachineBrain Brain;
        public Unity.Cinemachine.CinemachineCamera InCamera;
        public Unity.Cinemachine.CinemachineCamera OutCamera;
        [Tooltip("Shown for the countdown, then held by StartZone until the protagonist leaves it.")]
        public Unity.Cinemachine.CinemachineCamera CenterCamera;
        [Tooltip("Zone whose ZoneCamera is CenterCamera.")]
        public RaceCameraZone StartZone;

        [Header("Timing")]
        [Tooltip("Hold on the out camera after the in->out blend finishes.")]
        public float OutHoldSeconds = 0.5f;
        [Tooltip("Safety: max seconds to wait for any single camera step.")]
        public float StepTimeout = 20f;
        public int IntroPriority = 50;

        private enum Phase { Idle, Timeline, ShowIn, Sweep, OutHold, Center, Countdown }
        private Phase phase = Phase.Idle;
        private float t;
        private int generation = -1;

        private void OnEnable() { Mgr_Event.Reset += SyncGeneration; }
        private void OnDisable() { Mgr_Event.Reset -= SyncGeneration; if (phase != Phase.Idle) Finish(); }

        // Begin as early as possible so the very first rendered frame already shows the timeline,
        // not the priority-based cameras (Brain evaluates in LateUpdate before this script's LateUpdate).
        private void Start() { SyncGeneration(); }

        private void SyncGeneration()
        {
            if (Race == null || Race.Simulation == null || InCamera == null || OutCamera == null || CenterCamera == null) return;
            if (generation == Race.Generation) return;
            generation = Race.Generation;
            if (phase != Phase.Idle) Finish();
            if (Race.Simulation.Phase == E_RacePhase.Preparing && !Race.IsSkipping) Begin();
        }

        private void LateUpdate()
        {
            if (Race == null || Race.Simulation == null || InCamera == null || OutCamera == null || CenterCamera == null) return;
            SyncGeneration();
            if (phase == Phase.Idle) return;

            t += Time.unscaledDeltaTime;
            bool timedOut = t > StepTimeout;
            switch (phase)
            {
                case Phase.Timeline:
                    // Release the race so the countdown runs during the timeline's tail:
                    // countdown reaches 0 exactly when the timeline ends.
                    if (Director == null || Director.state != UnityEngine.Playables.PlayState.Playing || timedOut
                        || Director.duration - Director.time <= CountdownRealSeconds())
                    {
                        if (CenterCamera != null) CenterCamera.Priority = IntroPriority + 2;
                        Race.ReleasePresentationHold(this);
                        Playing = false;
                        Next(Phase.Countdown);
                    }
                    break;
                case Phase.ShowIn:
                    if (IsLive(InCamera) || timedOut) { OutCamera.Priority = IntroPriority + 1; Next(Phase.Sweep); }
                    break;
                case Phase.Sweep:
                    if (IsLive(OutCamera) || timedOut) Next(Phase.OutHold);
                    break;
                case Phase.OutHold:
                    if (t >= OutHoldSeconds) { CenterCamera.Priority = IntroPriority + 2; Next(Phase.Center); }
                    break;
                case Phase.Center:
                    if (IsLive(CenterCamera) || timedOut)
                    {
                        Race.ReleasePresentationHold(this);
                        Playing = false;
                        Next(Phase.Countdown);
                    }
                    break;
                case Phase.Countdown:
                    if (Race.Countdown <= 0) { Finish(); break; }
                    // Timeline keeps the camera through the countdown; lock its clock to the countdown
                    // (handles PlaybackSpeed / pause) so both end on the same frame.
                    if (Director != null && Director.state == UnityEngine.Playables.PlayState.Playing)
                        Director.time = System.Math.Max(0, Director.duration - CountdownRealSeconds());
                    break;
            }
        }

        private void Begin()
        {
            if (!Race.AcquirePresentationHold(this)) return;
            Playing = true;

            if (Director != null && Director.playableAsset != null)
            {
                Director.timeUpdateMode = UnityEngine.Playables.DirectorUpdateMode.UnscaledGameTime;
                Director.time = 0;
                Director.Play();
                // Push the first timeline frame (camera override) into the Brain right now.
                Director.Evaluate();
                Next(Phase.Timeline);
                return;
            }

            InCamera.Priority = IntroPriority;
            OutCamera.Priority = 0;
            CenterCamera.Priority = 0;
            Next(Phase.ShowIn);
        }

        /// <summary>Camera is the Brain's live camera and no blend is in progress.</summary>
        private bool IsLive(Unity.Cinemachine.CinemachineCamera cam)
        {
            if (Brain == null) return true;
            return ReferenceEquals(Brain.ActiveVirtualCamera, cam) && !Brain.IsBlending;
        }

        private void Next(Phase p) { phase = p; t = 0; }

        /// <summary>Remaining countdown in unscaled seconds (countdown ticks at PlaybackSpeed).</summary>
        private float CountdownRealSeconds() => Race.Countdown / Mathf.Max(0.01f, Race.PlaybackSpeed);

        private void Finish()
        {
            if (Director != null && Director.state == UnityEngine.Playables.PlayState.Playing)
            {
                Director.Stop();
            }
            if (Race != null) Race.ReleasePresentationHold(this);
            Playing = false;
            phase = Phase.Idle;
            if (InCamera != null) InCamera.Priority = 0;
            if (OutCamera != null) OutCamera.Priority = 0;
            // hand the center camera back to the start zone (stays live until the protagonist exits the zone)
            if (CenterCamera != null)
                CenterCamera.Priority = StartZone != null && StartZone.IsLive ? StartZone.ActivePriority : StartZone != null ? StartZone.InactivePriority : 0;
        }
    }
}
