using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectRun.Race
{
    public enum E_RiderPosture { Straddling, Carried, Kneeling, Seated }
    [DefaultExecutionOrder(-50)]
    public sealed class RaceRunnerView : MonoBehaviour
    {
        public int RunnerId;
        public Animator MountAnimator;
        public Animator RiderAnimator;
        public Transform MountRoot;
        public Transform RiderRoot;
        public Transform SeatBone;
        public Transform RiderPelvis;
        public Transform RiderHead;
        public Transform CameraTarget;
        public Transform FaceTarget;
        public Transform MonsterFaceTarget;
        public Vector3 SeatPosition;
        [Header("Mount-specific attachment")]
        [Tooltip("Use the authored socket as the character origin, preserving the boarding clip's full pose.")]
        public bool UseSocketAttachment;
        public E_RiderPosture Posture;
        public Vector3 RiderSocketOffset;
        public float CameraFocusHeight = 2.25f;
        public bool HasFootfalls = true;
        [Tooltip("Carried riders face sideways; frame their face instead of the course direction.")]
        public bool UseRiderFacingCloseup;
        public Vector3 FaceForwardAxis = Vector3.up;
        public Vector3 BurstCameraOffset = new Vector3(-11, 0.9f, 4);
        public float BurstTravel = 6;
        public AnimationClip IdleClip;
        public AnimationClip RunClip;
        public AnimationClip RidingClip;
        public ParticleSystem Dust;
        public ParticleSystem Sparks;
        public TrailRenderer[] Trails;
        public Transform Shadow;
        [Header("Gait calibration")]
        [Tooltip("World metres per second at the source clip's original playback speed. Calibrated from planted paws, including model scale.")]
        [UnityEngine.Serialization.FormerlySerializedAs("AnimationStride")]
        public float RunReferenceSpeed = 12;
        [Tooltip("Complete gallops in one source clip; some legacy clips contain two.")]
        public int GallopsPerClip = 1;
        [Range(0, 2)] public float RiderMotion = 1;
        public Vector3 Focus => CameraTarget.position;
        public bool HasRider => RiderAnimator != null && RiderRoot != null && RidingClip != null;
        public bool HasPose { get; private set; }
        public float CurrentAnimationRate { get; private set; }
        public float GaitPhase => RunClip == null ? 0 : Mathf.Repeat((float)(gaitClock / RunClip.length * GallopsPerClip), 1);
        private Sys_Race race;
        private PlayableGraph mountGraph;
        private PlayableGraph riderGraph;
        private AnimationClipPlayable idle;
        private AnimationClipPlayable run;
        private AnimationClipPlayable riding;
        private AnimationMixerPlayable mixer;
        private Vector3 seatRest;
        private Quaternion seatRestRotation;
        private double previousClock;
        private double gaitClock;
        private double idleClock;
        private float ridePitch;
        private float rideRoll;
        private float displayedSpeed;
        private Transform spine;
        private Transform chest;
        private Transform neck;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftForearm;
        private Transform rightForearm;
        private Transform[] cape;
        private Transform leftThigh, rightThigh, leftCalf, rightCalf, leftFoot, rightFoot;
        private Vector3 poseRight, poseUp, poseForward;
        private int generation = -1;
        private int lastFoot;

        public void Bind(Sys_Race value, int index)
        {
            race = value; RunnerId = index;
        }
        private void Start()
        {
            MountAnimator.applyRootMotion = false;
            MountAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            mountGraph = PlayableGraph.Create("Mount_" + RunnerId);
            mountGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            idle = AnimationClipPlayable.Create(mountGraph, IdleClip);
            run = AnimationClipPlayable.Create(mountGraph, RunClip);
            mixer = AnimationMixerPlayable.Create(mountGraph, 2);
            mountGraph.Connect(idle, 0, mixer, 0);
            mountGraph.Connect(run, 0, mixer, 1);
            var output = AnimationPlayableOutput.Create(mountGraph, "mount", MountAnimator);
            output.SetSourcePlayable(mixer);
            mountGraph.Play();
            mixer.SetInputWeight(0, 1); mixer.SetInputWeight(1, 0);
            idle.SetTime(0.2); mountGraph.Evaluate(0);
            if (!HasRider) return;
            RiderAnimator.applyRootMotion = false;
            RiderAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            riderGraph = PlayableGraph.Create("Rider_" + RunnerId);
            riderGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            riding = AnimationClipPlayable.Create(riderGraph, RidingClip);
            var riderOutput = AnimationPlayableOutput.Create(riderGraph, "rider", RiderAnimator);
            riderOutput.SetSourcePlayable(riding);
            riderGraph.Play();
            mixer.SetInputWeight(0, 1); mixer.SetInputWeight(1, 0);
            idle.SetTime(0.2); mountGraph.Evaluate(0);
            seatRest = MountRoot.InverseTransformPoint(SeatBone.position);
            seatRestRotation = Quaternion.Inverse(MountRoot.rotation) * SeatBone.rotation;
            spine = FindBone("bone_spine"); chest = FindBone("bone_spine1"); neck = FindBone("bone_neck");
            leftArm = FindBone("bone_l_upperarm"); rightArm = FindBone("bone_r_upperarm");
            leftForearm = FindBone("bone_l_forearm"); rightForearm = FindBone("bone_r_forearm");
            cape = new[] { FindBone("bone_y_cape"), FindBone("bone_y_cape1"), FindBone("bone_y_cape2") };
            leftThigh = FindBone("bone_l_thigh"); rightThigh = FindBone("bone_r_thigh");
            leftCalf = FindBone("bone_l_calf"); rightCalf = FindBone("bone_r_calf");
            leftFoot = FindBone("bone_l_foot"); rightFoot = FindBone("bone_r_foot");
        }
        private void LateUpdate()
        {
            if (race == null || !mountGraph.IsValid()) return;
            var s = race.Simulation.Runners[RunnerId];
            if (generation != race.Generation)
            {
                generation = race.Generation; previousClock = 0;
                gaitClock = RunnerId * 0.173 * RunClip.length / Mathf.Max(1, GallopsPerClip);
                idleClock = RunnerId * 0.3; ridePitch = 0; rideRoll = 0; displayedSpeed = 0;
                lastFoot = -1;
                foreach (var trail in Trails) trail.Clear();
                Dust.Clear(); Sparks.Clear();
            }
            // Match interpolated translation: do not hold the skeleton at 60 Hz while the camera renders faster.
            double clock = System.Math.Max(0, race.Simulation.Time - Data_RaceSimulation.StepSeconds
                + Mathf.Clamp01(race.Interpolation) * Data_RaceSimulation.StepSeconds);
            double delta = System.Math.Max(0, clock - previousClock);
            previousClock = clock;
            float poseDelta = race.Paused ? 0 : (race.Countdown > 0 || race.Simulation.Phase == E_RacePhase.Result
                ? Time.unscaledDeltaTime * race.PlaybackSpeed : (float)delta);
            idleClock += poseDelta;
            displayedSpeed = Mathf.Lerp(displayedSpeed, s.Speed, 1 - Mathf.Exp(-12 * poseDelta));
            CurrentAnimationRate = Mathf.Clamp(displayedSpeed / Mathf.Max(0.1f, RunReferenceSpeed), 0, 1.85f);
            gaitClock += delta * CurrentAnimationRate;
            int foot = (int)(gaitClock / RunClip.length * GallopsPerClip * 2);
            if (HasFootfalls && foot != lastFoot && s.Speed > 2 && !race.Paused)
            {
                lastFoot = foot;
                var camera = Camera.main;
                if (camera != null) Mgr_Sound.PlayFoot(Mathf.Clamp01(1 - Vector3.Distance(camera.transform.position, transform.position) / 38));
            }
            float distance = Mathf.Lerp((float)s.PreviousDistance, (float)s.Distance, Mathf.Clamp01(race.Interpolation));
            if (race.Simulation.Phase == E_RacePhase.Result) distance = (float)s.Distance;
            transform.position = Data_RaceSimulation.CoursePoint(distance, s.Lane);
            var forward = Data_RaceSimulation.CourseForward(distance);
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            float blend = Mathf.SmoothStep(0, 1, displayedSpeed / 4);
            mixer.SetInputWeight(0, 1 - blend); mixer.SetInputWeight(1, blend);
            idle.SetTime(idleClock * 0.65);
            run.SetTime(gaitClock);
            mountGraph.Evaluate(0);
            MountRoot.localPosition = Vector3.zero;
            MountRoot.localRotation = Quaternion.identity;
            float phase = (float)(gaitClock / RunClip.length * GallopsPerClip * Mathf.PI * 2);
            if (HasRider)
            {
                // Play the entire seated cycle; the old 0.23..0.37 s sample barely moved any bones.
                riding.SetTime(gaitClock / RunClip.length * GallopsPerClip * RidingClip.length + idleClock * (1 - blend) * 0.3);
                riderGraph.Evaluate(0);
                float surge = s.SkillAge >= 0 && s.SkillAge < 3.4f ? Mathf.Sin(Mathf.Clamp01(s.SkillAge / 3.4f) * Mathf.PI) : 0;
                var seatRotation = Quaternion.Inverse(MountRoot.rotation) * SeatBone.rotation * Quaternion.Inverse(seatRestRotation);
                float seatPitch = Mathf.Clamp(Mathf.DeltaAngle(0, seatRotation.eulerAngles.x), -18, 18);
                float bend = Vector3.SignedAngle(forward, Data_RaceSimulation.CourseForward(distance + 5), Vector3.up);
                float targetPitch = 5 + blend * 7 + surge * 5 + seatPitch * 0.3f;
                float targetRoll = Mathf.Clamp(-bend * displayedSpeed * 0.12f, -7, 7);
                float response = 1 - Mathf.Exp(-10 * poseDelta);
                ridePitch = Mathf.Lerp(ridePitch, targetPitch, response);
                rideRoll = Mathf.Lerp(rideRoll, targetRoll, response);
                // A carried / chair pose already includes its own orientation and seat height.
                // Applying the straddling lean to it pulls the passenger out of the hand or chair.
                if (UseSocketAttachment)
                {
                    RiderRoot.SetPositionAndRotation(SeatBone.TransformPoint(RiderSocketOffset), SeatBone.rotation);
                }
                else RiderRoot.localRotation = Quaternion.Euler(ridePitch, 0, rideRoll);
                float motion = blend * RiderMotion;
                float bodyPhase = Posture == E_RiderPosture.Carried ? phase * 2 : phase;
                float compression = Mathf.Sin(bodyPhase - 0.45f);
                float followThrough = Mathf.Sin(bodyPhase - 1.05f);
                float sway = Mathf.Sin(phase * 0.5f + RunnerId * 0.7f);
                float breath = Mathf.Sin((float)idleClock * 2.2f) * (1 - blend * 0.65f);
                poseRight = transform.right; poseUp = transform.up; poseForward = transform.forward;
                if (Posture == E_RiderPosture.Carried)
                {
                    // A horizontal passenger needs anatomical bending axes, not the mount's upright axes.
                    poseUp = (RiderHead.position - RiderPelvis.position).normalized;
                    poseForward = RiderHead.TransformDirection(FaceForwardAxis).normalized;
                    poseRight = Vector3.Cross(poseUp, poseForward).normalized;
                    poseForward = Vector3.Cross(poseRight, poseUp).normalized;
                    RotateBone(RiderPelvis, compression * 1.4f * motion, 0, sway * 1.5f * motion);
                }
                // Different phase delays let the chest, neck and limbs absorb the support's movement.
                RotateBone(spine, compression * 4.5f * motion + breath, 0, sway * 1.4f * motion);
                RotateBone(chest, followThrough * 6 * motion, 0, -sway * 1.1f * motion);
                RotateBone(neck, -followThrough * 3.8f * motion, 0, -sway * 0.4f * motion);
                RotateBone(RiderHead, -compression * 1.7f * motion, sway * 0.8f * motion, 0);
                RotateBone(leftArm, -followThrough * 3.5f * motion, 0, compression * 1.2f * motion);
                RotateBone(rightArm, -followThrough * 3.5f * motion, 0, -compression * 1.2f * motion);
                RotateBone(leftForearm, compression * 3 * motion, 0, 0);
                RotateBone(rightForearm, compression * 3 * motion, 0, 0);
                if (Posture == E_RiderPosture.Carried || Posture == E_RiderPosture.Seated)
                {
                    float leg = Mathf.Sin(phase - 1.45f) * motion;
                    float otherLeg = Mathf.Sin(phase - 2.1f) * motion;
                    RotateBone(leftThigh, leg * 2.2f, 0, 0);
                    RotateBone(rightThigh, otherLeg * 2.2f, 0, 0);
                    RotateBone(leftCalf, leg * 8, 0, 0);
                    RotateBone(rightCalf, otherLeg * 7, 0, 0);
                    RotateBone(leftFoot, -leg * 3.5f, 0, 0);
                    RotateBone(rightFoot, -otherLeg * 3.5f, 0, 0);
                }
                for (int i = 0; i < cape.Length; i++)
                    RotateBone(cape[i], (Mathf.Sin(phase - 1.2f - i * 0.6f) * (4 + i * 2) - blend * 5) * RiderMotion, 0, sway * motion);
                Vector3 seatDelta = MountRoot.InverseTransformPoint(SeatBone.position) - seatRest;
                Vector3 suspension = new Vector3(sway * 0.012f, (1 + Mathf.Sin(phase - 0.2f)) * 0.018f, followThrough * 0.012f) * motion;
                Vector3 target = MountRoot.TransformPoint(SeatPosition + seatDelta + suspension);
                if (!UseSocketAttachment) RiderRoot.position += target - RiderPelvis.position;
                FaceTarget.position = RiderHead.position + transform.forward * 0.08f;
            }
            else FaceTarget.SetPositionAndRotation(MonsterFaceTarget.position, MonsterFaceTarget.rotation);
            CameraTarget.position = transform.position + Vector3.up * CameraFocusHeight;
            bool boosted = s.SkillAge >= 0 && s.SkillAge < race.Entrants[RunnerId].SurgeDuration;
            var dustEmission = Dust.emission; dustEmission.rateOverTime = race.Paused || race.PresentationHeld || s.Speed < 2 ? 0 : (boosted ? 32 : 14);
            var sparkEmission = Sparks.emission; sparkEmission.rateOverTime = boosted && !race.Paused && !race.PresentationHeld ? 28 : 0;
            foreach (var trail in Trails) trail.emitting = boosted && !race.Paused && !race.PresentationHeld;
            Shadow.localPosition = new Vector3(0, 0.035f, -0.2f);
            HasPose = true;
        }
        private Transform FindBone(string boneName)
        {
            foreach (var bone in RiderAnimator.GetComponentsInChildren<Transform>())
                if (bone.name == boneName) return bone;
            return null;
        }
        private void RotateBone(Transform bone, float pitch, float yaw, float roll)
        {
            if (bone == null) return;
            var offset = Quaternion.AngleAxis(pitch, poseRight) * Quaternion.AngleAxis(yaw, poseUp)
                * Quaternion.AngleAxis(roll, poseForward);
            bone.rotation = offset * bone.rotation;
        }
        private void OnDestroy()
        {
            if (mountGraph.IsValid()) mountGraph.Destroy();
            if (riderGraph.IsValid()) riderGraph.Destroy();
        }
    }
}

