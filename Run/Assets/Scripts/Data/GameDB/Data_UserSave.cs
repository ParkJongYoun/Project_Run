using System;

namespace ProjectRun.Race
{
    [Serializable]
    public sealed class Data_UserSave
    {
        public int Version = 1;
        // The confirmed race entrant only. Browsing and training targets stay in their UI.
        public int SelectedMonster = -1;
        public Data_MonsterProgress[] Monsters = { new Data_MonsterProgress(), new Data_MonsterProgress(), new Data_MonsterProgress() };
        public int RaceCount;
        public string LastMode = "Scene_OvalToon";
        public bool RaceInProgress;
        public bool MotionEnabled = true;
        public float Volume = .65f;
    }

    [Serializable]
    public sealed class Data_MonsterProgress
    {
        public int Level = 4;
        public int Experience = 80;
        public int SpeedBonus;
        public bool Trained;
        public bool EndurancePassive;
    }
}
