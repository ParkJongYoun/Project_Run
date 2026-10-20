using System.Collections.Generic;
using UnityEngine;

namespace ProjectRun.Race
{
    // Optional presentation module. Reads the simulation; never writes race/skill state or Unity Random.
    [DefaultExecutionOrder(230)]
    public sealed class Sys_RaceSkillEffects : MonoBehaviour
    {
        public Data_RaceSkillEffects Configuration;
        private Sys_Race race;
        private Sys_RacePresentation presentation;
        private Sys_DemoSkillVideo video;
        private Sys_DemoSkillEffects legacyDemo;
        private int generation = -1;
        private readonly List<RunnerEffect> effects = new List<RunnerEffect>();

        private sealed class RunnerEffect
        {
            public RaceRunnerView View;
            public Data_RaceSkillEffectEntry Entry;
            public GameObject Root;
            public ParticleSystem[] Particles;
            public Behaviour[] Legacy;
            public bool[] LegacyEnabled;
            public bool LegacySuppressed;
            public bool OriginalParticleSuppression;
            public bool Visible;
            public bool Waiting;
            public int Activation;
            public double LastClock;
            public float VisibleAge;
            public int Starts;
        }

        public bool IsVisible(int runnerId) => effects.Exists(x => x.View.RunnerId == runnerId && x.Visible);
        public bool IsWaitingForVideo(int runnerId) => effects.Exists(x => x.View.RunnerId == runnerId && x.Waiting);
        public int StartCount(int runnerId)
        {
            var effect = effects.Find(x => x.View.RunnerId == runnerId);
            return effect == null ? 0 : effect.Starts;
        }

        private void Start()
        {
            race = GetComponent<Sys_Race>();
            if (race == null) race = FindFirstObjectByType<Sys_Race>();
            presentation = FindFirstObjectByType<Sys_RacePresentation>();
            video = FindFirstObjectByType<Sys_DemoSkillVideo>();
            legacyDemo = GetComponent<Sys_DemoSkillEffects>();
            if (Configuration == null)
                Configuration = Mgr_Bundle.Load<Data_RaceSkillEffects>(Data_RaceSkillEffects.ResourcePath);
        }

        private bool Initialize()
        {
            if (Configuration == null || race == null || presentation == null ||
                presentation.Views == null || presentation.Views.Length != race.Entrants.Length) return false;
            foreach (var view in presentation.Views) if (view == null || !view.HasPose) return false;
            foreach (var view in presentation.Views)
            {
                var entry = Configuration.Find(view.RunnerId);
                if (entry == null || entry.Prefab == null) continue;
                var root = Instantiate(entry.Prefab, transform);
                root.name = "obj_skill_fx_" + entry.MonsterName;
                root.SetActive(false);
                var legacy = new List<Behaviour>();
                legacy.AddRange(view.GetComponentsInChildren<RaceRunnerSkillAura>(true));
                legacy.AddRange(view.GetComponentsInChildren<RaceRunnerSkillTriggerEffect>(true));
                var effect = new RunnerEffect
                {
                    View = view, Entry = entry, Root = root,
                    Particles = root.GetComponentsInChildren<ParticleSystem>(true),
                    Legacy = legacy.ToArray(), LegacyEnabled = new bool[legacy.Count],
                    OriginalParticleSuppression = view.SuppressSkillParticles
                };
                for (int i = 0; i < legacy.Count; i++) effect.LegacyEnabled[i] = legacy[i].enabled;
                for (int i = 0; i < effect.Particles.Length; i++)
                {
                    var particle = effect.Particles[i];
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.useAutoRandomSeed = false;
                    particle.randomSeed = (uint)(17011 + view.RunnerId * 103 + i * 7);
                }
                effects.Add(effect);
            }
            return effects.Count > 0;
        }

        private void LateUpdate()
        {
            if (race == null || race.Simulation == null || Configuration == null) return;
            if (effects.Count == 0 && !Initialize()) return;
            var sim = race.Simulation;
            if (generation != race.Generation)
            {
                generation = race.Generation;
                foreach (var effect in effects)
                {
                    Stop(effect);
                    effect.Activation = 0;
                    effect.Starts = 0;
                }
            }
            Shader.SetGlobalFloat("_RunRaceEffectTime", (float)sim.Time);
            bool heroSuppressed = false;
            foreach (var effect in effects)
            {
                int id = effect.View.RunnerId;
                bool use = Configuration.Enabled && effect.Entry.Enabled;
                SetLegacySuppression(effect, use);
                bool hero = Mgr_Game.Instance != null && id == Mgr_Game.Instance.HeroRunner;
                if (hero) heroSuppressed = use;
                var runner = sim.Runners[id];
                if (!use || race.IsSkipping || runner.Finished || sim.Phase == E_RacePhase.Result ||
                    runner.SkillAge < 0 || runner.SkillAge >= race.Entrants[id].SurgeDuration)
                {
                    Stop(effect);
                    continue;
                }
                if (runner.SkillActivationCount != effect.Activation)
                {
                    Stop(effect);
                    effect.Activation = runner.SkillActivationCount;
                }
                // IsActive includes the intro and the complete return fade. Never flash underneath the movie.
                if (hero && video != null && video.IsActive)
                {
                    Stop(effect);
                    effect.Waiting = true;
                    continue;
                }
                effect.Waiting = false;
                if (race.Paused || race.PresentationHeld) continue;
                effect.Root.transform.SetPositionAndRotation(
                    effect.View.transform.position + Vector3.up * effect.Entry.GroundHeight,
                    Quaternion.LookRotation(sim.Forward((float)runner.Distance), Vector3.up));
                effect.Root.transform.localScale = Vector3.one * effect.Entry.Scale;
                if (!effect.Visible)
                {
                    effect.Root.SetActive(true);
                    foreach (var ps in effect.Particles) ps.Simulate(0, false, true, false);
                    effect.LastClock = sim.Time;
                    effect.VisibleAge = 0;
                    effect.Visible = true;
                    effect.Starts++;
                    // Show the authored opening burst on the first frame after the movie.
                    foreach (var ps in effect.Particles) ps.Simulate(.016f, false, false, false);
                }
                float delta = Mathf.Max(0, (float)(sim.Time - effect.LastClock));
                effect.LastClock = sim.Time;
                effect.VisibleAge += delta;
                // Simulate leaves systems paused; no independent engine-time advancement occurs.
                if (delta > 0)
                    foreach (var ps in effect.Particles) ps.Simulate(delta, false, false, false);
            }
            if (legacyDemo != null) legacyDemo.SuppressGeneratedEffects = heroSuppressed;
        }

        private static void SetLegacySuppression(RunnerEffect effect, bool suppress)
        {
            if (effect.LegacySuppressed == suppress) return;
            effect.LegacySuppressed = suppress;
            for (int i = 0; i < effect.Legacy.Length; i++)
                if (effect.Legacy[i] != null) effect.Legacy[i].enabled = !suppress && effect.LegacyEnabled[i];
            if (effect.View != null) effect.View.SuppressSkillParticles = suppress || effect.OriginalParticleSuppression;
            if (suppress && effect.View != null)
            {
                if (effect.View.Sparks != null) effect.View.Sparks.Clear();
                if (effect.View.Trails != null)
                    foreach (var trail in effect.View.Trails) if (trail != null) trail.Clear();
            }
        }

        private static void Stop(RunnerEffect effect)
        {
            if (effect.Visible)
                foreach (var ps in effect.Particles)
                    if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (effect.Root != null) effect.Root.SetActive(false);
            effect.Visible = effect.Waiting = false;
            effect.VisibleAge = 0;
        }

        private void OnDisable()
        {
            foreach (var effect in effects)
            {
                Stop(effect);
                SetLegacySuppression(effect, false);
            }
            if (legacyDemo != null) legacyDemo.SuppressGeneratedEffects = false;
        }

        private void OnDestroy()
        {
            foreach (var effect in effects) if (effect.Root != null) Destroy(effect.Root);
            effects.Clear();
        }
    }
}
