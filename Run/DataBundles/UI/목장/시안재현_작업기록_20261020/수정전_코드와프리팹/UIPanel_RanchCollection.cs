using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    // Both prefabs keep their serialized layout; only data and callbacks change at runtime.
    public abstract class UIPanel_RanchCollection : UIPanel_Base
    {
        [Header("Layout")]
        public bool TrainingMode;
        public RectTransform SafeArea;
        public RectTransform Layout;
        public ScrollRect Roster;
        public Button BackButton;
        public Button PreviousButton;
        public Button NextButton;
        public Button[] RosterButtons;
        public Image[] SelectionFrames;
        public Text[] RosterNames;
        public Text[] RosterLevels;
        public Text MonsterName;
        public Image Character;
        public Sprite[] CharacterSprites;
        public Data_DemoArt CollectionArt;
        public Image[] RosterPortraits;
        public Image HeaderPortrait;
        public Image UniqueSkillIcon;
        public Image[] StatFills;
        public Text[] StatValues;
        public Text Story;
        public Text[] SkillLabels;
        public Button[] SkillButtons;
        public GameObject SkillPopup;
        public Text SkillTitle;
        public Text SkillDescription;
        public Button SkillClose;
        [Header("Training")]
        public UIItem_LobbyAnimation TrainingFilm;
        public Data_LobbyAnimation[] TrainingClips;
        public Text GrowthLevel;
        public Text GrowthSpeed;
        public Text GrowthPassive;
        public Text Requirements;
        public Text TrainingStatus;
        public Text TrainLabel;
        public Button TrainButton;
        public Image TrainingProgress;
        public Image TrainingActor;

        private UIPanel_Ranch owner;
        private int selectedId;
        private bool training;
        private float trainingElapsed;
        private Vector2 trainingActorHome;
        public int SelectedId => selectedId;
        public bool IsTraining => training;

        public void SetOwner(UIPanel_Ranch value) => owner = value;

        public override void OnOpen()
        {
            if (!Application.isPlaying || BackButton == null) return;
            BindRosterArt();
            if (TrainingActor != null) trainingActorHome = TrainingActor.rectTransform.anchoredPosition;
            Bind(BackButton, ReturnToRanch);
            Bind(PreviousButton, () => Scroll(-1));
            Bind(NextButton, () => Scroll(1));
            for (int i = 0; i < RosterButtons.Length; i++)
            {
                int id = i;
                Bind(RosterButtons[i], () => SelectMonster(id));
            }
            if (TrainingMode) Bind(TrainButton, BeginTraining);
            else
            {
                for (int i = 0; i < SkillButtons.Length; i++)
                {
                    int category = i;
                    Bind(SkillButtons[i], () => ShowSkill(category));
                }
                Bind(SkillClose, () => SkillPopup.SetActive(false));
            }
            if (SkillPopup != null) SkillPopup.SetActive(false);
            Refresh();
            Canvas.ForceUpdateCanvases();
            UpdateScrollControls();
        }

        public override void OnClose()
        {
            if (!Application.isPlaying) return;
            training = false;
            if (TrainingFilm != null) TrainingFilm.SetPlaying(false);
            ResetTrainingActor();
            if (SkillPopup != null) SkillPopup.SetActive(false);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public void SelectMonster(int id)
        {
            if (training || id < 0 || id >= Mgr_Table.Demo.Monsters.Length) return;
            selectedId = id;
            if (SkillPopup != null) SkillPopup.SetActive(false);
            Refresh();
            EnsureSelectedVisible();
        }

        // Resolve by table ID on every open so a roster replacement cannot keep old art or films.
        private void BindRosterArt()
        {
            if (CollectionArt == null) CollectionArt = Mgr_Bundle.Load<Data_DemoArt>("UI/RanchCollection/Data_RanchCollectionArt");
            var monsters = Mgr_Table.Demo.Monsters;
            CharacterSprites = new Sprite[monsters.Length];
            TrainingClips = new Data_LobbyAnimation[monsters.Length];
            for (int i = 0; i < monsters.Length; i++)
            {
                string id = monsters[i].Id;
                CharacterSprites[i] = FindArt(id + "_body");
                if (RosterPortraits != null && i < RosterPortraits.Length && RosterPortraits[i] != null)
                    RosterPortraits[i].sprite = FindArt(id + "_face");
                // Films are optional: a new playable uses its own animated portrait until a film exists.
                TrainingClips[i] = Resources.Load<Data_LobbyAnimation>("UI/Demo/Training/" + id);
            }
        }

        private Sprite FindArt(string key)
        {
            var sprite = CollectionArt != null ? CollectionArt.Get(key) : null;
            return sprite != null ? sprite : DemoUI.Art.Get(key);
        }

        public void Refresh()
        {
            var table = Mgr_Table.Demo;
            selectedId = Mathf.Clamp(selectedId, 0, table.Monsters.Length - 1);
            for (int i = 0; i < RosterButtons.Length; i++)
            {
                RosterNames[i].text = table.Monsters[i].Name;
                RosterLevels[i].text = "Lv." + Mgr_User.Data.Monsters[i].Level;
                SelectionFrames[i].gameObject.SetActive(i == selectedId);
                RosterButtons[i].interactable = !training;
            }
            var monster = table.Monsters[selectedId];
            var progress = Mgr_User.Data.Monsters[selectedId];
            if (MonsterName != null) MonsterName.text = monster.Name + "  Lv." + progress.Level;
            if (TrainingMode)
            {
                if (TrainingFilm != null && TrainingClips != null && selectedId < TrainingClips.Length)
                {
                    var clip = TrainingClips[selectedId];
                    TrainingFilm.gameObject.SetActive(clip != null);
                    TrainingFilm.ConfigureStates(clip, null);
                    TrainingFilm.SetPlaying(false);
                    if (TrainingActor != null)
                    {
                        TrainingActor.gameObject.SetActive(clip == null);
                        TrainingActor.sprite = CharacterSprites[selectedId];
                        ResetTrainingActor();
                    }
                }
                GrowthLevel.text = progress.Trained ? "Lv." + progress.Level + "  ·  <color=#186B28>성장 완료</color>" : "Lv." + progress.Level + "  →  <color=#186B28>Lv." + table.RequiredLevel + "</color>";
                int currentSpeed = monster.Stats[0] + progress.SpeedBonus;
                GrowthSpeed.text = progress.Trained ? "스피드  " + currentSpeed + "  ·  <color=#186B28>+" + table.TrainingSpeed + "</color>" : "스피드  " + currentSpeed + "  →  <color=#186B28>" + (monster.Stats[0] + table.TrainingSpeed) + "</color>";
                GrowthPassive.text = progress.EndurancePassive ? "지치지 않는 발 · 습득 완료" : "후천 패시브 습득";
                Requirements.text = "Lv." + table.RequiredLevel + " · 스피드 " + table.RequiredSpeed;
                TrainingStatus.text = progress.Trained ? "훈련 완료 · 출전 조건 충족" : "선택한 몬스터만 성장해요";
                TrainLabel.text = progress.Trained ? "훈련 완료" : "훈련 시작";
                TrainButton.interactable = !training && !progress.Trained;
                TrainingProgress.fillAmount = 0;
                TrainingProgress.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                Character.sprite = CharacterSprites[selectedId];
                if (HeaderPortrait != null) HeaderPortrait.sprite = FindArt(monster.Id + "_face");
                if (UniqueSkillIcon != null) UniqueSkillIcon.sprite = monster.Id == "Poring"
                    ? FindArt("icon_poring_skill") : FindArt(monster.Id + "_face");
                Story.text = monster.Story;
                for (int i = 0; i < StatFills.Length; i++)
                {
                    int value = monster.Stats[i] + (i == 0 ? progress.SpeedBonus : 0);
                    StatFills[i].fillAmount = Mathf.Clamp01(value / 400f);
                    StatValues[i].text = value.ToString();
                }
                SkillLabels[0].text = monster.Skill;
                SkillLabels[1].text = "미보유";
                SkillLabels[2].text = progress.EndurancePassive ? "지치지 않는 발" : "훈련으로 습득";
            }
        }

        private void Scroll(int direction)
        {
            if (Roster == null) return;
            Roster.StopMovement();
            if (TrainingMode) Roster.horizontalNormalizedPosition = Mathf.Clamp01(Roster.horizontalNormalizedPosition + direction * .5f);
            else Roster.verticalNormalizedPosition = Mathf.Clamp01(Roster.verticalNormalizedPosition - direction * .5f);
        }

        private void UpdateScrollControls()
        {
            if (Roster == null) return;
            bool overflow = TrainingMode ? Roster.content.rect.width > Roster.viewport.rect.width + 1
                : Roster.content.rect.height > Roster.viewport.rect.height + 1;
            if (PreviousButton != null) PreviousButton.interactable = overflow;
            if (NextButton != null) NextButton.interactable = overflow;
            var bar = TrainingMode ? Roster.horizontalScrollbar : Roster.verticalScrollbar;
            if (bar != null)
            {
                bar.interactable = overflow;
                if (bar.targetGraphic != null) bar.targetGraphic.enabled = overflow;
            }
        }

        private void EnsureSelectedVisible()
        {
            if (Roster == null) return;
            Canvas.ForceUpdateCanvases();
            var card = (RectTransform)RosterButtons[selectedId].transform;
            Roster.StopMovement();
            if (TrainingMode)
            {
                float overflow = Roster.content.rect.width - Roster.viewport.rect.width;
                if (overflow <= 0) return;
                float offset = Roster.horizontalNormalizedPosition * overflow;
                float start = card.anchoredPosition.x;
                offset = Mathf.Clamp(offset, start + card.rect.width - Roster.viewport.rect.width, start);
                Roster.horizontalNormalizedPosition = Mathf.Clamp01(offset / overflow);
            }
            else
            {
                float overflow = Roster.content.rect.height - Roster.viewport.rect.height;
                if (overflow <= 0) return;
                float offset = (1 - Roster.verticalNormalizedPosition) * overflow;
                float start = -card.anchoredPosition.y;
                offset = Mathf.Clamp(offset, start + card.rect.height - Roster.viewport.rect.height, start);
                Roster.verticalNormalizedPosition = 1 - Mathf.Clamp01(offset / overflow);
            }
        }

        private void ResetTrainingActor()
        {
            if (TrainingActor == null) return;
            TrainingActor.rectTransform.anchoredPosition = trainingActorHome;
            TrainingActor.rectTransform.localRotation = Quaternion.identity;
        }

        public void BeginTraining()
        {
            if (!TrainingMode || training || Mgr_User.Data.Monsters[selectedId].Trained) return;
            training = true;
            trainingElapsed = 0;
            foreach (var button in RosterButtons) button.interactable = false;
            TrainButton.interactable = false;
            TrainLabel.text = "훈련 중";
            TrainingStatus.text = "준비, 출발!";
            TrainingProgress.transform.parent.gameObject.SetActive(true);
            TrainingProgress.fillAmount = 0;
            if (TrainingFilm != null && TrainingFilm.gameObject.activeSelf) { TrainingFilm.Restart(); TrainingFilm.SetPlaying(true); }
        }

        private void Update()
        {
            DemoUI.SafeArea(SafeArea);
            if (SafeArea != null && Layout != null)
                Layout.localScale = Vector3.one * Mathf.Min(1, SafeArea.rect.width / 1280f, SafeArea.rect.height / 720f);
            if (!training) return;
            trainingElapsed += Mathf.Min(Time.unscaledDeltaTime, .1f);
            float ratio = Mathf.Clamp01(trainingElapsed / Mathf.Max(.1f, Mgr_Table.Demo.TrainingSeconds));
            TrainingProgress.fillAmount = ratio;
            if (TrainingActor != null && TrainingActor.gameObject.activeSelf)
            {
                TrainingActor.rectTransform.anchoredPosition = trainingActorHome + new Vector2(Mathf.Sin(trainingElapsed * 2) * 22, Mathf.Abs(Mathf.Sin(trainingElapsed * 10)) * 12);
                TrainingActor.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(trainingElapsed * 10) * 3);
            }
            TrainingStatus.text = ratio < .2f ? "준비, 출발!" : ratio < .7f ? "리듬에 맞춰 달려요" : "마지막 스퍼트!";
            if (ratio < 1) return;
            training = false;
            Mgr_User.Train(selectedId);
            Refresh();
        }

        public void ReturnToRanch()
        {
            // Leaving an unfinished training cancels it; growth is granted only at completion.
            if (owner != null) owner.CloseCollection();
        }

        private void ShowSkill(int category)
        {
            var monster = Mgr_Table.Demo.Monsters[selectedId];
            var progress = Mgr_User.Data.Monsters[selectedId];
            SkillTitle.text = category == 0 ? monster.Skill : category == 1 ? "선천 스킬 · 미보유" : "지치지 않는 발";
            SkillDescription.text = category == 0 ? "발동 조건\n" + monster.SkillCondition + "\n\n효과\n" + monster.SkillDescription
                : category == 1 ? "현재 보유한 선천 스킬이 없어요."
                : progress.EndurancePassive ? "후천 패시브 · 습득 완료\n\n레이스 전 구간의 체력 소모가 20% 감소합니다."
                : "스피드 훈련을 마치면 습득해요.\n\n레이스 전 구간의 체력 소모가 20% 감소합니다.";
            SkillPopup.SetActive(true);
        }
    }
}
