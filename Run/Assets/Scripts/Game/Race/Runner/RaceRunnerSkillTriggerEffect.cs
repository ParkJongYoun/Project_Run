using UnityEngine;

namespace ProjectRun.Race
{
    /// <summary>
    /// Plays the one-shot skill start effect (e.g. eff_skill_start) whenever this runner triggers a skill.
    /// Responds to both direct race events and simulation skill clock resets.
    /// </summary>
    [DefaultExecutionOrder(75)]
    public sealed class RaceRunnerSkillTriggerEffect : MonoBehaviour
    {
        [Tooltip("Runner view owning this effect. Found automatically if null.")]
        public RaceRunnerView View;
        [Tooltip("Optional runner id override if View is not available.")]
        public int RunnerId = -1;
        [Tooltip("One-shot particle systems attached to or under this object.")]
        public ParticleSystem[] Particles;

        private Sys_Race race;
        private int lastActivationCount;
        private int lastGeneration = -1;

        public void Bind(Sys_Race target, int id)
        {
            race = target;
            RunnerId = id;
            if (race != null)
            {
                lastGeneration = race.Generation;
                var sim = race.Simulation;
                if (sim != null && RunnerId >= 0 && RunnerId < sim.Runners.Length)
                {
                    lastActivationCount = sim.Runners[RunnerId].SkillActivationCount;
                }
            }
        }

        private void Awake()
        {
            if (View == null) View = GetComponentInParent<RaceRunnerView>();
            if (View != null && RunnerId < 0) RunnerId = View.RunnerId;
            CacheParticles();
            StopAndClear();
        }

        private void OnEnable()
        {
            Mgr_Event.RaceChanged += OnRaceChanged;
            Mgr_Event.Reset += OnReset;
        }

        private void OnDisable()
        {
            Mgr_Event.RaceChanged -= OnRaceChanged;
            Mgr_Event.Reset -= OnReset;
            StopAndClear();
        }

        public void CacheParticles()
        {
            Particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void OnReset()
        {
            lastActivationCount = 0;
            lastGeneration = -1;
            StopAndClear();
        }

        private void OnRaceChanged(Data_RaceEvent evt)
        {
            int targetId = RunnerId >= 0 ? RunnerId : (View != null ? View.RunnerId : -1);
            if (targetId < 0) return;

            if (evt.Kind == E_RaceEvent.Skill && evt.RunnerId == targetId)
            {
                Play();
            }
        }

        private void LateUpdate()
        {
            if (race == null)
            {
                race = FindFirstObjectByType<Sys_Race>();
                if (race == null) return;
            }

            var sim = race.Simulation;
            if (sim == null) return;

            int targetId = RunnerId >= 0 ? RunnerId : (View != null ? View.RunnerId : -1);
            if (targetId < 0 || targetId >= sim.Runners.Length) return;

            if (race.Generation != lastGeneration)
            {
                lastGeneration = race.Generation;
                lastActivationCount = sim.Runners[targetId].SkillActivationCount;
                StopAndClear();
                return;
            }

            var runner = sim.Runners[targetId];
            if (runner.SkillActivationCount > lastActivationCount)
            {
                lastActivationCount = runner.SkillActivationCount;
                Play();
            }
        }

        [ContextMenu("Play Skill Start Effect")]
        public void Play()
        {
            if (Particles == null || Particles.Length == 0) CacheParticles();
            if (Particles == null) return;

            for (int i = 0; i < Particles.Length; i++)
            {
                var ps = Particles[i];
                if (ps == null) continue;
                ps.gameObject.SetActive(true);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
            }
        }

        public void StopAndClear()
        {
            if (Particles == null || Particles.Length == 0) CacheParticles();
            if (Particles == null) return;

            for (int i = 0; i < Particles.Length; i++)
            {
                var ps = Particles[i];
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
