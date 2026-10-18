using UnityEngine;

namespace ProjectRun.Race
{
    public sealed partial class UIPanel_DemoRace
    {
        private Sys_DemoSkillVideo skillVideo;
        private RectTransform videoRoot,videoSafe;
        private CanvasGroup videoOpacity;
        private UnityEngine.UI.RawImage videoImage;
        private UnityEngine.UI.Text videoPauseLabel;
        public bool IsVideoPlaying=>skillVideo!=null&&skillVideo.IsActive;

        private void BuildVideo()
        {
            videoRoot=DemoUI.Fill(transform,"group_skill_video");
            videoOpacity=videoRoot.gameObject.AddComponent<CanvasGroup>();
            DemoUI.Shade(videoRoot,"bg_video",Color.black).raycastTarget=true;
            var imageRoot=DemoUI.Fill(videoRoot,"img_video");
            videoImage=imageRoot.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            videoImage.raycastTarget=false;
            var fit=imageRoot.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio=16f/9f;
            videoSafe=DemoUI.Fill(videoRoot,"obj_safe_area");
            var title=DemoUI.Text(videoSafe,"txt_skill",Monster.Skill,0,-45,900,65,36,DemoUI.Paper,TextAnchor.MiddleCenter,true);
            title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(.5f,1);
            title.gameObject.AddComponent<UnityEngine.UI.Outline>().effectDistance=new Vector2(2,-2);
            var skip=DemoUI.Button(videoSafe,"btn_skip_video","연출 건너뛰기",-145,43,245,46,()=>skillVideo?.Skip(),false);
            var skipRect=(RectTransform)skip.transform;skipRect.anchorMin=skipRect.anchorMax=new Vector2(1,0);
            var pause=DemoUI.Button(videoSafe,"btn_pause_video","정지",75,43,110,46,()=>race.Paused=!race.Paused,false);
            var pauseRect=(RectTransform)pause.transform;pauseRect.anchorMin=pauseRect.anchorMax=new Vector2(0,0);
            videoPauseLabel=pause.GetComponentInChildren<UnityEngine.UI.Text>();
            SetVideoOpacity(0);
        }
        public void BindVideo(Sys_DemoSkillVideo controller,Texture texture)
        {
            skillVideo=controller;videoImage.texture=texture;
        }
        public void SetVideoOpacity(float alpha)
        {
            if(videoRoot==null)return;
            bool visible=alpha>0;
            videoRoot.gameObject.SetActive(visible);
            videoOpacity.alpha=Mathf.Clamp01(alpha);
            videoOpacity.interactable=videoOpacity.blocksRaycasts=visible;
            if(visible)videoRoot.SetAsLastSibling();
        }
        private void UpdateVideoControls()
        {
            DemoUI.SafeArea(videoSafe);
            if(videoPauseLabel!=null)videoPauseLabel.text=race.Paused?"재개":"정지";
        }
        private void CancelSkillPresentation()
        {
            CancelComic();if(skillVideo!=null)skillVideo.Cancel();
        }
    }
}
