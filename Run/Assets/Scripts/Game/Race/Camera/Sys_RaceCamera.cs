using UnityEngine;
namespace ProjectRun.Race
{
    public enum E_RaceShot { Opening, Pack, Tracking, Low, Front, Duel, Rider, Charge, Burst, Finish, Winner, SkillVideo }

    [DefaultExecutionOrder(100)]
    public sealed class Sys_RaceCamera : MonoBehaviour
    {
        public Sys_Race Race;
        public Sys_RacePresentation Presentation;
        public Camera RaceCamera;
        public E_RaceShot CurrentShot { get; private set; }
        public string ShotLabel { get; private set; }
        public int FocusRunner { get; private set; }
        public int ManualShot = -1;
        public float CameraDistanceScale = 1;
        public bool LandscapeComposition;
#if UNITY_EDITOR
        // Capture tooling only; never serialized or used in a player build.
        [System.NonSerialized] public int CaptureShot = -1;
        [System.NonSerialized] public int CaptureRunner = -1;
#endif
        private Vector3 smoothedOffset;
        private Vector3 smoothedLook;
        private E_RaceShot previousShot = (E_RaceShot)(-1);
        private int previousFocus = -1;
        private int generation = -1;
        private float previousYaw;
        private int videoRunner = -1;
        private bool videoPlaying;
        private float videoReturnUntil;
        private float videoBlendRemaining;

        public void BeginSkillVideo(int runnerId) { videoRunner = runnerId; videoPlaying = true; }
        public void EndSkillVideo() { videoPlaying = false; videoReturnUntil = Time.unscaledTime + 1.6f; }
        public void CancelSkillVideo() { videoRunner = -1; videoPlaying = false; videoReturnUntil = videoBlendRemaining = 0; }

        private void LateUpdate()
        {
            if (Presentation.Views == null || Presentation.Views.Length != Race.Entrants.Length || Presentation.Views.Length == 0 || !Presentation.Views[0].HasPose) return;
            if (Race.Paused && !videoPlaying && videoRunner >= 0) videoReturnUntil += Time.unscaledDeltaTime;
            var sim = Race.Simulation;
            float time = (float)sim.Time;
            bool solo = Presentation.Views.Length == 1;
            int leader = sim.Order[0], second = solo ? leader : sim.Order[1];
            FocusRunner = leader;
            float left = Presentation.Remaining;
            if (Race.Countdown > 0) CurrentShot = E_RaceShot.Opening;
            else if (sim.Phase == E_RacePhase.Result || (sim.Phase == E_RacePhase.Finishing && sim.Runners[second].Finished)) CurrentShot = E_RaceShot.Winner;
            else if (left < 32) CurrentShot = E_RaceShot.Finish;
            else if (left < 100) CurrentShot = E_RaceShot.Duel;
            else if (Presentation.InCutscene && !Presentation.VideoSequenceActive)
            {
                FocusRunner = Presentation.SkillRunner;
                float age = Presentation.SkillAge;
                CurrentShot = age < 0.8f ? E_RaceShot.Rider : age < 1.6f ? E_RaceShot.Charge : E_RaceShot.Burst;
            }
            else if (time < 5) CurrentShot = E_RaceShot.Pack;
            else
            {
                int cycle = Mathf.FloorToInt((time - 5) / 4.5f) % 5;
                CurrentShot = cycle == 0 ? E_RaceShot.Tracking : cycle == 1 ? E_RaceShot.Low : cycle == 2 ? E_RaceShot.Front : cycle == 3 ? E_RaceShot.Duel : E_RaceShot.Pack;
                if (cycle == 1) FocusRunner = second;
                if (CurrentShot == E_RaceShot.Duel)
                {
                    // Predict the nearest closing pair rather than cutting after a pass is over.
                    float best = float.MaxValue;
                    for (int i = 1; i < sim.Order.Length; i++)
                    {
                        var ahead = sim.Runners[sim.Order[i - 1]]; var behind = sim.Runners[sim.Order[i]];
                        float gap = (float)(ahead.Distance - behind.Distance);
                        float closing = behind.Speed - ahead.Speed;
                        float score = gap / Mathf.Max(0.4f, closing + 0.8f);
                        if (score < best) { best = score; FocusRunner = ahead.Id; second = behind.Id; }
                    }
                }
            }
            if (ManualShot >= 0 && ManualShot <= 5) CurrentShot = new[] { E_RaceShot.Pack, E_RaceShot.Tracking, E_RaceShot.Low, E_RaceShot.Front, E_RaceShot.Duel, E_RaceShot.Rider }[ManualShot];
            if (generation != Race.Generation) CancelSkillVideo();
            bool returningFromVideo = videoRunner >= 0 && !videoPlaying && Time.unscaledTime >= videoReturnUntil;
            if (videoRunner >= 0 && !returningFromVideo)
            {
                FocusRunner = videoRunner;
                CurrentShot = E_RaceShot.SkillVideo;
            }
            else if (returningFromVideo) { videoRunner = -1; videoBlendRemaining = .9f; }
#if UNITY_EDITOR
            if (CaptureShot >= 0) CurrentShot = (E_RaceShot)CaptureShot;
            if (CaptureRunner >= 0) FocusRunner = CaptureRunner;
#endif
            var view = Presentation.Views[FocusRunner];
            var state = sim.Runners[FocusRunner];
            Vector3 forward = sim.Forward((float)state.Distance);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 focus = view.Focus;
            Vector3 offset;
            float fov = 48;
            bool group = false;
            ShotLabel = "중계";
            switch (CurrentShot)
            {
                case E_RaceShot.SkillVideo:
                    focus = view.MountRoot.position + Vector3.up * 1.5f;
                    offset = forward * 7.2f + right * 4.7f + Vector3.up * 1.6f;
                    fov = 42; ShotLabel = "필살기 질주"; break;
                case E_RaceShot.Opening:
                    focus = PackCenter(); offset = right * -20 + forward * 15 + Vector3.up * Mathf.Lerp(9, 15, Race.Countdown / Race.CountdownSeconds); group = true; ShotLabel = "출발 준비"; break;
                case E_RaceShot.Pack:
                    focus = PackCenter(); offset = right * -22 + forward * 16 + Vector3.up * 12; group = true; ShotLabel = "전체 대열"; break;
                case E_RaceShot.Tracking:
                    focus = PackCenter(); offset = right * -19 + forward * 12 + Vector3.up * 5.5f; group = true; ShotLabel = "측면 추적"; break;
                case E_RaceShot.Low:
                    offset = right * -10.5f + forward * 6 + Vector3.up * 0.6f; fov = 53; ShotLabel = "질주"; break;
                case E_RaceShot.Front:
                    focus = PackCenter(); offset = forward * 25 + right * -5 + Vector3.up * 4.5f; group = true; ShotLabel = "정면 경쟁"; break;
                case E_RaceShot.Duel:
                    focus = (view.Focus + Presentation.Views[second].Focus) * 0.5f;
                    offset = right * -17 + forward * 8 + Vector3.up * 3; group = true; ShotLabel = left < 100 ? "라스트 스퍼트" : "추월 경쟁"; break;
                case E_RaceShot.Rider:
                    focus = view.FaceTarget.position + (view.HasRider ? Vector3.down * .3f : Vector3.up * .2f - forward * .35f);
                    offset = view.HasRider
                        ? (view.UseRiderFacingCloseup ? view.RiderHead.TransformDirection(view.FaceForwardAxis).normalized * 5.6f + Vector3.up * .5f
                            : forward * 4.4f - right * 3.5f + Vector3.up * .5f)
                        : forward * 5.4f + right * (sim.Runners[FocusRunner].Lane < 0 ? 2 : -2) + Vector3.up * 1.8f;
                    fov = 40; ShotLabel = "필살기"; break;
                case E_RaceShot.Charge:
                    focus = view.MonsterFaceTarget.position + Vector3.up * .2f;
                    offset = forward * 6.8f + right * (sim.Runners[FocusRunner].Lane < 0 ? 2.8f : -2.8f) + Vector3.up * 1.7f;
                    fov = 44; ShotLabel = "각성"; break;
                case E_RaceShot.Burst:
                    var burstOffset = view.BurstCameraOffset;
                    offset = right * burstOffset.x + forward * (burstOffset.z - Mathf.Lerp(0, view.BurstTravel, (Presentation.SkillAge - 1.6f) / 1.8f)) + Vector3.up * burstOffset.y;
                    fov = Mathf.Lerp(45, 56, Mathf.Clamp01((Presentation.SkillAge - 1.6f) / 0.7f)); ShotLabel = "폭발적 가속"; break;
                case E_RaceShot.Finish:
                    focus = (view.Focus + Presentation.Views[second].Focus) * 0.5f;
                    offset = right * -24 + forward * 1 + Vector3.up * 5; fov = 43; group = true; ShotLabel = "결승선"; break;
                default:
                    focus = view.Focus + Vector3.up * 0.3f; offset = forward * 8 + right * -7 + Vector3.up * 1.5f; fov = 42; ShotLabel = "우승"; break;
            }
            // A one-runner test uses the same race/camera flow with closer composition.
            if (solo)
            {
                group = false;
                switch (CurrentShot)
                {
                    case E_RaceShot.Opening:
                        offset = right * -8.5f + forward * 6 + Vector3.up * 2.8f;
                        fov = 43; break;
                    case E_RaceShot.Pack:
                    case E_RaceShot.Tracking:
                    case E_RaceShot.Duel:
                        focus = view.Focus;
                        offset = right * -8.6f + forward * 4.8f + Vector3.up * 1.4f;
                        fov = 40; ShotLabel = left < 100 ? "라스트 스퍼트" : "단독 주행"; break;
                    case E_RaceShot.Low:
                        offset = right * -8.4f + forward * 5.3f + Vector3.up * .6f;
                        fov = 40; break;
                    case E_RaceShot.Front:
                        offset = forward * 10.5f + right * -1.8f + Vector3.up * 1.2f;
                        fov = 40; ShotLabel = "정면 주행"; break;
                    case E_RaceShot.Finish:
                        offset = right * -9 + forward * 2 + Vector3.up;
                        fov = 40; break;
                    case E_RaceShot.Winner:
                        offset = forward * 6 + right * -7 + Vector3.up * 1.4f;
                        fov = 40; break;
                }
            }
            // Follow the road through scenery; oval side cameras would pass through town buildings.
            if (sim.Course != null && !solo && CurrentShot != E_RaceShot.Rider && CurrentShot != E_RaceShot.Charge && CurrentShot != E_RaceShot.Burst)
            {
                float d = (float)state.Distance;
                bool frontView = ManualShot == 3 || CurrentShot == E_RaceShot.Winner;
                float behind = CurrentShot == E_RaceShot.Opening ? 28 : 23;
                Vector3 cameraPoint = sim.Point(d + (frontView ? 22 : -behind));
                focus = sim.Point(d + (frontView ? 0 : 8)) + Vector3.up * 2;
                offset = cameraPoint + Vector3.up * (ManualShot == 2 ? 6 : 13) - focus;
                fov = 55;
                group = false;
                ShotLabel = sim.Course.Region(d);
            }
            float aspect = RaceCamera.aspect;
            if (LandscapeComposition && aspect >= 1 && group)
            {
                // Bring the pack closer in a wide frame, then fit both axes inside the HUD margins.
                offset *= .78f;
                Quaternion aim = Quaternion.LookRotation(-offset.normalized, Vector3.up);
                Vector3 cameraRight = aim * Vector3.right, cameraUp = aim * Vector3.up, cameraForward = aim * Vector3.forward;
                float tangent = Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
                float required = offset.magnitude;
                for (int i = 0; i < Presentation.Views.Length; i++)
                {
                    if ((CurrentShot == E_RaceShot.Duel || CurrentShot == E_RaceShot.Finish) && i != FocusRunner && i != second) continue;
                    Vector3 delta = Presentation.Views[i].Focus - focus;
                    float depth = Vector3.Dot(delta, cameraForward);
                    required = Mathf.Max(required, (Mathf.Abs(Vector3.Dot(delta, cameraRight)) + 2f) / (tangent * aspect * .88f) - depth);
                    required = Mathf.Max(required, (Mathf.Abs(Vector3.Dot(delta, cameraUp)) + 2.3f) / (tangent * .58f) - depth);
                }
                offset = offset.normalized * required;
            }
            if (group) offset *= Mathf.Clamp(0.62f / Mathf.Max(0.4f, aspect), 1, 1.35f);
            // Group composition accounts for actual distances, not just the nominal lanes.
            if (group && CurrentShot != E_RaceShot.Duel && CurrentShot != E_RaceShot.Finish)
            {
                Quaternion aim = Quaternion.LookRotation(-offset.normalized, Vector3.up);
                Vector3 cameraRight = aim * Vector3.right;
                float maxWidth = 0;
                foreach (var runner in Presentation.Views) maxWidth = Mathf.Max(maxWidth, Mathf.Abs(Vector3.Dot(runner.Focus - focus, cameraRight)) + 1.7f);
                float required = maxWidth / (Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f) * Mathf.Max(0.4f, aspect) * 0.8f);
                if (offset.magnitude < required) offset = offset.normalized * required;
            }
            offset *= CameraDistanceScale;
            bool cut = !returningFromVideo && (previousShot != CurrentShot || previousFocus != FocusRunner || generation != Race.Generation);
            // Offset smoothing in the course frame avoids the large lag of chasing a world-space position.
            Vector3 localOffset = new Vector3(Vector3.Dot(offset, right), offset.y, Vector3.Dot(offset, forward));
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 8);
            if (cut) { smoothedOffset = localOffset; smoothedLook = focus; }
            else { smoothedOffset = Vector3.Lerp(smoothedOffset, localOffset, blend); smoothedLook = focus; }
            Vector3 cameraPosition = focus + right * smoothedOffset.x + Vector3.up * smoothedOffset.y + forward * smoothedOffset.z;
            cameraPosition.y = Mathf.Max(2.4f, cameraPosition.y);
            var cameraRotation = Quaternion.LookRotation(smoothedLook - cameraPosition, Vector3.up);
            if (videoBlendRemaining > 0)
            {
                // Returning to a different runner also changes the focus point; blend world pose as well as offset.
                float returnBlend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10);
                cameraPosition = Vector3.Lerp(RaceCamera.transform.position, cameraPosition, returnBlend);
                cameraRotation = Quaternion.Slerp(RaceCamera.transform.rotation, cameraRotation, returnBlend);
                if (!Race.Paused) videoBlendRemaining -= Time.unscaledDeltaTime;
            }
            RaceCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            RaceCamera.fieldOfView = cut ? fov : Mathf.Lerp(RaceCamera.fieldOfView, fov, blend);
            if (CurrentShot == E_RaceShot.Burst && !Race.Paused)
            {
                float impulse = Mathf.Exp(-Mathf.Max(0, Presentation.SkillAge - 1.6f) * 5) * 0.08f;
                RaceCamera.transform.position += Vector3.up * Mathf.Sin(time * 65) * impulse;
            }
            previousShot = CurrentShot; previousFocus = FocusRunner; generation = Race.Generation;
            previousYaw = RaceCamera.transform.eulerAngles.y;
        }
        private Vector3 PackCenter()
        {
            Vector3 sum = Vector3.zero;
            foreach (var view in Presentation.Views) sum += view.Focus;
            return sum / Presentation.Views.Length;
        }
        public void CycleCamera() { ManualShot++; if (ManualShot > 5) ManualShot = -1; }
    }
}

