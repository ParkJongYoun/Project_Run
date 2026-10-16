using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace ProjectRun.Race
{
    public sealed class UIPanel_FoxImpostorTest : UIPanel_Base
    {
        public Sys_FoxImpostorTest Test;
        public RectTransform SafeArea;
        public Button CompareButton;
        public Button AutoButton;
        public Button ResetButton;
        public Slider YawSlider;
        public Slider ElevationSlider;
        public Slider ZoomSlider;
        public Text CompareLabel;
        public Text AutoLabel;
        public Text StatusLabel;
        public Text AngleLabel;
        public Button[] PresetButtons;
        public Text PresetLabel;
        private UnityAction[] presetActions;
        public override void OnOpen()
        {
            if(Test==null) return;
            CompareButton.onClick.AddListener(Test.ToggleOriginal);
            AutoButton.onClick.AddListener(Test.ToggleAuto);
            ResetButton.onClick.AddListener(Test.ResetView);
            YawSlider.onValueChanged.AddListener(Test.SetYaw);
            ElevationSlider.onValueChanged.AddListener(Test.SetElevation);
            ZoomSlider.onValueChanged.AddListener(Test.SetZoom);
            if(PresetButtons!=null)
            {
                presetActions=new UnityAction[PresetButtons.Length];
                for(int i=0;i<PresetButtons.Length;i++)
                {
                    int index=i;
                    presetActions[i]=()=>Test.SelectPreset(index);
                    PresetButtons[i].onClick.AddListener(presetActions[i]);
                }
            }
        }
        public override void OnClose()
        {
            if(Test==null) return;
            CompareButton.onClick.RemoveListener(Test.ToggleOriginal);
            AutoButton.onClick.RemoveListener(Test.ToggleAuto);
            ResetButton.onClick.RemoveListener(Test.ResetView);
            YawSlider.onValueChanged.RemoveListener(Test.SetYaw);
            ElevationSlider.onValueChanged.RemoveListener(Test.SetElevation);
            ZoomSlider.onValueChanged.RemoveListener(Test.SetZoom);
            if(presetActions!=null && PresetButtons!=null)
                for(int i=0;i<Mathf.Min(presetActions.Length,PresetButtons.Length);i++)
                    PresetButtons[i].onClick.RemoveListener(presetActions[i]);
            presetActions=null;
        }
        private void LateUpdate()
        {
            if(Test==null) return;
            Rect safe=Screen.safeArea;
            if(Screen.width>0 && Screen.height>0)
            {
                SafeArea.anchorMin=new Vector2(safe.xMin/Screen.width,safe.yMin/Screen.height);
                SafeArea.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);
                SafeArea.offsetMin=SafeArea.offsetMax=Vector2.zero;
            }
            CompareLabel.text=Test.ShowOriginal?"2D 임포스터 보기":"원본 3D 보기";
            AutoLabel.text=Test.AutoOrbit?"자동 회전 멈춤":"자동 회전";
            StatusLabel.text=Test.ShowOriginal?"원본 3D · 고정 자세":"2D 임포스터 · 고정 자세";
            bool outside=!Test.ShowOriginal && Test.Impostor.IsOutsideCaptureRange;
            if(outside) StatusLabel.text="촬영 범위 밖 · 아래쪽 이미지 없음";
            StatusLabel.color=outside?new Color(1,.69f,.32f):new Color(.88f,.92f,.95f);
            AngleLabel.text=$"좌우 {Test.Yaw:0}°   /   위아래 {Test.Elevation:+0;-0;0}°";
            YawSlider.SetValueWithoutNotify(Test.Yaw);
            ElevationSlider.SetValueWithoutNotify(Test.Elevation);
            ZoomSlider.SetValueWithoutNotify(Test.DistanceRatio);
            if(PresetLabel!=null && Test.Impostor.Data!=null)
            {
                var data=Test.Impostor.Data;
                PresetLabel.text=$"좌우 {data.AzimuthCount}방향 × 높이 {data.ElevationCount}단계 · {data.AzimuthCount*data.ElevationCount}장";
                if(data.MinimumElevation>=0) PresetLabel.text+=$" · {data.MinimumElevation:0}~{data.MaximumElevation:0}°";
            }
            if(PresetButtons!=null)
                for(int i=0;i<PresetButtons.Length;i++)
                {
                    bool available=Test.Presets!=null && i<Test.Presets.Length && Test.Presets[i]!=null;
                    PresetButtons[i].interactable=available;
                    PresetButtons[i].targetGraphic.color=available && i==Test.SelectedPresetIndex
                        ? new Color(.19f,.53f,.46f) : new Color(.13f,.23f,.28f);
                }
        }
    }
}
