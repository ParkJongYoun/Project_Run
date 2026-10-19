using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static class RanchArenaVerification
    {
        private const string folder = "DataBundles/UI/목장/경기장_우드수첩구현_20261020/검증";
        public static string Status { get; private set; }
        private static readonly StringBuilder report = new StringBuilder();

        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in Scene_LobbyTest first.");
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "/주행프레임");
            Status = "RUNNING";
            report.Clear();
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            Mgr_Game.Instance.StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            string previousData = JsonUtility.ToJson(Mgr_User.Data);
            try
            {
                var ui = UnityEngine.Object.FindFirstObjectByType<Sys_RanchUI>();
                var panel = ui.ArenaPanel;
                var arenaButton = ui.Panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "btn_arena");
                arenaButton.onClick.Invoke();
                panel.ReturnToRanch();
                arenaButton.onClick.Invoke();
                var exhibition = panel.GetComponentInChildren<UIItem_ArenaExhibition>();
                float deadline = Time.realtimeSinceStartup + 25;
                while (exhibition.transform.childCount < 6 && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Check(exhibition.transform.childCount == 6, "Six exhibition sprites load after an immediate close/reopen");
                Check(ui.Panel.Placement.InputBlocked && ui.Panel.GetComponent<CanvasGroup>().alpha == 0, "Ranch controls hidden and world input blocked");
                Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "Exactly one EventSystem");
                Check(panel.GetComponentInParent<Canvas>().GetComponent<UnityEngine.UI.GraphicRaycaster>() != null, "Canvas raycaster present");
                var runners = exhibition.GetComponentsInChildren<UnityEngine.UI.Image>().OrderBy(i => i.name).ToArray();
                var before = runners.Select(i => i.rectTransform.anchoredPosition).ToArray();
                yield return new WaitForSecondsRealtime(.35f);
                Check(runners.Select((r, i) => Vector2.Distance(before[i], r.rectTransform.anchoredPosition) > 1).All(v => v), "Every sprite moves along the track");
                Check(runners.All(r => r.sprite != null && r.gameObject.activeInHierarchy), "Every runner has a visible directional animation frame");
                yield return new WaitForEndOfFrame();
                Capture("03_경기장_최종.png");

                for (int i = 0; i < 72; i++)
                {
                    yield return new WaitForEndOfFrame();
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    try { File.WriteAllBytes(folder + "/주행프레임/" + i.ToString("D4") + ".jpg", texture.EncodeToJPG(85)); }
                    finally { UnityEngine.Object.Destroy(texture); }
                    yield return new WaitForSecondsRealtime(1f / 12f);
                }

                panel.EntryButton.onClick.Invoke();
                panel.SelectEntrant(1);
                Check(panel.EntryPopup.activeSelf && panel.ConfirmLabel.text == "훈련하러 가기", "Untrained entrant is routed to training");
                Check(Mgr_User.Data.SelectedMonster == -1, "Browsing entrants does not change committed entry");
                yield return new WaitForEndOfFrame();
                Capture("04_출전준비_훈련조건.png");
                panel.ConfirmEntry.onClick.Invoke();
                Check(ui.TrainingPanel.gameObject.activeInHierarchy && !panel.gameObject.activeSelf, "Training navigation opens the existing training panel");
                ui.TrainingPanel.ReturnToRanch();
                Check(!ui.Panel.Placement.InputBlocked && ui.Panel.GetComponent<CanvasGroup>().alpha == 1, "Return restores ranch UI and camera input");
                arenaButton.onClick.Invoke();
                panel.NotebookButton.onClick.Invoke();
                Check(panel.NotebookPopup.activeSelf, "Course notebook opens");
                yield return new WaitForEndOfFrame();
                Capture("05_경기장_수첩.png");
                panel.NotebookClose.onClick.Invoke();
                panel.SettingsButton.onClick.Invoke();
                Check(panel.SettingsPopup.activeSelf, "Settings opens");
                panel.SettingsClose.onClick.Invoke();

                // Only this temporary verification session receives growth. Restore it in finally.
                var progress = Mgr_User.Data.Monsters[2];
                progress.Level = Mgr_Table.Demo.RequiredLevel;
                progress.SpeedBonus = Mgr_Table.Demo.TrainingSpeed;
                progress.Trained = true;
                progress.EndurancePassive = true;
                panel.EntryButton.onClick.Invoke();
                panel.SelectEntrant(2);
                Check(panel.ConfirmLabel.text == "전술 확정 · 레이스 시작", "Qualified entrant can confirm the recommended tactic");
                yield return new WaitForEndOfFrame();
                Capture("06_출전준비_조건충족.png");
                panel.ConfirmEntry.onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 40;
                while ((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Define_Scene.OvalToon || Mgr_Game.Instance.IsTransitioning) && Time.realtimeSinceStartup < deadline) yield return null;
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Define_Scene.OvalToon, "Entry loads Scene_OvalToon");
                Check(Mgr_Game.Instance.IsDemoRace && Mgr_User.Data.SelectedMonster == 2, "Entry keeps the selected playable and demo race rules");
                yield return new WaitForEndOfFrame();
                Capture("07_카툰경기장_진입.png");
                Status = "PASSED";
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(previousData, Mgr_User.Data);
                Mgr_User.Save();
                if (Status == "RUNNING") Status = "FAILED";
                report.AppendLine("Status: " + Status);
                File.WriteAllText(folder + "/동작검증.txt", report.ToString());
            }
        }

        private static void Check(bool condition, string message)
        {
            report.AppendLine((condition ? "PASS " : "FAIL ") + message);
            if (!condition) throw new InvalidOperationException("Ranch arena: " + message);
        }

        private static void Capture(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(folder + "/" + name, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(texture); }
        }
    }
}
