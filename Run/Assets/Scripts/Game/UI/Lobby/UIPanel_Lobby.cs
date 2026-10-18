using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed class UIPanel_Lobby : UIPanel_Base
    {
        [SerializeField] private RectTransform SafeArea;
        [SerializeField] private UIItem_LobbyAnimation Animation;
        [SerializeField] private UIItem_LobbyAnimation[] AmbientAnimations;
        [SerializeField] private Button PauseButton;
        [SerializeField] private Text PauseLabel;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        public void Configure(RectTransform safeArea, UIItem_LobbyAnimation animation, Button pauseButton, Text pauseLabel)
        {
            SafeArea = safeArea;
            Animation = animation;
            PauseButton = pauseButton;
            PauseLabel = pauseLabel;
        }

        public void ConfigureAmbient(UIItem_LobbyAnimation[] ambientAnimations) => AmbientAnimations = ambientAnimations;

        private void Start()
        {
            var manager = FindFirstObjectByType<Mgr_UI>();
            if (manager != null) manager.Register(E_PanelType.Lobby, this);
        }

        public override void OnOpen()
        {
            if (PauseButton != null) PauseButton.onClick.AddListener(ToggleAnimation);
            RefreshPauseLabel();
            ApplySafeArea();
        }

        public override void OnClose()
        {
            if (PauseButton != null) PauseButton.onClick.RemoveListener(ToggleAnimation);
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
                ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (SafeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            SafeArea.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
            SafeArea.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
            SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
        }

        private void ToggleAnimation()
        {
            if (Animation == null) return;
            bool playing = !Animation.IsPlaying;
            Animation.SetPlaying(playing);
            if (AmbientAnimations != null)
                foreach (var ambient in AmbientAnimations)
                    if (ambient != null) ambient.SetPlaying(playing);
            RefreshPauseLabel();
        }

        private void RefreshPauseLabel()
        {
            if (PauseLabel != null && Animation != null)
                PauseLabel.text = Animation.IsPlaying ? "움직임 멈추기" : "다시 재생";
        }
    }
}
