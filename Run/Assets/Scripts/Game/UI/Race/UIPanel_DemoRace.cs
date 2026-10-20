using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoRace : UIPanel_Base
    {
        [Header("경기 HUD · 배치는 프리팹에서 편집")]
        [SerializeField] private RectTransform SafeArea;
        [SerializeField] private Text RankLabel;
        [SerializeField] private Text RemainingLabel;
        [SerializeField] private Text StandingsLabel;
        [SerializeField] private Text HeroNameLabel;
        [SerializeField] private Image HeroImage;
        [SerializeField] private Image StaminaImage;
        [SerializeField] private Text CountdownLabel;
        [SerializeField] private Text CommentaryLabel;
        [SerializeField] private UIItem_RaceCommentary CommentaryFeed;

        [Header("경기 조작")]
        [SerializeField] private Button SpeedButton;
        [SerializeField] private Text SpeedLabel;
        [SerializeField] private Button PauseButton;
        [SerializeField] private Text PauseLabel;
        [SerializeField] private Button ResultButton;
        [SerializeField] private Button LobbyButton;

        [Header("결과 화면")]
        [SerializeField] private RectTransform ResultRoot;
        [SerializeField] private Text ResultTitle;
        [SerializeField] private Image ResultHero;
        [SerializeField] private Text ResultName;
        [SerializeField] private Text ResultSummary;
        [SerializeField] private Button ReplayButton;
        [SerializeField] private Button EndDemoButton;

        private Sys_Race race;
        private bool cutscenes = true;
        private bool opened;
        private int generation = -1;
        private string latest = "선택한 친구의 첫 레이스를 응원해 주세요";
        private float latestUntil;
        public bool CutscenesEnabled
        {
            get => cutscenes;
            set
            {
                cutscenes = value;
                if (!value) CancelSkillPresentation();
            }
        }
        private int Hero => Mgr_Game.Instance.HeroRunner;
        private Data_DemoMonster Monster => Mgr_Table.Demo.Monsters[Mgr_User.Data.SelectedMonster];

        public void Configure(Sys_Race target)
        {
            if (opened) OnClose();
            race = target;
            if (isActiveAndEnabled) OnOpen();
        }

        public override void OnOpen()
        {
            if (!Application.isPlaying || race == null || opened) return;
            if (SafeArea == null)
            {
                Debug.LogError("UIPanel_DemoRace의 프리팹 UI 참조가 없습니다.", this);
                return;
            }
            opened = true;
            generation = -1;
            HeroImage.sprite = race.Entrants[Hero].FacePortrait;
            HeroImage.preserveAspect = true;
            HeroNameLabel.text = Monster.Name;
            VideoTitle.text = Monster.Skill;
            CommentaryFeed.Race = race;
            CommentaryFeed.enabled = FindFirstObjectByType<Sys_RacePresentation>() != null;
            CommentaryFeed.gameObject.SetActive(CommentaryFeed.enabled);
            CommentaryLabel.enabled = !CommentaryFeed.enabled;
            CommentaryLabel.text = latest;
            ResultRoot.gameObject.SetActive(false);
            SetVideoOpacity(0);
            CutscenesEnabled = cutscenes;
            InitStandings(); // 왼쪽 순위 리스트(row_1~6) 연결
            InitProgress(); // 상단 START-GOAL 진행바 연결
            InitBroadcast();
            InitResultSkin(); // 결과창을 네온 스타일 프리팹으로 교체 (버튼 연결 전에!)
            BindButtons();
            Mgr_Event.RaceChanged += Handle;
            Mgr_Event.Reset += CancelSkillPresentation;
        }

        public override void OnClose()
        {
            if (!opened) return;
            opened = false;
            Mgr_Event.RaceChanged -= Handle;
            Mgr_Event.Reset -= CancelSkillPresentation;
            UnbindButtons();
            CloseBroadcast();
            CancelSkillPresentation();
        }

        private void BindButtons()
        {
            SpeedButton.onClick.AddListener(ToggleSpeed);
            PauseButton.onClick.AddListener(TogglePause);
            ResultButton.onClick.AddListener(SkipToResult);
            LobbyButton.onClick.AddListener(ReturnToLobby);
            ReplayButton.onClick.AddListener(Replay);
            EndDemoButton.onClick.AddListener(EndDemo);
        }

        private void UnbindButtons()
        {
            SpeedButton.onClick.RemoveListener(ToggleSpeed);
            PauseButton.onClick.RemoveListener(TogglePause);
            ResultButton.onClick.RemoveListener(SkipToResult);
            LobbyButton.onClick.RemoveListener(ReturnToLobby);
            ReplayButton.onClick.RemoveListener(Replay);
            EndDemoButton.onClick.RemoveListener(EndDemo);
        }

        private void ToggleSpeed() => race.PlaybackSpeed = race.PlaybackSpeed > 1 ? 1 : 2;
        private void TogglePause() => race.Paused = !race.Paused;
        private void SkipToResult() { CancelSkillPresentation(); race.SkipToResult(); }
        private void ReturnToLobby()
        {
            CancelSkillPresentation();
            Mgr_User.Data.RaceInProgress = false;
            Mgr_User.Save();
            Mgr_Game.Instance.Go(Define_Scene.Lobby);
        }
        private void Replay() => Mgr_Game.Instance.Replay();
        private void EndDemo() => Mgr_Game.Instance.Go(Define_Scene.Title);

        private void Handle(Data_RaceEvent value)
        {
            if (race == null || race.IsSkipping) return;
            HandleBroadcast(value);
            if (value.Kind == E_RaceEvent.Skill && value.RunnerId == Hero)
            {
                latest = Monster.Skill + "! 고유 능력 자동 발동";
                latestUntil = (float)race.Simulation.Time + 5;
                if (cutscenes && skillVideo != null) skillVideo.TryPlay();
            }
            else if (value.Kind == E_RaceEvent.Overtake && value.RunnerId == Hero)
            {
                latest = Monster.Name + "! 앞 대열을 돌파합니다!";
                latestUntil = value.Time + 3;
            }
        }

        private void LateUpdate()
        {
            if (!opened || race == null || race.Simulation == null) return;
            DemoUI.SafeArea(SafeArea);
            UpdateVideoLayout();
            if (generation != race.Generation)
            {
                generation = race.Generation;
                CancelSkillPresentation();
                ResultRoot.gameObject.SetActive(false);
            }
            var sim = race.Simulation;
            var hero = sim.Runners[Hero];
            UpdateRankLabel(hero.Rank, sim.Runners.Length); // "2위 / 6" (크기·색은 Inspector "하단 순위 글자"에서 조절)
            RemainingLabel.text = "남은 거리  " + Mathf.CeilToInt(Mathf.Max(0, sim.CourseLength - (float)hero.Distance)) + "m";
            SpeedLabel.text = race.PlaybackSpeed > 1 ? "2×" : "1×";
            PauseLabel.text = race.Paused ? "재개" : "정지";
            StaminaImage.fillAmount = hero.Stamina;
            CountdownLabel.text = CameraRig.RaceStartIntro.Playing ? "" : race.Countdown > 0 ? Mathf.CeilToInt(race.Countdown).ToString() : sim.Time < 1 ? "START!" : "";
            UpdateProgress(sim); // 진행바 마커 이동
            UpdateBroadcast();
            if (standingsReady) UpdateStandings(sim); // 새 순위 리스트(줄 6개) 갱신
            else
            {
                // 순위 리스트를 못 찾았을 때만 예전 텍스트 방식으로 표시
                string ranks = "";
                for (int i = 0; i < sim.Order.Length; i++)
                {
                    int id = sim.Order[i];
                    ranks += (id == Hero ? "<color=#FFE99A>" : "") + (i + 1) + "  " + race.Entrants[id].Name + (id == Hero ? " ◀</color>" : "") + "\n";
                }
                StandingsLabel.text = ranks;
            }
            if (!CommentaryFeed.isActiveAndEnabled)
                CommentaryLabel.text = race.Paused ? "잠시 쉬어가기" : (float)sim.Time < latestUntil ? latest : hero.SkillUsed ? "결승선까지, 한 걸음 더!" : sim.Time < 8 ? Monster.Tactic + " 전술로 대열을 잡습니다" : hero.BlockedBy >= 0 ? "앞 주자를 피해 빈틈을 찾고 있어요" : "힘을 아끼며 기회를 기다립니다";
            if (sim.Phase == E_RacePhase.Result && !CameraRig.RaceFinishOutro.Showing && !ResultRoot.gameObject.activeSelf) ShowResult();
        }

        private void ShowResult()
        {
            CancelSkillPresentation();
            var hero = race.Simulation.Runners[Hero];
            ResultHero.sprite = DemoUI.Art.Get(Monster.Id + "_body");
            ResultName.text = Monster.Name;
            // 제목/순위/기록 텍스트 채우기 (UIPanel_DemoRace.Result.cs)
            FillResultTexts(hero.Rank, hero.FinishTime.ToString("0.00"),
                (Monster.Stats[0] + Mgr_User.Data.Monsters[Mgr_User.Data.SelectedMonster].SpeedBonus).ToString());
            ResultRoot.SetAsLastSibling(); // 결과창을 순위 리스트·진행바보다 앞(위)에 그림
            ResultRoot.gameObject.SetActive(true);
        }
    }
}
