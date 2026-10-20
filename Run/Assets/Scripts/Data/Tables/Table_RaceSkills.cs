using System;
using UnityEngine;

namespace ProjectRun.Race
{
    public enum E_RaceSkillRarity { Normal, Rare, Unique }
    public enum E_RaceLaneIntent { None, Middle, Outside }

    [Serializable]
    public sealed class Data_RaceSkillSet
    {
        public int RunnerId;
        public string MonsterId;
        public string Name;
        public string Normal;
        public string Rare;
        public string Unique;
        public int NormalCount = 2;
        public int RareCountMin = 1;
        public int RareCountMax = 2;
        public float NormalPower = .012f;
        public float NormalDuration = 2;
        public float RarePower = .025f;
        public float RareDuration = 3.2f;
        public E_RaceLaneIntent RareLaneIntent;
        public float UniqueProgress;
        public float UniquePower;
        public float UniqueDuration;

        public string SkillName(E_RaceSkillRarity rarity) =>
            rarity == E_RaceSkillRarity.Normal ? Normal : rarity == E_RaceSkillRarity.Rare ? Rare : Unique;
    }

    [Serializable]
    public sealed class Table_RaceSkills
    {
        public float ComebackChance = .82f;
        public Data_RaceSkillSet[] Runners;
        private static Table_RaceSkills cached;
        public static Table_RaceSkills Load()
        {
            if (cached != null) return cached;
            var source = Mgr_Bundle.Load<TextAsset>("Tables/Table_RaceSkills");
            if (source == null) throw new InvalidOperationException("Missing Tables/Table_RaceSkills");
            cached = JsonUtility.FromJson<Table_RaceSkills>(source.text);
            if (cached.Runners == null || cached.Runners.Length != 6)
                throw new InvalidOperationException("Race skill table requires six runners.");
            return cached;
        }
    }
}
