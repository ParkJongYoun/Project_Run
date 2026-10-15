using UnityEngine;
namespace ProjectRun.Race
{
    public enum E_PanelType { RaceHUD, RaceSkillVideo }
    public enum E_CanvasLayer { World = 0, Game = 100, Panel = 200, Cutscene = 300, Popup = 400, System = 500 }
    public abstract class UIPanel_Base : MonoBehaviour
    {
        public abstract void OnOpen();
        public abstract void OnClose();
        private void OnEnable() => OnOpen();
        private void OnDisable() => OnClose();
    }
}
