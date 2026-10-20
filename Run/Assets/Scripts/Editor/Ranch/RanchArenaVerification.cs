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
        private const string folder = "DataBundles/검증/훈련없이_바로출전_20261020/경기장회귀검증";
        public static string Status { get; private set; }
        private static readonly StringBuilder report = new StringBuilder();

        public static void Run(bool includeExhibition = true)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in Scene_LobbyTest first.");
            Directory.CreateDirectory(folder);
            Directory.CreateDirectory(folder + "/주행프레임");
            Status = "RUNNING";
            report.Clear();
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            Mgr_Game.Instance.StartCoroutine(Verify(includeExhibition));
        }

        private static IEnumerator Verify(bool includeExhibition)
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
                if (includeExhibition)
                {
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
                }

                panel.EntryButton.onClick.Invoke();
                Check(!panel.ConfirmEntry.interactable, "Entry is disabled until a monster is selected");
                Check(!Mgr_User.CanEnter(-1) && !Mgr_User.CanEnter(3), "Invalid entrants are rejected");
                for (int i = 0; i < 3; i++)
                {
                    panel.SelectEntrant(i);
                    Check(!Mgr_User.Data.Monsters[i].Trained && Mgr_User.CanEnter(i), "Untrained monster can enter " + i);
                    Check(panel.EntryPopup.activeSelf && panel.ConfirmEntry.interactable && panel.ConfirmLabel.text == "출전하기", "Selecting a monster enables direct entry " + i);
                }
                Check(Mgr_User.Data.SelectedMonster == -1, "Browsing entrants does not change committed entry");
                yield return new WaitForEndOfFrame();
                Capture("04_출전준비_훈련없이선택.png");
                panel.EntryClose.onClick.Invoke();
                Check(Mgr_User.Data.SelectedMonster == -1, "Cancelling does not commit entry");
                panel.ReturnToRanch();
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

                // Training remains optional and must not grant growth to another entrant.
                Check(Mgr_User.Train(0) && Mgr_User.CanEnter(0), "Trained monsters remain eligible");
                Check(!Mgr_User.Train(0), "Training rewards cannot be claimed twice");
                panel.EntryButton.onClick.Invoke();
                panel.SelectEntrant(2);
                Check(panel.ConfirmLabel.text == "출전하기", "Untrained entrant can start the race immediately");
                string entrantGrowth = JsonUtility.ToJson(Mgr_User.Data.Monsters[2]);
                yield return new WaitForEndOfFrame();
                Capture("06_출전준비_바로출전.png");
                panel.ConfirmEntry.onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 40;
                while ((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Define_Scene.OvalToon || Mgr_Game.Instance.IsTransitioning) && Time.realtimeSinceStartup < deadline) yield return null;
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Define_Scene.OvalToon, "Entry loads Scene_OvalToon");
                Check(Mgr_Game.Instance.IsDemoRace && Mgr_User.Data.SelectedMonster == 2, "Entry keeps the selected playable and demo race rules");
                Check(JsonUtility.ToJson(Mgr_User.Data.Monsters[2]) == entrantGrowth, "Race entry never grants training rewards");
                yield return new WaitForEndOfFrame();
                Capture("07_카툰경기장_진입.png");
                // Restore the saved selection only after the race HUD stops reading it.
                Mgr_Game.Instance.Go(Define_Scene.Lobby);
                deadline = Time.realtimeSinceStartup + 40;
                while ((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Define_Scene.Lobby || Mgr_Game.Instance.IsTransitioning) && Time.realtimeSinceStartup < deadline) yield return null;
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Define_Scene.Lobby && !Mgr_Game.Instance.IsTransitioning, "Race returns to the lobby before verification restores data");
                Check(Mgr_User.Data.SelectedMonster == 2 && JsonUtility.ToJson(Mgr_User.Data.Monsters[2]) == entrantGrowth, "Lobby return preserves the untrained entrant and growth");
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
