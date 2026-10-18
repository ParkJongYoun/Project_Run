using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed class UIPanel_SpriteRaceHUD : UIPanel_Base
    {
        public Sys_Race Race;
        public Sys_SpriteRaceCamera Director;
        public Sys_SpriteRacePresentation Presentation;
        public Sprite[] Portraits;
        public UnityEngine.UI.Image FocusPortrait;
        public UnityEngine.UI.Text FocusRank;
        public UnityEngine.UI.Text Remaining;
        public UnityEngine.UI.Image[] RankPortraits;
        public UnityEngine.UI.Button[] RankButtons;
        public GameObject SkillCard;
        public UnityEngine.UI.Image SkillPortrait;
        public UnityEngine.UI.Text SkillName;
        public UnityEngine.UI.Text SkillRunner;
        public UnityEngine.UI.Text CountdownText;
        public RectTransform SafeRoot;
        public UnityEngine.UI.Text Status;
        public UnityEngine.UI.Text Clock;
        public UnityEngine.UI.Text FocusName;
        public UnityEngine.UI.Text FocusStats;
        public UnityEngine.UI.Text Commentary;
        public UnityEngine.UI.Text SpeedLabel;
        public UnityEngine.UI.Text PauseLabel;
        public UnityEngine.UI.Text[] RankLabels;
        public UnityEngine.UI.Image[] ProgressDots;
        public UnityEngine.UI.Image Stamina;
        public GameObject Result;
        public UnityEngine.UI.Text ResultText;
        public UnityEngine.UI.Text ResultTimes;
        public UnityEngine.UI.Button RestartButton;
        public UnityEngine.UI.Button SpeedButton;
        public UnityEngine.UI.Button PauseButton;
        public UnityEngine.UI.Button CameraButton;
        public UnityEngine.UI.Button FollowButton;
        public UnityEngine.UI.Button SkipButton;
        public UnityEngine.UI.Button TitleButton;
        public UnityEngine.UI.Text CameraLabel;
        private UnityEngine.Events.UnityAction[] rankActions;
        private float nextRefresh;
        private int resultGeneration = -1;

        private void Start()
        {
            var manager = FindFirstObjectByType<Mgr_UI>();
            if (manager != null) { manager.Register(E_PanelType.SpriteRaceHUD, this); manager.Open(E_PanelType.SpriteRaceHUD); }
        }

        public override void OnOpen()
        {
            if (RestartButton == null || Race == null || Director == null) return;
            RestartButton.onClick.AddListener(Restart);
            SpeedButton.onClick.AddListener(ChangeSpeed);
            PauseButton.onClick.AddListener(TogglePause);
            CameraButton.onClick.AddListener(Director.ToggleOverview);
            FollowButton.onClick.AddListener(Director.NextRunner);
            SkipButton.onClick.AddListener(Race.SkipToResult);
            if (TitleButton != null) TitleButton.onClick.AddListener(ReturnTitle);
            if (RankButtons != null)
            {
                rankActions = new UnityEngine.Events.UnityAction[RankButtons.Length];
                for(int i=0;i<RankButtons.Length;i++)
                {
                    int slot=i; rankActions[i]=()=>Director.SelectRunner(Race.Simulation.Order[slot]);
                    RankButtons[i].onClick.AddListener(rankActions[i]);
                }
            }
        }
        public override void OnClose()
        {
            if (RestartButton == null || Race == null || Director == null) return;
            RestartButton.onClick.RemoveListener(Restart);
            SpeedButton.onClick.RemoveListener(ChangeSpeed);
            PauseButton.onClick.RemoveListener(TogglePause);
            CameraButton.onClick.RemoveListener(Director.ToggleOverview);
            FollowButton.onClick.RemoveListener(Director.NextRunner);
            SkipButton.onClick.RemoveListener(Race.SkipToResult);
            if (TitleButton != null) TitleButton.onClick.RemoveListener(ReturnTitle);
            if(rankActions!=null) for(int i=0;i<rankActions.Length;i++) RankButtons[i].onClick.RemoveListener(rankActions[i]);
        }
        private void ReturnTitle() { Mgr_Scene.Load(Define_Scene.Title); }
        private void Restart() { Race.RestartRace(); }
        private void ChangeSpeed() { Race.PlaybackSpeed = Race.PlaybackSpeed >= 2 ? 1 : 2; }
        private void TogglePause() { Race.Paused = !Race.Paused; }
        private void Update()
        {
            if (Race == null || Race.Simulation == null || Screen.width <= 0 || Screen.height <= 0) return;
            if(Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.08f;
            var safe = Screen.safeArea;
            SafeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            SafeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            var sim = Race.Simulation;
            int id = Director.FollowRunner;
            var runner = sim.Runners[id];
            Status.text = Race.Countdown > 0 ? "출발 준비  /  " + Mathf.CeilToInt(Race.Countdown)
                : sim.Phase == E_RacePhase.Result ? "FINISH  /  경기 종료"
                : "초원 컵  ·  " + sim.CurrentLap(sim.Runners[sim.Order[0]].Distance) + " / " + sim.LapCount + "바퀴";
            Clock.text = $"{(int)sim.Time / 60:00}:{sim.Time % 60:00.0}";
            FocusName.text = Race.Entrants[id].Name;
            FocusStats.text = $"{runner.Speed * 3.6f:0} km/h   ·   " + (runner.Finished ? "완주" : Race.Countdown>0 ? "출발 준비" : runner.BlockedBy>=0 ? "앞 주자 견제 · 틈 탐색" : Mathf.Abs(runner.LaneVelocity)>.2f ? "진로 변경 · 추월 시도" : runner.IsContesting ? "자리 경합 중" : "주행 중");
            if(FocusPortrait!=null && Portraits!=null) FocusPortrait.sprite=Portraits[id];
            if(FocusRank!=null) FocusRank.text=runner.Rank+"위";
            if(Remaining!=null) Remaining.text="남은 거리  "+Mathf.Max(0,sim.CourseLength-(float)sim.Runners[sim.Order[0]].Distance).ToString("0")+" m";
            Stamina.fillAmount = runner.Stamina;
            SpeedLabel.text = Race.PlaybackSpeed + "× 배속";
            PauseLabel.text = Race.Paused ? "계속" : "정지";
            if (CameraLabel != null) CameraLabel.text = Director.Overview ? "추적 보기" : "전체 보기";
            Commentary.text = Race.Countdown > 0 ? "잠시 후 출발합니다" : sim.Phase == E_RacePhase.Result
                ? Race.Entrants[sim.Order[0]].Name + " 우승!" : Director.ShotLabel + "   /   " + (runner.SkillAge >= 0 && runner.SkillAge < Race.Entrants[id].SurgeDuration ? "스퍼트!" : "선두 " + Race.Entrants[sim.Order[0]].Name);
            if(Presentation!=null && Race.Countdown<=0 && sim.Phase!=E_RacePhase.Result)
                Commentary.text=Director.ShotLabel+"  ·  "+Presentation.Announcement;
            if(CountdownText!=null) CountdownText.text=Race.Countdown>0 ? Mathf.CeilToInt(Race.Countdown).ToString() : sim.Time<.65 ? "출발!" : "";
            if(SkillCard!=null)
            {
                bool show=Presentation!=null && Presentation.SkillAge>=0 && Presentation.SkillAge<2 && sim.Phase!=E_RacePhase.Result;
                if(SkillCard.activeSelf!=show) SkillCard.SetActive(show);
                if(show)
                {
                    int skill=Presentation.SkillRunner;
                    SkillName.text=Race.Entrants[skill].SkillName;
                    SkillRunner.text=Race.Entrants[skill].Name+"  ·  가속 스킬";
                    SkillPortrait.sprite=Portraits[skill];
                }
            }
            for (int i = 0; i < RankLabels.Length; i++)
            {
                int index = sim.Order[i];
                RankLabels[i].text = RankPortraits!=null && RankPortraits.Length>i ? (i+1)+"위" : $"{i + 1}   {Race.Entrants[index].Name}";
                RankLabels[i].color = index == id ? new Color(.72f,.36f,.09f) : new Color(.22f,.28f,.23f);
                if(RankPortraits!=null && RankPortraits.Length>i) RankPortraits[i].sprite=Portraits[index];
            }
            for (int i = 0; i < ProgressDots.Length; i++)
            {
                var rect = ProgressDots[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(Mathf.Clamp01((float)(sim.Runners[i].Distance / sim.CourseLength)), .5f);
            }
            bool finished = sim.Phase == E_RacePhase.Result;
            if(Result.activeSelf!=finished) Result.SetActive(finished);
            if (finished && resultGeneration!=Race.Generation)
            {
                resultGeneration=Race.Generation;
                string text = "";
                string times = "";
                for (int i = 0; i < sim.Order.Length; i++)
                {
                    var state = sim.Runners[sim.Order[i]];
                    text += $"{i + 1}    {Race.Entrants[state.Id].Name}" + (ResultTimes == null ? $"    {state.FinishTime:0.00}초" : "") + "\n";
                    times += $"{state.FinishTime:0.00}초\n";
                }
                ResultText.text = text;
                if (ResultTimes != null) ResultTimes.text = times;
            }
        }
    }
}
