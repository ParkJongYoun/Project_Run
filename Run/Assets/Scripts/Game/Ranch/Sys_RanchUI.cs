using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Ranch
{
    public sealed class Sys_RanchUI : MonoBehaviour
    {
        public Mgr_UI Manager;
        public UIPanel_Ranch Panel;
        public UIPanel_RanchCollection MonsterPanel;
        public UIPanel_RanchCollection TrainingPanel;
        public UIPanel_RanchArena ArenaPanel;
        public UIPanel_RanchGuide GuidePanel;
        public bool HasPreparationFailed { get; private set; }
        public bool IsPresentationReady => !HasPreparationFailed && Panel != null && Panel.gameObject.activeInHierarchy
            && Panel.Placement != null && Panel.Placement.Layout != null && Panel.CollectionManager == Manager;
        private void Awake()
        {
            HasPreparationFailed = Manager == null || Panel == null || Panel.Placement == null
                || MonsterPanel == null || TrainingPanel == null || ArenaPanel == null;
            if (HasPreparationFailed)
            {
                Debug.LogError("Ranch lobby UI is missing its manager or required panels.", this);
                return;
            }
            Manager.Register(E_PanelType.Ranch, Panel);
            if (GuidePanel != null)
            {
                Panel.Guide = GuidePanel;
                Manager.Register(E_PanelType.RanchGuide, GuidePanel);
                Manager.Open(E_PanelType.RanchGuide);
            }
            if (ArenaPanel != null)
            {
                Panel.CollectionManager = Manager;
                Manager.Register(E_PanelType.RanchArena, ArenaPanel);
                ArenaPanel.SetOwner(Panel);
            }
            if (MonsterPanel == null || TrainingPanel == null) return;
            if (Mgr_Game.Instance != null) Mgr_Game.Instance.EnsureDemoSession();
            Manager.Register(E_PanelType.RanchMonsters, MonsterPanel);
            Manager.Register(E_PanelType.RanchTraining, TrainingPanel);
            MonsterPanel.SetOwner(Panel);
            TrainingPanel.SetOwner(Panel);
            Panel.CollectionManager = Manager;
        }
    }
}
