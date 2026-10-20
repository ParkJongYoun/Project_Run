using UnityEngine;

namespace ProjectRun.Race
{
    // The scenic exhibition is presentation only; entry uses the shared demo table and rules.
    public sealed class UIPanel_RanchArena : UIPanel_Base
    {
        public RectTransform SafeArea;
        public RectTransform Layout;
        public UnityEngine.UI.Button BackButton;
        public UnityEngine.UI.Button EntryButton;
        public UnityEngine.UI.Button NotebookButton;
        public UnityEngine.UI.Button SettingsButton;
        public UnityEngine.UI.Text Requirements;
        public GameObject EntryPopup;
        public UnityEngine.UI.Button EntryClose;
        public UnityEngine.UI.Button[] EntrantButtons;
        public UnityEngine.UI.Image[] EntrantFrames;
        public UnityEngine.UI.Image[] EntrantPortraits;
        public UnityEngine.UI.Text[] EntrantNames;
        public UnityEngine.UI.Text[] EntrantStats;
        public UnityEngine.UI.Text[] EntrantStates;
        public UnityEngine.UI.Text TacticDescription;
        public UnityEngine.UI.Button ConfirmEntry;
        public UnityEngine.UI.Text ConfirmLabel;
        public GameObject NotebookPopup;
        public UnityEngine.UI.Button NotebookClose;
        public GameObject SettingsPopup;
        public UnityEngine.UI.Button SettingsClose;
        public UnityEngine.UI.Button SoundButton;
        public UnityEngine.UI.Text SoundLabel;

        private UIPanel_Ranch owner;
        private int selectedId = -1;
        public int SelectedId => selectedId;

        public void SetOwner(UIPanel_Ranch value) => owner = value;

        public override void OnOpen()
        {
            if (!Application.isPlaying || BackButton == null) return;
            Bind(BackButton, ReturnToRanch);
            Bind(EntryButton, ShowEntrants);
            Bind(NotebookButton, () => { ClosePopups(); NotebookPopup.SetActive(true); });
            Bind(SettingsButton, () => { ClosePopups(); RefreshSound(); SettingsPopup.SetActive(true); });
            Bind(EntryClose, ClosePopups);
            Bind(NotebookClose, ClosePopups);
            Bind(SettingsClose, ClosePopups);
            Bind(ConfirmEntry, ConfirmSelection);
            Bind(SoundButton, ToggleSound);
            for (int i = 0; i < EntrantButtons.Length; i++)
            {
                int id = i;
                Bind(EntrantButtons[i], () => SelectEntrant(id));
            }
            Requirements.text = "Lv." + Mgr_Table.Demo.RequiredLevel + " · 스피드 " + Mgr_Table.Demo.RequiredSpeed;
            ClosePopups();
        }

        public override void OnClose() => ClosePopups();

        private static void Bind(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        private void Update()
        {
            DemoUI.SafeArea(SafeArea);
            if (SafeArea != null && Layout != null)
                Layout.localScale = Vector3.one * Mathf.Min(1, SafeArea.rect.width / 1280f, SafeArea.rect.height / 720f);
        }

        public void ClosePopups()
        {
            if (EntryPopup != null) EntryPopup.SetActive(false);
            if (NotebookPopup != null) NotebookPopup.SetActive(false);
            if (SettingsPopup != null) SettingsPopup.SetActive(false);
        }

        public void ShowEntrants()
        {
            ClosePopups();
            selectedId = Mgr_User.Data.SelectedMonster;
            if (selectedId >= EntrantButtons.Length) selectedId = -1;
            var art = Mgr_Bundle.Load<Data_DemoArt>("UI/RanchCollection/Data_RanchCollectionArt");
            for (int i = 0; i < EntrantButtons.Length; i++)
            {
                var monster = Mgr_Table.Demo.Monsters[i];
                var progress = Mgr_User.Data.Monsters[i];
                var portrait = art != null ? art.Get(monster.Id + "_face") : null;
                EntrantPortraits[i].sprite = portrait != null ? portrait : DemoUI.Art.Get(monster.Id + "_face");
                EntrantNames[i].text = monster.Name;
                EntrantStats[i].text = "Lv." + progress.Level + "  ·  스피드 " + (monster.Stats[0] + progress.SpeedBonus);
                bool eligible = Mgr_User.CanEnter(i);
                EntrantStates[i].text = eligible ? "출전 가능" : "훈련이 필요해요";
                EntrantStates[i].color = eligible ? new Color(.10f,.39f,.17f) : new Color(.60f,.26f,.12f);
            }
            RefreshSelection();
            EntryPopup.SetActive(true);
        }

        public void SelectEntrant(int id)
        {
            if (id < 0 || id >= EntrantButtons.Length) return;
            selectedId = id;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < EntrantFrames.Length; i++) EntrantFrames[i].gameObject.SetActive(i == selectedId);
            ConfirmEntry.interactable = selectedId >= 0;
            if (selectedId < 0)
            {
                TacticDescription.text = "이번 경기에 출전할 친구 한 마리를 선택하세요.";
                ConfirmLabel.text = "친구를 선택하세요";
                return;
            }
            var monster = Mgr_Table.Demo.Monsters[selectedId];
            TacticDescription.text = "추천 전술 · " + monster.Tactic + "\n" + monster.Strength + "에 강한 친구예요.";
            ConfirmLabel.text = Mgr_User.CanEnter(selectedId) ? "전술 확정 · 레이스 시작" : "훈련하러 가기";
        }

        private void ConfirmSelection()
        {
            if (selectedId < 0) return;
            if (!Mgr_User.CanEnter(selectedId))
            {
                if (owner != null) owner.ShowTrainingFromArena();
                return;
            }
            if (Mgr_Game.Instance == null || Mgr_Game.Instance.IsTransitioning) return;
            // Browsing or cancelling never changes monster/training selections or entry data.
            Mgr_User.Select(selectedId);
            ConfirmEntry.interactable = false;
            Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon);
        }

        private void RefreshSound() => SoundLabel.text = "소리  " + (Mgr_User.Data.Volume > 0 ? "켜짐" : "꺼짐");

        private void ToggleSound()
        {
            Mgr_User.Data.Volume = Mgr_User.Data.Volume > 0 ? 0 : .65f;
            AudioListener.volume = Mgr_User.Data.Volume;
            Mgr_User.Save();
            RefreshSound();
        }

        public void ReturnToRanch()
        {
            if (owner != null) owner.CloseCollection();
        }
    }
}
