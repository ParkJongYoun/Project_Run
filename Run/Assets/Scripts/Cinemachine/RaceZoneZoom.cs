using UnityEngine;
using Unity.Cinemachine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Broadcast-operator zoom for a zone camera: once the protagonist passes <see cref="StartFraction"/> of the zone,
    /// the lens zooms toward a target framing following <see cref="ZoomCurve"/> (push in, ease back a little, push in again).
    /// The target FOV is recomputed every frame from the camera-to-protagonist distance, so the framing holds as the runner moves.
    /// Restores the authored FOV when the zone is no longer live.
    /// </summary>
    [DefaultExecutionOrder(0)] // after RaceRunnerView (-50) moves the runner, before the Cinemachine brain
    public sealed class RaceZoneZoom : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Zone that drives the camera. Found on this object if empty.")]
        public RaceCameraZone Zone;
        [Tooltip("Camera whose lens is zoomed. Uses the zone camera if empty.")]
        public CinemachineCamera Camera;

        [Header("When")]
        [Tooltip("Zoom starts when the protagonist has crossed this fraction of the zone (0 = entry, 1 = exit).")]
        [Range(0f, 1f)] public float StartFraction = 0.65f;

        [Header("Framing")]
        [Tooltip("Height (m) to fit in frame at full zoom: the runner plus some headroom.")]
        [Min(0.1f)] public float FrameHeight = 2.6f;
        [Tooltip("Fraction of the screen height FrameHeight fills at full zoom.")]
        [Range(0.05f, 1f)] public float ScreenFraction = 0.32f;
        [Tooltip("Narrowest FOV allowed (strongest zoom).")]
        [Range(1f, 60f)] public float MinFov = 5f;

        [Header("Motion")]
        [Tooltip("Zoom amount over time (s) after the start. 0 = authored FOV, 1 = target framing. " +
                 "Default: push in, ease back a little, push in again.")]
        public AnimationCurve ZoomCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.75f, 1f, 0f, 0f),
            new Keyframe(1.25f, 0.7f, 0f, 0f),
            new Keyframe(2.0f, 1f, 0f, 0f));
        [Tooltip("Lens response time (s). Small values follow the curve tightly; larger values feel heavier.")]
        [Min(0f)] public float LensSmoothing = 0.08f;

        public bool IsZooming { get; private set; }

        private float baseFov;
        private bool hasBase;
        private float elapsed = -1f;
        private float fovVelocity;

        private void Awake()
        {
            if (Zone == null) Zone = GetComponent<RaceCameraZone>();
            if (Camera == null && Zone != null) Camera = Zone.ZoneCamera as CinemachineCamera;
            if (Camera != null) { baseFov = Camera.Lens.FieldOfView; hasBase = true; }
        }

        private void OnDisable() => Restore();

        private void LateUpdate()
        {
            if (Camera == null || Zone == null || !hasBase) return;
            var rig = Zone.Rig;
            var race = rig != null ? rig.Race : null;
            var views = rig != null && rig.Presentation != null ? rig.Presentation.Views : null;
            int id = rig != null ? rig.ProtagonistId : -1;
            if (!Zone.IsLive || race == null || race.IsSkipping || views == null || id < 0 || id >= views.Length || views[id] == null)
            {
                Restore();
                return;
            }

            var hero = views[id];
            if (elapsed < 0f)
            {
                if (Progress(hero.transform) < StartFraction) return;
                elapsed = 0f; fovVelocity = 0f; IsZooming = true;
            }

            // Follow the race clock: hold while paused / held for a presentation.
            if (!race.Paused && !race.PresentationHeld) elapsed += Time.unscaledDeltaTime * race.PlaybackSpeed;

            Vector3 aim = Camera.LookAt != null ? Camera.LookAt.position : hero.transform.position + Vector3.up;
            float distance = Mathf.Max(0.1f, Vector3.Distance(Camera.transform.position, aim));
            float targetFov = 2f * Mathf.Atan(FrameHeight / ScreenFraction * 0.5f / distance) * Mathf.Rad2Deg;
            targetFov = Mathf.Clamp(targetFov, MinFov, baseFov);

            float wanted = Mathf.LerpUnclamped(baseFov, targetFov, ZoomCurve.Evaluate(elapsed));
            float fov = LensSmoothing > 0f
                ? Mathf.SmoothDamp(Camera.Lens.FieldOfView, wanted, ref fovVelocity, LensSmoothing, Mathf.Infinity, Time.unscaledDeltaTime)
                : wanted;
            Camera.Lens.FieldOfView = Mathf.Clamp(fov, 1f, 179f);
        }

        /// <summary>0 at the zone entry, 1 at the exit, along the zone's long horizontal axis in the runner's travel direction.</summary>
        private float Progress(Transform runner)
        {
            var box = Zone.GetComponent<BoxCollider>();
            if (box == null) return 0f;
            bool alongX = box.size.x >= box.size.z;
            Vector3 axis = Zone.transform.TransformDirection(alongX ? Vector3.right : Vector3.forward);
            float half = (alongX ? box.size.x : box.size.z) * 0.5f;
            Vector3 local = Zone.transform.InverseTransformPoint(runner.position) - box.center;
            float pos = alongX ? local.x : local.z;
            bool positive = Vector3.Dot(runner.forward, axis) >= 0f;
            return positive ? Mathf.InverseLerp(-half, half, pos) : Mathf.InverseLerp(half, -half, pos);
        }

        private void Restore()
        {
            if (Camera != null && hasBase && (IsZooming || !Mathf.Approximately(Camera.Lens.FieldOfView, baseFov)))
                Camera.Lens.FieldOfView = baseFov;
            elapsed = -1f; fovVelocity = 0f; IsZooming = false;
        }
    }
}
