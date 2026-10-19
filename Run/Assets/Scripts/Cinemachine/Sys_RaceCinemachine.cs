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
        [Tooltip("Close-up used while a skill cutscene plays.")]
        public Unity.Cinemachine.CinemachineCamera SkillCamera;
        public int SkillActivePriority = 30;
        [Tooltip("Seconds (race time) the skill camera holds on non-protagonist runners. The protagonist keeps the full cutscene length.")]
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
        private int videoRunner = -1;
        private bool videoPlaying;
        private float videoReturnUntil;
        private Transform videoTarget;
        private RaceRunnerView videoView;
        private SkinnedMeshRenderer videoBody;
        private Unity.Cinemachine.CinemachineFollow videoFollow;
        private Vector3 authoredSkillOffset;
        private bool hasVideoOffset;
        public bool IsSkillVideoFraming => videoRunner >= 0 && skillTarget == videoRunner;

        public void BeginSkillVideo(int runnerId)
        {
            videoRunner = runnerId; videoPlaying = true;
            videoFollow = SkillCamera.GetComponent<Unity.Cinemachine.CinemachineFollow>();
            if (videoFollow != null)
            {
                if (!hasVideoOffset) authoredSkillOffset = videoFollow.FollowOffset;
                hasVideoOffset = true;
                videoFollow.FollowOffset = authoredSkillOffset * 1.35f;
            }
            videoView = Presentation.Views[runnerId]; videoBody = null;
            foreach (var mesh in videoView.MountRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (mesh.enabled && !mesh.name.StartsWith("obj_outline")) { videoBody = mesh; break; }
            if (videoTarget == null)
            {
                videoTarget = new GameObject("obj_skill_video_target").transform;
                videoTarget.SetParent(transform, false);
            }
            UpdateVideoTarget();
        }
        public void EndSkillVideo() { videoPlaying = false; videoReturnUntil = Time.unscaledTime + 1.6f; }
        public void CancelSkillVideo() { videoRunner = -1; videoPlaying = false; videoReturnUntil = 0; }
        private void OnEnable() => Mgr_Event.Reset += CancelSkillVideo;
        private void OnDisable() { Mgr_Event.Reset -= CancelSkillVideo; CancelSkillVideo(); RestoreSkillOffset(); }
        private void RestoreSkillOffset()
        {
            if (hasVideoOffset && videoFollow != null) videoFollow.FollowOffset = authoredSkillOffset;
            hasVideoOffset = false;
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
            if (restarted) skillTarget = -1;
        }

        private void UpdateSkillCamera()
        {
            if (SkillCamera == null) return;
            // Video entry waits for the opaque frame; return keeps the same runner, then uses the rig's authored blend.
            int target = videoRunner >= 0 ? videoRunner : Presentation.SkillRunner;
            bool active = target >= 0 && target < Presentation.Views.Length && (videoRunner >= 0
                || (Presentation.InCutscene && !Presentation.VideoSequenceActive
                    && (target == ProtagonistId || Presentation.SkillAge < OtherSkillCameraSeconds)));
            if (active && skillTarget != target)
            {
                if (videoRunner < 0) RestoreSkillOffset();
                skillTarget = target;
                var view = Presentation.Views[skillTarget];
                SkillCamera.Follow = videoRunner >= 0 ? videoTarget : view.transform;
                SkillCamera.LookAt = videoRunner >= 0 ? videoTarget : view.FaceTarget != null ? view.FaceTarget : view.transform;
                SkillCamera.PreviousStateIsValid = false;
            }
            if (!active) skillTarget = -1;
            SkillCamera.Priority = active ? SkillActivePriority : 0;
        }

        private string ResolveLabel()
        {
            if (SkillCamera != null && skillTarget >= 0) return SkillLabel;
            RaceCameraZone best = null;
            if (Zones != null)
                foreach (var zone in Zones)
                    if (zone != null && zone.IsLive && (best == null || zone.ActivePriority > best.ActivePriority)) best = zone;
            return best != null ? best.ShotLabel : FollowLabel;
        }
    }
}
