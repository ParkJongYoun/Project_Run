using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRun.Race
{
    public sealed class Mgr_Game : MonoBehaviour
    {
        public static Mgr_Game Instance { get; private set; }
        public bool IsDemoRace { get; private set; }
        public int HeroRunner => Mgr_User.Data.SelectedMonster < 0 ? 0 : Mgr_Table.Demo.Monsters[Mgr_User.Data.SelectedMonster].RunnerIndex;
        public bool IsTransitioning { get; private set; }
        private bool demoSessionStarted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var root = new GameObject("[Managers] Demo");
            Instance = root.AddComponent<Mgr_Game>(); DontDestroyOnLoad(root);
            SceneManager.sceneLoaded += Instance.SceneLoaded;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AudioListener.volume = Mgr_User.Data.Volume;
            var ui=FindFirstObjectByType<Mgr_UI>();
            var title=FindFirstObjectByType<UIPanel_DemoTitle>();if(ui!=null&&title!=null)ui.Register(E_PanelType.DemoTitle,title);
            if (scene.name == Define_Scene.Title || scene.name == Define_Scene.Lobby) IsDemoRace = false;
            if (IsDemoRace && scene.name == Define_Scene.OvalToon)
            {
                var systems = GameObject.Find("[Systems]") ?? new GameObject("[Systems]");
                systems.AddComponent<Sys_DemoRace>();
            }
        }
        public void EnterRace(string scene)
        {
            if (!Mgr_User.CanEnter(Mgr_User.Data.SelectedMonster) || scene != Define_Scene.OvalToon) return;
            Mgr_User.Data.LastMode = scene; Mgr_User.Data.RaceInProgress = true; Mgr_User.Save();
            IsDemoRace = true; Go(scene);
        }
        public void PrepareDirectDemoRace(string scene)
        {
            if (IsDemoRace || scene != Define_Scene.OvalToon) return;
            // Direct Play has no lobby entry step. Use the selected monster, or Poring by default,
            // with the demo's entry-ready growth. Normal lobby entry already sets IsDemoRace.
            int selected = Mgr_User.Data.SelectedMonster;
            if (selected < 0) selected = 0;
            Mgr_User.Data.SelectedMonster = selected;
            var monster = Mgr_User.Data.Monsters[selected];
            monster.Level = Mgr_Table.Demo.RequiredLevel;
            monster.SpeedBonus = Mgr_Table.Demo.TrainingSpeed;
            monster.Experience = Mathf.Max(monster.Experience, Mgr_Table.Demo.TrainingExperience);
            monster.Trained = true;
            monster.EndurancePassive = true;
            demoSessionStarted = true;
            IsDemoRace = true;
        }
        public void Go(string scene)
        {
            if (!IsTransitioning && !Mgr_Scene.IsLoading) StartCoroutine(Transition(scene));
        }
        public void StartDemo()
        {
            if (IsTransitioning || Mgr_Scene.IsLoading) return;
            BeginDemoSession();
            Go(Define_Scene.Lobby);
        }
        public void EnsureDemoSession()
        {
            // Direct Play from the ranch starts the same fresh demo as GAME START.
            if (!demoSessionStarted) BeginDemoSession();
        }
        private void BeginDemoSession()
        {
            Mgr_User.NewSession();
            Mgr_Guide.BeginSession();
            IsDemoRace = false;
            demoSessionStarted = true;
        }
        private IEnumerator Transition(string scene)
        {
            IsTransitioning = true;
            var canvas = DemoUI.Canvas(transform, "Canvas_System", (int)E_CanvasLayer.System + 10);
            var shade = DemoUI.Shade(canvas.transform, "bg_transition", Color.clear); shade.raycastTarget = true;
            yield return Fade(shade, 0, 1, .4f);
            // Present a fully black frame before scene activation or synchronous sprite uploads.
            yield return null;
            bool loaded = Mgr_Scene.Load(scene);
            if (loaded)
            {
                while (Mgr_Scene.IsLoading) yield return null;
                if (scene == Define_Scene.Lobby)
                {
                    var lobby = FindFirstObjectByType<ProjectRun.Ranch.Sys_RanchUI>();
                    if (lobby != null)
                    {
                        float deadline = Time.realtimeSinceStartup + 15;
                        while (!lobby.IsPresentationReady && !lobby.HasPreparationFailed && Time.realtimeSinceStartup < deadline)
                            yield return null;
                        loaded = lobby.IsPresentationReady && !lobby.HasPreparationFailed;
                    }
                    else loaded = false;
                }
                // Let layout, font glyphs, UI materials and the first completed composition render under black.
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
            }
            if (!loaded)
            {
                var label = DemoUI.Text(canvas.transform,"txt_load_error","화면을 불러오지 못했어요. 다시 시도해 주세요.",0,0,850,60,24,Color.white);
                yield return new WaitForSecondsRealtime(1.5f);
                label.gameObject.SetActive(false);
            }
            yield return Fade(shade, 1, 0, .55f);
            Destroy(canvas.gameObject); IsTransitioning = false;
        }
        private static IEnumerator Fade(UnityEngine.UI.Image shade, float from, float to, float duration)
        {
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                shade.color = new Color(0, 0, 0, Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / duration)));
                yield return null;
            }
            shade.color = new Color(0, 0, 0, to);
        }
        public void Replay() => StartDemo();
        private void OnDestroy() { SceneManager.sceneLoaded -= SceneLoaded; if (Instance == this) Instance = null; }
    }
}
