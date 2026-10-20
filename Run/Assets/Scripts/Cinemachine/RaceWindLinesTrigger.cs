using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Shows horizontal <see cref="Lines"/> (wind sweeping across the screen) while one of <see cref="Cameras"/>
    /// is the Brain's live camera. Add cameras to the list to reuse the effect.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class RaceWindLinesTrigger : MonoBehaviour
    {
        [Header("Refs")]
        public Unity.Cinemachine.CinemachineBrain Brain;
        [Tooltip("Source of race state (pause / skip / countdown).")]
        public Sys_RaceCinemachine Rig;
        public UIItem_WindLines Lines;

        [Header("When")]
        [Tooltip("Wind lines appear while any of these cameras is live.")]
        public List<Unity.Cinemachine.CinemachineCamera> Cameras = new List<Unity.Cinemachine.CinemachineCamera>();

        [Header("Look")]
        [Min(0.01f)] public float FadeInSeconds = 0.25f;
        [Min(0.01f)] public float FadeOutSeconds = 0.2f;
        [Tooltip("Streak speed multiplier.")]
        [Min(0f)] public float AnimationSpeed = 1f;

        private float intensity;

        private void OnDisable()
        {
            intensity = 0f;
            if (Lines != null) Lines.SetIntensity(0f);
        }

        private void LateUpdate()
        {
            if (Brain == null || Lines == null) return;
            bool want = IsTargetLive() && !IsRaceFrozen();
            float dt = Time.unscaledDeltaTime;
            intensity = Mathf.MoveTowards(intensity, want ? 1f : 0f, dt / (want ? FadeInSeconds : FadeOutSeconds));
            Lines.SetIntensity(intensity);
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
    }
}
