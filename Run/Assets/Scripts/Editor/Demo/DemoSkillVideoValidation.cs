using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRun.Race.Editor
{
    public static class DemoSkillVideoValidation
    {
        private const string folder = "DataBundles/UI/스킬영상_고화질_20261020/작업기록/Unity검증";
        private static IEnumerator<float> sequence;
        private static double next, deadline;
        private static string savedUser;
        private static readonly List<string> notes = new List<string>();
        public static string Status { get; private set; }
        private static Sys_Race Race => UnityEngine.Object.FindFirstObjectByType<Sys_Race>();
        private static UIPanel_DemoRace Panel => UnityEngine.Object.FindFirstObjectByType<UIPanel_DemoRace>();
        private static Sys_DemoSkillVideo Video => UnityEngine.Object.FindFirstObjectByType<Sys_DemoSkillVideo>();

        [Serializable] private sealed class SimulationReport { public List<SimulationResult> Races = new List<SimulationResult>(); }
        [Serializable] private sealed class SimulationResult
        {
            public string Monster;
            public int Count;
            public float ActivationTime, ActivationProgress, FinishTime;
            public bool Deterministic;
        }
        public static string Simulations()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before simulation validation.");
            var report = new SimulationReport();
            var scene = EditorSceneManager.OpenPreviewScene(Define_Scene.Path(Define_Scene.OvalToon));
            try
            {
                var race = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Sys_Race>(true)).Single();
                for (int selected = 0; selected < 3; selected++)
                {
                    Sys_DemoRace.ConfigureSetups(race, selected);
                    int hero = Mgr_Table.Demo.Monsters[selected].RunnerIndex;
                    race.Entrants[hero].BaseSpeed = (race.Course != null ? race.Course.Length : Data_RaceSimulation.Length)
                        / Sys_DemoRace.RaceSeconds * (.91f + (Mgr_Table.Demo.Monsters[selected].Stats[0] + 30 - 240) * .0018f);
                    race.Entrants[hero].StaminaDrainMultiplier = .8f;
                    var first = new Data_RaceSimulation(race.Entrants, race.Course, true, 1);
                    var second = new Data_RaceSimulation(race.Entrants, race.Course, true, 1);
                    var result = new SimulationResult { Monster = Mgr_Table.Demo.Monsters[selected].Id };
                    first.Changed += e => {
                        if (e.Kind != E_RaceEvent.Skill || e.RunnerId != hero) return;
                        result.Count++; result.ActivationTime = e.Time;
                        result.ActivationProgress = (float)(first.Runners[hero].Distance / first.CourseLength);
                    };
                    first.Begin(); second.Begin();
                    for (int i = 0; i < 12000 && first.Phase != E_RacePhase.Result; i++) first.Step();
                    for (int i = 0; i < 6000 && second.Phase != E_RacePhase.Result; i++) { second.Step(); second.Step(); }
                    result.FinishTime = (float)first.Runners[hero].FinishTime;
                    result.Deterministic = Equal(first, second);
                    if (result.Count != 1 || !result.Deterministic || result.ActivationTime >= result.FinishTime || result.ActivationProgress < .59f)
                        throw new InvalidOperationException("Single skill/determinism failed: " + result.Monster);
                    report.Races.Add(result);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Directory.CreateDirectory(folder);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(folder + "/경기계산_검증.json", json);
            return json;
        }
        private static bool Equal(Data_RaceSimulation a, Data_RaceSimulation b) =>
            a.Phase == E_RacePhase.Result && b.Phase == E_RacePhase.Result && a.Order.SequenceEqual(b.Order)
            && a.Runners.Zip(b.Runners, (x, y) => x.FinishTime == y.FinishTime && x.SkillActivationCount == y.SkillActivationCount).All(x => x);

        public static void RunPlaythrough(int first = 0, int count = 3)
        {
            if (!EditorApplication.isPlaying || sequence != null) throw new InvalidOperationException("Enter Play, with no verification running.");
            if (first < 0 || count < 1 || first + count > 3) throw new ArgumentOutOfRangeException("first/count");
            Directory.CreateDirectory(folder); notes.Clear();
            savedUser = JsonUtility.ToJson(Mgr_User.Data);
            File.WriteAllText(folder + "/검증전_사용자데이터.json", savedUser);
            sequence = Steps(first, count); next = 0; deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Tick; WriteStatus("RUNNING");
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline) throw new InvalidOperationException("Play stopped or verification timed out.");
                if (!sequence.MoveNext()) { Finish("PASSED"); return; }
                next = EditorApplication.timeSinceStartup + sequence.Current;
            }
            catch (Exception e) { notes.Add(e.ToString()); Finish("FAILED"); Debug.LogException(e); }
        }
        private static void Finish(string status)
        {
            EditorApplication.update -= Tick; sequence?.Dispose(); sequence = null;
            if (savedUser != null) { JsonUtility.FromJsonOverwrite(savedUser, Mgr_User.Data); Mgr_User.Save(); savedUser = null; }
            if (EditorApplication.isPlaying && Mgr_Game.Instance != null) Mgr_Game.Instance.Go(Define_Scene.Title);
            WriteStatus(status);
        }
        private static void WriteStatus(string state) { Status = state; File.WriteAllLines(folder + "/실제플레이_검증.txt", new[] { state }.Concat(notes)); }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            notes.Add("PASS " + message); WriteStatus("RUNNING");
        }
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath(folder + "/" + name + ".png"));
        private static bool FramesHero(int hero)
        {
            var rig = UnityEngine.Object.FindFirstObjectByType<CameraRig.Sys_RaceCinemachine>();
            if (rig != null && rig.isActiveAndEnabled)
            {
                var brain = UnityEngine.Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
                return rig.IsSkillVideoFraming && rig.ProtagonistId == hero && brain != null && brain.ActiveVirtualCamera == rig.SkillCamera;
            }
            var director = UnityEngine.Object.FindFirstObjectByType<Sys_RaceCamera>();
            return director != null && director.isActiveAndEnabled && director.CurrentShot == E_RaceShot.SkillVideo && director.FocusRunner == hero;
        }
        private static bool HeroBodyInFrame(int hero)
        {
            var view = UnityEngine.Object.FindFirstObjectByType<Sys_RacePresentation>().Views[hero];
            var mesh = view.MountRoot.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(r => r.enabled && !r.name.StartsWith("obj_outline"));
            if (mesh == null || Camera.main == null) return false;
            Vector3 screen = Camera.main.WorldToViewportPoint(mesh.bounds.center);
            Vector3 top = Camera.main.WorldToViewportPoint(mesh.bounds.center + Vector3.up * mesh.bounds.extents.y);
            Vector3 bottom = Camera.main.WorldToViewportPoint(mesh.bounds.center - Vector3.up * mesh.bounds.extents.y);
            return screen.z > 0 && screen.x > .08f && screen.x < .92f && screen.y > .08f && screen.y < .92f
                && top.y < .97f && bottom.y > .03f;
        }

        private static IEnumerator<float> Steps(int first, int count)
        {
            for (int id = first; id < first + count; id++)
            {
                string label = Mgr_Table.Demo.Monsters[id].Id;
                Mgr_User.Data.SelectedMonster = id;
                var growth = Mgr_User.Data.Monsters[id]; growth.Level = 5; growth.SpeedBonus = 30; growth.Trained = true; growth.EndurancePassive = true;
                Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon); yield return 1;
                while (Race == null || Panel == null || Mgr_Game.Instance.IsTransitioning) yield return .1f;
                var race = Race; var panel = Panel; var video = Video; int hero = Mgr_Game.Instance.HeroRunner;
                Check(video != null && video.VideoPath == Sys_DemoSkillVideo.PathFor(label), label + " correct movie mapping");
                Check(panel.transform.Find("obj_safe_area/group_comic") == null, label + " no comic UI");
                var expected = new Data_RaceSimulation(race.Entrants, race.Course, race.EnableLaneContests, race.LapCount); expected.Begin();
                for (int step = 0; step < 12000 && expected.Phase != E_RacePhase.Result; step++) expected.Step();
                race.PlaybackSpeed = 2;
                while (!video.IsActive && race.Simulation.Phase != E_RacePhase.Result) yield return .05f;
                Check(video.IsActive && race.Simulation.Runners[hero].SkillActivationCount == 1, label + " one skill starts video");
                while (video.IsActive && !video.HasFirstFrame) yield return .05f;
                Check(video.IsActive && video.HasFirstFrame && !video.HasFailed, label + " actual decoded first frame");
                double held = race.Simulation.Time; yield return .7f;
                var decoder = video.GetComponent<UnityEngine.Video.VideoPlayer>();
                Check(decoder.width == 1920 && decoder.height == 1080 && Math.Abs(decoder.length - 7) < .1, label + " actual 1080p seven-second media");
                Check(race.Simulation.Time == held && video.MediaTime > .1, label + " movie advances while all runners hold");
                var opacity = panel.transform.Find("group_skill_video").GetComponent<CanvasGroup>();
                Check(opacity.alpha > .98f, label + " fade reaches full opacity");
                Check(FramesHero(hero), label + " camera reframes under opaque movie");
                Capture(label + "_영상초반");
                if (id == 0)
                {
                    GameObject.Find("btn_pause_video").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return .2f;
                    double media = video.MediaTime; yield return .35f;
                    Check(race.Paused && race.Simulation.Time == held && Math.Abs(media - video.MediaTime) < .09, label + " pause freezes media and race");
                    GameObject.Find("btn_pause_video").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                }
                yield return 3; Capture(label + "_영상후반");
                while (video.IsActive && opacity.alpha > .98f) yield return .02f;
                if (video.IsActive) Capture(label + "_복귀디졸브");
                while (video.IsActive) yield return .05f;
                Check(!race.PresentationHeld && opacity.alpha == 0 && !video.TryPlay(), label + " full playback and fade release hold, duplicate replay blocked");
                Check(FramesHero(hero), label + " fade returns to the selected runner");
                Capture(label + "_질주복귀");
                Check(HeroBodyInFrame(hero), label + " visible character body remains inside the return frame");
                while (race.Simulation.Phase != E_RacePhase.Result) yield return .1f;
                Check(race.Simulation.Runners[hero].SkillActivationCount == 1 && Equal(race.Simulation, expected), label + " one skill and deterministic result");
                race.RestartRace(); yield return .1f;
                Check(race.Simulation.Runners[hero].SkillActivationCount == 0 && !video.IsActive, label + " restart clears presentation");
                race.SkipToResult(); yield return .2f;
                Check(!race.PresentationHeld && !video.IsActive && Equal(race.Simulation, expected), label + " result skip preserves skill and result");
                if (id != 2) continue;

                race.RestartRace(); race.PlaybackSpeed = 20;
                while (!video.IsActive && race.Simulation.Phase != E_RacePhase.Result) yield return .05f;
                while (video.IsActive && !video.HasFirstFrame) yield return .05f;
                Check(video.HasFirstFrame && video.IsActive, "skip scenario has actual media");
                yield return .5f; video.Skip(); yield return .6f;
                Check(!video.IsActive && !race.PresentationHeld, "skip fades back and releases hold");
                race.SkipToResult(); yield return .1f;
                Check(Equal(race.Simulation, expected), "skip preserves deterministic result");

                race.RestartRace(); race.PlaybackSpeed = 20;
                while (!video.IsActive && race.Simulation.Phase != E_RacePhase.Result) yield return .05f;
                Check(video.IsActive, "OFF scenario starts presentation");
                panel.CutscenesEnabled = false; yield return .1f;
                Check(!video.IsActive && !race.PresentationHeld, "turning OFF immediately clears active presentation");
                while (race.Simulation.Phase != E_RacePhase.Result)
                {
                    if (video.IsActive || race.PresentationHeld) throw new InvalidOperationException("OFF reopened a presentation");
                    yield return .2f;
                }
                Check(Equal(race.Simulation, expected), "OFF preserves one skill and deterministic result");

                panel.CutscenesEnabled = true; race.RestartRace(); race.PlaybackSpeed = 20;
                while (!video.IsActive && race.Simulation.Phase != E_RacePhase.Result) yield return .05f;
                Check(video.IsActive, "restart scenario starts presentation");
                race.RestartRace(); yield return .1f;
                // Restart legitimately gives the hold to the opening camera sequence.
                // Verify that the video relinquished ownership without rejecting that new intro.
                var owner = typeof(Sys_Race).GetField("presentationOwner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(race);
                Check(!video.IsActive && owner != video && (owner == null || owner is CameraRig.RaceStartIntro)
                    && race.Simulation.Runners[hero].SkillActivationCount == 0, "restart during video clears media and transfers hold only to the new intro");
                decoder.Stop(); decoder.url = Application.streamingAssetsPath + "/Video/RaceSkills/VerificationMissing.mp4"; decoder.Prepare();
                yield return .5f; race.PlaybackSpeed = 20;
                while (race.Simulation.Phase != E_RacePhase.Result) yield return .2f;
                Check(video.HasFailed && !video.IsActive && !race.PresentationHeld && Equal(race.Simulation, expected), "missing media keeps one skill, no comic fallback, same result");
            }
        }
    }
}
