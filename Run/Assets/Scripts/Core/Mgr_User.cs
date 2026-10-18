using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ProjectRun.Race
{
    public static class Mgr_User
    {
        private static Data_UserSave data;
        public static string Notice { get; private set; }
        public static Data_UserSave Data { get { if (data == null) Load(); return data; } }
        private static string SavePath => Path.Combine(Application.persistentDataPath, "project_run_demo_v1.json");
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void ProjectRunSyncSave();
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { data = null; Notice = null; }

        public static void Load()
        {
            Notice = null;
            try
            {
                if (!File.Exists(SavePath)) { data = new Data_UserSave(); return; }
                string json = File.ReadAllText(SavePath);
                bool legacyGuide = json.Contains("\"TutorialStep\"") || json.Contains("\"GuideEnabled\"");
                var candidate = JsonUtility.FromJson<Data_UserSave>(json);
                if (!IsValid(candidate)) throw new InvalidDataException("Unsupported or damaged demo save.");
                data = candidate;
                if (data.RaceInProgress)
                {
                    data.RaceInProgress = false;
                    Notice = "진행 중이던 경기는 출전 준비에서 다시 시작할 수 있어요.";
                }
                // Re-serialize old saves without guide fields, preserving growth and settings.
                if (legacyGuide || !string.IsNullOrEmpty(Notice)) Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Demo save recovery: " + exception.Message);
                try { if (File.Exists(SavePath)) File.Copy(SavePath, SavePath + ".recovery", true); } catch (IOException) { }
                data = new Data_UserSave();
                Notice = "저장 기록을 읽지 못해 새 체험을 준비했어요. 이전 파일은 복구용으로 보관합니다.";
            }
        }

        public static bool IsValid(Data_UserSave value)
        {
            if (value == null || value.Version != 1 || value.Monsters == null || value.Monsters.Length != 3 ||
                value.SelectedMonster < -1 || value.SelectedMonster > 2 ||
                float.IsNaN(value.Volume) || value.Volume < 0 || value.Volume > 1) return false;
            foreach (var monster in value.Monsters)
                if (monster == null || monster.Level < 4 || monster.Level > 5 || monster.SpeedBonus < 0 || monster.SpeedBonus > 30 ||
                    monster.Experience < 0 || monster.Experience > 100 || (monster.Trained && (monster.Level != 5 || !monster.EndurancePassive))) return false;
            return true;
        }

        public static bool Save()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                string temporary = SavePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(Data, true));
                File.Copy(temporary, SavePath, true);
#if UNITY_WEBGL && !UNITY_EDITOR
                ProjectRunSyncSave();
#endif
                return true;
            }
            catch (Exception exception)
            {
                Notice = "저장 공간을 확인해 주세요. 현재 플레이는 계속할 수 있어요.";
                Debug.LogWarning("Demo save failed: " + exception.Message);
                return false;
            }
        }

        public static void NewSession()
        {
            var previous = Data;
            data = new Data_UserSave { Volume = previous.Volume, MotionEnabled = previous.MotionEnabled };
            Notice = null;
            Save();
        }
        public static void Select(int id) { if (id < 0 || id > 2) return; Data.SelectedMonster = id; Save(); }
        public static bool CanEnter(int id) => id >= 0 && id < 3 && Data.Monsters[id].Level >= Mgr_Table.Demo.RequiredLevel &&
            Mgr_Table.Demo.Monsters[id].Stats[0] + Data.Monsters[id].SpeedBonus >= Mgr_Table.Demo.RequiredSpeed;
        public static bool Train(int id)
        {
            if (id < 0 || id > 2 || Data.Monsters[id].Trained) return false;
            var monster = Data.Monsters[id];
            monster.SpeedBonus = Mgr_Table.Demo.TrainingSpeed;
            monster.Experience += Mgr_Table.Demo.TrainingExperience;
            monster.Level = 5; monster.Trained = true; monster.EndurancePassive = true;
            Save(); return true;
        }
    }
}
