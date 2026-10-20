using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Race;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static class RanchArenaRacePreviewChecks
    {
        private const string folder = "DataBundles/UI/목장/경기장_수첩톤과동시출발_20261020";
        private static readonly BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly StringBuilder report = new StringBuilder();
        public static string Status { get; private set; }

        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Scene_LobbyTest Play mode.");
            Directory.CreateDirectory(folder + "/검증");
            Directory.CreateDirectory(folder + "/결과");
            report.Clear();
            Status = "RUNNING";
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            Mgr_Game.Instance.StartCoroutine(Verify());
        }

        private static IEnumerator Verify()
        {
            UIItem_ArenaExhibition exhibition = null;
            string originalData = null;
            try
            {
                var ui = UnityEngine.Object.FindFirstObjectByType<Sys_RanchUI>();
                var panel = ui.ArenaPanel;
                ui.Panel.Guide.Clear();
                var open = ui.Panel.GetComponentsInChildren<Button>(true).Single(b => b.name == "btn_arena");
                open.onClick.Invoke();
                // Exercise cancellation while the six sprite sets are still loading.
                panel.ReturnToRanch();
                open.onClick.Invoke();
                exhibition = panel.GetComponentInChildren<UIItem_ArenaExhibition>();
                var type = typeof(UIItem_ArenaExhibition);
                var ready = type.GetField("ready", flags);
                float deadline = Time.realtimeSinceStartup + 30;
                while (!(bool)ready.GetValue(exhibition) && Time.realtimeSinceStartup < deadline) yield return null;
                Check((bool)ready.GetValue(exhibition), "All asynchronous sprite loads complete after immediate close/reopen");
                var runners = exhibition.GetComponentsInChildren<Image>(true).OrderBy(i => i.name).ToArray();
                Check(runners.Length == 6 && runners.All(r => r.sprite != null), "All six runners have animation sprites");
                Check(panel.BackButton != null && panel.EntryButton != null && panel.NotebookButton != null && panel.SettingsButton != null, "All four arena controls retain references");
                string[] images = { "group_title/bg_surface", "group_title/img_frame", "btn_back/bg_surface",
                    "btn_back/img_frame", "btn_notebook/bg_surface", "btn_notebook/img_frame",
                    "btn_settings/bg_surface", "btn_settings/img_frame", "btn_entry/bg_surface", "btn_entry/img_frame" };
                Check(images.All(p => panel.Layout.Find(p).GetComponent<Image>().sprite != null), "Title and button surfaces/frames have valid sprite references");
                Check(panel.Layout.Find("group_title/bg_surface").GetComponent<Image>().sprite != panel.Layout.Find("group_title/img_frame").GetComponent<Image>().sprite, "Title background and frame use separate sprites");
                originalData = JsonUtility.ToJson(Mgr_User.Data);
                var clock = type.GetField("elapsed", flags);
                var render = type.GetMethod("RenderRunners", flags);
                var travel = type.GetMethod("TravelAt", flags);
                // Freeze only this presentation after loading, then sample its drawing method.
                exhibition.enabled = false;
                yield return null;
                float previousSpread = -1;
                foreach (float seconds in new[] { .8f, 8f, 24f })
                {
                    clock.SetValue(exhibition, seconds);
                    render.Invoke(exhibition, null);
                    float[] distances = Enumerable.Range(0, 6).Select(i => (float)travel.Invoke(exhibition, new object[] { seconds, i })).ToArray();
                    float spread = distances.Max() - distances.Min();
                    Check(spread > previousSpread, "Longitudinal spread grows at " + seconds + " s: " + spread.ToString("F4") + " laps");
                    previousSpread = spread;
                    if (seconds < 1)
                    {
                        Check(distances.All(d => Mathf.Abs(d) < .00001f), "All six runners start at the exact same course distance");
                        Check(runners.Select(r => r.rectTransform.anchoredPosition).Distinct().Count() == 6, "Six separate start lanes");
                    }
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();
                    Capture(seconds < 1 ? "결과/01_동시출발_대기.png" : seconds < 10 ? "결과/02_초반경합.png" : "결과/03_속도차이_경주.png");
                }

                float cycle = (float)type.GetProperty("CycleSeconds", flags).GetValue(exhibition);
                Check((float)travel.Invoke(exhibition, new object[] { cycle - .5f, 3 }) > 1, "Slowest runner completes a lap before reset");
                clock.SetValue(exhibition, cycle + .1f);
                ready.SetValue(exhibition, true);
                type.GetMethod("LateUpdate", flags).Invoke(exhibition, null);
                Check((float)clock.GetValue(exhibition) < 1, "Heat loops back to the shared start");

                panel.NotebookButton.onClick.Invoke();
                Check(panel.NotebookPopup.activeSelf, "Notebook button opens existing notebook");
                panel.NotebookClose.onClick.Invoke();
                Check(!panel.NotebookPopup.activeSelf, "Notebook close returns to arena");
                panel.SettingsButton.onClick.Invoke();
                Check(panel.SettingsPopup.activeSelf, "Settings button opens");
                yield return new WaitForEndOfFrame();
                Capture("검증/04_설정.png");
                panel.SettingsClose.onClick.Invoke();
                Check(!panel.SettingsPopup.activeSelf, "Settings close returns to arena");
                panel.EntryButton.onClick.Invoke();
                Check(panel.EntryPopup.activeSelf, "Entry button opens entrant selection");
                panel.SelectEntrant(2);
                Check(panel.ConfirmEntry.interactable, "Existing entrant selection remains interactive");
                Check(panel.ConfirmEntry.GetComponentsInChildren<Image>().All(i => i.color.a == 0 || i.sprite != null), "Direct entry button has valid visible surfaces");
                yield return new WaitForEndOfFrame();
                Capture("검증/05_출전준비.png");
                Mgr_User.Data.Monsters[2].Level = Mgr_Table.Demo.RequiredLevel;
                Mgr_User.Data.Monsters[2].SpeedBonus = Mgr_Table.Demo.TrainingSpeed;
                panel.SelectEntrant(2);
                Check(panel.ConfirmLabel.text == "출전하기", "Entry label is independent of growth");
                var labelOutline = panel.ConfirmLabel.GetComponent<Outline>();
                Check(labelOutline == null || (labelOutline.effectColor.r < .3f && labelOutline.effectDistance.magnitude < 1), "Race confirmation text has a thin dark edge despite old scene overrides");
                Check(panel.ConfirmEntry.GetComponentsInChildren<Image>().All(i => i.color.a == 0 || i.sprite != null), "Race confirmation button has valid visible surfaces");
                yield return new WaitForEndOfFrame();
                Capture("검증/06_출전확정.png");
                JsonUtility.FromJsonOverwrite(originalData, Mgr_User.Data);
                panel.EntryClose.onClick.Invoke();
                Check(!panel.EntryPopup.activeSelf, "Entry cancel returns to arena");
                Check(JsonUtility.ToJson(Mgr_User.Data) == originalData, "Preview and popup browsing do not change progress, stats or committed entry");
                exhibition.enabled = true;
                panel.ReturnToRanch();
                Check(!panel.gameObject.activeSelf && !ui.Panel.Placement.InputBlocked, "Return restores ranch input");
                open.onClick.Invoke();
                Check((float)clock.GetValue(exhibition) == 0, "Reopening restarts the common clock");
                Check(exhibition.GetComponentsInChildren<Image>(true).Length == 6, "Reopening reuses exactly six runners");
                Status = "PASSED";
            }
            finally
            {
                if (exhibition != null) exhibition.enabled = true;
                if (originalData != null) JsonUtility.FromJsonOverwrite(originalData, Mgr_User.Data);
                if (Status == "RUNNING") Status = "FAILED";
                report.AppendLine("Status: " + Status);
                File.WriteAllText(folder + "/검증/동작검증.txt", report.ToString());
            }
        }

        private static void Check(bool condition, string message)
        {
            report.AppendLine((condition ? "PASS " : "FAIL ") + message);
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Capture(string relative)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(folder + "/" + relative, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(texture); }
        }
    }
}
