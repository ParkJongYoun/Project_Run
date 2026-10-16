using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed class UIPanel_PlayMode : UIPanel_Base
    {
        public RectTransform SafeArea;
        public Button[] Buttons;
        public string[] Scenes;
        public Text StatusLabel;
        private UnityAction[] actions;
        private bool requested;

        private void Start()
        {
            var manager = FindFirstObjectByType<Mgr_UI>();
            if (manager != null) { manager.Register(E_PanelType.PlayMode, this); manager.Open(E_PanelType.PlayMode); }
        }
        public override void OnOpen()
        {
            if (Buttons == null || Scenes == null) return;
            requested = false;
            actions = new UnityAction[Buttons.Length];
            for (int i = 0; i < Buttons.Length; i++)
            {
                int index = i;
                actions[i] = () => Select(index);
                Buttons[i].onClick.AddListener(actions[i]);
            }
        }
        private void Select(int index)
        {
            if (requested || Mgr_Scene.IsLoading) return;
            requested = Mgr_Scene.Load(Scenes[index]);
            if (StatusLabel != null) StatusLabel.text = requested ? "불러오는 중..." : "씬을 불러올 수 없습니다. 빌드 목록을 확인하세요.";
        }
        private void Update()
        {
            if (SafeArea != null && Screen.width > 0 && Screen.height > 0)
            {
                var safe = Screen.safeArea;
                SafeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                SafeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
            }
            foreach (var button in Buttons) button.interactable = !requested && !Mgr_Scene.IsLoading;
        }
        public override void OnClose()
        {
            if (actions == null) return;
            for (int i = 0; i < actions.Length; i++) Buttons[i].onClick.RemoveListener(actions[i]);
            actions = null;
        }
    }
}
