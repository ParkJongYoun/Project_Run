using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Plays <see cref="Director"/>'s timeline when the protagonist crosses the final finish line
    /// (distance based, so it works for any LapCount).
    /// Every camera in <see cref="Shots"/> is retargeted to the protagonist, so finish cameras do NOT need
    /// to be registered in Sys_RaceCinemachine.ProtagonistCameras and must NOT be linked to a RaceCameraZone.
    /// To add/remove a camera: edit <see cref="Shots"/>, then press "Build Timeline From Shots" in the Inspector.
    /// </summary>
    [DefaultExecutionOrder(130)]
    public sealed class RaceFinishOutro : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Shot
        {
            public Unity.Cinemachine.CinemachineCamera Camera;
            [Tooltip("Seconds this shot is on screen (excluding the blend into the next shot).")]
            [Min(0.1f)] public float Duration = 2f;
            [Tooltip("Blend from the previous shot (0 = cut). Ignored for the first shot.")]
            [Min(0f)] public float BlendIn = 0.5f;
        }

        public static bool Playing { get; private set; }

        [Header("Refs")]
        public Sys_Race Race;
        [Tooltip("Source of ProtagonistId and runner views.")]
        public Sys_RaceCinemachine Rig;
        public PlayableDirector Director;

        [Header("Shots (timeline source)")]
        [Tooltip("Order = timeline order. Rebuild the timeline after editing.")]
        public List<Shot> Shots = new List<Shot>();

        [Header("Effect")]
        [Tooltip("Scene effect (kept inactive) that is re-activated and played from the start when the outro begins, e.g. eff_color_paper.")]
        public GameObject FinishEffect;

        [Header("Timing")]
        [Tooltip("Delay (race-independent, unscaled) between the protagonist finishing and the first shot.")]
        [Min(0f)] public float StartDelay = 0f;
        [Tooltip("Stop the outro and give the camera back to the normal rig when the result screen appears.")]
        public bool StopOnResult = false;

        /// <summary>
        /// True from the protagonist's finish until the timeline reaches its end (includes StartDelay).
        /// Result screens wait while this is true. Updated synchronously, so it is valid in the same frame as the Finish event.
        /// </summary>
        public static bool Showing => active != null && active.IsShowing;
        private static RaceFinishOutro active;

        private bool IsShowing => triggered && Race != null && !Race.IsSkipping && Director != null
            && (delayLeft > 0f || (Director.state == PlayState.Playing && Director.time < Director.duration - 0.001));

        private int generation = -1;
        private int boundProtagonist = -1;
        private bool triggered;
        private float delayLeft = -1f;

        private void OnEnable() { active = this; Mgr_Event.RaceChanged += OnRaceEvent; }
        private void OnDisable() { Mgr_Event.RaceChanged -= OnRaceEvent; Stop(); if (active == this) active = null; }

        private void OnRaceEvent(Data_RaceEvent e)
        {
            if (triggered || !IsReady || Race.IsSkipping) return;
            if (e.Kind != E_RaceEvent.Finish || e.RunnerId != Rig.ProtagonistId) return;
            triggered = true;
            delayLeft = StartDelay;
            if (delayLeft <= 0f) Play();
        }

        private bool IsReady => Race != null && Rig != null && Director != null && Director.playableAsset != null;

        private void LateUpdate()
        {
            if (Race == null) return;
            if (generation != Race.Generation)
            {
                generation = Race.Generation;
                Stop();
                HideEffect();
                triggered = false;
                delayLeft = -1f;
            }
            // Keep finish cameras aimed at the protagonist at all times (no "missing target" state, correct preview).
            if (Rig != null && Rig.ProtagonistId >= 0 && Rig.ProtagonistId != boundProtagonist) RetargetShots();

            if (Race.IsSkipping) { Stop(); HideEffect(); delayLeft = -1f; return; }

            if (delayLeft > 0f)
            {
                delayLeft -= Time.unscaledDeltaTime;
                if (delayLeft <= 0f) Play();
            }
            if (StopOnResult && Playing && !IsShowing && Race.Simulation != null && Race.Simulation.Phase == E_RacePhase.Result) Stop();
        }

        private void Play()
        {
            delayLeft = -1f;
            RetargetShots();
            Playing = true;
            Director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            Director.extrapolationMode = DirectorWrapMode.Hold; // stay on the last shot
            Director.time = 0;
            Director.Play();
            Director.Evaluate(); // apply the first shot this frame
            PlayEffect();
        }

        private void PlayEffect()
        {
            if (FinishEffect == null) return;
            FinishEffect.SetActive(false);
            FinishEffect.SetActive(true);
            foreach (var ps in FinishEffect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        private void HideEffect()
        {
            if (FinishEffect != null && FinishEffect.activeSelf) FinishEffect.SetActive(false);
        }

        private void RetargetShots()
        {
            var presentation = Rig.Presentation;
            int id = Rig.ProtagonistId;
            if (presentation == null || presentation.Views == null || id < 0 || id >= presentation.Views.Length) return;
            boundProtagonist = id;
            var view = presentation.Views[id];
            Transform look = view.FaceTarget != null ? view.FaceTarget : view.transform;
            foreach (var shot in Shots)
            {
                if (shot == null || shot.Camera == null) continue;
                shot.Camera.Follow = view.transform;
                shot.Camera.LookAt = look;
                shot.Camera.PreviousStateIsValid = false; // snap to the runner instead of sweeping in
            }
        }

        private void Stop()
        {
            if (Director != null && Director.state == PlayState.Playing) Director.Stop();
            Playing = false;
        }
    }
}
