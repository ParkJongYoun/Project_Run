using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoRace
    {
        [Header("스킬 영상 · 배치는 프리팹에서 편집")]
        [SerializeField] private RectTransform VideoRoot;
        [SerializeField] private RectTransform VideoSafeArea;
        [SerializeField] private CanvasGroup VideoOpacity;
        [SerializeField] private RawImage VideoImage;
        [SerializeField] private Text VideoTitle;
        [SerializeField] private Button VideoSkipButton;
        [SerializeField] private Button VideoPauseButton;
        [SerializeField] private Text VideoPauseLabel;
        private Sys_DemoSkillVideo skillVideo;
        public bool IsVideoPlaying => skillVideo != null && skillVideo.IsActive;

        public void BindVideo(Sys_DemoSkillVideo controller, Texture texture)
        {
            skillVideo = controller;
            VideoImage.texture = texture;
        }

        public void SetVideoOpacity(float alpha)
        {
            if (VideoRoot == null) return;
            bool visible = alpha > 0;
            VideoRoot.gameObject.SetActive(visible);
            VideoOpacity.alpha = Mathf.Clamp01(alpha);
            VideoOpacity.interactable = VideoOpacity.blocksRaycasts = visible;
            if (visible) VideoRoot.SetAsLastSibling();
        }

        private void UpdateVideoControls()
        {
            DemoUI.SafeArea(VideoSafeArea);
            VideoPauseLabel.text = race.Paused ? "재개" : "정지";
        }

        private void SkipVideo() => skillVideo?.Skip();
        private void CancelSkillPresentation()
        {
            CancelComic();
            if (skillVideo != null) skillVideo.Cancel();
        }
    }
}
