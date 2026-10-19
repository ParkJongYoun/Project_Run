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
        [SerializeField] private Button CutsceneButton;
        [SerializeField] private Text CutsceneLabel;

        [Header("만화 연출 · 콘텐츠만 실행 중 생성")]
        [SerializeField] private RectTransform ComicRoot;
        [SerializeField] private RectTransform ComicContent;
        [SerializeField] private Button ComicSkipButton;

        [Header("결과 화면")]
        [SerializeField] private RectTransform ResultRoot;
        [SerializeField] private Text ResultTitle;
        [SerializeField] private Image ResultHero;
        [SerializeField] private Text ResultName;
        [SerializeField] private Text ResultSummary;
        [SerializeField] private Button ReplayButton;
        [SerializeField] private Button EndDemoButton;

        private Sys_Race race;
        private float comicAge = -1;
        private UIItem_MinorousComic minorousComic;
        private UIItem_DemoHeroComic heroComic;
        private bool cutscenes = true;
        private bool opened;
        private int generation = -1;
        private string latest = "선택한 친구의 첫 레이스를 응원해 주세요";
        private float latestUntil;
        public bool IsComicPlaying => comicAge >= 0;
        public bool CutscenesEnabled
        {
            get => cutscenes;
            set
            {
                cutscenes = value;
                if (CutsceneLabel != null) CutsceneLabel.text = value ? "연출 ON" : "연출 OFF";
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
            HeroImage.sprite = DemoUI.Art.Get(Monster.Id + "_face");
            HeroNameLabel.text = Monster.Name;
            VideoTitle.text = Monster.Skill;
            CommentaryFeed.Race = race;
            CommentaryFeed.enabled = FindFirstObjectByType<Sys_RacePresentation>() != null;
            CommentaryFeed.gameObject.SetActive(CommentaryFeed.enabled);
            CommentaryLabel.enabled = !CommentaryFeed.enabled;
            CommentaryLabel.text = latest;
            ComicRoot.gameObject.SetActive(false);
            ResultRoot.gameObject.SetActive(false);
            SetVideoOpacity(0);
            CutscenesEnabled = cutscenes;
            BindButtons();
            Mgr_Event.RaceChanged += Handle;
            Mgr_Event.Reset += CancelComic;
        }

        public override void OnClose()
        {
            if (!opened) return;
            opened = false;
            Mgr_Event.RaceChanged -= Handle;
            Mgr_Event.Reset -= CancelComic;
            UnbindButtons();
            CancelSkillPresentation();
        }

        private void BindButtons()
        {
            SpeedButton.onClick.AddListener(ToggleSpeed);
            PauseButton.onClick.AddListener(TogglePause);
            ResultButton.onClick.AddListener(SkipToResult);
            LobbyButton.onClick.AddListener(ReturnToLobby);
            CutsceneButton.onClick.AddListener(ToggleCutscenes);
            ComicSkipButton.onClick.AddListener(CancelComic);
            ReplayButton.onClick.AddListener(Replay);
            EndDemoButton.onClick.AddListener(EndDemo);
            VideoSkipButton.onClick.AddListener(SkipVideo);
            VideoPauseButton.onClick.AddListener(TogglePause);
        }

        private void UnbindButtons()
        {
            SpeedButton.onClick.RemoveListener(ToggleSpeed);
            PauseButton.onClick.RemoveListener(TogglePause);
            ResultButton.onClick.RemoveListener(SkipToResult);
            LobbyButton.onClick.RemoveListener(ReturnToLobby);
            CutsceneButton.onClick.RemoveListener(ToggleCutscenes);
            ComicSkipButton.onClick.RemoveListener(CancelComic);
            ReplayButton.onClick.RemoveListener(Replay);
            EndDemoButton.onClick.RemoveListener(EndDemo);
            VideoSkipButton.onClick.RemoveListener(SkipVideo);
            VideoPauseButton.onClick.RemoveListener(TogglePause);
        }

        private void ToggleSpeed() => race.PlaybackSpeed = race.PlaybackSpeed > 1 ? 1 : 2;
        private void TogglePause() => race.Paused = !race.Paused;
        private void ToggleCutscenes() => CutscenesEnabled = !CutscenesEnabled;
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
            if (value.Kind == E_RaceEvent.Skill && value.RunnerId == Hero)
            {
                latest = Monster.Skill + "! 고유 능력 자동 발동";
                latestUntil = (float)race.Simulation.Time + 5;
                if (cutscenes)
                {
                    bool second = race.Simulation.Runners[Hero].SkillActivationCount == 2;
                    if (second && skillVideo != null && skillVideo.TryPlay()) return;
                    PlayComicFallback();
                }
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
            UpdateVideoControls();
            if (generation != race.Generation)
            {
                generation = race.Generation;
                CancelComic();
                ResultRoot.gameObject.SetActive(false);
            }
            var sim = race.Simulation;
            var hero = sim.Runners[Hero];
            RankLabel.text = hero.Rank + "위 / " + sim.Runners.Length;
            RemainingLabel.text = "남은 거리  " + Mathf.CeilToInt(Mathf.Max(0, sim.CourseLength - (float)hero.Distance)) + "m";
            SpeedLabel.text = race.PlaybackSpeed > 1 ? "2×" : "1×";
            PauseLabel.text = race.Paused ? "재개" : "정지";
            StaminaImage.fillAmount = hero.Stamina;
            CountdownLabel.text = race.Countdown > 0 ? Mathf.CeilToInt(race.Countdown).ToString() : sim.Time < 1 ? "START!" : "";
            string ranks = "";
            for (int i = 0; i < sim.Order.Length; i++)
            {
                int id = sim.Order[i];
                ranks += (id == Hero ? "<color=#FFE99A>" : "") + (i + 1) + "  " + race.Entrants[id].Name + (id == Hero ? " ◀</color>" : "") + "\n";
            }
            StandingsLabel.text = ranks;
            if (!CommentaryFeed.isActiveAndEnabled)
                CommentaryLabel.text = race.Paused ? "잠시 쉬어가기" : (float)sim.Time < latestUntil ? latest : hero.SkillUsed ? "결승선까지, 한 걸음 더!" : sim.Time < 8 ? Monster.Tactic + " 전술로 대열을 잡습니다" : hero.BlockedBy >= 0 ? "앞 주자를 피해 빈틈을 찾고 있어요" : "힘을 아끼며 기회를 기다립니다";
            if (comicAge >= 0 && !race.Paused)
            {
                comicAge += Mathf.Min(Time.unscaledDeltaTime, .1f);
                AnimateComic();
                if (comicAge >= (minorousComic != null ? UIItem_MinorousComic.Duration : heroComic != null ? UIItem_DemoHeroComic.Duration : 0)) CancelComic();
            }
            if (sim.Phase == E_RacePhase.Result && !ResultRoot.gameObject.activeSelf) ShowResult();
        }

        private void StartComic()
        {
            // Only animated character content is transient; authored controls are never cleared.
            DemoUI.Clear(ComicContent);
            ComicRoot.gameObject.SetActive(true);
            comicAge = 0;
            minorousComic = null;
            heroComic = null;
            if (Monster.Id == "Minorous") minorousComic = UIItem_MinorousComic.Create(ComicContent);
            else if (Monster.Id == "Poring" || Monster.Id == "Eddga")
                heroComic = UIItem_DemoHeroComic.Create(ComicContent, Monster.Id, Monster.Skill);
            if (minorousComic == null && heroComic == null)
            {
                Debug.LogWarning("Demo comic resources are missing for " + Monster.Id, this);
                CancelComic();
                return;
            }
            Mgr_Sound.PlayAccent(true);
        }

        private void AnimateComic()
        {
            if (minorousComic != null) minorousComic.Evaluate(comicAge);
            else if (heroComic != null) heroComic.Evaluate(comicAge);
        }

        public void CancelComic()
        {
            comicAge = -1;
            if (ComicRoot != null) ComicRoot.gameObject.SetActive(false);
            if (race != null) race.ReleasePresentationHold(this);
        }

        public void PlayComicFallback()
        {
            if (opened && cutscenes && isActiveAndEnabled && !race.IsSkipping && race.Simulation.Phase != E_RacePhase.Result
                && race.AcquirePresentationHold(this)) StartComic();
        }

        private void ShowResult()
        {
            CancelSkillPresentation();
            var hero = race.Simulation.Runners[Hero];
            ResultTitle.text = hero.Rank == 1 ? "프론테라컵 우승!" : "프론테라컵 완주!";
            ResultHero.sprite = DemoUI.Art.Get(Monster.Id + "_body");
            ResultName.text = Monster.Name;
            ResultSummary.text = hero.Rank + "위  ·  " + hero.FinishTime.ToString("0.00") + "초\nLv.5  ·  스피드 "
                + (Monster.Stats[0] + Mgr_User.Data.Monsters[Mgr_User.Data.SelectedMonster].SpeedBonus)
                + "\n전술  " + Monster.Tactic + "\n고유  " + Monster.Skill + "\n후천  지치지 않는 발";
            ResultRoot.gameObject.SetActive(true);
        }
    }
}
