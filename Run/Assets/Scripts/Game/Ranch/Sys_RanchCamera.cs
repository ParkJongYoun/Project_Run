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
        private Vector2 previousTouchCenter;
        private float previousTouchDistance;
        private bool wasTwoTouches;
        private bool panning;

        public void ResetView() { transform.position = HomePosition; WorldCamera.orthographicSize = HomeSize; }

        private void LateUpdate()
        {
            if (Placement != null && Placement.InputBlocked) { wasTwoTouches = false; panning = false; return; }
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
            if ((mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame) && !Sys_RanchPlacement.IsOverUI(mouse.position.ReadValue())) panning = true;
            if (!mouse.rightButton.isPressed && !mouse.middleButton.isPressed) panning = false;
            if (panning) Pan(mouse.delta.ReadValue());
            if (!Sys_RanchPlacement.IsOverUI(mouse.position.ReadValue())) Zoom(-mouse.scroll.ReadValue().y * .008f);
        }

        private void Zoom(float delta) => WorldCamera.orthographicSize = Mathf.Clamp(WorldCamera.orthographicSize + delta, 9, 21);
        private void Pan(Vector2 delta)
        {
            var right = transform.right; right.y = 0; right.Normalize();
            var forward = Vector3.ProjectOnPlane(transform.up, Vector3.up).normalized;
            transform.position -= (right * delta.x + forward * delta.y) * (WorldCamera.orthographicSize * 2 / Mathf.Max(1, Screen.height));
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, HomePosition.x - 12, HomePosition.x + 12);
            p.z = Mathf.Clamp(p.z, HomePosition.z - 10, HomePosition.z + 10);
            transform.position = p;
        }
    }
}
