using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRun.Ranch
{
    public sealed class Sys_RanchCamera : MonoBehaviour
    {
        public Camera WorldCamera;
        public Sys_RanchPlacement Placement;
        public Vector3 HomePosition = new Vector3(1.08389819f, 23.3474884f, -28.5533619f);
        [Min(.1f)] public float HomeSize = 7;
        public Vector3 HomeAngles = new Vector3(41.84146f, 359.9193f, 0);
        [Tooltip("시작 배율을 중심으로 확대 2단계·기본·축소 2단계를 사용합니다.")]
        [Min(.1f)] public float WheelZoomStep = .4f;
        [Tooltip("지면 표시 경계. 상하 제한은 Y(최소 Z)·W(최대 Z)를 사용하며, 좌우는 기존 시작 중심 ±12 범위를 사용합니다.")]
        public Vector4 GroundBounds = new Vector4(-14.7f,-12,14.7f,12);
        [Range(0,.3f)] public float GroundViewportBottom = .12f;
        private Vector2 previousTouchCenter;
        private float previousTouchDistance;
        private bool wasTwoTouches;
        private bool panning;
        private int zoomLevel;
        private float pinchDistance;

        private void Awake()
        {
            if (WorldCamera == null) WorldCamera = GetComponent<Camera>();
            ResetView();
        }

        public void ResetView()
        {
            panning = wasTwoTouches = false;
            zoomLevel = 0; pinchDistance = 0;
            transform.SetPositionAndRotation(HomePosition, Quaternion.Euler(HomeAngles));
            WorldCamera.orthographicSize = HomeSize;
            ClampView();
        }

        private void LateUpdate()
        {
            if (Placement != null && Placement.InputBlocked) { wasTwoTouches = panning = false; pinchDistance = 0; return; }
            if (Touchscreen.current != null)
            {
                UnityEngine.InputSystem.Controls.TouchControl first = null, second = null;
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) { if (first == null) first = touch; else { second = touch; break; } }
                if (second != null)
                {
                    var a = first.position.ReadValue(); var b = second.position.ReadValue();
                    if (Sys_RanchPlacement.IsOverUI(a) || Sys_RanchPlacement.IsOverUI(b)) { wasTwoTouches = false; pinchDistance = 0; return; }
                    var center = (a + b) * .5f; float distance = Vector2.Distance(a, b);
                    if (wasTwoTouches)
                    {
                        Pan(center - previousTouchCenter);
                        pinchDistance += previousTouchDistance - distance;
                        if (Mathf.Abs(pinchDistance) >= 32) { StepZoom(pinchDistance > 0 ? 1 : -1); pinchDistance = 0; }
                    }
                    previousTouchCenter = center; previousTouchDistance = distance; wasTwoTouches = true; return;
                }
            }
            wasTwoTouches = false; pinchDistance = 0;
            if (Mouse.current == null) return;
            var mouse = Mouse.current;
            bool overUI = Sys_RanchPlacement.IsOverUI(mouse.position.ReadValue());
            if (mouse.rightButton.wasPressedThisFrame && !overUI) panning = true;
            if (!mouse.rightButton.isPressed) panning = false;
            if (panning) Pan(mouse.delta.ReadValue());
            if (!overUI)
            {
                float wheel = mouse.scroll.ReadValue().y;
                // One event advances one level, independent of backend wheel units.
                if (Mathf.Abs(wheel) > .01f) StepZoom(wheel > 0 ? -1 : 1);
            }
        }

        private void StepZoom(int direction)
        {
            zoomLevel = Mathf.Clamp(zoomLevel + direction, -2, 2);
            WorldCamera.orthographicSize = Mathf.Max(.1f, HomeSize + zoomLevel * WheelZoomStep);
            ClampView();
        }
        private void Pan(Vector2 delta)
        {
            var right = transform.right; right.y = 0; right.Normalize();
            var forward = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            transform.position -= (right * delta.x + forward * delta.y) * (WorldCamera.orthographicSize * 2 / Mathf.Max(1, Screen.height));
            ClampView();
        }

        private void ClampView()
        {
            // Keep the original horizontal focus range; clamp vertical visible ground at the fence.
            var plane=new Plane(Vector3.up,Vector3.zero);
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<4;i++)
            {
                var ray=WorldCamera.ViewportPointToRay(new Vector3(i%2,i<2?GroundViewportBottom:1,0));
                if(!plane.Raycast(ray,out float distance))return;
                var p=ray.GetPoint(distance);var point=new Vector2(p.x,p.z);
                min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
            var centerRay=WorldCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if(!plane.Raycast(centerRay,out float centerDistance))return;
            var focus=centerRay.GetPoint(centerDistance);
            var homeDirection=Quaternion.Euler(HomeAngles)*Vector3.forward;
            var homeFocus=HomePosition-homeDirection*(HomePosition.y/homeDirection.y);
            float x=Mathf.Clamp(focus.x,homeFocus.x-12,homeFocus.x+12)-focus.x;
            float z=Constrain(min.y,max.y,GroundBounds.y,GroundBounds.w);
            transform.position+=new Vector3(x,0,z);
        }

        private static float Constrain(float min,float max,float lower,float upper) =>
            max-min>upper-lower ? (lower+upper-min-max)*.5f : Mathf.Max(0,lower-min)+Mathf.Min(0,upper-max);

        private void OnDisable() { panning = wasTwoTouches = false; pinchDistance = 0; }
    }
}
