using UnityEngine;
using UnityEngine.Events;
namespace ProjectRun.Race
{
    [DefaultExecutionOrder(150)]
    public sealed class UIPanel_RaceHUD : UIPanel_Base
    {
        public Sys_Race Race;
        public Sys_RaceCamera Director;
        public Sys_RacePresentation Presentation;
        public RectTransform SafeArea;
        public UnityEngine.UI.Text DistanceLabel, PhaseLabel, ShotLabel, CountdownLabel;
        public UnityEngine.UI.Image ProgressFill;
        public RectTransform ProgressHandle;
        public RectTransform[] RankBadges;
        public UnityEngine.UI.Text[] RankLabels;
        public UnityEngine.UI.Image[] RankBackgrounds;
        public GameObject[] MyBadges;
        public UnityEngine.UI.Button[] RunnerButtons;
        public UnityEngine.UI.Image FocusPortrait;
        public UnityEngine.UI.Text FocusName, FocusGap, FocusRank, FocusCount, FocusChange, FocusNumber;
        public UnityEngine.UI.Image FocusChangeIcon;
        public Sprite RankUpSprite, RankDownSprite;
        public UnityEngine.UI.Image[] RankFrames;
        public GameObject CountdownRoot;
        public RectTransform WorldMarker;
        public UnityEngine.UI.Text WorldMarkerText;
        public UnityEngine.UI.Text Announcement, SkillName, SkillRunner;
        public UIItem_RaceCommentary Commentary;
        public GameObject AnnouncementRoot, SkillRoot, ResultRoot, MenuRoot;
        public UnityEngine.UI.Text ResultName, ResultDetails;
        public UIItem_RaceResult[] ResultRows;
        public UnityEngine.UI.Image WinnerPortrait;
        public UnityEngine.UI.Text WinnerMount, WinnerTime, ResultSummary;
        public UnityEngine.UI.Button CameraButton, SpeedButton, SkipButton, MenuButton;
        public UnityEngine.UI.Button PauseButton, CutsceneButton, RestartButton, MenuCloseButton, ResultReplay;
        public UnityEngine.UI.Text CameraButtonLabel, SpeedButtonLabel, CutsceneButtonLabel;
        public int SelectedRunnerId = 2;
        // Left, bottom, right, top clearance in Canvas reference units.
        public Vector4 WorldMarkerInsets = new Vector4(40, 335, 40, 275);
        private UnityAction[] selectionActions;
        private bool menuOpen, pausedBeforeMenu;
        private int generation = -1, previousRank;
        private int shownResultGeneration = -1;
        private float rankChangeUntil;
        private Rect lastSafe;
        private Vector2Int lastScreen;
        private void Start()
        {
            var manager = FindFirstObjectByType<Mgr_UI>();
            if (manager != null) manager.Register(E_PanelType.RaceHUD, this);
        }
        public override void OnOpen()
        {
            CameraButton.onClick.AddListener(ChangeCamera); SpeedButton.onClick.AddListener(ChangeSpeed);
            SkipButton.onClick.AddListener(Skip); MenuButton.onClick.AddListener(ToggleMenu);
            PauseButton.onClick.AddListener(ToggleMenu); MenuCloseButton.onClick.AddListener(ToggleMenu);
            CutsceneButton.onClick.AddListener(ChangeCutscene); RestartButton.onClick.AddListener(Restart);
            ResultReplay.onClick.AddListener(Restart);
            selectionActions = new UnityAction[RunnerButtons.Length];
            for (int i = 0; i < selectionActions.Length; i++)
            {
                int id = i;
                selectionActions[i] = () => { SelectedRunnerId = id; previousRank = 0; rankChangeUntil = 0; };
                RunnerButtons[i].onClick.AddListener(selectionActions[i]);
            }
        }
        public override void OnClose()
        {
            CameraButton.onClick.RemoveListener(ChangeCamera); SpeedButton.onClick.RemoveListener(ChangeSpeed);
            SkipButton.onClick.RemoveListener(Skip); MenuButton.onClick.RemoveListener(ToggleMenu);
            PauseButton.onClick.RemoveListener(ToggleMenu); MenuCloseButton.onClick.RemoveListener(ToggleMenu);
            CutsceneButton.onClick.RemoveListener(ChangeCutscene); RestartButton.onClick.RemoveListener(Restart);
            ResultReplay.onClick.RemoveListener(Restart);
            if (selectionActions != null) for (int i = 0; i < selectionActions.Length; i++) RunnerButtons[i].onClick.RemoveListener(selectionActions[i]);
            CloseMenu();
        }
        private void ChangeCamera() => Director.CycleCamera();
        private void ChangeSpeed() => Race.PlaybackSpeed = Race.PlaybackSpeed > 1 ? 1 : 2;
        private void ChangeCutscene() => Presentation.Cutscenes = !Presentation.Cutscenes;
        private void Skip() { CloseMenu(); Race.SkipToResult(); }
        private void Restart() { CloseMenu(); Director.ManualShot = -1; Race.RestartRace(); }
        public void ToggleMenu()
        {
            if (menuOpen) { CloseMenu(); return; }
            pausedBeforeMenu = Race.Paused; Race.Paused = true; menuOpen = true; MenuRoot.SetActive(true);
            // The prefab has no parent Canvas until instantiated; set the popup layer after activation.
            var menuCanvas = MenuRoot.GetComponent<Canvas>();
            if (menuCanvas != null) { menuCanvas.overrideSorting = true; menuCanvas.sortingOrder = (int)E_CanvasLayer.Popup; }
        }
        private void CloseMenu()
        {
            if (menuOpen && Race != null) Race.Paused = pausedBeforeMenu;
            menuOpen = false; if (MenuRoot != null) MenuRoot.SetActive(false);
        }
        private void LateUpdate()
        {
            if (Race == null || Race.Simulation == null) return;
            UpdateSafeArea(); var sim = Race.Simulation; int count = sim.Runners.Length;
            SelectedRunnerId = Mathf.Clamp(SelectedRunnerId, 0, count - 1);
            var selected = sim.Runners[SelectedRunnerId]; var setup = Race.Entrants[SelectedRunnerId];
            if (generation != Race.Generation) { generation = Race.Generation; previousRank = 0; rankChangeUntil = 0; }
            DistanceLabel.text = Mathf.CeilToInt(Presentation.Remaining).ToString();
            PhaseLabel.text = sim.Phase == E_RacePhase.Result ? "경기 종료" : Presentation.Remaining < 120 ? "최종 직선" : sim.Time < 5 ? "출발" : sim.Course != null ? sim.Course.Region((float)sim.Runners[sim.Order[0]].Distance) : "초원 레이스";
            ShotLabel.text = Director.ShotLabel;
            CameraButtonLabel.text = "카메라";
            SpeedButtonLabel.text = Race.PlaybackSpeed > 1 ? "2배" : "1배";
            CutsceneButtonLabel.text = Presentation.Cutscenes ? "컷씬 연출  ON" : "컷씬 연출  OFF";
            float progress = Mathf.Clamp01(1 - Presentation.Remaining / sim.CourseLength);
            ProgressFill.fillAmount = progress;
            ProgressHandle.anchorMin = ProgressHandle.anchorMax = new Vector2(progress, .5f);
            ProgressHandle.anchoredPosition = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                RankBadges[i].anchoredPosition = new Vector2((sim.Runners[i].Rank - 1 - (count - 1) * .5f) * 55, 0);
                RankLabels[i].text = (i + 1).ToString();
                RankBackgrounds[i].color = i == SelectedRunnerId ? new Color(1, .8f, .29f) : Race.Entrants[i].Color;
                RankLabels[i].color = i == SelectedRunnerId ? new Color(.24f, .15f, .06f) : Color.white;
                MyBadges[i].SetActive(i == SelectedRunnerId);
                if (RankFrames != null && i < RankFrames.Length) RankFrames[i].enabled = i == SelectedRunnerId;
            }
            FocusPortrait.sprite = setup.FacePortrait != null ? setup.FacePortrait : setup.Portrait;
            FocusName.text = setup.Name == setup.Mount ? setup.Name : setup.Name + " · " + setup.Mount;
            FocusNumber.text = (SelectedRunnerId + 1).ToString("00");
            float gap = Mathf.Max(0, (float)(sim.Runners[sim.Order[0]].Distance - selected.Distance));
            FocusGap.text = selected.Rank == 1 ? "선두로 달리는 중" : "선두와  " + gap.ToString("0.0") + " m";
            FocusRank.text = selected.Rank.ToString(); FocusCount.text = "/ " + count;
            if (previousRank > 0 && selected.Rank != previousRank)
            {
                int change = previousRank - selected.Rank;
                FocusChange.text = (change > 0 ? "+" : "-") + Mathf.Abs(change);
                FocusChange.color = change > 0 ? new Color(.22f, .48f, .12f) : new Color(.7f, .25f, .12f);
                if (FocusChangeIcon != null) FocusChangeIcon.sprite = change > 0 ? RankUpSprite : RankDownSprite;
                rankChangeUntil = (float)sim.Time + 3;
            }
            previousRank = selected.Rank;
            if (sim.Time >= rankChangeUntil) FocusChange.text = "";
            if (FocusChangeIcon != null) FocusChangeIcon.gameObject.SetActive(FocusChange.text.Length > 0);
            CountdownLabel.text = Race.Countdown > .9f ? Mathf.CeilToInt(Race.Countdown - .9f).ToString() : Race.Countdown > 0 ? "READY" : sim.Time < .9 ? "START!" : "";
            CountdownLabel.gameObject.SetActive(CountdownLabel.text.Length > 0);
            if (CountdownRoot != null) CountdownRoot.SetActive(CountdownLabel.text.Length > 0);
            if (Commentary == null) Announcement.text = string.IsNullOrEmpty(Presentation.Announcement)
                ? (count == 1 ? setup.Name + " 단독 주행" : sim.Course != null ? sim.Course.CourseName + " 레이스" : "여섯 주자의 초원컵 레이스") : Presentation.Announcement;
            bool cinematic = Presentation.InCutscene; SkillRoot.SetActive(cinematic);
            if (cinematic)
            {
                var skill = Race.Entrants[Presentation.SkillRunner]; SkillName.text = skill.SkillName;
                SkillRunner.text = (Presentation.SkillRunner + 1).ToString("00") + " · " + skill.Name;
            }
            UpdateWorldMarker();
            bool result = sim.Phase == E_RacePhase.Result; ResultRoot.SetActive(result && !menuOpen);
            if (result && shownResultGeneration != Race.Generation)
            {
                shownResultGeneration = Race.Generation;
                var winner = Race.Entrants[sim.Order[0]];
                double winnerTime = sim.Runners[sim.Order[0]].FinishTime;
                ResultName.text = winner.Name + " 우승!";
                WinnerPortrait.sprite = winner.FacePortrait != null ? winner.FacePortrait : winner.Portrait;
                WinnerMount.text = winner.Mount;
                WinnerTime.text = winnerTime.ToString("0.000") + " s";
                ResultSummary.text = "내 주자 " + (SelectedRunnerId + 1).ToString("00") + " · " + setup.Name + "   " + selected.Rank + "위";
                for (int i = 0; i < ResultRows.Length; i++)
                {
                    if (i >= sim.Order.Length) { ResultRows[i].ResetItem(); continue; }
                    var runner = sim.Runners[sim.Order[i]];
                    ResultRows[i].Bind(new Data_RaceResultRow { Runner = Race.Entrants[runner.Id], Place = i + 1,
                        Time = runner.FinishTime, Gap = runner.FinishTime - winnerTime, Selected = runner.Id == SelectedRunnerId });
                }
            }
        }
        private void UpdateWorldMarker()
        {
            var view = Presentation.Views[SelectedRunnerId];
            var point = Director.RaceCamera.WorldToScreenPoint(view.FaceTarget.position + Vector3.up * .55f);
            float uiScale = GetComponentInParent<Canvas>().scaleFactor;
            // Keep the badge and its pointer above the face at every camera distance.
            point.y += 44f * uiScale;
            var safe = Screen.safeArea;
            bool visible = point.z > 0 && point.x > safe.xMin + WorldMarkerInsets.x * uiScale && point.x < safe.xMax - WorldMarkerInsets.z * uiScale
                && point.y > safe.yMin + WorldMarkerInsets.y * uiScale && point.y < safe.yMax - WorldMarkerInsets.w * uiScale
                && !menuOpen && !Presentation.InCutscene && Race.Simulation.Phase != E_RacePhase.Result;
            WorldMarker.gameObject.SetActive(visible);
            if (!visible) return;
            Vector2 local; RectTransformUtility.ScreenPointToLocalPointInRectangle(SafeArea, point, null, out local);
            WorldMarker.localPosition = new Vector3(local.x, local.y, 0); WorldMarkerText.text = "MY " + (SelectedRunnerId + 1).ToString("00");
        }
        private void UpdateSafeArea()
        {
            var safe = Screen.safeArea; var size = new Vector2Int(Screen.width, Screen.height);
            if (safe == lastSafe && size == lastScreen) return;
            lastSafe = safe; lastScreen = size;
            SafeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            SafeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
        }
    }
}

