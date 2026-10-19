using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ProjectRun.Race;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static class RanchFlowVerification
    {
        private const string Folder = "DataBundles/작업기록/로비전환과고화질영상_20261020/진입흐름검증";
        private static readonly StringBuilder report = new StringBuilder();
        public static string Status { get; private set; }
        public static void Run()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().name != Define_Scene.Title)
                throw new InvalidOperationException("Start in the title scene Play mode.");
            Directory.CreateDirectory(Folder);
            report.Clear(); Status = "RUNNING";
            Mgr_Game.Instance.StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            string previous = JsonUtility.ToJson(Mgr_User.Data);
            float volume = Mgr_User.Data.Volume;
            bool motion = Mgr_User.Data.MotionEnabled;
            try
            {
                Button("btn_game_start").onClick.Invoke();
                yield return WaitScene(Define_Scene.Lobby);
                var lobby = UnityEngine.Object.FindFirstObjectByType<Sys_RanchUI>();
                Check(lobby != null && lobby.IsPresentationReady, "GAME START → 준비 완료된 Scene_LobbyTest");
                Check(Mgr_User.Data.SelectedMonster == -1 && Mgr_User.Data.Monsters.All(x => !x.Trained), "새 체험의 선택·훈련 초기화");
                Check(Mgr_User.Data.Volume == volume && Mgr_User.Data.MotionEnabled == motion, "소리·배경 설정 보존");
                Check(UnityEngine.Object.FindObjectsByType<Sys_RanchResident>(FindObjectsSortMode.None).Length == 6, "로비 몬스터 6마리 유지");
                yield return new WaitForEndOfFrame(); Capture("01_새로비.png");
                Button("btn_training").onClick.Invoke();
                lobby.TrainingPanel.SelectMonster(0);
                lobby.TrainingPanel.BeginTraining();
                float deadline = Time.realtimeSinceStartup + 25;
                while (lobby.TrainingPanel.IsTraining && Time.realtimeSinceStartup < deadline) yield return null;
                Check(Mgr_User.Data.Monsters[0].Trained && Mgr_User.CanEnter(0), "새 로비 실제 훈련 후 출전 조건 충족");
                lobby.TrainingPanel.ReturnToRanch();
                Button("btn_arena").onClick.Invoke();
                lobby.ArenaPanel.EntryButton.onClick.Invoke();
                lobby.ArenaPanel.SelectEntrant(0);
                yield return new WaitForEndOfFrame(); Capture("02_출전준비.png");
                lobby.ArenaPanel.ConfirmEntry.onClick.Invoke();
                yield return WaitScene(Define_Scene.OvalToon);
                var race = UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
                var panel = UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();
                Check(Mgr_Game.Instance.IsDemoRace && race != null && panel != null && Mgr_User.Data.SelectedMonster == 0,
                    "출전 확정 → Scene_OvalToon 단일 경기");
                Check(race.Entrants.Length == 6, "경기 6마리 구성 유지");
                yield return new WaitForEndOfFrame(); Capture("03_경기진입.png");
                panel.CutscenesEnabled = false;
                race.SkipToResult(); yield return null; yield return null;
                Check(race.Simulation.Phase == E_RacePhase.Result && !Mgr_User.Data.RaceInProgress, "결과·저장 처리");
                Button("btn_lobby").onClick.Invoke();
                yield return WaitScene(Define_Scene.Lobby);
                Check(Mgr_User.Data.Monsters[0].Trained && Mgr_User.Data.SelectedMonster == 0, "일반 경기 복귀 시 성장·출전 선택 유지");
                Check(!Mgr_Game.Instance.IsDemoRace, "로비 복귀 시 경기 모드 종료");
                yield return new WaitForEndOfFrame(); Capture("04_경기후로비.png");
                Mgr_Game.Instance.Replay();
                yield return WaitScene(Define_Scene.Lobby);
                Check(Mgr_User.Data.SelectedMonster == -1 && Mgr_User.Data.Monsters.All(x => !x.Trained), "처음부터 다시 체험 시 새 세션");
                Check(Mgr_User.Data.Volume == volume && Mgr_User.Data.MotionEnabled == motion, "재체험도 설정 보존");
                Status = "PASSED";
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(previous, Mgr_User.Data); Mgr_User.Save();
                if (Status == "RUNNING") Status = "FAILED";
                report.AppendLine("결과: " + Status);
                File.WriteAllText(Folder + "/진입흐름_검증.txt", report.ToString());
            }
        }
        private static IEnumerator WaitScene(string scene)
        {
            float deadline = Time.realtimeSinceStartup + 50;
            yield return null;
            while ((SceneManager.GetActiveScene().name != scene || Mgr_Game.Instance.IsTransitioning) && Time.realtimeSinceStartup < deadline)
            {
                Check(GameObject.Find("txt_load_error") == null, "화면 준비 오류 없음");
                yield return null;
            }
            Check(SceneManager.GetActiveScene().name == scene && !Mgr_Game.Instance.IsTransitioning, "전환 완료 " + scene);
        }
        private static Button Button(string name)
        {
            var item = GameObject.Find(name);
            if (item == null) throw new InvalidOperationException("Missing button " + name);
            return item.GetComponent<Button>();
        }
        private static void Check(bool success, string message)
        {
            // Avoid repeating the same no-error check on every transition frame.
            if (!success || message != "화면 준비 오류 없음") report.AppendLine((success ? "PASS " : "FAIL ") + message);
            if (!success) throw new InvalidOperationException(message);
        }
        private static void Capture(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Folder + "/" + name, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(texture); }
        }
    }
}
