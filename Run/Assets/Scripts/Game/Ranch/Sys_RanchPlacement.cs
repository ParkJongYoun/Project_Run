using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectRun.Ranch
{
    public sealed class Sys_RanchPlacement : MonoBehaviour
    {
        public Data_RanchCatalog Catalog;
        public Camera WorldCamera;
        public Transform FacilitiesRoot;
        public GameObject Grid;
        public Transform FootprintMarker;
        public Material ValidMaterial;
        public Material InvalidMaterial;
        public Vector2Int GridSize = new Vector2Int(20, 16);
        public float CellSize = 1.35f;
        public Vector3 GridOrigin = new Vector3(-13.5f, 0, -10.8f);
        public Data_RanchSave InitialLayout = new Data_RanchSave();
        public event Action Changed;
        public event Action<string> Message;
        public Data_RanchSave Layout { get; private set; }
        // A domain reload must not turn a null runtime preview into an empty serialized facility.
        [field: NonSerialized] public Data_RanchPlacement Preview { get; private set; }
        public bool IsBuilding { get; private set; }
        public bool InputBlocked { get; set; }
        public bool IsDraggingPreview => pointerHeld && !pointerRejected && Preview != null;
        public bool CanConfirm => Preview != null && RanchGrid.CanPlace(Catalog, Layout.Facilities, Preview, GridSize, out _);
        public string PreviewReason => Preview == null ? "시설을 선택하거나 배치한 시설을 눌러 이동하세요" :
            RanchGrid.CanPlace(Catalog, Layout.Facilities, Preview, GridSize, out var reason) ? "배치할 수 있어요" : reason;
        public bool IsMoving => Preview != null && Layout.Facilities.Exists(item => item.InstanceId == Preview.InstanceId);
        private GameObject previewObject;
        private Renderer[] previewRenderers;
        private GameObject hiddenOriginal;
        private readonly Dictionary<string, GameObject> instances = new Dictionary<string, GameObject>();
        private bool ready;
        private Vector3 previewCenter;
        private Sys_RanchCamera ranchCamera;
        private bool pointerHeld, pointerIsTouch, pointerRejected, suppressTouches;
        private int pointerTouchId;
        private Vector3 dragOffset;

        private void OnEnable()
        {
            if (WorldCamera != null) ranchCamera = WorldCamera.GetComponent<Sys_RanchCamera>();
            // Unity restores the layout and scene objects after reload, but not this dictionary.
            if (!ready || Layout == null || FacilitiesRoot == null) return;
            instances.Clear();
            foreach (var entry in Layout.Facilities)
            {
                var instance = FacilitiesRoot.Find("obj_" + entry.FacilityId + "_" + entry.InstanceId);
                if (instance != null) instances[entry.InstanceId] = instance.gameObject;
            }
        }

        public RanchFacilitySpot FindActivitySpot(string instanceId)
        {
            return instances.TryGetValue(instanceId, out var instance) && instance != null
                ? instance.GetComponent<RanchFacilitySpot>() : null;
        }

        private void Start()
        {
            Layout = JsonUtility.FromJson<Data_RanchSave>(JsonUtility.ToJson(InitialLayout));
            Rebuild(); ready = true; SetBuildMode(false);
        }

        public Vector3 Position(Data_RanchPlacement placement)
        {
            var size = Catalog.Find(placement.FacilityId).Size(placement.Rotation);
            return GridOrigin + new Vector3((placement.X + size.x * .5f) * CellSize, 0, (placement.Y + size.y * .5f) * CellSize);
        }

        private GameObject Create(Data_RanchPlacement placement)
        {
            var definition = Catalog.Find(placement.FacilityId);
            var instance = Instantiate(definition.Prefab, FacilitiesRoot);
            instance.name = "obj_" + placement.FacilityId + "_" + placement.InstanceId;
            instance.transform.SetPositionAndRotation(Position(placement), Quaternion.Euler(0, placement.Rotation * 90, 0));
            return instance;
        }

        private void Rebuild()
        {
            foreach (Transform child in FacilitiesRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            instances.Clear();
            foreach (var placement in Layout.Facilities) instances[placement.InstanceId] = Create(placement);
        }

        public void SetBuildMode(bool enabled)
        {
            Cancel(); IsBuilding = enabled; Grid.SetActive(enabled);
            if (ranchCamera != null) ranchCamera.SetBuildView(enabled);
            Changed?.Invoke();
        }

        public void BeginNew(string id)
        {
            if (!ready || Catalog.Find(id) == null) return;
            if (Layout.Facilities.Count >= 256) { Message?.Invoke("시설은 최대 256개까지 배치할 수 있어요"); return; }
            SetBuildMode(true);
            Preview = new Data_RanchPlacement { InstanceId = Guid.NewGuid().ToString("N"), FacilityId = id };
            SetPreviewCenter(GridOrigin + new Vector3(GridSize.x, 0, GridSize.y) * (CellSize * .5f));
            CreatePreview();
        }

        public void BeginMove(string instanceId)
        {
            if (!ready) return;
            var entry = Layout.Facilities.Find(item => item.InstanceId == instanceId);
            if (entry == null) return;
            SetBuildMode(true); Preview = entry.Copy();
            previewCenter = Position(Preview);
            if (instances.TryGetValue(instanceId, out hiddenOriginal)) hiddenOriginal.SetActive(false);
            CreatePreview();
        }

        private void CreatePreview()
        {
            previewObject = Create(Preview); previewObject.name = "obj_placement_preview";
            previewRenderers = previewObject.GetComponentsInChildren<Renderer>();
            if (Catalog.Find(Preview.FacilityId).IsRoad)
                foreach (var renderer in previewObject.GetComponentsInChildren<Renderer>()) renderer.enabled = true;
            foreach (var collider in previewObject.GetComponentsInChildren<Collider>()) collider.enabled = false;
            UpdatePreview();
        }

        public bool TryGetPreviewBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            if (previewObject == null) return false;
            if (previewRenderers == null) previewRenderers = previewObject.GetComponentsInChildren<Renderer>();
            bool found = false;
            foreach (var renderer in previewRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                // Flat ground dressing should not push the action buttons away from the model.
                if (renderer.name == "obj_ground_detail_apron" || renderer.name == "obj_village_apron" ||
                    renderer.name == "obj_entrance_path") continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            return found;
        }

        public void MovePreview(int x, int y)
        {
            if (Preview == null) return;
            if (Preview.X == x && Preview.Y == y) return;
            Preview.X = x; Preview.Y = y;
            previewCenter = Position(Preview);
            UpdatePreview();
        }

        public void Rotate()
        {
            if (Preview == null) return;
            Preview.Rotation = (Preview.Rotation + 1) % 4;
            SetPreviewCenter(previewCenter);
            KeepPreviewOnFarm();
            UpdatePreview();
        }

        public void DragPreviewToScreen(Vector2 point)
        {
            if (!ready || !IsBuilding || InputBlocked || Preview == null || IsOverUI(point)) return;
            if (GroundPoint(point, out var position)) MovePreviewCenter(position);
        }

        private void MovePreviewCenter(Vector3 center)
        {
            int x = Preview.X, y = Preview.Y;
            SetPreviewCenter(center);
            KeepPreviewOnFarm();
            if (Preview.X != x || Preview.Y != y) UpdatePreview();
        }

        private void KeepPreviewOnFarm()
        {
            // The entire farm is visible in build mode; edge drags stay reachable above the dock.
            var size = Catalog.Find(Preview.FacilityId).Size(Preview.Rotation);
            int x = Mathf.Clamp(Preview.X, 0, Mathf.Max(0, GridSize.x - size.x));
            int y = Mathf.Clamp(Preview.Y, 0, Mathf.Max(0, GridSize.y - size.y));
            if (x == Preview.X && y == Preview.Y) return;
            Preview.X = x; Preview.Y = y;
            previewCenter = Position(Preview);
        }

        private bool GroundPoint(Vector2 point, out Vector3 position)
        {
            position = Vector3.zero;
            if (WorldCamera == null) return false;
            var ray = WorldCamera.ScreenPointToRay(point);
            if (!new Plane(Vector3.up, GridOrigin).Raycast(ray, out float distance)) return false;
            position = ray.GetPoint(distance);
            return true;
        }

        private void SetPreviewCenter(Vector3 center)
        {
            previewCenter = center;
            var local = (center - GridOrigin) / CellSize;
            var size = Catalog.Find(Preview.FacilityId).Size(Preview.Rotation);
            Preview.X = Mathf.FloorToInt(local.x - size.x * .5f + .5f);
            Preview.Y = Mathf.FloorToInt(local.z - size.y * .5f + .5f);
        }

        private void UpdatePreview()
        {
            var size = Catalog.Find(Preview.FacilityId).Size(Preview.Rotation);
            previewObject.transform.SetPositionAndRotation(Position(Preview), Quaternion.Euler(0, Preview.Rotation * 90, 0));
            FootprintMarker.gameObject.SetActive(true);
            FootprintMarker.position = Position(Preview) + Vector3.up * .045f;
            FootprintMarker.localScale = new Vector3(size.x * CellSize - .07f, .035f, size.y * CellSize - .07f);
            FootprintMarker.GetComponent<Renderer>().sharedMaterial = CanConfirm ? ValidMaterial : InvalidMaterial;
            Changed?.Invoke();
        }

        public bool Confirm()
        {
            if (!CanConfirm) { Message?.Invoke(PreviewReason); return false; }
            var existing = Layout.Facilities.Find(item => item.InstanceId == Preview.InstanceId);
            if (existing != null) Layout.Facilities.Remove(existing);
            Layout.Facilities.Add(Preview.Copy());
            Cancel(); Rebuild(); Changed?.Invoke(); return true;
        }

        public void Cancel()
        {
            if (previewObject != null) { previewObject.SetActive(false); Destroy(previewObject); }
            if (hiddenOriginal != null) hiddenOriginal.SetActive(true);
            hiddenOriginal = null; previewObject = null; previewRenderers = null; Preview = null;
            ClearPointer();
            if (FootprintMarker != null) FootprintMarker.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        public void RemoveSelected()
        {
            if (!IsMoving) return;
            string id = Preview.InstanceId; Cancel();
            Layout.Facilities.RemoveAll(item => item.InstanceId == id); Rebuild(); Changed?.Invoke();
        }

        public void ResetLayout()
        {
            Cancel(); Layout = JsonUtility.FromJson<Data_RanchSave>(JsonUtility.ToJson(InitialLayout));
            Rebuild(); Changed?.Invoke();
        }

        public static bool IsOverUI(Vector2 point)
        {
            if (EventSystem.current == null) return false;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, results);
            return results.Count > 0;
        }

        private void Update()
        {
            if (!ready || !IsBuilding || InputBlocked) { ClearPointer(); return; }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) { Cancel(); return; }
                if (Keyboard.current.rKey.wasPressedThisFrame) Rotate();
                if (Keyboard.current.enterKey.wasPressedThisFrame) Confirm();
            }
            UpdateConstructionPointer();
        }

        private void UpdateConstructionPointer()
        {
            UnityEngine.InputSystem.Controls.TouchControl first = null;
            int touchCount = 0;
            if (Touchscreen.current != null)
                foreach (var touch in Touchscreen.current.touches)
                    if (touch.press.isPressed) { if (first == null) first = touch; touchCount++; }
            if (touchCount > 1) { ClearPointer(); suppressTouches = true; return; }
            if (suppressTouches)
            {
                if (touchCount == 0) suppressTouches = false;
                return;
            }
            if (first != null)
            {
                var point = first.position.ReadValue();
                if (first.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    BeginConstructionPointer(point, true, first.touchId.ReadValue());
                if (pointerHeld && pointerIsTouch && pointerTouchId == first.touchId.ReadValue()) MoveConstructionPointer(point);
                return;
            }
            if (pointerHeld && pointerIsTouch)
            {
                if (Touchscreen.current != null)
                    foreach (var touch in Touchscreen.current.touches)
                        if (touch.touchId.ReadValue() == pointerTouchId && touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended)
                            MoveConstructionPointer(touch.position.ReadValue());
                ClearPointer(); return;
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var mousePoint = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) BeginConstructionPointer(mousePoint, false, 0);
            if (!pointerHeld || pointerIsTouch) return;
            MoveConstructionPointer(mousePoint);
            if (!mouse.leftButton.isPressed) ClearPointer();
        }

        private void BeginConstructionPointer(Vector2 point, bool isTouch, int touchId)
        {
            bool rejected = IsOverUI(point);
            if (!rejected && Preview == null)
            {
                var entry = FindWorldFacility(point);
                if (entry != null) BeginMove(entry.InstanceId);
            }
            pointerHeld = true; pointerIsTouch = isTouch; pointerTouchId = touchId;
            pointerRejected = rejected || Preview == null; dragOffset = Vector3.zero;
            if (pointerRejected || !GroundPoint(point, out var position)) return;
            var center = Position(Preview);
            var size = Catalog.Find(Preview.FacilityId).Size(Preview.Rotation);
            if (Mathf.Abs(position.x - center.x) <= size.x * CellSize * .5f &&
                Mathf.Abs(position.z - center.z) <= size.y * CellSize * .5f)
                dragOffset = center - position;
            MoveConstructionPointer(point);
        }

        private void MoveConstructionPointer(Vector2 point)
        {
            if (!pointerHeld || pointerRejected || Preview == null || InputBlocked || IsOverUI(point)) return;
            if (GroundPoint(point, out var position)) MovePreviewCenter(position + dragOffset);
        }

        private void ClearPointer()
        {
            pointerHeld = pointerIsTouch = pointerRejected = suppressTouches = false;
            dragOffset = Vector3.zero;
        }

        private Data_RanchPlacement FindWorldFacility(Vector2 point)
        {
            var ray = WorldCamera.ScreenPointToRay(point);
            if (!Physics.Raycast(ray, out var hit, 200)) return null;
            foreach (var pair in instances)
                if (pair.Value != null && (hit.transform == pair.Value.transform || hit.transform.IsChildOf(pair.Value.transform)))
                    return Layout.Facilities.Find(item => item.InstanceId == pair.Key);
            return null;
        }

        public void HandleWorldTap(Vector2 point)
        {
            if (!ready || IsBuilding || InputBlocked || IsOverUI(point)) return;
            var entry = FindWorldFacility(point);
            if (entry != null && ranchCamera != null) ranchCamera.FocusOn(Position(entry));
        }

        private void OnApplicationFocus(bool focused) { if (!focused) ClearPointer(); }
        private void OnDisable() { Cancel(); }
    }
}
