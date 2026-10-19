using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Ranch
{
    public sealed class Sys_RanchUI : MonoBehaviour
    {
        public Mgr_UI Manager;
        public UIPanel_Ranch Panel;
        private void Awake() { Manager.Register(E_PanelType.Ranch, Panel); }
    }
}
