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
        [Range(.5f,1f)] public float LayoutScale = .94f;
        public ScrollRect Roster;
        public Scrollbar RosterScrollbar;
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
        public float StatFillWidth = 234;
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
        public float TrainingProgressWidth = 704;
        public GameObject TrainingOverlay;
        public Text TrainingOverlayLabel;
        public Image TrainingActor;
        public Image TrainingShadow;
        public Image TrainingScene;
        public Sprite[] TrainingPosters;
        public Sprite[] TrainingStartFrames;

        private UIPanel_Ranch owner;
        private int selectedId;
        private bool training;
        private float trainingElapsed;
        private UIItem_RanchTrainingVideo trainingVideo;
        private Vector2 trainingActorHome;
        public int SelectedId => selectedId;
        public bool IsTraining => training;
        public UIItem_RanchTrainingVideo TrainingVideo => trainingVideo;

        public void SetOwner(UIPanel_Ranch value) => owner = value;

        public override void OnOpen()
        {
            if (!Application.isPlaying || BackButton == null) return;
            BindRosterArt();
            FitRosterCards();
            if (TrainingActor != null) trainingActorHome = TrainingActor.rectTransform.anchoredPosition;
            Bind(BackButton, ReturnToRanch);
            Bind(PreviousButton, () => Scroll(-1));
            Bind(NextButton, () => Scroll(1));
            if (RosterScrollbar != null)
            {
                RosterScrollbar.onValueChanged.RemoveAllListeners();
                RosterScrollbar.onValueChanged.AddListener(OnRosterBarChanged);
            }
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
            owner?.Guide?.Clear();
            training = false;
            if (TrainingOverlay != null) TrainingOverlay.SetActive(false);
            if (TrainingProgress != null) TrainingProgress.transform.parent.gameObject.SetActive(false);
            if (trainingVideo != null) trainingVideo.Shutdown();
            if (TrainingFilm != null) TrainingFilm.SetPlaying(false);
            if (TrainingFilm != null) TrainingFilm.gameObject.SetActive(false);
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

        // Keep all three cards, their painted borders and both text rows inside the
        // viewport. Older scene overrides had larger artwork than the clickable cards.
        public void FitRosterCards()
        {
            if (TrainingMode || Roster == null) return;
            const float width = 166, height = 170, gap = 10, inset = 5;
            Roster.StopMovement();
            PlaceRosterRect(Roster.content, 0, 0, width, 540);
            for (int i = 0; i < RosterButtons.Length; i++)
            {
                var card = RosterButtons[i].transform;
                PlaceRosterRect((RectTransform)card, 0, inset + i * (height + gap), width, height);
                foreach (string name in new[] { "bg_surface", "img_frame", "img_selected" })
                {
                    var border = card.Find(name) as RectTransform;
                    if (border != null) PlaceRosterRect(border, 0, 0, width, height);
                }
                var portrait = card.Find("obj_portrait") as RectTransform;
                if (portrait != null)
                {
                    PlaceRosterRect(portrait, 6, 6, 154, 158);
                    PlaceRosterRect(portrait.Find("bg_scene") as RectTransform, 0, 0, 154, 112);
                    PlaceRosterRect(portrait.Find("img_face") as RectTransform, 0, 0, 154, 112);
                    var face = portrait.Find("img_face").GetComponent<Image>();
                    face.preserveAspect = true;
                    face.rectTransform.pivot = new Vector2(.5f, .5f);
                    face.rectTransform.anchoredPosition = new Vector2(77, -56);
                    PlaceRosterRect(portrait.Find("bg_name_strip") as RectTransform, 0, 110, 154, 48);
                }
                var nameLabel = RosterNames[i];
                PlaceRosterRect(nameLabel.rectTransform, 10, 115, 136, 24);
                nameLabel.fontSize = 17;
                nameLabel.resizeTextForBestFit = true;
                nameLabel.resizeTextMinSize = 15;
                nameLabel.resizeTextMaxSize = 17;
                nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                nameLabel.verticalOverflow = VerticalWrapMode.Truncate;
                nameLabel.alignment = TextAnchor.MiddleCenter;
                var levelLabel = RosterLevels[i];
                PlaceRosterRect(levelLabel.rectTransform, 10, 136, 146, 21);
                levelLabel.fontSize = 16;
                levelLabel.resizeTextForBestFit = false;
                levelLabel.alignment = TextAnchor.MiddleCenter;
                nameLabel.transform.SetAsLastSibling();
                levelLabel.transform.SetAsLastSibling();
            }
            Roster.verticalNormalizedPosition = 1;
        }

        private static void PlaceRosterRect(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        // Resolve by table ID on every open so a roster replacement cannot keep old art or films.
        private void BindRosterArt()
        {
            if (CollectionArt == null) CollectionArt = Mgr_Bundle.Load<Data_DemoArt>("UI/RanchCollection/Data_RanchCollectionArt");
            var monsters = Mgr_Table.Demo.Monsters;
            CharacterSprites = new Sprite[monsters.Length];
            if (TrainingClips == null || TrainingClips.Length != monsters.Length)
                System.Array.Resize(ref TrainingClips, monsters.Length);
            TrainingPosters = new Sprite[monsters.Length];
            TrainingStartFrames = new Sprite[monsters.Length];
            for (int i = 0; i < monsters.Length; i++)
            {
                string id = monsters[i].Id;
                CharacterSprites[i] = FindArt(id + "_body");
                if (RosterPortraits != null && i < RosterPortraits.Length && RosterPortraits[i] != null)
                    RosterPortraits[i].sprite = FindArt(id + "_face");
                TrainingPosters[i] = FindArt(id + "_training");
                TrainingStartFrames[i] = FindArt(id + "_training_start");
                // The original lobby frame player is retained for future reviewed Comfy clips.
                // Do not silently reuse an older film with different characters or scenery.
                if (TrainingClips[i] == null)
                    TrainingClips[i] = Resources.Load<Data_LobbyAnimation>("UI/RanchCollection/Training/" + id);
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
            if (MonsterName != null) MonsterName.text = monster.Name + " <size=24><color=#B06E3C>Lv." + progress.Level + "</color></size>";
            if (TrainingMode)
            {
                if (TrainingFilm != null) { TrainingFilm.SetPlaying(false); TrainingFilm.gameObject.SetActive(false); }
                if (TrainingActor != null) TrainingActor.gameObject.SetActive(false);
                if (TrainingShadow != null) TrainingShadow.gameObject.SetActive(false);
                if (TrainingScene != null)
                {
                    TrainingScene.gameObject.SetActive(true);
                    TrainingScene.sprite = TrainingPosters[selectedId];
                    if (trainingVideo == null) trainingVideo = gameObject.AddComponent<UIItem_RanchTrainingVideo>();
                    trainingVideo.Prepare(TrainingScene, monster.Id);
                }
                GrowthLevel.text = progress.Trained ? "Lv." + progress.Level + "  ·  <color=#186B28>성장 완료</color>" : "Lv." + progress.Level + "  →  <color=#186B28>Lv." + table.RequiredLevel + "</color>";
                int currentSpeed = monster.Stats[0] + progress.SpeedBonus;
                GrowthSpeed.text = progress.Trained ? "스피드  " + currentSpeed + "  ·  <color=#186B28>+" + table.TrainingSpeed + "</color>" : "스피드  " + currentSpeed + "  →  <color=#186B28>" + (monster.Stats[0] + table.TrainingSpeed) + "</color>";
                GrowthPassive.text = progress.EndurancePassive ? "지치지 않는 발 습득" : "후천 패시브 습득";
                Requirements.text = "훈련 없이도 출전 가능";
                TrainingStatus.text = progress.Trained ? "훈련 완료 · 성장 보너스 적용" : "선택한 몬스터만 성장해요";
                TrainLabel.text = progress.Trained ? "훈련 완료" : "훈련 시작";
                TrainButton.interactable = !training && !progress.Trained;
                SetTrainingProgress(0);
                if (TrainingOverlay != null) TrainingOverlay.SetActive(false);
                TrainingProgress.transform.parent.gameObject.SetActive(false);
            }
            else
            {
                Character.sprite = CharacterSprites[selectedId];
                ConfigureCharacterPortrait(Character, monster.Id);
                if (HeaderPortrait != null) HeaderPortrait.sprite = FindArt(monster.Id + "_face");
                if (UniqueSkillIcon != null) UniqueSkillIcon.sprite = monster.Id == "Poring"
                    ? FindArt("icon_poring_skill") : FindArt(monster.Id + "_face");
                Story.text = StorySummary(monster.Story);
                for (int i = 0; i < StatFills.Length; i++)
                {
                    int value = monster.Stats[i] + (i == 0 ? progress.SpeedBonus : 0);
                    float ratio = Mathf.Clamp01(value / 400f);
                    if (StatFills[i].type == Image.Type.Sliced)
                        StatFills[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, StatFillWidth * ratio);
                    else StatFills[i].fillAmount = ratio;
                    StatValues[i].text = value.ToString();
                }
                SkillLabels[0].text = monster.Skill;
                SkillLabels[1].text = "미보유";
                SkillLabels[2].text = progress.EndurancePassive ? "지치지 않는 발" : "훈련으로 습득";
            }
            UpdateScrollControls();
        }

        public static string StorySummary(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int end = value.IndexOf('.');
            return end < 0 ? value : value.Substring(0, end + 1);
        }

        public static void ConfigureCharacterPortrait(Image image, string id)
        {
            if (image == null || image.sprite == null) return;
            var rect = image.rectTransform;
            Vector2 bounds = id == "Poring" ? new Vector2(300, 242) : id == "Pecopeco" ? new Vector2(280, 334) : new Vector2(315, 340);
            Vector2 spriteSize = image.sprite.rect.size;
            Vector2 size = spriteSize * Mathf.Min(bounds.x / spriteSize.x, bounds.y / spriteSize.y);
            // Center the trimmed sprite itself, including when its aspect differs from the display box.
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            image.preserveAspect = true;
            var shadow = rect.parent.Find("img_ground_shadow") as RectTransform;
            if (shadow != null)
            {
                shadow.anchorMin = shadow.anchorMax = shadow.pivot = new Vector2(.5f, .5f);
                shadow.sizeDelta = new Vector2(size.x * .85f, 28);
                shadow.anchoredPosition = new Vector2(0, -size.y * .5f + 4);
            }
        }

        private void Scroll(int direction)
        {
            if (Roster == null || training) return;
            if (!RosterOverflows()) { SelectMonster((selectedId + direction + RosterButtons.Length) % RosterButtons.Length); return; }
            Roster.StopMovement();
            if (TrainingMode) Roster.horizontalNormalizedPosition = Mathf.Clamp01(Roster.horizontalNormalizedPosition + direction * .5f);
            else Roster.verticalNormalizedPosition = Mathf.Clamp01(Roster.verticalNormalizedPosition - direction * .5f);
        }

        private void UpdateScrollControls()
        {
            if (Roster == null) return;
            if (PreviousButton != null) PreviousButton.interactable = !training;
            if (NextButton != null) NextButton.interactable = !training;
            if (RosterScrollbar == null) return;
            RosterScrollbar.interactable = !training;
            RosterScrollbar.size = TrainingMode ? .10f : .14f;
            float value = selectedId / Mathf.Max(1f, RosterButtons.Length - 1f);
            RosterScrollbar.SetValueWithoutNotify(TrainingMode ? value : 1 - value);
        }

        private bool RosterOverflows() => TrainingMode ? Roster.content.rect.width > Roster.viewport.rect.width + 1 : Roster.content.rect.height > Roster.viewport.rect.height + 1;

        private void OnRosterBarChanged(float value)
        {
            if (training) return;
            if (RosterOverflows())
            {
                if (TrainingMode) Roster.horizontalNormalizedPosition = value;
                else Roster.verticalNormalizedPosition = value;
            }
            else SelectMonster(Mathf.RoundToInt((TrainingMode ? value : 1 - value) * (RosterButtons.Length - 1)));
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
            TrainingActor.rectTransform.localScale = Vector3.one;
            if (TrainingShadow != null) { TrainingShadow.rectTransform.localScale = Vector3.one; TrainingShadow.color = Color.white; }
        }

        public void BeginTraining()
        {
            if (!TrainingMode || training || Mgr_User.Data.Monsters[selectedId].Trained) return;
            training = true;
            trainingElapsed = 0;
            foreach (var button in RosterButtons) button.interactable = false;
            TrainButton.interactable = false;
            UpdateScrollControls();
            TrainLabel.text = "훈련 중";
            TrainingStatus.text = "준비, 출발!";
            TrainingProgress.transform.parent.gameObject.SetActive(true);
            SetTrainingProgress(0);
            if (TrainingOverlay != null) TrainingOverlay.SetActive(true);
            if (TrainingOverlayLabel != null) TrainingOverlayLabel.text = "훈련중....";
            if (trainingVideo == null) trainingVideo = gameObject.AddComponent<UIItem_RanchTrainingVideo>();
            if (trainingVideo.HasFailed || string.IsNullOrEmpty(trainingVideo.VideoPath))
                trainingVideo.Prepare(TrainingScene, Mgr_Table.Demo.Monsters[selectedId].Id);
            trainingVideo.Play();
        }

        private void Update()
        {
            DemoUI.SafeArea(SafeArea);
            if (SafeArea != null && Layout != null)
                Layout.localScale = Vector3.one * Mathf.Min(LayoutScale, SafeArea.rect.width / 1280f, SafeArea.rect.height / 720f);
            if (!training) return;
            if (trainingVideo == null || trainingVideo.HasFailed)
            {
                training = false;
                Refresh();
                TrainingStatus.text = "영상을 다시 불러와 주세요";
                return;
            }
            if (!trainingVideo.HasStarted)
            {
                TrainingStatus.text = "훈련 준비 중";
                return;
            }
            trainingElapsed = (float)trainingVideo.MediaTime;
            float ratio = trainingVideo.Progress;
            SetTrainingProgress(ratio);
            TrainingStatus.text = ratio < .2f ? "준비, 출발!" : ratio < .7f ? "리듬에 맞춰 달려요" : "마지막 스퍼트!";
            if (!trainingVideo.IsComplete) return;
            training = false;
            Mgr_User.Train(selectedId);
            Refresh();
        }

        private void SetTrainingProgress(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            TrainingProgress.fillAmount = ratio;
            TrainingProgress.type = Image.Type.Sliced;
            TrainingProgress.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, TrainingProgressWidth * ratio);
            TrainingProgress.enabled = ratio > 0;
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
