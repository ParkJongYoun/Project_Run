using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// While <see cref="WatchCamera"/> is the shot on screen, checks whether another runner's body sits between
    /// that camera and the protagonist's face. If it stays blocked for <see cref="OccludeDelay"/>, raises
    /// <see cref="FallbackCamera"/> (e.g. a higher back camera) to <see cref="FallbackPriority"/>; once the line is
    /// clear again for <see cref="ClearDelay"/> (and at least <see cref="MinHold"/> has passed) it drops back.
    /// Runners have no colliders, so bodies are tested with their renderer bounds.
    /// The watched position is computed from the camera's CinemachineFollow offset, so it stays valid while the
    /// fallback camera is live and the watched camera is on standby.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class RaceOcclusionSwitch : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Race rig (protagonist id + runner views). Found automatically if empty.")]
        public Sys_RaceCinemachine Rig;
        [Tooltip("Camera whose view is checked for occlusion.")]
        public CinemachineCamera WatchCamera;
        [Tooltip("Camera shown instead while the protagonist is blocked.")]
        public CinemachineCamera FallbackCamera;

        [Header("Priority")]
        [Tooltip("Fallback priority while active. Keep it just above the watched camera and below zone cameras (20) so zones still win.")]
        public int FallbackPriority = 11;
        public int InactivePriority = 0;

        [Header("Detection")]
        [Tooltip("Shrinks each blocker's bounds so grazing an edge does not count (0.15 = 15% smaller).")]
        [Range(0f, 0.5f)] public float BoundsShrink = 0.15f;
        [Tooltip("Ignore blockers closer than this to the protagonist along the view line (runner overlapping the protagonist itself).")]
        [Min(0f)] public float IgnoreNearTarget = 0.6f;
        [Tooltip("Also test the protagonist's body center. When on, blocked = face AND body blocked (stricter, fewer switches).")]
        public bool RequireBodyBlocked = false;

        [Header("Timing (seconds, unscaled)")]
        [Tooltip("Must stay blocked this long before switching (prevents flicker).")]
        [Min(0f)] public float OccludeDelay = 0.2f;
        [Tooltip("Must stay clear this long before switching back.")]
        [Min(0f)] public float ClearDelay = 0.5f;
        [Tooltip("Minimum time the fallback camera stays on once switched.")]
        [Min(0f)] public float MinHold = 1.5f;

        public bool IsSwitched { get; private set; }
        public bool IsOccluded { get; private set; }

        private CinemachineFollow watchFollow;
        private float blockedFor, clearFor, switchedAt;
        private readonly Dictionary<RaceRunnerView, Renderer[]> renderers = new Dictionary<RaceRunnerView, Renderer[]>();

        private void Awake()
        {
            if (Rig == null) Rig = FindFirstObjectByType<Sys_RaceCinemachine>();
            if (WatchCamera != null) watchFollow = WatchCamera.GetComponent<CinemachineFollow>();
        }

        private void OnEnable()
        {
            Mgr_Event.Reset += ResetState;
            ResetState();
        }

        private void OnDisable()
        {
            Mgr_Event.Reset -= ResetState;
            ResetState();
        }

        private void ResetState()
        {
            blockedFor = clearFor = 0f;
            IsOccluded = false;
            renderers.Clear();
            SetSwitched(false);
        }

        private void LateUpdate()
        {
            if (WatchCamera == null || FallbackCamera == null) return;
            // Only care while the watched shot (or our replacement for it) is the one on screen.
            bool relevant = CinemachineCore.IsLive(WatchCamera) || (IsSwitched && CinemachineCore.IsLive(FallbackCamera));
            IsOccluded = relevant && CheckOccluded();

            float dt = Time.unscaledDeltaTime;
            blockedFor = IsOccluded ? blockedFor + dt : 0f;
            clearFor = IsOccluded ? 0f : clearFor + dt;

            if (!IsSwitched && blockedFor >= OccludeDelay) SetSwitched(true);
            else if (IsSwitched && clearFor >= ClearDelay && Time.unscaledTime - switchedAt >= MinHold) SetSwitched(false);
        }

        private void SetSwitched(bool on)
        {
            IsSwitched = on;
            switchedAt = Time.unscaledTime;
            if (FallbackCamera != null) FallbackCamera.Priority = on ? FallbackPriority : InactivePriority;
        }

        private bool CheckOccluded()
        {
            var views = Rig != null && Rig.Presentation != null ? Rig.Presentation.Views : null;
            int id = Rig != null ? Rig.ProtagonistId : -1;
            if (views == null || id < 0 || id >= views.Length || views[id] == null) return false;

            var hero = views[id];
            Vector3 eye = WatchedPosition(hero.transform);
            Vector3 face = hero.FaceTarget != null ? hero.FaceTarget.position : hero.transform.position + Vector3.up;
            bool faceBlocked = Blocked(views, id, eye, face);
            if (!RequireBodyBlocked) return faceBlocked;
            if (!faceBlocked) return false;
            return Blocked(views, id, eye, BodyCenter(hero));
        }

        /// <summary>Where the watched camera is (or would be) for this target, from its follow offset.</summary>
        private Vector3 WatchedPosition(Transform target)
        {
            if (watchFollow == null) return WatchCamera.State.GetFinalPosition();
            // LockToTargetWithWorldUp: offset rotates with the target's yaw only.
            return target.position + Quaternion.Euler(0f, target.eulerAngles.y, 0f) * watchFollow.FollowOffset;
        }

        private bool Blocked(RaceRunnerView[] views, int heroId, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f) return false;
            var ray = new Ray(from, dir / length);
            for (int i = 0; i < views.Length; i++)
            {
                if (i == heroId || views[i] == null || !views[i].isActiveAndEnabled) continue;
                if (!TryGetBounds(views[i], out var b)) continue;
                b.Expand(-b.size * BoundsShrink);
                if (b.IntersectRay(ray, out float hit) && hit < length - IgnoreNearTarget) return true;
            }
            return false;
        }

        private Vector3 BodyCenter(RaceRunnerView view)
        {
            return TryGetBounds(view, out var b) ? b.center : view.transform.position + Vector3.up * 0.8f;
        }

        private bool TryGetBounds(RaceRunnerView view, out Bounds bounds)
        {
            bounds = default;
            if (!renderers.TryGetValue(view, out var list) || list == null)
            {
                var all = view.GetComponentsInChildren<Renderer>(true);
                var keep = new List<Renderer>(all.Length);
                foreach (var r in all)
                    if (!(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)) keep.Add(r);
                list = keep.ToArray();
                renderers[view] = list;
            }
            bool any = false;
            foreach (var r in list)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || Rig == null || Rig.Presentation == null || Rig.Presentation.Views == null) return;
            int id = Rig.ProtagonistId;
            var views = Rig.Presentation.Views;
            if (id < 0 || id >= views.Length || views[id] == null || WatchCamera == null) return;
            Vector3 eye = WatchedPosition(views[id].transform);
            Vector3 face = views[id].FaceTarget != null ? views[id].FaceTarget.position : views[id].transform.position;
            Gizmos.color = IsOccluded ? Color.red : Color.green;
            Gizmos.DrawLine(eye, face);
        }
#endif
    }
}
