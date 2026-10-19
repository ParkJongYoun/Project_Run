using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRun.Ranch
{
    public sealed class Sys_RanchCamera : MonoBehaviour
    {
        public Camera WorldCamera;
        public Sys_RanchPlacement Placement;
        public Vector3 HomePosition;
        public float HomeSize = 17;
        public Vector3 HomeAngles = new Vector3(38, 14, 0);
        [Min(.1f)] public float WheelZoomStep = 2.4f;
        [Min(.01f)] public float OrbitSensitivity = .22f;
        public Vector2 ZoomLimits = new Vector2(4.5f, 25);
        public Vector2 PitchLimits = new Vector2(22, 70);
        private Vector2 previousTouchCenter;
        private float previousTouchDistance;
        private bool wasTwoTouches;
        private bool panning;
        private bool orbiting;
        private Vector3 orbitPivot;
        private float orbitDistance;

        public void ResetView()
        {
            panning = orbiting = false;
            transform.SetPositionAndRotation(HomePosition, Quaternion.Euler(HomeAngles));
            WorldCamera.orthographicSize = HomeSize;
        }

        private void LateUpdate()
        {
            if (Placement != null && Placement.InputBlocked) { wasTwoTouches = false; panning = orbiting = false; return; }
            if (Touchscreen.current != null)
            {
                UnityEngine.InputSystem.Controls.TouchControl first = null, second = null;
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) { if (first == null) first = touch; else { second = touch; break; } }
                if (second != null)
                {
                    var a = first.position.ReadValue(); var b = second.position.ReadValue();
                    if (Sys_RanchPlacement.IsOverUI(a) || Sys_RanchPlacement.IsOverUI(b)) { wasTwoTouches = false; return; }
                    var center = (a + b) * .5f; float distance = Vector2.Distance(a, b);
                    if (wasTwoTouches) { Pan(center - previousTouchCenter); Zoom((previousTouchDistance - distance) * .025f); }
                    previousTouchCenter = center; previousTouchDistance = distance; wasTwoTouches = true; return;
                }
            }
            wasTwoTouches = false;
            if (Mouse.current == null) return;
            var mouse = Mouse.current;
            bool overUI = Sys_RanchPlacement.IsOverUI(mouse.position.ReadValue());
            if (mouse.middleButton.wasPressedThisFrame && !overUI)
            {
                orbiting = true; panning = false;
                var ray = WorldCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
                orbitPivot = new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)
                    ? ray.GetPoint(distance) : transform.position + transform.forward * 35;
                orbitDistance = Vector3.Distance(transform.position, orbitPivot);
            }
            if (!mouse.middleButton.isPressed) orbiting = false;
            if (mouse.rightButton.wasPressedThisFrame && !overUI && !orbiting) panning = true;
            if (!mouse.rightButton.isPressed) panning = false;
            if (orbiting)
            {
                var delta = mouse.delta.ReadValue();
                float pitch = Mathf.Clamp(Mathf.DeltaAngle(0, transform.eulerAngles.x) - delta.y * OrbitSensitivity, PitchLimits.x, PitchLimits.y);
                transform.rotation = Quaternion.Euler(pitch, transform.eulerAngles.y + delta.x * OrbitSensitivity, 0);
                transform.position = orbitPivot - transform.forward * orbitDistance;
            }
            else if (panning) Pan(mouse.delta.ReadValue());
            if (!overUI)
            {
                float wheel = mouse.scroll.ReadValue().y;
                // Input backends report either wheel steps or Windows' 120-unit deltas.
                float steps = Mathf.Abs(wheel) >= 20 ? wheel / 120 : wheel;
                Zoom(-steps * WheelZoomStep);
            }
        }

        private void Zoom(float delta) => WorldCamera.orthographicSize = Mathf.Clamp(WorldCamera.orthographicSize + delta, ZoomLimits.x, ZoomLimits.y);
        private void Pan(Vector2 delta)
        {
            var right = transform.right; right.y = 0; right.Normalize();
            var forward = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            transform.position -= (right * delta.x + forward * delta.y) * (WorldCamera.orthographicSize * 2 / Mathf.Max(1, Screen.height));
            // Clamp the ground focus, not camera XZ: orbiting changes camera XZ independently.
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

        private void OnDisable() { panning = orbiting = wasTwoTouches = false; }
    }
}
