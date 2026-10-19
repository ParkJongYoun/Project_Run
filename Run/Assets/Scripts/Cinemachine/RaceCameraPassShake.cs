using UnityEngine;
using Unity.Cinemachine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Shakes this camera every time a runner passes in front of it (any runner, any number of times, every lap).
    /// "In front" = the point on the track closest to the camera. Closer runners shake harder; simultaneous passes stack.
    /// Intended for a fixed-position camera (no Follow body).
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineImpulseSource), typeof(CinemachineImpulseListener))]
    public sealed class RaceCameraPassShake : MonoBehaviour
    {
        [Header("Refs")]
        public Sys_Race Race;

        [Header("Shake")]
        [Tooltip("Shake only while this camera is on screen.")]
        public bool OnlyWhenLive = true;
        [Tooltip("Shake strength for a runner passing right next to the camera.")]
        [Min(0f)] public float Strength = 1.8f;
        [Tooltip("Length of one shake (seconds).")]
        [Min(0.05f)] public float Duration = 0.5f;
        [Tooltip("Runners passing farther than this (m) from the camera do not shake it.")]
        [Min(0.5f)] public float MaxDistance = 15f;
        [Tooltip("Base shake displacement in camera space (X=lateral, Y=vertical, Z=depth).")]
        public Vector3 Velocity = new Vector3(0.85f, 1.3f, 0.45f);
        [Tooltip("Randomize lateral and depth directions slightly per pass for a dynamic feel.")]
        public bool RandomizeDirection = true;
        [Tooltip("Custom shake curve. If empty, a rapid high-frequency shudder curve (바바바박) is used.")]
        public AnimationCurve ShakeCurve;

        public int PassCount { get; private set; }

        private CinemachineCamera cam;
        private CinemachineImpulseSource source;
        private CinemachineBrain brain;
        private double[] last;
        private int generation = -1;
        private float trackDistance = -1f;
        private Vector3 measuredAt;

        private void Awake()
        {
            cam = GetComponent<CinemachineCamera>();
            source = GetComponent<CinemachineImpulseSource>();
            ConfigureImpulseSource();
            brain = FindFirstObjectByType<CinemachineBrain>();
        }

        private void OnValidate()
        {
            if (source == null) source = GetComponent<CinemachineImpulseSource>();
            if (source != null) ConfigureImpulseSource();
        }

        public void ConfigureImpulseSource()
        {
            if (source == null) return;
            source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Custom;
            source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            source.ImpulseDefinition.CustomImpulseShape = (ShakeCurve != null && ShakeCurve.length > 0)
                ? ShakeCurve
                : GetDefaultShudderCurve();
            source.ImpulseDefinition.ImpulseDuration = Duration;
            source.DefaultVelocity = Velocity;
        }

        public static AnimationCurve GetDefaultShudderCurve()
        {
            var curve = new AnimationCurve();
            // Intense multi-hit rapid shudder ("바바바박")
            curve.AddKey(new Keyframe(0.00f,  0.0f));
            curve.AddKey(new Keyframe(0.035f, 1.0f));
            curve.AddKey(new Keyframe(0.09f, -0.90f));
            curve.AddKey(new Keyframe(0.155f, 0.80f));
            curve.AddKey(new Keyframe(0.23f, -0.65f));
            curve.AddKey(new Keyframe(0.31f,  0.50f));
            curve.AddKey(new Keyframe(0.40f, -0.35f));
            curve.AddKey(new Keyframe(0.50f,  0.22f));
            curve.AddKey(new Keyframe(0.62f, -0.12f));
            curve.AddKey(new Keyframe(0.76f,  0.05f));
            curve.AddKey(new Keyframe(0.88f, -0.02f));
            curve.AddKey(new Keyframe(1.00f,  0.0f));

            for (int i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }
            return curve;
        }

        private void LateUpdate()
        {
            var sim = Race != null ? Race.Simulation : null;
            if (sim == null) return;
            int n = sim.Runners.Length;
            if (last == null || last.Length != n || generation != Race.Generation)
            {
                generation = Race.Generation;
                last = new double[n];
                for (int i = 0; i < n; i++) last[i] = sim.Runners[i].Distance;
                trackDistance = -1f;
                PassCount = 0;
            }
            // Re-measure when the camera is moved (e.g. while tweaking its position in play mode).
            if (trackDistance < 0f || (transform.position - measuredAt).sqrMagnitude > 1f) Measure(sim);

            bool live = brain == null || ReferenceEquals(brain.ActiveVirtualCamera, cam);
            float lap = sim.LapLength;
            for (int i = 0; i < n; i++)
            {
                var r = sim.Runners[i];
                double prev = last[i], cur = r.Distance;
                last[i] = cur;
                if (cur <= prev) continue;
                // Latest crossing of the camera point (any lap) at or before the current distance.
                double cross = trackDistance + System.Math.Floor((cur - trackDistance) / lap) * lap;
                if (cross <= prev || cross < 0) continue; // did not pass the camera this frame
                if (OnlyWhenLive && !live) continue;
                float dist = Vector3.Distance(transform.position, sim.Point((float)cross, r.Lane));
                float k = 1f - dist / MaxDistance;
                if (k <= 0f) continue;

                source.ImpulseDefinition.ImpulseDuration = Duration;
                Vector3 passVelocity = Velocity;
                if (RandomizeDirection)
                {
                    float signX = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.85f, 1.25f);
                    float signZ = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.7f, 1.2f);
                    passVelocity = new Vector3(Velocity.x * signX, Velocity.y * Random.Range(0.9f, 1.15f), Velocity.z * signZ);
                }
                source.GenerateImpulseWithVelocity(passVelocity * (Strength * k));
                PassCount++;
            }
        }

        /// <summary>Finds the track distance closest to the camera.</summary>
        private void Measure(Data_RaceSimulation sim)
        {
            measuredAt = transform.position;
            float best = float.MaxValue, lap = sim.LapLength;
            for (float d = 0f; d < lap; d += 0.25f)
            {
                Vector3 p = sim.Point(d); p.y = measuredAt.y;
                float s = (p - measuredAt).sqrMagnitude;
                if (s < best) { best = s; trackDistance = d; }
            }
        }
    }
}
