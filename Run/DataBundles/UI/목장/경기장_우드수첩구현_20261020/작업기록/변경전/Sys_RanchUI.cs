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
        private void Awake()
        {
            Manager.Register(E_PanelType.Ranch, Panel);
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
