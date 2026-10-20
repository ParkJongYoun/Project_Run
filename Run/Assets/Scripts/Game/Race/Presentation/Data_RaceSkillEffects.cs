using System;
using UnityEngine;

namespace ProjectRun.Race
{
    [Serializable]
    public sealed class Data_RaceSkillEffectEntry
    {
        public int RunnerId;
        public string MonsterName;
        public bool Enabled = true;
        public GameObject Prefab;
        [Min(.1f)] public float Scale = 1;
        [Min(0)] public float GroundHeight = .12f;
    }

    [CreateAssetMenu(menuName = "Project Run/Race Skill Effects")]
    public sealed class Data_RaceSkillEffects : ScriptableObject
    {
        public const string ResourcePath = "RaceSkillEffects/Data_RaceSkillEffects";
        [Tooltip("Uncheck to restore the previous race effects. Does not change any race calculation.")]
        public bool Enabled = true;
        public Data_RaceSkillEffectEntry[] Runners = Array.Empty<Data_RaceSkillEffectEntry>();

        public Data_RaceSkillEffectEntry Find(int runnerId)
        {
            foreach (var entry in Runners)
                if (entry != null && entry.RunnerId == runnerId) return entry;
            return null;
        }
    }
}
