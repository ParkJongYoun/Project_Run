using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    [DefaultExecutionOrder(300)]
    public sealed class UIPanel_MonsterRenderCompare : UIPanel_Base
    {
        public Sys_MonsterRenderCompare Comparison;
        public RectTransform SafeArea;
        public RectTransform[] Markers;
        public Button PauseButton, SpeedButton, AngleButton, ResetButton, ReturnButton;
        public Text PauseLabel, SpeedLabel, AngleLabel;
        private Canvas canvas;

        private void Start()
        {
            canvas = GetComponentInParent<Canvas>();
            var manager = FindFirstObjectByType<Mgr_UI>();
            manager.Register(E_PanelType.MonsterRenderCompare, this);
            manager.Open(E_PanelType.MonsterRenderCompare);
        }

        public override void OnOpen()
        {
            if (Comparison == null) Comparison = FindFirstObjectByType<Sys_MonsterRenderCompare>();
            PauseButton.onClick.AddListener(TogglePause);
            SpeedButton.onClick.AddListener(ToggleSpeed);
            AngleButton.onClick.AddListener(ToggleAngle);
            ResetButton.onClick.AddListener(ResetRun);
            ReturnButton.onClick.AddListener(ReturnToTitle);
        }

        public override void OnClose()
        {
            PauseButton.onClick.RemoveListener(TogglePause);
            SpeedButton.onClick.RemoveListener(ToggleSpeed);
            AngleButton.onClick.RemoveListener(ToggleAngle);
            ResetButton.onClick.RemoveListener(ResetRun);
            ReturnButton.onClick.RemoveListener(ReturnToTitle);
        }

        private void TogglePause() => Comparison.TogglePause();
        private void ToggleSpeed() => Comparison.ToggleSpeed();
        private void ToggleAngle() => Comparison.ToggleAngle();
        private void ResetRun() => Comparison.ResetRun();
        private void ReturnToTitle() => Mgr_Scene.Load("Scene_Title");

        private void LateUpdate()
        {
            if (Comparison == null || canvas == null) return;
            var safe = Screen.safeArea;
            SafeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            SafeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
            PauseLabel.text = Comparison.Paused ? "재생" : "일시정지";
            SpeedLabel.text = Comparison.SlowMotion ? "속도 0.5×" : "속도 1×";
            AngleLabel.text = Comparison.Angled ? "정면 보기" : "사선 보기";
            for (int i = 0; i < Markers.Length; i++)
            {
                Vector3 screen = Comparison.ViewCamera.WorldToScreenPoint(Comparison.Views[i].transform.position + Vector3.up * 3.5f);
                Vector2 point;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)Markers[i].parent, screen, null, out point);
                Markers[i].anchoredPosition = point;
                Markers[i].gameObject.SetActive(screen.z > 0);
            }
        }
    }
}
