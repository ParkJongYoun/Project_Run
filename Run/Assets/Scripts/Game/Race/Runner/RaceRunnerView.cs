using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ProjectRun.Race
{
    public enum E_RiderPosture { Straddling, Carried, Kneeling, Seated }
    [System.Serializable]
    public sealed class Data_MotionPropAttachment
    {
        public Transform Prop;
        public Transform Hand;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation = Quaternion.identity;
    }
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
        [Header("Run animation")]
        [Tooltip("Scale this monster's base animation rate by current race speed / the entrant's BaseSpeed. Disable while tuning a fixed cadence.")]
        [UnityEngine.Serialization.FormerlySerializedAs("SyncRunAnimationToMovement")]
        [SerializeField] private bool ScaleRunAnimationWithSpeed = true;
        [Tooltip("Animation playback rate at the entrant's BaseSpeed. Each monster can be tuned independently; acceleration scales from this value.")]
        [InspectorName("Base Run Animation Speed")]
        [SerializeField, Min(0)] private float RunAnimationSpeed = 1;
        [Header("Monster motion preview")]
        [Tooltip("Enable before entering Play Mode. Disable to restore the original Run Clip and turn off the added skill gesture.")]
        [SerializeField] private bool UseMonsterMotionPreview;
        [SerializeField] private AnimationClip PreviewRunClip;
        [SerializeField] private AnimationClip SkillGestureClip;
        [SerializeField] private AvatarMask SkillGestureMask;
        [Tooltip("Gesture duration in race seconds, independent of the monster's run animation rate.")]
        [SerializeField, Min(0.1f)] private float SkillGestureDuration = 1.8f;
        [SerializeField, Range(0, 1)] private float SkillGestureWeight = 1;
        [Tooltip("Independent prop bones that must follow the hand when the copied arm motion is active.")]
        [SerializeField] private Data_MotionPropAttachment[] MotionPropAttachments = new Data_MotionPropAttachment[0];
        // Retained for older prefab/builder compatibility; the base-speed ratio replaces stride calibration.
        [UnityEngine.Serialization.FormerlySerializedAs("AnimationStride")]
        [HideInInspector] public float RunReferenceSpeed = 12;
        [Tooltip("Complete gallops in one source clip; some legacy clips contain two.")]
        public int GallopsPerClip = 1;
        [Range(0, 2)] public float RiderMotion = 1;
        public Vector3 Focus => CameraTarget.position;
        public bool HasRider => RiderAnimator != null && RiderRoot != null && RidingClip != null;
        public bool HasPose { get; private set; }
        public float CurrentAnimationRate { get; private set; }
        public float GaitPhase => activeRunClip == null ? 0 : Mathf.Repeat((float)(gaitClock / activeRunClip.length * GallopsPerClip), 1);
        private Sys_Race race;
        private PlayableGraph mountGraph;
        private PlayableGraph riderGraph;
        private AnimationClipPlayable idle;
        private AnimationClipPlayable run;
        private AnimationClipPlayable riding;
        private AnimationMixerPlayable mixer;
        private AnimationClip activeRunClip;
        private AnimationClipPlayable previewRun;
        private AnimationLayerMixerPlayable runLayers;
        private AnimationClipPlayable skillGesture;
        private AnimationLayerMixerPlayable skillLayers;
        private Vector3 seatRest;
        private Quaternion seatRestRotation;
        private double previousClock;
        private Vector3 previousPosition;
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
            activeRunClip = RunClip;
            run = AnimationClipPlayable.Create(mountGraph, activeRunClip);
            mixer = AnimationMixerPlayable.Create(mountGraph, 2);
            mountGraph.Connect(idle, 0, mixer, 0);
            mountGraph.Connect(run, 0, mixer, 1);
            var output = AnimationPlayableOutput.Create(mountGraph, "mount", MountAnimator);
            output.SetSourcePlayable(mixer);
            if (UseMonsterMotionPreview && PreviewRunClip != null && SkillGestureMask != null)
            {
                previewRun = AnimationClipPlayable.Create(mountGraph, PreviewRunClip);
                runLayers = AnimationLayerMixerPlayable.Create(mountGraph, 2);
                mountGraph.Connect(mixer, 0, runLayers, 0);
                mountGraph.Connect(previewRun, 0, runLayers, 1);
                runLayers.SetInputWeight(0, 1);
                runLayers.SetInputWeight(1, 0);
                runLayers.SetLayerMaskFromAvatarMask(1, SkillGestureMask);
                output.SetSourcePlayable(runLayers);
            }
            if (UseMonsterMotionPreview && SkillGestureClip != null && SkillGestureMask != null)
            {
                skillGesture = AnimationClipPlayable.Create(mountGraph, SkillGestureClip);
                skillLayers = AnimationLayerMixerPlayable.Create(mountGraph, 2);
                if (runLayers.IsValid()) mountGraph.Connect(runLayers, 0, skillLayers, 0);
                else mountGraph.Connect(mixer, 0, skillLayers, 0);
                mountGraph.Connect(skillGesture, 0, skillLayers, 1);
                skillLayers.SetInputWeight(0, 1);
                skillLayers.SetInputWeight(1, 0);
                skillLayers.SetLayerMaskFromAvatarMask(1, SkillGestureMask);
                output.SetSourcePlayable(skillLayers);
            }
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
            float interpolation = Mathf.Clamp01(race.Interpolation);
            float distance = Mathf.Lerp((float)s.PreviousDistance, (float)s.Distance, interpolation);
            float lane = race.Simulation.UsesLaneContests ? Mathf.Lerp(s.PreviousLane, s.Lane, interpolation) : s.Lane;
            bool coasting = race.Simulation.Phase == E_RacePhase.Result && race.Simulation.CoolingDown && race.CoolDownAfterFinish;
            bool moving = race.Simulation.Phase == E_RacePhase.Running || race.Simulation.Phase == E_RacePhase.Finishing || coasting;
            if (race.Simulation.Phase == E_RacePhase.Result && !coasting) { distance = (float)s.Distance; lane = s.Lane; }
            Vector3 position = race.Simulation.Point(distance, lane);
            if (generation != race.Generation)
            {
                generation = race.Generation; previousClock = 0;
                previousPosition = position;
                gaitClock = RunnerId * 0.173 * activeRunClip.length / Mathf.Max(1, GallopsPerClip);
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
            // Start intro timeline holds the race before the start: keep the idle pose alive.
            // Mid-race holds (skill video / comic) still freeze the pose.
            bool preStartHold = race.PresentationHeld && race.Simulation.Phase == E_RacePhase.Preparing;
            float poseDelta = race.Paused || (race.PresentationHeld && !preStartHold) ? 0
                : (preStartHold || race.Countdown > 0 || race.Simulation.Phase == E_RacePhase.Result
                    ? Time.unscaledDeltaTime * race.PlaybackSpeed : (float)delta);
            idleClock += poseDelta;
            // Tune cadence at BaseSpeed, then preserve that relationship through acceleration and slowdown.
            float travel = moving && delta > 0 ? Vector3.Distance(previousPosition, position) : 0;
            previousPosition = position;
            float speedRatio = ScaleRunAnimationWithSpeed
                ? Mathf.Max(0, s.Speed) / Mathf.Max(0.1f, race.Entrants[RunnerId].BaseSpeed) : 1;
            double gaitDelta = travel > 0
                ? delta * Mathf.Max(0, RunAnimationSpeed) * speedRatio : 0;
            CurrentAnimationRate = delta > 0 ? (float)(gaitDelta / delta) : 0;
            gaitClock += gaitDelta;
            float motionSpeed = moving && delta > 0 ? travel / (float)delta : 0;
            displayedSpeed = Mathf.Lerp(displayedSpeed, motionSpeed, 1 - Mathf.Exp(-12 * poseDelta));
            int foot = (int)(gaitClock / activeRunClip.length * GallopsPerClip * 2);
            if (HasFootfalls && foot != lastFoot && s.Speed > 2 && !race.Paused)
            {
                lastFoot = foot;
                var camera = Camera.main;
                if (camera != null) Mgr_Sound.PlayFoot(Mathf.Clamp01(1 - Vector3.Distance(camera.transform.position, transform.position) / 38));
            }
            transform.position = position;
            var forward = race.Simulation.Forward(distance);
            // Face the chosen gap as well as the course: a lane change must not look like sideways sliding.
            if (race.Simulation.UsesLaneContests && s.Speed > .5f)
            {
                var lateral = (race.Simulation.Point(distance, lane + .1f) - position).normalized;
                forward = (forward * s.Speed + lateral * s.LaneVelocity).normalized;
            }
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            float blend = Mathf.SmoothStep(0, 1, displayedSpeed / 4);
            mixer.SetInputWeight(0, 1 - blend); mixer.SetInputWeight(1, blend);
            idle.SetTime(idleClock * 0.65);
            run.SetTime(gaitClock);
            if (runLayers.IsValid())
            {
                runLayers.SetInputWeight(1, blend);
                previewRun.SetTime(gaitClock / activeRunClip.length * PreviewRunClip.length);
            }
            UpdateSkillGesture(s.SkillAge, clock - race.Simulation.Time, moving && !s.Finished && !race.IsSkipping);
            mountGraph.Evaluate(0);
            UpdateMotionProps();
            MountRoot.localPosition = Vector3.zero;
            MountRoot.localRotation = Quaternion.identity;
            float phase = (float)(gaitClock / activeRunClip.length * GallopsPerClip * Mathf.PI * 2);
            if (HasRider)
            {
                // Play the entire seated cycle; the old 0.23..0.37 s sample barely moved any bones.
                riding.SetTime(gaitClock / activeRunClip.length * GallopsPerClip * RidingClip.length + idleClock * (1 - blend) * 0.3);
                riderGraph.Evaluate(0);
                float surge = s.SkillAge >= 0 && s.SkillAge < 3.4f ? Mathf.Sin(Mathf.Clamp01(s.SkillAge / 3.4f) * Mathf.PI) : 0;
                var seatRotation = Quaternion.Inverse(MountRoot.rotation) * SeatBone.rotation * Quaternion.Inverse(seatRestRotation);
                float seatPitch = Mathf.Clamp(Mathf.DeltaAngle(0, seatRotation.eulerAngles.x), -18, 18);
                float bend = Vector3.SignedAngle(forward, race.Simulation.Forward(distance + 5), Vector3.up);
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
        private void UpdateMotionProps()
        {
            if (!UseMonsterMotionPreview || MotionPropAttachments == null) return;
            foreach (var attachment in MotionPropAttachments)
            {
                if (attachment == null || attachment.Prop == null || attachment.Hand == null) continue;
                attachment.Prop.SetPositionAndRotation(attachment.Hand.TransformPoint(attachment.LocalPosition),
                    attachment.Hand.rotation * attachment.LocalRotation);
            }
        }
        private void UpdateSkillGesture(float skillAge, double interpolationOffset, bool moving)
        {
            if (!skillLayers.IsValid()) return;
            float age = skillAge < 0 ? -1 : Mathf.Max(0, skillAge + (float)interpolationOffset);
            float duration = Mathf.Max(0.1f, SkillGestureDuration);
            bool active = UseMonsterMotionPreview && moving && age >= 0 && age < duration;
            float weight = active
                ? Mathf.SmoothStep(0, 1, age / Mathf.Min(0.18f, duration * 0.25f))
                    * Mathf.SmoothStep(0, 1, (duration - age) / Mathf.Min(0.3f, duration * 0.25f))
                    * SkillGestureWeight : 0;
            skillLayers.SetInputWeight(1, weight);
            // Use the simulation clock so pause, presentation holds, playback speed and restart stay aligned.
            // Only the mask's arm/shoulder bones change; locomotion and race calculation continue underneath.
            skillGesture.SetTime(Mathf.Clamp01(age / duration) * SkillGestureClip.length);
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

