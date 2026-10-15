using UnityEngine;
namespace ProjectRun.Race
{
    [DefaultExecutionOrder(-100)]
    public sealed class Sys_RacePresentation : MonoBehaviour
    {
        public Sys_Race Race;
        public RaceRunnerView[] Views;
        public int SkillRunner { get; private set; } = -1;
        public float SkillStarted { get; private set; }
        public string Announcement { get; private set; }
        public Color AnnouncementColor { get; private set; }
        public bool Cutscenes = true;
        public float SkillAge => SkillRunner < 0 ? -1 : (float)Race.Simulation.Time - SkillStarted;
        public bool VideoSequenceActive { get; private set; }
        public bool InCutscene => VideoSequenceActive || (Cutscenes && SkillStarted != videoReplacedSkillStarted && SkillAge >= 0 && SkillAge < 3.4f && Remaining > 85);
        public float Remaining => Mathf.Max(0, Data_RaceSimulation.Length - (float)Race.Simulation.Runners[Race.Simulation.Order[0]].Distance);
        private float announcementUntil;
        private int generation = -1;
        private float videoReplacedSkillStarted = float.NegativeInfinity;

        private void OnEnable() { Mgr_Event.RaceChanged += Handle; Mgr_Event.Reset += ResetPresentation; }
        private void OnDisable() { Mgr_Event.RaceChanged -= Handle; Mgr_Event.Reset -= ResetPresentation; Mgr_Sound.SetWind(0); }
        private void Start()
        {
            if (Views != null && Views.Length == Race.Entrants.Length && Views[0] != null)
            {
                for (int i = 0; i < Views.Length; i++) Views[i].Bind(Race, i);
                return;
            }
            Views = new RaceRunnerView[Race.Entrants.Length];
            var parent = new GameObject("obj_runners").transform;
            parent.SetParent(transform, false);
            for (int i = 0; i < Views.Length; i++)
            {
                var runner = Instantiate(Race.Entrants[i].Prefab, parent);
                runner.name = "P_Runner_" + (i + 1).ToString("00");
                Views[i] = runner.GetComponent<RaceRunnerView>();
                Views[i].Bind(Race, i);
            }
        }
        private void Update()
        {
            if (generation != Race.Generation) { generation = Race.Generation; ResetPresentation(); }
            if (Race.Simulation.Time > announcementUntil) Announcement = "";
            Mgr_Sound.SetWind(Race.Paused || Race.PresentationHeld || Race.Simulation.Phase == E_RacePhase.Result ? 0 : Race.Simulation.Runners[0].Speed / 17);
        }
        private void ResetPresentation() { SkillRunner = -1; Announcement = ""; announcementUntil = 0; VideoSequenceActive = false; videoReplacedSkillStarted = float.NegativeInfinity; }
        public void BeginSkillVideo(int runnerId)
        {
            VideoSequenceActive = true;
            if (SkillRunner == runnerId) videoReplacedSkillStarted = SkillStarted;
        }
        public void EndSkillVideo() { VideoSequenceActive = false; }
        private void Handle(Data_RaceEvent value)
        {
            var entry = Race.Entrants[value.RunnerId];
            AnnouncementColor = entry.Color;
            if (value.Kind == E_RaceEvent.Skill)
            {
                SkillRunner = value.RunnerId; SkillStarted = value.Time;
                Announcement = entry.Name + "  ·  " + entry.SkillName;
                announcementUntil = value.Time + 4.5f;
                Mgr_Sound.PlayAccent(true);
            }
            else if (value.Kind == E_RaceEvent.Overtake && value.Time > announcementUntil - 1)
            {
                Announcement = entry.Name + "  ·  " + Race.Simulation.Runners[value.RunnerId].Rank + "위로 추월!";
                announcementUntil = value.Time + 2.5f;
            }
            else if (value.Kind == E_RaceEvent.Start) { Announcement = "출발!  첫 번째 코너를 향해"; announcementUntil = 3; Mgr_Sound.PlayAccent(false); }
            else if (value.Kind == E_RaceEvent.Finish && Race.Simulation.Runners[value.RunnerId].FinishTime <= Race.Simulation.Runners[Race.Simulation.Order[0]].FinishTime + 0.001)
            { Announcement = entry.Name + "  결승선 통과!"; announcementUntil = value.Time + 4; Mgr_Sound.PlayAccent(false); }
        }
    }
}
