using System;
using UnityEngine;

namespace ProjectRun.Race
{
    public sealed class UIPanel_RaceSkillVideo : UIPanel_Base
    {
        public CanvasGroup Opacity;
        public UnityEngine.UI.RawImage VideoImage;
        public UnityEngine.UI.Button SkipButton;
        public UnityEngine.UI.Button MenuButton;
        public RectTransform SafeArea;
        public event Action SkipRequested;
        public event Action MenuRequested;
        private Rect lastSafe;
        private Vector2Int lastScreen;

        private void Start()
        {
            var manager = FindFirstObjectByType<Mgr_UI>();
            if (manager != null) manager.Register(E_PanelType.RaceSkillVideo, this);
        }
        public override void OnOpen()
        {
            if (SkipButton != null) SkipButton.onClick.AddListener(RequestSkip);
            if (MenuButton != null) MenuButton.onClick.AddListener(RequestMenu);
        }
        public override void OnClose()
        {
            if (SkipButton != null) SkipButton.onClick.RemoveListener(RequestSkip);
            if (MenuButton != null) MenuButton.onClick.RemoveListener(RequestMenu);
            SkipRequested?.Invoke();
        }
        private void RequestSkip() => SkipRequested?.Invoke();
        private void RequestMenu() => MenuRequested?.Invoke();
        public void SetOpacity(float value)
        {
            if (Opacity == null) return;
            Opacity.alpha = Mathf.Clamp01(value);
            Opacity.interactable = value > .05f;
            Opacity.blocksRaycasts = value > .05f;
        }
        private void Update()
        {
            if (SafeArea == null) return;
            var safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (safe == lastSafe && size == lastScreen) return;
            lastSafe = safe; lastScreen = size;
            SafeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            SafeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
        }
    }
}
