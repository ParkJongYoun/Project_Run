using UnityEngine;
namespace ProjectRun.Race
{
    public enum E_PanelType { RaceHUD = 0, RaceSkillVideo = 1, PlayMode = 2, Lobby = 4, SpriteRaceHUD = 5, DemoTitle = 6, DemoLobby = 7, DemoRace = 8, Ranch = 9, RanchMonsters = 10, RanchTraining = 11, RanchArena = 12 }
    public enum E_CanvasLayer { World = 0, Game = 100, Panel = 200, Cutscene = 300, Popup = 400, System = 500 }
    public abstract class UIPanel_Base : MonoBehaviour
    {
        public abstract void OnOpen();
        public abstract void OnClose();
        private void OnEnable() => OnOpen();
        private void OnDisable() => OnClose();
    }
}
