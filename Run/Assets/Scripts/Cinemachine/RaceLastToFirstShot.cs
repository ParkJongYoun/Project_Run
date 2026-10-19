using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Zone shot: while <see cref="Zone"/> is live, frames the last runner for <see cref="LastHoldSeconds"/>,
    /// glides along the track to the leader in <see cref="MoveSeconds"/> (camera keeps the same angle to the track),
    /// then stays on the leader until the zone ends.
    /// <see cref="Camera"/> must Follow/LookAt <see cref="Target"/> and must NOT be in Sys_RaceCinemachine.ProtagonistCameras.
    /// </summary>
    public sealed class RaceLastToFirstShot : MonoBehaviour
    {
        public enum E_State { Idle, HoldLast, Move, HoldFirst }

        [Header("Refs")]
        public Sys_Race Race;
        public RaceCameraZone Zone;
        public Unity.Cinemachine.CinemachineCamera Camera;
        [Tooltip("Empty transform moved along the track. Camera.Follow and Camera.LookAt point here.")]
        public Transform Target;

        [Header("Timing (race seconds, follows PlaybackSpeed / pause)")]
        [Tooltip("How long the last runner is shown before the camera starts moving.")]
        [Min(0f)] public float LastHoldSeconds = 1f;
        [Tooltip("How long the move from the last runner to the leader takes.")]
        [Min(0.05f)] public float MoveSeconds = 2.5f;
        [Tooltip("Move progress curve (x: time 0-1, y: last 0 -> leader 1).")]
        public AnimationCurve MoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Framing")]
        [Tooltip("Height of the framing point above the track (m).")]
        public float TargetHeight = 1.2f;

        public E_State State { get; private set; }
        public int LastId { get; private set; } = -1;
        public int FirstId { get; private set; } = -1;

        private float timer;
        private bool wasLive;
        private int generation = -1;

        // Sys_Race (order -200) steps the simulation in Update; this runs after it and before the Brain (LateUpdate).
        private void Update()
        {
            if (Race == null || Race.Simulation == null || Zone == null || Target == null) return;
            var sim = Race.Simulation;
            if (generation != Race.Generation) { generation = Race.Generation; wasLive = false; State = E_State.Idle; }

            bool live = Zone.IsLive && !Race.IsSkipping && sim.Runners.Length > 1;
            if (live && !wasLive) Begin();
            wasLive = live;
            if (!live) { State = E_State.Idle; return; }

            if (!Race.Paused && !Race.PresentationHeld)
                timer += Time.unscaledDeltaTime * Mathf.Max(0f, Race.PlaybackSpeed);

            float k;
            if (timer < LastHoldSeconds) { State = E_State.HoldLast; k = 0f; }
            else if (timer < LastHoldSeconds + MoveSeconds) { State = E_State.Move; k = MoveCurve.Evaluate((timer - LastHoldSeconds) / MoveSeconds); }
            else { State = E_State.HoldFirst; k = 1f; }
            Place(k);
        }

        private void Begin()
        {
            var order = Race.Simulation.Order;
            LastId = order[order.Length - 1];
            FirstId = order[0];
            timer = 0f;
            State = E_State.HoldLast;
            Place(0f);
            if (Camera != null) Camera.PreviousStateIsValid = false; // cut straight to the last runner
        }

        private void Place(float k)
        {
            var sim = Race.Simulation;
            float i = Mathf.Clamp01(Race.Interpolation); // same smoothing as RaceRunnerView
            var a = sim.Runners[LastId];
            var b = sim.Runners[FirstId];
            double dA = a.PreviousDistance + (a.Distance - a.PreviousDistance) * i;
            double dB = b.PreviousDistance + (b.Distance - b.PreviousDistance) * i;
            float d = (float)(dA + (dB - dA) * k); // along the track, so bends are followed
            float lane = Mathf.Lerp(Mathf.Lerp(a.PreviousLane, a.Lane, i), Mathf.Lerp(b.PreviousLane, b.Lane, i), k);
            Target.SetPositionAndRotation(sim.Point(d, lane) + Vector3.up * TargetHeight,
                Quaternion.LookRotation(sim.Forward(d), Vector3.up));
        }
    }
}
