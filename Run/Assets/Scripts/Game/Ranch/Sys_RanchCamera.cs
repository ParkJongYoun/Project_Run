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
        [Tooltip("상하 제한은 Y(최소 Z)·W(최대 Z)를 사용하며, 좌우는 시작 중심 ±12 범위입니다.")]
        public Vector4 GroundBounds = new Vector4(-14.7f, -12, 14.7f, 14.3f);
        [Range(0, .3f)] public float GroundViewportBottom = .12f;
        [Tooltip("시작 각도를 기준으로 한 좌우 회전 한계. 상하 각도는 고정합니다.")]
        [Range(0, 60)] public float MaxYawOffset = 30;
        [Min(.01f)] public float YawDegreesPerPixel = .18f;
        [Tooltip("실제 하단 건설 목록과 Safe Area에 맞춰 UI가 갱신하는 농장 표시 영역입니다.")]
        public Rect BuildViewport = new Rect(.01f, .267f, .98f, .719f);
        [Min(0)] public float BuildPadding = .45f;
        private bool buildView, savedFacilityFocus;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedSize;
        private Vector3 targetPosition, followVelocity, inertiaVelocity;
        private Vector2 pressPoint, previousPoint, previousTouchCenter;
        private float previousTouchDistance, pinchDistance;
        private bool pointerHeld, touchGesture, rejected, dragging, twoTouches, suppressUntilRelease;
        private int zoomLevel, touchId;
        private Vector2 rightPressPoint;
        private float rightStartYaw, previousOrbitX;
        private bool rightHeld, rightRejected, rightDragged;
        private bool facilityFocus;

        private void Awake()
        {
            if (WorldCamera == null) WorldCamera = GetComponent<Camera>();
            ResetView();
        }

        public void ResetView()
        {
            if (buildView) { ApplyBuildView(); return; }
            if (WorldCamera == null) WorldCamera = GetComponent<Camera>();
            ClearGesture(); zoomLevel = 0;
            facilityFocus = false;
            transform.SetPositionAndRotation(HomePosition, Quaternion.Euler(HomeAngles));
            WorldCamera.orthographicSize = HomeSize;
            targetPosition = LimitPosition(HomePosition, false);
            transform.position = targetPosition;
        }

        public void ZoomIn() => StepZoom(-1);
        public void ZoomOut() => StepZoom(1);

        public void StopMotion()
        {
            ClearGesture(); facilityFocus = false;
            targetPosition = transform.position;
        }

        public void FocusOn(Vector3 groundPosition)
        {
            if (buildView) return;
            var ray = WorldCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            if (!new Plane(Vector3.up, groundPosition).Raycast(ray, out float distance)) return;
            StopMotion(); facilityFocus = true;
            var offset = groundPosition - ray.GetPoint(distance); offset.y = 0;
            targetPosition = LimitPosition(transform.position + offset, false);
        }

        public void SetBuildView(bool enabled)
        {
            if (buildView == enabled) return;
            if (enabled)
            {
                savedPosition = transform.position; savedRotation = transform.rotation;
                savedSize = WorldCamera.orthographicSize; savedFacilityFocus = facilityFocus;
                StopMotion(); buildView = true;
                ApplyBuildView();
            }
            else
            {
                buildView = false; StopMotion(); facilityFocus = savedFacilityFocus;
                transform.SetPositionAndRotation(savedPosition, savedRotation);
                WorldCamera.orthographicSize = savedSize;
                targetPosition = savedPosition;
            }
        }

        public void SetBuildViewport(Rect viewport)
        {
            if (BuildViewport == viewport) return;
            BuildViewport = viewport;
            if (buildView) ApplyBuildView();
        }

        private void ApplyBuildView()
        {
            if (Placement == null || WorldCamera == null) return;
            float width = Placement.GridSize.x * Placement.CellSize + BuildPadding * 2;
            float depth = Placement.GridSize.y * Placement.CellSize + BuildPadding * 2;
            float size = Mathf.Max(depth / (2 * Mathf.Max(.1f, BuildViewport.height)),
                width / (2 * Mathf.Max(.1f, WorldCamera.aspect * BuildViewport.width)));
            // The overhead view deliberately exceeds the normal five-step zoom range.
            size = Mathf.Max(size, HomeSize + WheelZoomStep * 2 + 1);
            var center = Placement.GridOrigin + new Vector3(Placement.GridSize.x, 0, Placement.GridSize.y) * (Placement.CellSize * .5f);
            center.x -= (BuildViewport.center.x - .5f) * size * 2 * WorldCamera.aspect;
            center.z -= (BuildViewport.center.y - .5f) * size * 2;
            center.y = Placement.GridOrigin.y + Mathf.Max(40, HomePosition.y);
            transform.SetPositionAndRotation(center, Quaternion.Euler(90, 0, 0));
            WorldCamera.orthographicSize = size;
            targetPosition = center;
        }

        private void LateUpdate()
        {
            if (Placement != null) SetBuildView(Placement.IsBuilding);
            if (buildView) { ApplyBuildView(); return; }
            if (Placement != null && Placement.InputBlocked)
            { ClearGesture(); targetPosition = transform.position; return; }
            float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            UnityEngine.InputSystem.Controls.TouchControl first = null, second = null;
            if (Touchscreen.current != null)
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) { if (first == null) first = touch; else { second = touch; break; } }
            if (second != null)
            {
                var a = first.position.ReadValue(); var b = second.position.ReadValue();
                var center = (a + b) * .5f; float distance = Vector2.Distance(a, b);
                if (!twoTouches)
                {
                    bool startedOnUI = pointerHeld && rejected;
                    pointerHeld = dragging = false; inertiaVelocity = Vector3.zero;
                    targetPosition = transform.position;
                    rejected = startedOnUI || Sys_RanchPlacement.IsOverUI(a) || Sys_RanchPlacement.IsOverUI(b);
                    twoTouches = suppressUntilRelease = true; pinchDistance = 0;
                }
                else if (!rejected)
                {
                    Pan(previousTouchCenter, center, dt);
                    pinchDistance += previousTouchDistance - distance;
                    if (Mathf.Abs(pinchDistance) >= 32)
                    { StepZoom(pinchDistance > 0 ? 1 : -1); pinchDistance = 0; }
                }
                previousTouchCenter = center; previousTouchDistance = distance;
            }
            else if (suppressUntilRelease)
            {
                // A pinch must end completely before the remaining finger can become a tap.
                twoTouches = false;
                if (first == null) { suppressUntilRelease = rejected = false; inertiaVelocity = Vector3.zero; }
            }
            else if (first != null)
            {
                var point = first.position.ReadValue();
                if (!pointerHeld) BeginPointer(point, true, first.touchId.ReadValue());
                if (!touchGesture || touchId != first.touchId.ReadValue())
                { pointerHeld = dragging = false; suppressUntilRelease = true; inertiaVelocity = Vector3.zero; }
                else MovePointer(point, dt);
            }
            else
            {
                if (pointerHeld && touchGesture) EndTouch(dt);
                var mouse = Mouse.current;
                if (mouse != null && !HandleRightMouse(mouse))
                {
                    var point = mouse.position.ReadValue();
                    if (mouse.leftButton.wasPressedThisFrame) BeginPointer(point, false, 0);
                    if (pointerHeld && !touchGesture)
                    {
                        MovePointer(point, dt);
                        if (!mouse.leftButton.isPressed) EndPointer(point);
                    }
                    float wheel = mouse.scroll.ReadValue().y;
                    if (!Sys_RanchPlacement.IsOverUI(point) && Mathf.Abs(wheel) > .01f)
                        StepZoom(wheel > 0 ? -1 : 1);
                }
            }
            bool manipulating = (pointerHeld && dragging && !rejected) || (twoTouches && !rejected);
            if (!pointerHeld && !twoTouches)
            {
                targetPosition += inertiaVelocity * dt;
                inertiaVelocity *= Mathf.Exp(-9 * dt);
                var limited = LimitPosition(targetPosition, false);
                if (Mathf.Abs(limited.x - targetPosition.x) > .001f) inertiaVelocity.x = 0;
                if (Mathf.Abs(limited.z - targetPosition.z) > .001f) inertiaVelocity.z = 0;
                targetPosition = limited;
            }
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity,
                manipulating ? .045f : .16f, Mathf.Infinity, dt);
        }

        private void BeginPointer(Vector2 point, bool isTouch, int id)
        {
            pointerHeld = true; touchGesture = isTouch; touchId = id;
            rejected = Sys_RanchPlacement.IsOverUI(point); dragging = false;
            pressPoint = previousPoint = point;
            inertiaVelocity = followVelocity = Vector3.zero; targetPosition = transform.position;
        }

        private bool HandleRightMouse(Mouse mouse)
        {
            var point = mouse.position.ReadValue();
            if (mouse.rightButton.wasPressedThisFrame)
            {
                rightHeld = true; rightDragged = false;
                rightRejected = Sys_RanchPlacement.IsOverUI(point);
                rightPressPoint = point; previousOrbitX = point.x;
                rightStartYaw = Mathf.DeltaAngle(HomeAngles.y, transform.eulerAngles.y);
                if (!rightRejected)
                {
                    pointerHeld = dragging = false;
                    inertiaVelocity = followVelocity = Vector3.zero; targetPosition = transform.position;
                }
            }
            if (!rightHeld) return false;
            float threshold = Mathf.Max(10, Screen.height * (12f / 720));
            if ((point - rightPressPoint).sqrMagnitude >= threshold * threshold) rightDragged = true;
            if (rightDragged && !rightRejected && !Mathf.Approximately(point.x, previousOrbitX))
            {
                float yaw = rightStartYaw + (point.x - rightPressPoint.x) * YawDegreesPerPixel * (720f / Mathf.Max(1, Screen.height));
                ApplyYaw(Mathf.Clamp(yaw, -MaxYawOffset, MaxYawOffset));
            }
            previousOrbitX = point.x;
            if (!mouse.rightButton.isPressed)
            {
                if (!rightDragged && !rightRejected && !Sys_RanchPlacement.IsOverUI(point)) ApplyYaw(0);
                rightHeld = false;
            }
            return true;
        }

        private void ApplyYaw(float offset)
        {
            if (buildView) return;
            facilityFocus = false;
            var ray = WorldCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            var pivot = ray.GetPoint(distance);
            float orbitDistance = Vector3.Distance(transform.position, pivot);
            var rotation = Quaternion.Euler(HomeAngles.x, HomeAngles.y + offset, HomeAngles.z);
            // Orbit the current view center. Resetting the angle keeps the current zoom and location.
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * orbitDistance, rotation);
            targetPosition = LimitPosition(transform.position, false);
            transform.position = targetPosition;
            inertiaVelocity = followVelocity = Vector3.zero;
        }

        private void MovePointer(Vector2 point, float dt)
        {
            float threshold = Mathf.Max(10, Screen.height * (12f / 720));
            if (!rejected && !dragging && (point - pressPoint).sqrMagnitude >= threshold * threshold)
            { dragging = true; previousPoint = pressPoint; }
            if (!rejected && dragging) Pan(previousPoint, point, dt);
            previousPoint = point;
        }

        private void EndPointer(Vector2 point)
        {
            bool tap = !dragging && !rejected && !Sys_RanchPlacement.IsOverUI(point);
            pointerHeld = dragging = false;
            if (tap && Placement != null) Placement.HandleWorldTap(point);
        }

        private void EndTouch(float dt)
        {
            if (Touchscreen.current != null)
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.touchId.ReadValue() == touchId && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended)
                    {
                        // Include movement on the release frame before deciding whether this was a tap.
                        var point = touch.position.ReadValue(); MovePointer(point, dt); EndPointer(point); return;
                    }
            pointerHeld = dragging = false; inertiaVelocity = Vector3.zero;
        }

        private void Pan(Vector2 from, Vector2 to, float dt)
        {
            if (buildView) return;
            if ((to - from).sqrMagnitude < .0001f)
            {
                inertiaVelocity = Vector3.Lerp(inertiaVelocity, Vector3.zero, .65f);
                return;
            }
            facilityFocus = false;
            var plane = new Plane(Vector3.up, Vector3.zero);
            var oldRay = WorldCamera.ScreenPointToRay(from); var newRay = WorldCamera.ScreenPointToRay(to);
            if (!plane.Raycast(oldRay, out float oldDistance) || !plane.Raycast(newRay, out float newDistance)) return;
            var delta = oldRay.GetPoint(oldDistance) - newRay.GetPoint(newDistance); delta.y = 0;
            targetPosition = LimitPosition(targetPosition + delta, true);
            if (dt > .0001f) inertiaVelocity = Vector3.Lerp(inertiaVelocity, Vector3.ClampMagnitude(delta / dt, 18), .65f);
        }

        private void StepZoom(int direction)
        {
            if (buildView) return;
            facilityFocus = false;
            zoomLevel = Mathf.Clamp(zoomLevel + direction, -2, 2);
            WorldCamera.orthographicSize = Mathf.Max(.1f, HomeSize + zoomLevel * WheelZoomStep);
            inertiaVelocity = Vector3.zero;
            targetPosition = LimitPosition(targetPosition, false);
        }

        private Vector3 LimitPosition(Vector3 position, bool elastic)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            var offset = position - transform.position;
            for (int i = 0; i < 4; i++)
            {
                var ray = WorldCamera.ViewportPointToRay(new Vector3(i % 2, i < 2 ? GroundViewportBottom : 1, 0));
                if (!plane.Raycast(ray, out float distance)) return position;
                float z = ray.GetPoint(distance).z + offset.z;
                minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            }
            var centerRay = WorldCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            if (!plane.Raycast(centerRay, out float centerDistance)) return position;
            var focus = centerRay.GetPoint(centerDistance) + offset;
            if (Placement != null && facilityFocus)
            {
                // Normal lobby focus can reach facilities near the edge of the ranch.
                var origin = Placement.GridOrigin;
                float margin = Placement.CellSize * .5f;
                var correction = new Vector3(
                    Mathf.Clamp(focus.x, origin.x + margin, origin.x + Placement.GridSize.x * Placement.CellSize - margin) - focus.x,
                    0,
                    Mathf.Clamp(focus.z, origin.z + margin, origin.z + Placement.GridSize.y * Placement.CellSize - margin) - focus.z);
                var placementLimit = position + correction;
                if (elastic) placementLimit += new Vector3(Mathf.Clamp(-correction.x * .25f, -.45f, .45f), 0,
                    Mathf.Clamp(-correction.z * .25f, -.45f, .45f));
                return placementLimit;
            }
            var homeDirection = Quaternion.Euler(HomeAngles) * Vector3.forward;
            var homeFocus = HomePosition - homeDirection * (HomePosition.y / homeDirection.y);
            float x = Mathf.Clamp(focus.x, homeFocus.x - 12, homeFocus.x + 12) - focus.x;
            float zCorrection = maxZ - minZ > GroundBounds.w - GroundBounds.y
                ? (GroundBounds.y + GroundBounds.w - minZ - maxZ) * .5f
                : Mathf.Max(0, GroundBounds.y - minZ) + Mathf.Min(0, GroundBounds.w - maxZ);
            var limited = position + new Vector3(x, 0, zCorrection);
            if (elastic) limited += new Vector3(Mathf.Clamp(-x * .25f, -.45f, .45f), 0,
                Mathf.Clamp(-zCorrection * .25f, -.45f, .45f));
            return limited;
        }

        private void ClearGesture()
        {
            pointerHeld = dragging = twoTouches = suppressUntilRelease = rejected = false;
            rightHeld = rightRejected = rightDragged = false;
            pinchDistance = 0; inertiaVelocity = followVelocity = Vector3.zero;
        }
        private void OnDisable() => ClearGesture();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { ClearGesture(); targetPosition = transform.position; }
        }
    }
}
