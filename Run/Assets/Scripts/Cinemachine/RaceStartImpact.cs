using UnityEngine;
using Unity.Cinemachine;

namespace ProjectRun.Race.CameraRig
{
    /// <summary>
    /// Fires once at the race start (E_RaceEvent.Start): camera shake, FOV punch on <see cref="Cameras"/>
    /// and a weight pulse on <see cref="ImpactVolume"/> (chromatic aberration / lens distortion / vignette / bloom).
    /// Each target camera needs a CinemachineImpulseListener on the same channel as <see cref="Source"/>.
    /// </summary>
    [DefaultExecutionOrder(140)]
    public sealed class RaceStartImpact : MonoBehaviour
    {
        [Header("Refs")]
        public Sys_Race Race;
        [Tooltip("Cameras that receive the FOV punch (the camera that is live when the race starts).")]
        public CinemachineCamera[] Cameras;
        public CinemachineImpulseSource Source;
        [Tooltip("Global volume whose weight is pulsed 0 -> 1 -> 0. Keep its weight at 0 in the scene.")]
        public UnityEngine.Rendering.Volume ImpactVolume;

        [Header("Trigger")]
        [Tooltip("On: plays at the race start. Off: plays only when Play() is called (e.g. skill camera entry).")]
        public bool PlayOnRaceStart = true;

        [Header("Shake")]
        public bool EnableShake = true;
        [Min(0f)] public float ShakeStrength = 1.3f;
        [Min(0.05f)] public float ShakeDuration = 0.35f;
        public Vector3 ShakeVelocity = new Vector3(0.6f, 1f, 0.35f);

        [Header("FOV Punch")]
        public bool EnableFovPunch = true;
        [Tooltip("Degrees added to the camera FOV at the peak.")]
        public float FovKick = 10f;
        [Min(0.01f)] public float FovAttack = 0.15f;
        [Min(0.01f)] public float FovRelease = 0.5f;

        [Header("Post-processing Pulse")]
        public bool EnableVolumePulse = true;
        [Min(0.01f)] public float VolumeAttack = 0.08f;
        [Min(0.01f)] public float VolumeRelease = 0.45f;

        private float[] baseFov;
        private float t = -1f;

        private void OnEnable()
        {
            Mgr_Event.RaceChanged += OnRaceChanged;
            Mgr_Event.Reset += StopImpact;
            if (ImpactVolume != null) ImpactVolume.weight = 0f;
        }

        private void OnDisable()
        {
            Mgr_Event.RaceChanged -= OnRaceChanged;
            Mgr_Event.Reset -= StopImpact;
            StopImpact();
        }

        private void OnRaceChanged(Data_RaceEvent e)
        {
            if (!PlayOnRaceStart || e.Kind != E_RaceEvent.Start) return;
            if (Race != null && Race.IsSkipping) return;
            Play();
        }

        [ContextMenu("Play Impact")]
        public void Play()
        {
            StopImpact();
            if (Cameras != null)
            {
                baseFov = new float[Cameras.Length];
                for (int i = 0; i < Cameras.Length; i++)
                    if (Cameras[i] != null) baseFov[i] = Cameras[i].Lens.FieldOfView;
            }
            if (EnableShake && Source != null)
            {
                Source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Custom;
                Source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
                Source.ImpulseDefinition.CustomImpulseShape = RaceCameraPassShake.GetDefaultShudderCurve();
                Source.ImpulseDefinition.ImpulseDuration = ShakeDuration;
                Source.GenerateImpulseWithVelocity(ShakeVelocity * ShakeStrength);
            }
            t = 0f;
        }

        private void LateUpdate()
        {
            if (t < 0f) return;
            t += Time.unscaledDeltaTime;

            if (EnableFovPunch && Cameras != null && baseFov != null)
            {
                float k = Envelope(t, FovAttack, FovRelease);
                for (int i = 0; i < Cameras.Length && i < baseFov.Length; i++)
                    if (Cameras[i] != null) Cameras[i].Lens.FieldOfView = baseFov[i] + FovKick * k;
            }
            if (EnableVolumePulse && ImpactVolume != null)
                ImpactVolume.weight = Envelope(t, VolumeAttack, VolumeRelease);

            if (t >= Mathf.Max(FovAttack + FovRelease, VolumeAttack + VolumeRelease)) StopImpact();
        }

        /// <summary>Fast ease-out rise, smooth fall. Returns 0..1.</summary>
        private static float Envelope(float time, float attack, float release)
        {
            if (time <= 0f) return 0f;
            if (time < attack) { float a = time / attack; return 1f - (1f - a) * (1f - a); }
            float r = (time - attack) / release;
            return r >= 1f ? 0f : 1f - Mathf.SmoothStep(0f, 1f, r);
        }

        private void StopImpact()
        {
            if (t >= 0f && Cameras != null && baseFov != null)
                for (int i = 0; i < Cameras.Length && i < baseFov.Length; i++)
                    if (Cameras[i] != null) Cameras[i].Lens.FieldOfView = baseFov[i];
            if (ImpactVolume != null) ImpactVolume.weight = 0f;
            t = -1f;
        }
    }
}
