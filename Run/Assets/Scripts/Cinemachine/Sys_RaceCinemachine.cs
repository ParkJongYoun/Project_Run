using UnityEngine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Cinemachine replacement for the procedural Sys_RaceCamera.
    /// - Keeps every virtual camera under the "cinemachine" root aimed at the protagonist (HUD selected runner).
    /// - Moves the trigger probe with the protagonist so <see cref="RaceCameraZone"/> volumes switch cameras.
    /// - Raises the skill camera during skill cutscenes (former Rider/Charge/Burst shots).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class Sys_RaceCinemachine : MonoBehaviour
    {
        [Header("Race")]
        public Sys_Race Race;
        public Sys_RacePresentation Presentation;
        [Tooltip("Source of the protagonist (SelectedRunnerId).")]
        public UIPanel_RaceHUD Hud;

        [Header("Rig")]
        public RaceCameraProbe Probe;
        [Tooltip("Default camera that follows the protagonist.")]
        public Unity.Cinemachine.CinemachineCamera FollowCamera;
        [Tooltip("Protagonist skill close-up (entry impact + skill video).")]
        public Unity.Cinemachine.CinemachineCamera SkillCamera;
        [Tooltip("Played once when the protagonist skill camera goes live (shake / FOV punch / post-processing).")]
        public RaceStartImpact SkillImpact;
        public int SkillActivePriority = 30;

        [Header("Skill Camera Push-In (protagonist)")]
        public bool SkillPushIn = true;
        [Tooltip("Entry starts at this multiple of the authored FollowOffset distance.")]
        [Min(0.1f)] public float PushInStartScale = 3f;
        [Tooltip("Push-in ends at this multiple. Below 1 pushes closer than authored (offsets the FOV punch widening).")]
        [Min(0.1f)] public float PushInEndScale = 0.85f;
        [Tooltip("Seconds (unscaled) of the push-in.")]
        [Min(0.01f)] public float PushInSeconds = 0.3f;
        [Tooltip("Ease power. 1 = linear, 3 = fast start / soft stop.")]
        [Range(1f, 6f)] public float PushInEase = 3f;

        [Header("Skill Camera Arrival Burst (end of push-in)")]
        [Tooltip("Impact played when the push-in arrives (shake / FOV / post-processing). Optional.")]
        public RaceStartImpact ArrivalImpact;
        [Tooltip("One-shot effect attached to the protagonist and burst when the push-in arrives. Optional.")]
        public GameObject ArrivalEffectPrefab;
        public Vector3 ArrivalEffectOffset = new Vector3(0f, 0.05f, 0f);
        [Min(0.01f)] public float ArrivalEffectScale = 1.5f;
        [Tooltip("Seconds after the arrival burst before the movie starts (movie then fades in over the burst).")]
        [Min(0f)] public float ArrivalToVideoSeconds = 0.1f;
        [Tooltip("The arrival burst fires this many seconds before the push-in ends. The movie timing is unchanged.")]
        [Min(0f)] public float ArrivalLeadSeconds = 0.15f;

        [Header("Skill Camera Focus (depth of field on SkillImpact's volume)")]
        [Tooltip("Keeps the protagonist sharp while the background blurs during the push-in. Needs a Gaussian DepthOfField override in SkillImpact's volume profile.")]
        public bool SkillFocusBlur = true;
        [Tooltip("Blur starts this many meters behind the protagonist.")]
        [Min(0f)] public float FocusBlurStartOffset = 1.5f;
        [Tooltip("Blur reaches full strength this many meters behind the start.")]
        [Min(0.1f)] public float FocusBlurRange = 6f;

        [Header("Skill Video")]
[Tooltip("Seconds before the movie starts when Push-In is off. With Push-In on, the movie starts at arrival + ArrivalToVideoSeconds.")]
        [Min(0f)] public float SkillIntroSeconds = 0.35f;
        /// <summary>Seconds from skill camera entry to movie start (race keeps running until then).</summary>
        public float SkillVideoDelay => SkillPushIn ? PushInSeconds + ArrivalToVideoSeconds : SkillIntroSeconds;
        [Tooltip("Distance multiplier while the skill video sequence frames the protagonist.")]
        [Min(0.1f)] public float VideoFramingScale = 1.35f;
        [Tooltip("Seconds the skill camera holds after the movie ends, before returning.")]
        [Min(0f)] public float VideoReturnSeconds = 1.6f;

        [Header("Other Runners' Skills")]
        [Tooltip("Close-up for non-protagonist skills. No entry effects. Leave empty to disable.")]
        public Unity.Cinemachine.CinemachineCamera OtherSkillCamera;
        [Tooltip("Off: other runners' skills never take the camera.")]
        public bool UseOtherSkillCamera = true;
        [Tooltip("Seconds (race time) the other-runner skill camera holds.")]
        [Min(0)] public float OtherSkillCameraSeconds = 0.5f;
        [Tooltip("Cameras retargeted to the protagonist (Follow + LookAt).")]
        public Unity.Cinemachine.CinemachineCamera[] ProtagonistCameras;
        public RaceCameraZone[] Zones;

        [Header("Labels")]
        public string FollowLabel = "주자 추적";
        public string SkillLabel = "필살기";

        public string ShotLabel { get; private set; }
        public int ProtagonistId { get; private set; } = -1;

        private int generation = -1;
        private int skillTarget = -1;
        private int otherTarget = -1;
        private int videoRunner = -1;
        private bool videoPlaying;
        private float videoReturnUntil;
        private Transform videoTarget;
        private RaceRunnerView videoView;
        private SkinnedMeshRenderer videoBody;
        private Unity.Cinemachine.CinemachineFollow heroFollow;
        private Vector3 heroBaseOffset;
        private bool hasHeroBase;
        private float pushInAge = -1f;
        public bool IsSkillVideoFraming => videoRunner >= 0 && skillTarget == videoRunner;
        /// <summary>True while the skill camera holds on the protagonist after the movie ended (VideoReturnSeconds).</summary>
        public bool IsSkillVideoReturning => videoRunner >= 0 && !videoPlaying && skillTarget == videoRunner;

        public void BeginSkillVideo(int runnerId)
        {
            videoRunner = runnerId; videoPlaying = true;
            CacheHeroBase();
            videoView= Presentation.Views[runnerId]; videoBody = null;
            foreach (var mesh in videoView.MountRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (mesh.enabled && !mesh.name.StartsWith("obj_outline")) { videoBody = mesh; break; }
            if (videoTarget == null)
            {
                videoTarget = new GameObject("obj_skill_video_target").transform;
                videoTarget.SetParent(transform, false);
            }
            UpdateVideoTarget();
        }
        public void EndSkillVideo() { videoPlaying = false; videoReturnUntil = Time.unscaledTime + VideoReturnSeconds; }
        public void CancelSkillVideo() { videoRunner = -1; videoPlaying = false; videoReturnUntil = 0; ReleaseSkillFocus(); if (Race != null && Race.IsSkipping) StopArrivalEffect(); }
        /// <summary>Removes the held skill post-processing (called when the movie fully covers the screen).</summary>
        public void ReleaseSkillFocus() { if (SkillImpact != null) SkillImpact.ReleaseHold(); }
        private void OnEnable() { Mgr_Event.Reset += CancelSkillVideo; CacheHeroBase(); }
        private void OnDisable() { Mgr_Event.Reset -= CancelSkillVideo; CancelSkillVideo(); RestoreSkillOffset(); }

        /// <summary>Remembers the authored FollowOffset once; every frame the offset is rebuilt from it.</summary>
        private void CacheHeroBase()
        {
            if (hasHeroBase || SkillCamera == null) return;
            heroFollow = SkillCamera.GetComponent<Unity.Cinemachine.CinemachineFollow>();
            if (heroFollow == null) return;
            heroBaseOffset = heroFollow.FollowOffset;
            hasHeroBase = true;
        }
        private void RestoreSkillOffset()
        {
            if (hasHeroBase && heroFollow != null) heroFollow.FollowOffset = heroBaseOffset;
            pushInAge = -1f;
        }
        private void ApplyHeroOffset()
        {
            if (!hasHeroBase || heroFollow == null) return;
            float scale = videoRunner >= 0 ? VideoFramingScale : 1f;
            if (pushInAge >= 0f)
            {
                pushInAge += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(pushInAge / PushInSeconds);
                float eased = 1f - Mathf.Pow(1f - t, PushInEase);
                scale *= Mathf.Lerp(PushInStartScale, PushInEndScale, eased); // t stays 1 afterwards: holds end scale
                if (!arrivalFired && pushInAge >= Mathf.Max(0f, PushInSeconds - ArrivalLeadSeconds)) { arrivalFired = true; PlayArrivalBurst(); }
            }
            heroFollow.FollowOffset = heroBaseOffset * scale;
        }

        private bool arrivalFired;
        private GameObject arrivalEffect;
        private ParticleSystem[] arrivalParticles;

        /// <summary>End of push-in: impact + one-shot effect on the protagonist, right before the movie.</summary>
        private void PlayArrivalBurst()
        {
            if (Race.IsSkipping || skillTarget < 0 || skillTarget >= Presentation.Views.Length) return;
            if (ArrivalImpact != null) ArrivalImpact.Play();
            if (ArrivalEffectPrefab == null) return;
            var view = Presentation.Views[skillTarget];
            if (arrivalEffect == null)
            {
                arrivalEffect = Instantiate(ArrivalEffectPrefab);
                arrivalEffect.name = "eff_skill_arrival";
                arrivalParticles = arrivalEffect.GetComponentsInChildren<ParticleSystem>(true);
            }
            arrivalEffect.transform.SetParent(view.transform, false);
            arrivalEffect.transform.localPosition = ArrivalEffectOffset;
            arrivalEffect.transform.localRotation = Quaternion.identity;
            arrivalEffect.transform.localScale = Vector3.one * ArrivalEffectScale;
            arrivalEffect.SetActive(true);
            foreach (var ps in arrivalParticles)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }
        private UnityEngine.Rendering.Universal.DepthOfField focusDof;
        private bool focusDofSearched;

        /// <summary>Keeps the DOF focus on the protagonist so only the background blurs as the camera pushes in.</summary>
        private void UpdateFocusBlur()
        {
            if (!SkillFocusBlur || skillTarget < 0 || SkillImpact == null || SkillImpact.ImpactVolume == null) return;
            if (!focusDofSearched)
            {
                focusDofSearched = true;
                SkillImpact.ImpactVolume.profile.TryGet(out focusDof); // runtime instance; the asset stays untouched
            }
            if (focusDof == null || skillTarget >= Presentation.Views.Length) return;
            var view = Presentation.Views[skillTarget];
            var target = SkillCamera.LookAt != null ? SkillCamera.LookAt.position : view.Focus;
            float distance = Vector3.Distance(SkillCamera.transform.position, target);
            focusDof.gaussianStart.Override(distance + FocusBlurStartOffset);
            focusDof.gaussianEnd.Override(distance + FocusBlurStartOffset + FocusBlurRange);
        }

        private void StopArrivalEffect()
        {
            if (arrivalParticles == null) return;
            foreach (var ps in arrivalParticles) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private void UpdateVideoTarget()
        {
            // Follow the visible body through Poring's bounce instead of aiming above or below it.
            if (videoTarget != null && videoView != null)
                videoTarget.SetPositionAndRotation(videoBody != null ? videoBody.bounds.center : videoView.Focus, videoView.transform.rotation);
        }

        private void LateUpdate()
        {
            if (Race == null || Race.Simulation == null || Presentation == null || Presentation.Views == null || Presentation.Views.Length == 0) return;
            int count = Presentation.Views.Length;
            int selected = ResolveProtagonist(count);
            bool restarted = generation != Race.Generation;
            if (restarted || Race.IsSkipping || Race.Simulation.Phase == E_RacePhase.Result) CancelSkillVideo();
            if (videoRunner >= 0 && !videoPlaying)
            {
                if (Race.Paused) videoReturnUntil += Time.unscaledDeltaTime;
                if (Time.unscaledTime >= videoReturnUntil) CancelSkillVideo();
            }
            if (selected != ProtagonistId || restarted) BindProtagonist(selected, restarted);
            generation = Race.Generation;

            UpdateVideoTarget();
            UpdateSkillCamera();
            ShotLabel = ResolveLabel();
        }

        /// <summary>
        /// Lobby entry (demo race): the monster picked in the lobby is the protagonist.
        /// Direct scene play: falls back to the HUD selection.
        /// </summary>
        private int ResolveProtagonist(int count)
        {
            var game = Mgr_Game.Instance;
            if (game != null && game.IsDemoRace)
            {
                int hero = game.HeroRunner;
                return hero >= 0 && hero < count ? hero : 0;
            }
            return Hud != null ? Mathf.Clamp(Hud.SelectedRunnerId, 0, count - 1) : 0;
        }

        private void BindProtagonist(int id, bool restarted)
        {
            ProtagonistId = id;
            var view = Presentation.Views[id];
            Transform follow = view.transform;
            Transform look = view.FaceTarget != null ? view.FaceTarget : view.transform;
            if (ProtagonistCameras != null)
                foreach (var cam in ProtagonistCameras)
                {
                    if (cam == null) continue;
                    cam.Follow = follow; cam.LookAt = look;
                    cam.PreviousStateIsValid = false; // cut instead of sweeping across the track
                }
            if (Probe != null) Probe.SetTarget(view.transform);
            if (restarted) { skillTarget = -1; StopArrivalEffect(); }
        }

        private void UpdateSkillCamera()
        {
            int target = videoRunner >= 0 ? videoRunner : Presentation.SkillRunner;
            bool valid = target >= 0 && target < Presentation.Views.Length;
            bool cutscene = Presentation.InCutscene && !Presentation.VideoSequenceActive;

            // Protagonist: skill video sequence or own cutscene. Entry plays the impact once.
            bool hero = valid && (videoRunner >= 0 || (cutscene && target == ProtagonistId));
            if (SkillCamera != null)
            {
                if (hero && skillTarget != target)
                {
                    var view = Presentation.Views[target];
                    SkillCamera.Follow = videoRunner >= 0 ? videoTarget : view.transform;
                    SkillCamera.LookAt = videoRunner >= 0 ? videoTarget : view.FaceTarget != null ? view.FaceTarget : view.transform;
                    if (skillTarget < 0)
                    {
                        pushInAge = SkillPushIn ? 0f : -1f; // ApplyHeroOffset below sets the far start this frame
if (SkillImpact != null && !Race.IsSkipping) SkillImpact.Play();
                    }
                    SkillCamera.PreviousStateIsValid = false;
                    skillTarget = target;
                }
                if (!hero) { if (skillTarget >= 0) ReleaseSkillFocus(); skillTarget = -1; pushInAge = -1f; }
                ApplyHeroOffset();
                UpdateFocusBlur();
                SkillCamera.Priority = hero ? SkillActivePriority : 0;
            }

            // Other runners: short close-up, no effects.
            bool other = valid && !hero && UseOtherSkillCamera && OtherSkillCamera != null
                && cutscene && target != ProtagonistId && Presentation.SkillAge < OtherSkillCameraSeconds;
            if (OtherSkillCamera != null)
            {
                if (other && otherTarget != target)
                {
                    otherTarget = target;
                    var view = Presentation.Views[target];
                    OtherSkillCamera.Follow = view.transform;
                    OtherSkillCamera.LookAt = view.FaceTarget != null ? view.FaceTarget : view.transform;
                    OtherSkillCamera.PreviousStateIsValid = false;
                }
                if (!other) otherTarget = -1;
                OtherSkillCamera.Priority = other ? SkillActivePriority : 0;
            }
        }

        private string ResolveLabel()
        {
            if (skillTarget >= 0 || otherTarget >= 0) return SkillLabel;
            RaceCameraZone best = null;
            if (Zones != null)
                foreach (var zone in Zones)
                    if (zone != null && zone.IsLive && (best == null || zone.ActivePriority > best.ActivePriority)) best = zone;
            return best != null ? best.ShotLabel : FollowLabel;
        }
    }
}
