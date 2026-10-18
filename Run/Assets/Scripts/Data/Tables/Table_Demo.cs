using System;

namespace ProjectRun.Race
{
    [Serializable]
    public sealed class Table_Demo
    {
        public int RequiredLevel = 5;
        public int RequiredSpeed = 260;
        public int TrainingSpeed = 30;
        public int TrainingExperience = 20;
        public float TrainingSeconds = 7;
        public float LobbyMinimumSeconds = 10;
        public float LobbyMaximumSeconds = 14;
        public float DissolveSeconds = 1.5f;
        public Data_DemoMonster[] Monsters;
    }

    [Serializable]
    public sealed class Data_DemoMonster
    {
        public string Id;
        public string Name;
        public string Strength;
        public string Story;
        public string Skill;
        public string SkillDescription;
        public string SkillCondition;
        public string Tactic;
        public int TacticIndex;
        public int RunnerIndex;
        public int[] Stats;
    }
}
