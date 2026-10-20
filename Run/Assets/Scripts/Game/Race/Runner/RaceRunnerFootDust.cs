using System;
using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>
    /// Emits a dust puff on the ground under each tracked foot when it touches down.
    /// Bipeds track 2 feet, quadrupeds 4, blob runners (Poring) one body bone with a larger puff.
    /// Touch-down is detected from each bone's height above the runner root, relative to the
    /// range that bone has been moving through, so it works at any creature scale or clip.
    /// </summary>
    [DefaultExecutionOrder(100)] // after RaceRunnerView (-50) has posed the skeleton
    public sealed class RaceRunnerFootDust : MonoBehaviour
    {
        [Serializable]
        public class Foot
        {
            public Transform Bone;
            [Tooltip("Puff size multiplier for this foot (blob body: ~2).")]
            [Min(0.1f)] public float Size = 1f;
        }

        [Header("Refs")]
        [Tooltip("Found in parents if empty.")]
        public RaceRunnerView View;
        [Tooltip("Dust particle prefab (eff_foot_dust). One instance per runner, emitted on demand.")]
        public ParticleSystem DustPrefab;
        public Foot[] Feet = Array.Empty<Foot>();

        [Header("Touch-down Detection")]
        [Tooltip("Foot counts as down below this fraction of its height range.")]
        [Range(0.05f, 0.6f)] public float ContactFraction = 0.25f;
        [Tooltip("Foot counts as lifted above this fraction (must be above ContactFraction).")]
        [Range(0.1f, 0.9f)] public float ReleaseFraction = 0.5f;
        [Tooltip("Bones moving less than this (m) are ignored (no puffs from feet that barely move).")]
        [Min(0f)] public float MinStepRange = 0.05f;
        [Tooltip("No dust below this race speed.")]
        [Min(0f)] public float MinSpeed = 2f;

        [Header("Look")]
        [Tooltip("Particles per puff at Size 1.")]
        [Min(1)] public int ParticlesPerPuff = 5;
        [Tooltip("Global size multiplier.")]
        [Min(0.1f)] public float PuffScale = 1f;

        private struct Track { public float Low, High; public bool Down, Init; }
        private Track[] tracks;
        private ParticleSystem dust;
        private Sys_Race race;
        private int generation = -1;

        private void Awake()
        {
            if (View == null) View = GetComponentInParent<RaceRunnerView>();
            tracks = new Track[Feet != null ? Feet.Length : 0];
            if (DustPrefab != null)
            {
                dust = Instantiate(DustPrefab, transform);
                dust.name = "eff_foot_dust";
                dust.transform.localPosition = Vector3.zero;
                dust.gameObject.SetActive(true); // an inactive system never simulates or renders
                var main = dust.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                // A stopped system does not simulate Emit()-ed particles (they freeze at birth, alpha 0).
                // Keep it playing; rate 0 means nothing is emitted except our puffs.
                main.loop = true;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate; // looping systems pause when culled otherwise
                var emission = dust.emission; emission.rateOverTime = 0f; emission.rateOverDistance = 0f;
                dust.Play(true);
            }
        }

        private void LateUpdate()
        {
            if (dust == null || View == null || Feet == null) return;
            if (!dust.isPlaying) dust.Play(true); // emitted particles only simulate while the system plays
            if (race == null) { race = FindFirstObjectByType<Sys_Race>(); if (race == null) return; }
            var sim = race.Simulation;
            if (sim == null || View.RunnerId < 0 || View.RunnerId >= sim.Runners.Length) return;
            if (generation != race.Generation || race.IsSkipping)
            {
                generation = race.Generation;
                for (int i = 0; i < tracks.Length; i++) tracks[i].Init = false;
                dust.Clear();
            }

            bool canEmit = !race.Paused && !race.PresentationHeld && !race.IsSkipping && race.Countdown <= 0f
                && sim.Runners[View.RunnerId].Speed >= MinSpeed;
            float groundY = View.transform.position.y;
            float dt = Time.deltaTime;

            for (int i = 0; i < Feet.Length && i < tracks.Length; i++)
            {
                var bone = Feet[i] != null ? Feet[i].Bone : null;
                if (bone == null) continue;
                float h = bone.position.y - groundY;
                ref var t = ref tracks[i];
                if (!t.Init) { t.Low = t.High = h; t.Down = true; t.Init = true; continue; }

                // Learn the bone's motion range; relax slowly so it adapts to clip / speed changes.
                float range = t.High - t.Low;
                t.Low = Mathf.Min(h, t.Low + range * 0.2f * dt);
                t.High = Mathf.Max(h, t.High - range * 0.2f * dt);
                range = t.High - t.Low;
                if (range < MinStepRange) continue;

                float k = (h - t.Low) / range;
                if (!t.Down && k <= ContactFraction)
                {
                    t.Down = true;
                    if (canEmit) Puff(bone.position, groundY, Feet[i].Size);
                }
                else if (t.Down && k >= ReleaseFraction) t.Down = false;
            }
        }

        private void Puff(Vector3 footPosition, float groundY, float size)
        {
            float scale = size * PuffScale;
            var p = new ParticleSystem.EmitParams
            {
                position = new Vector3(footPosition.x, groundY + 0.03f, footPosition.z),
                applyShapeToPosition = true
            };
            var main = dust.main;
            int count = Mathf.Max(1, Mathf.RoundToInt(ParticlesPerPuff * Mathf.Sqrt(scale)));
            for (int n = 0; n < count; n++)
            {
                p.startSize = main.startSize.Evaluate(UnityEngine.Random.value) * scale;
                dust.Emit(p, 1);
            }
        }

        private void OnEnable()
        {
            if (dust != null && !dust.isPlaying) dust.Play(true);
        }

        private void OnDisable()
        {
            if (dust != null) dust.Clear();
            if (tracks != null) for (int i = 0; i < tracks.Length; i++) tracks[i].Init = false;
        }
    }
}
