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

        private void UpdateVideoLayout()
        {
            DemoUI.SafeArea(VideoSafeArea);
        }

        private void CancelSkillPresentation()
        {
            if (skillVideo != null) skillVideo.Cancel();
        }
    }
}
