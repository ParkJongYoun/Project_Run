using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectRun.Race
{
    [DefaultExecutionOrder(400)]
    public sealed class Sys_FoxImpostorTest : MonoBehaviour
    {
        public FoxDirectionalImpostor Impostor;
        public GameObject ReferenceModel;
        public Camera OrbitCamera;
        public float Yaw = 65;
        [Range(-89.9f,89.9f)] public float Elevation = 15;
        [Range(2.5f,7)] public float DistanceRatio = 4.1f;
        public bool AutoOrbit;
        public bool ShowOriginal;
        [Header("Captured view count comparison")]
        public Data_FoxDirectionalImpostor[] Presets;
        [SerializeField] private int PresetIndex;
        public int SelectedPresetIndex => PresetIndex;
        private bool dragging;
        private Vector2 lastPointer;
        private float orbitTime;

        private void Update()
        {
            if (AutoOrbit)
            {
                orbitTime += Time.unscaledDeltaTime;
                Yaw = Mathf.Repeat(Yaw + Time.unscaledDeltaTime*20,360);
                Elevation = Mathf.Sin(orbitTime*0.3f)*75;
            }
            var pointer=Pointer.current;
            if(pointer!=null)
            {
                Vector2 point=pointer.position.ReadValue();
                if(pointer.press.wasPressedThisFrame)
                {
                    dragging=EventSystem.current==null || !EventSystem.current.IsPointerOverGameObject();
                    // Touch UI uses pointer IDs; viewport containment also prevents slider drags from orbiting.
                    dragging &= OrbitCamera.pixelRect.Contains(point);
                    lastPointer=point;
                }
                if(dragging && pointer.press.isPressed)
                {
                    Vector2 delta=point-lastPointer;
                    if(delta.sqrMagnitude>0) { AutoOrbit=false; Yaw=Mathf.Repeat(Yaw-delta.x*0.3f,360); Elevation=Mathf.Clamp(Elevation-delta.y*0.25f,-89.9f,89.9f); }
                }
                if(pointer.press.wasReleasedThisFrame) dragging=false;
                lastPointer=point;
            }
            if(Mouse.current!=null && OrbitCamera.pixelRect.Contains(Mouse.current.position.ReadValue()))
                DistanceRatio=Mathf.Clamp(DistanceRatio-Mouse.current.scroll.ReadValue().y*0.002f,2.5f,7);
            if(Keyboard.current!=null)
            {
                if(Keyboard.current.spaceKey.wasPressedThisFrame) ToggleOriginal();
                if(Keyboard.current.rKey.wasPressedThisFrame) ResetView();
            }
            ApplyView();
        }

        public void ApplyView()
        {
            if(Impostor==null || Impostor.Data==null || OrbitCamera==null) return;
            Data_FoxDirectionalImpostor.Basis(Yaw,Elevation,out var d,out var r,out var u);
            Vector3 center=Impostor.transform.TransformPoint(Impostor.Data.Center);
            float portraitFit=Mathf.Max(1,.9f/OrbitCamera.aspect);
            OrbitCamera.transform.SetPositionAndRotation(center+d*(Impostor.Data.Radius*DistanceRatio*portraitFit),Quaternion.LookRotation(-d,u));
            ReferenceModel.SetActive(ShowOriginal);
            Impostor.Display.enabled=!ShowOriginal;
            Impostor.Refresh();
        }

        public void ToggleOriginal() { ShowOriginal=!ShowOriginal; ApplyView(); }
        public void ToggleAuto() { AutoOrbit=!AutoOrbit; orbitTime=Mathf.Asin(Mathf.Clamp(Elevation/75,-1,1))/0.3f; }
        public void ResetView() { AutoOrbit=false; Yaw=65; Elevation=15; DistanceRatio=4.1f; ApplyView(); }
        public void SetYaw(float value) { AutoOrbit=false; Yaw=value; ApplyView(); }
        public void SetElevation(float value) { AutoOrbit=false; Elevation=value; ApplyView(); }
        public void SetZoom(float value) { DistanceRatio=value; ApplyView(); }

        public void SelectPreset(int index)
        {
            if(Presets==null || index<0 || index>=Presets.Length || Presets[index]==null) return;
            PresetIndex=index;
            Impostor.Data=Presets[index];
            ShowOriginal=false;
            // Preserve the camera and orbit state so differences come only from the captured view count.
            ApplyView();
        }
    }
}
