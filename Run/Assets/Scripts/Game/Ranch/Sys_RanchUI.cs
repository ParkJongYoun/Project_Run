using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Ranch
{
    [DefaultExecutionOrder(-100)]
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
            if (Mgr_Game.Instance != null) Mgr_Game.Instance.EnsureDemoSession();
            Manager.Register(E_PanelType.Ranch, Panel);
            Manager.Register(E_PanelType.RanchMonsters, MonsterPanel);
            Manager.Register(E_PanelType.RanchTraining, TrainingPanel);
            Manager.Register(E_PanelType.RanchArena, ArenaPanel);
            MonsterPanel.SetOwner(Panel);
            TrainingPanel.SetOwner(Panel);
            ArenaPanel.SetOwner(Panel);
            Panel.CollectionManager = Manager;

            // Editor previews must never become the initial screen of a new lobby visit.
            Manager.Close(E_PanelType.RanchMonsters);
            Manager.Close(E_PanelType.RanchTraining);
            Manager.Close(E_PanelType.RanchArena);
            if (GuidePanel != null)
            {
                Panel.Guide = GuidePanel;
                var guideCanvas = GuidePanel.GetComponentInParent<Canvas>(true);
                if (guideCanvas != null) guideCanvas.gameObject.SetActive(true);
                Manager.Register(E_PanelType.RanchGuide, GuidePanel);
                Manager.Open(E_PanelType.RanchGuide);
            }
            Manager.Open(E_PanelType.Ranch);
        }
    }
}
