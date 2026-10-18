using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    public static class HeroComicValidation
    {
        private const string folder = "DataBundles/아트/포링과에드가_만화연출/작업기록/7컷_확장";
        private static readonly List<string> notes = new List<string>();
        private static readonly FieldInfo ageField = typeof(UIPanel_DemoRace).GetField("comicAge", BindingFlags.NonPublic | BindingFlags.Instance);
        private static string savedUser;
        private static IEnumerator routine;
        private static double deadline;
        private static int selectionFilter = -1;
        private static bool allCharacters;
        private static string runFolder;
        public static string Status { get; private set; } = "IDLE";
        private static Sys_Race Race => UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
        private static UIPanel_DemoRace Panel => UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();

        public static void Run() => Begin(-1);
        public static void RunAllChecks() => Begin(-1, true);

        public static void RunSingle(int selection)
        {
            if (selection != 0 && selection != 2) throw new ArgumentOutOfRangeException(nameof(selection));
            Begin(selection);
        }

        private static void Begin(int selection, bool all = false)
        {
            if (!EditorApplication.isPlaying || routine != null) throw new InvalidOperationException("Enter Play; validation must be idle.");
            selectionFilter = selection;
            allCharacters = all;
            runFolder = all ? "DataBundles/아트/만화연출_3종_경기장적용/작업기록" : folder;
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            Directory.CreateDirectory(runFolder);
            savedUser = JsonUtility.ToJson(Mgr_User.Data);
            notes.Clear(); Status = "RUNNING"; Write();
            deadline = EditorApplication.timeSinceStartup + 420;
            routine = Steps();
            Mgr_Game.Instance.StartCoroutine(Guarded());
        }

        private static IEnumerator Guarded()
        {
            while (routine != null)
            {
                object wait;
                try
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Comic validation timed out.");
                    if (!routine.MoveNext()) { Finish("COMPLETED"); yield break; }
                    wait = routine.Current;
                }
                catch (Exception error) { notes.Add(error.ToString()); Finish("FAILED"); Debug.LogException(error); yield break; }
                yield return wait;
            }
        }

        private static void Finish(string status)
        {
            (routine as IDisposable)?.Dispose(); routine = null;
            if (Panel != null) Panel.CancelComic();
            if (savedUser != null) { JsonUtility.FromJsonOverwrite(savedUser, Mgr_User.Data); Mgr_User.Save(); savedUser = null; }
            Status = status; Write();
            Mgr_Game.Instance.Go(Define_Scene.Title);
        }

        private static void Write() => File.WriteAllLines(runFolder + (selectionFilter < 0 ? "/경기연결_검증.txt" : "/구도보정_검증.txt"), new[] { Status }.Concat(notes));
        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            notes.Add("PASS " + message); Write();
        }

        private static bool SameResult(Sys_Race race, Data_RaceSimulation expected) =>
            race.Simulation.Order.SequenceEqual(expected.Order) && race.Simulation.Runners.Zip(expected.Runners,
                (a, b) => a.FinishTime == b.FinishTime && a.SkillActivationCount == b.SkillActivationCount).All(equal => equal);

        private static IEnumerator Steps()
        {
            foreach (int selection in (allCharacters ? new[] { 0, 1, 2 } : new[] { 0, 2 }).Where(value => selectionFilter < 0 || value == selectionFilter))
            foreach (string scene in new[] { Define_Scene.Sprite2D, Define_Scene.OvalToon })
            {
                Mgr_User.Data.SelectedMonster = selection;
                string id = selection == 0 ? "Poring" : selection == 1 ? "Minorous" : "Eddga";
                string label = id + " " + scene;
                var growth = Mgr_User.Data.Monsters[selection];
                growth.Level = 5; growth.SpeedBonus = 30; growth.Trained = true; growth.EndurancePassive = true;
                Mgr_Game.Instance.EnterRace(scene);
                yield return null;
                while (Mgr_Game.Instance.IsTransitioning || Race == null || Panel == null) yield return null;
                var race = Race; var panel = Panel; int hero = Mgr_Game.Instance.HeroRunner;
                var expected = new Data_RaceSimulation(race.Entrants, race.Course, race.EnableLaneContests, race.LapCount);
                expected.Begin(); for (int i = 0; i < 18000 && expected.Phase != E_RacePhase.Result; i++) expected.Step();
                race.PlaybackSpeed = 20;
                while (!panel.IsComicPlaying && race.Simulation.Phase != E_RacePhase.Result) yield return null;
                var comic = UnityEngine.Object.FindFirstObjectByType<UIItem_DemoHeroComic>();
                var minorous = UnityEngine.Object.FindFirstObjectByType<UIItem_MinorousComic>();
                Check((selection == 1 ? minorous != null && comic == null : comic != null && comic.MonsterId == id && minorous == null)
                    && race.Simulation.Runners[hero].SkillActivationCount == 1, label + " first activation uses approved character comic");
                Transform comicRoot = selection == 1 ? minorous.transform : comic.transform;
                Action<float> evaluate = selection == 1 ? (Action<float>)minorous.Evaluate : comic.Evaluate;
                int expectedCuts = selection == 1 ? 6 : 7;
                Check(comicRoot.Find("obj_comic_viewport/obj_comic_camera").GetComponentsInChildren<UIItem_ComicPanel>().Length == expectedCuts * 2,
                    label + " " + expectedCuts + " art cuts with separate frames");
                Check(panel.transform.Find("obj_safe_area/group_comic/bg_comic") == null, label + " legacy three-strip presentation is absent");
                double held = race.Simulation.Time;
                race.Paused = true;
                float frozenAge = (float)ageField.GetValue(panel);
                yield return new WaitForSecondsRealtime(.3f);
                Check((float)ageField.GetValue(panel) == frozenAge && race.Simulation.Time == held, label + " pause freezes presentation and race");
                if (allCharacters)
                {
                    float age = selection == 1 ? .9f : 6.8f;
                    ageField.SetValue(panel, age); evaluate(age);
                    yield return new WaitForEndOfFrame();
                    var screen = ScreenCapture.CaptureScreenshotAsTexture();
                    string characterName = selection == 0 ? "포링" : selection == 1 ? "미노타우루스" : "에드가";
                    string mode = scene == Define_Scene.Sprite2D ? "2D" : "카툰";
                    try { File.WriteAllBytes(runFolder + "/" + characterName + "_" + mode + "_적용확인.png", screen.EncodeToPNG()); }
                    finally { UnityEngine.Object.Destroy(screen); }
                    Check(race.Simulation.Time == held, label + " all runners remain held while the new comic is displayed");
                }
                else if (scene == Define_Scene.Sprite2D)
                {
                    string frames = runFolder + "/미리보기프레임/" + id;
                    Directory.CreateDirectory(frames);
                    for (int frame = 0; frame < Mathf.CeilToInt(UIItem_DemoHeroComic.Duration * 30); frame++)
                    {
                        float age = frame / 30f;
                        ageField.SetValue(panel, age); evaluate(age);
                        yield return new WaitForEndOfFrame();
                        var screen = ScreenCapture.CaptureScreenshotAsTexture();
                        try { File.WriteAllBytes(frames + "/" + frame.ToString("D4") + ".png", screen.EncodeToPNG()); }
                        finally { UnityEngine.Object.Destroy(screen); }
                    }
                    Check(race.Simulation.Time == held, label + " all runners stay held through full visual timeline");
                }
                ageField.SetValue(panel, 0f); evaluate(0); race.Paused = false;
                while (panel.IsComicPlaying) yield return null;
                Check(!race.PresentationHeld, label + " normal completion releases presentation hold");
                var video = UnityEngine.Object.FindFirstObjectByType<Sys_DemoSkillVideo>();
                while (!video.IsActive && race.Simulation.Phase != E_RacePhase.Result) yield return null;
                Check(video.IsActive && race.Simulation.Runners[hero].SkillActivationCount == 2, label + " second activation remains character video");
                video.Skip();
                while (race.Simulation.Phase != E_RacePhase.Result) yield return null;
                Check(SameResult(race, expected), label + " normal playback preserves every finish time, rank and activation count");

                race.RestartRace(); race.PlaybackSpeed = 20;
                while (!panel.IsComicPlaying && race.Simulation.Phase != E_RacePhase.Result) yield return null;
                Check(panel.IsComicPlaying, label + " replay starts comic again");
                GameObject.Find("btn_skip_comic").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Check(!panel.IsComicPlaying && !race.PresentationHeld, label + " skip button immediately releases hold");
                race.SkipToResult();
                Check(SameResult(race, expected), label + " skipped playback has identical result");

                race.RestartRace(); race.PlaybackSpeed = 20;
                while (!panel.IsComicPlaying && race.Simulation.Phase != E_RacePhase.Result) yield return null;
                panel.CutscenesEnabled = false;
                Check(!panel.IsComicPlaying && !race.PresentationHeld, label + " turning presentation off closes active comic");
                while (race.Simulation.Phase != E_RacePhase.Result)
                {
                    if (panel.IsComicPlaying || race.PresentationHeld || video.IsActive) throw new InvalidOperationException("OFF opened a presentation");
                    yield return null;
                }
                Check(SameResult(race, expected), label + " OFF has identical result and two activations");

                panel.CutscenesEnabled = true; race.RestartRace(); race.PlaybackSpeed = 20;
                while (!panel.IsComicPlaying && race.Simulation.Phase != E_RacePhase.Result) yield return null;
                race.RestartRace();
                Check(!panel.IsComicPlaying && !race.PresentationHeld && race.Simulation.Runners[hero].SkillActivationCount == 0, label + " restart clears cut and hold");
                race.Paused = true;
            }
        }
    }
}
