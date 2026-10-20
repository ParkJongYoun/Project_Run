using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Shows <see cref="Lines"/> while one of <see cref="Cameras"/> is the Brain's live camera.
    /// The radial center tracks the protagonist's head on screen. Add cameras to the list to reuse the effect.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after the Brain has positioned the camera this frame
    public sealed class RaceSpeedLinesTrigger : MonoBehaviour
    {
        [Header("Refs")]
        public Unity.Cinemachine.CinemachineBrain Brain;
        [Tooltip("Source of the protagonist and race state.")]
        public Sys_RaceCinemachine Rig;
        public UIItem_SpeedLines Lines;

        [Header("When")]
        [Tooltip("Speed lines appear while any of these cameras is live.")]
        public List<Unity.Cinemachine.CinemachineCamera> Cameras = new List<Unity.Cinemachine.CinemachineCamera>();
        [Tooltip("While one of these cameras is live, the lines are centered on the screen (+ScreenCenterOffset) instead of the protagonist's face. Use for fixed cameras.")]
        public List<Unity.Cinemachine.CinemachineCamera> ScreenCenterCameras = new List<Unity.Cinemachine.CinemachineCamera>();
        [Tooltip("Offset from the screen center as a fraction of the screen size. (0, 0.05) = slightly above center.")]
        public Vector2 ScreenCenterOffset = Vector2.zero;

        [Header("Look")]
        [Tooltip("World offset from the protagonist's face anchor.")]
        public Vector3 HeadOffset = new Vector3(0f, 0.2f, 0f);
        [Min(0.01f)] public float FadeInSeconds = 0.25f;
        [Min(0.01f)] public float FadeOutSeconds = 0.2f;
        [Tooltip("Streak speed multiplier.")]
        [Min(0f)] public float AnimationSpeed = 1f;

        private Camera outputCamera;
        private float intensity;

        private void Awake()
        {
            if (Brain != null) outputCamera = Brain.GetComponent<Camera>();
        }

        private void OnDisable()
        {
            intensity = 0f;
            if (Lines != null) Lines.SetState(Lines.Center, 0f);
        }

        private void LateUpdate()
        {
            if (Brain == null || Lines == null || outputCamera == null) return;
            bool want = IsTargetLive() && !IsRaceFrozen();
            float dt = Time.unscaledDeltaTime;
            intensity = Mathf.MoveTowards(intensity, want ? 1f : 0f, dt / (want ? FadeInSeconds : FadeOutSeconds));

            Vector2 center = Lines.Center;
            if (intensity > 0f && TryGetHeadScreenPoint(out var screen))
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Lines.rectTransform, screen, null, out center);

            Lines.SetState(center, intensity);
            Lines.Tick(dt * AnimationSpeed);
        }

        private bool IsTargetLive()
        {
            var active = Brain.ActiveVirtualCamera as Object;
            if (active == null) return false;
            foreach (var cam in Cameras) if (cam != null && ReferenceEquals(cam, active)) return true;
            return false;
        }

        private bool IsRaceFrozen()
        {
            var race = Rig != null ? Rig.Race : null;
            return race == null || race.Simulation == null || race.Paused || race.PresentationHeld || race.IsSkipping
                || race.Countdown > 0f || race.Simulation.Phase == E_RacePhase.Preparing;
        }

        private bool TryGetHeadScreenPoint(out Vector2 screen)
        {
            screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var live = Brain.ActiveVirtualCamera as Object;
            foreach (var cam in ScreenCenterCameras)
                if (cam != null && ReferenceEquals(cam, live))
                {
                    screen += new Vector2(Screen.width * ScreenCenterOffset.x, Screen.height * ScreenCenterOffset.y);
                    return true;
                }
            if (Rig == null|| Rig.Presentation == null || Rig.Presentation.Views == null) return true;
            int id = Rig.ProtagonistId;
            if (id < 0 || id >= Rig.Presentation.Views.Length) return true;
            var view = Rig.Presentation.Views[id];
            var anchor = view.FaceTarget != null ? view.FaceTarget : view.transform;
            var p = outputCamera.WorldToScreenPoint(anchor.position + HeadOffset);
            if (p.z <= 0f) return true; // behind the camera: keep the screen center
            screen = p;
            return true;
        }
    }
}
