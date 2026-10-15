using System.Collections.Generic;
using UnityEngine;
namespace ProjectRun.Race
{
    public sealed class Mgr_UI : MonoBehaviour
    {
        private readonly Dictionary<E_PanelType, UIPanel_Base> panels = new Dictionary<E_PanelType, UIPanel_Base>();
        public void Register(E_PanelType type, UIPanel_Base panel) => panels[type] = panel;
        public void Open(E_PanelType type) { if (panels.TryGetValue(type, out var panel) && panel != null) panel.gameObject.SetActive(true); }
        public void Close(E_PanelType type) { if (panels.TryGetValue(type, out var panel) && panel != null) panel.gameObject.SetActive(false); }
    }
}
