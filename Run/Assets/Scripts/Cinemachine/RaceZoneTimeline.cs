using UnityEngine;
using UnityEngine.Playables;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Plays <see cref="Director"/> from 0 when <see cref="Zone"/> goes live, and stops it when the zone
    /// turns off or the race resets. Use for zone cameras animated on a Timeline (e.g. CM_Arc_Orbit).
    /// </summary>
    // Runs before CinemachineBrain (100) so the animated + matched axis values are used on the same frame.
    [DefaultExecutionOrder(90)]
    public sealed class RaceZoneTimeline : MonoBehaviour
    {
        public enum E_StartMode
        {
            [Tooltip("Original: cut to the authored start pose.")] Authored,
            [Tooltip("Start exactly at the previous camera. The whole spin is rotated to begin there; height/distance ease into the authored path.")] FromPrevious,
            [Tooltip("Start exactly at the previous camera, then angle/height/distance all ease into the authored path.")] Blend,
        }

        [Tooltip("Zone whose live state drives the timeline.")]
        public RaceCameraZone Zone;
        [Tooltip("Director holding the camera animation. Play On Awake should be off.")]
        public PlayableDirector Director;

        [Header("Seamless Start (Orbital Follow)")]
        [Tooltip("How the orbit starts relative to the camera that was showing right before it.")]
        public E_StartMode StartMode = E_StartMode.FromPrevious;
        [Tooltip("Seconds (timeline time) to ease the matched start into the authored animation.")]
        [Min(0.01f)] public float MatchSeconds = 1.5f;

        private bool wasLive;
        private bool running;
        private Unity.Cinemachine.CinemachineVirtualCameraBase vcam;
        private Unity.Cinemachine.CinemachineOrbitalFollow orbit;
        private float offsetH, offsetV, offsetR;
        private bool hasOffset;
        private Vector3 lastTargetPos;
        private Quaternion lastTargetRot = Quaternion.identity;
        private bool hasLastTarget;

        private void OnEnable() { Mgr_Event.Reset += Stop; }

        private void OnDisable()
        {
            Mgr_Event.Reset -= Stop;
            Stop();
            wasLive = false;
        }

        private void LateUpdate()
        {
            bool live = Zone != null && Zone.IsLive;
            if (live && !wasLive && Director != null) Begin();
            else if (!live && wasLive) { /* keep holding the pose until the brain has blended away (see below) */ }
            wasLive = live;

            if (running)
            {
                // Stop only once the camera is no longer visible, otherwise the animated values snap back mid blend-out.
                if (!live && (vcam == null || !Unity.Cinemachine.CinemachineCore.IsLive(vcam))) Stop();
                else ApplyOffsets();
            }
            CacheTarget();
        }

        private void Begin()
        {
            vcam = Zone.ZoneCamera;
            orbit = vcam != null ? vcam.GetComponent<Unity.Cinemachine.CinemachineOrbitalFollow>() : null;
            if (orbit != null)
            {
                // Offsets can push the angle past the authored range; don't let the axis clamp them.
                var r = orbit.HorizontalAxis.Range;
                if (r.x > -1080f || r.y < 1080f) orbit.HorizontalAxis.Range = new Vector2(Mathf.Min(r.x, -1080f), Mathf.Max(r.y, 1080f));
            }

            Director.time = 0;
            Director.Play();
            Director.Evaluate();
            running = true;

            hasOffset = StartMode != E_StartMode.Authored && orbit != null && TryMatchPrevious();
            ApplyOffsets();
            if (vcam != null) vcam.PreviousStateIsValid = false; // snap to the matched pose, no damping from stale state
        }

        /// <summary>Converts the pose the brain showed last frame into orbit axes and stores the difference to the authored start.</summary>
        private bool TryMatchPrevious()
        {
            var brain = Unity.Cinemachine.CinemachineCore.FindPotentialTargetBrain(vcam);
            var follow = vcam.Follow;
            if (brain == null || follow == null) return false;
            if (!hasLastTarget) { lastTargetPos = follow.position; lastTargetRot = follow.rotation; }

            Vector3 up = Vector3.up;
            Vector3 center = lastTargetPos + lastTargetRot * orbit.TargetOffset;
            Vector3 dir = brain.transform.position - center;
            float distance = dir.magnitude;
            if (distance < 0.01f) return false;

            Quaternion orient = ReferenceOrientation(lastTargetRot, up);
            Vector3 local = Quaternion.Inverse(orient) * (dir / distance);
            Vector3 e = Unity.Cinemachine.UnityVectorExtensions.SafeFromToRotation(Vector3.back, local, up).eulerAngles;
            float startV = orbit.VerticalAxis.ClampValue(Unity.Cinemachine.UnityVectorExtensions.NormalizeAngle(e.x));
            float startH = Unity.Cinemachine.UnityVectorExtensions.NormalizeAngle(e.y);

            // Values the timeline wrote for t = 0.
            offsetH = Mathf.DeltaAngle(orbit.HorizontalAxis.Value, startH);
            offsetV = startV - orbit.VerticalAxis.Value;
            offsetR = distance - orbit.Radius;
            return true;
        }

        private Quaternion ReferenceOrientation(Quaternion targetRot, Vector3 up)
        {
            switch (orbit.TrackerSettings.BindingMode)
            {
                case Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace:
                case Unity.Cinemachine.TargetTracking.BindingMode.LazyFollow:
                    return Quaternion.identity;
                case Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp:
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(targetRot * Vector3.forward, up);
                    return fwd.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(fwd, up) : Quaternion.identity;
                }
                default:
                    return targetRot;
            }
        }

        private void ApplyOffsets()
        {
            if (!hasOffset || orbit == null || Director == null) return;
            // Re-sample the timeline so the offset is always added to the clean animated value (never accumulated).
            Director.Evaluate();
            float t = Mathf.Clamp01((float)Director.time / MatchSeconds);
            float fade = 1f - t * t * (3f - 2f * t); // 1 -> 0, smoothstep
            float h = StartMode == E_StartMode.FromPrevious ? offsetH : offsetH * fade;
            orbit.HorizontalAxis.Value += h;
            orbit.VerticalAxis.Value = orbit.VerticalAxis.ClampValue(orbit.VerticalAxis.Value + offsetV * fade);
            orbit.Radius = Mathf.Max(0.1f, orbit.Radius + offsetR * fade);
        }

        private void CacheTarget()
        {
            var cam = Zone != null ? Zone.ZoneCamera : null;
            var follow = cam != null ? cam.Follow : null;
            if (follow == null) { hasLastTarget = false; return; }
            lastTargetPos = follow.position; lastTargetRot = follow.rotation; hasLastTarget = true;
        }

        private void Stop()
        {
            running = false; hasOffset = false;
            if (Director != null && Director.state == PlayState.Playing) Director.Stop();
        }
    }
}
