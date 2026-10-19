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
        [Tooltip("시작 배율을 중심으로 가까움·기본·멀리 3단계만 사용합니다.")]
        [Min(.1f)] public float WheelZoomStep = .4f;
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
            zoomLevel = Mathf.Clamp(zoomLevel + direction, -1, 1);
            WorldCamera.orthographicSize = Mathf.Max(.1f, HomeSize + zoomLevel * WheelZoomStep);
        }
        private void Pan(Vector2 delta)
        {
            var right = transform.right; right.y = 0; right.Normalize();
            var forward = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            transform.position -= (right * delta.x + forward * delta.y) * (WorldCamera.orthographicSize * 2 / Mathf.Max(1, Screen.height));
            // Keep the visible ground focus within the ranch's movement bounds.
            var ray = WorldCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
            {
                var focus = ray.GetPoint(distance);
                var homeDirection = Quaternion.Euler(HomeAngles) * Vector3.forward;
                var homeFocus = HomePosition - homeDirection * (HomePosition.y / homeDirection.y);
                var limited = new Vector3(Mathf.Clamp(focus.x, homeFocus.x-12, homeFocus.x+12),0,
                    Mathf.Clamp(focus.z, homeFocus.z-10, homeFocus.z+10));
                transform.position += limited-focus;
            }
        }

        private void OnDisable() { panning = wasTwoTouches = false; pinchDistance = 0; }
    }
}
