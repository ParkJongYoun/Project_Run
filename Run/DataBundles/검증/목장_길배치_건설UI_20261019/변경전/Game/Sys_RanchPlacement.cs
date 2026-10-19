using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectRun.Ranch
{
    public sealed class Sys_RanchPlacement : MonoBehaviour
    {
        public const string SaveKey = "ProjectRun.Ranch.LobbyTest.Layout.v1";
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
        public Data_RanchPlacement Preview { get; private set; }
        public bool IsBuilding { get; private set; }
        public bool InputBlocked { get; set; }
        public bool CanConfirm => Preview != null && RanchGrid.CanPlace(Catalog, Layout.Facilities, Preview, GridSize, out _);
        public string PreviewReason => Preview == null ? "시설을 선택하거나 배치한 시설을 눌러 이동하세요" :
            RanchGrid.CanPlace(Catalog, Layout.Facilities, Preview, GridSize, out var reason) ? "배치할 수 있어요" : reason;
        public bool IsMoving => Preview != null && Layout.Facilities.Exists(item => item.InstanceId == Preview.InstanceId);
        private GameObject previewObject;
        private GameObject hiddenOriginal;
        private readonly Dictionary<string, GameObject> instances = new Dictionary<string, GameObject>();
        private bool dragging;
        private Vector2 dragOffset;
        private bool ready;

        private void Start()
        {
            Layout = JsonUtility.FromJson<Data_RanchSave>(JsonUtility.ToJson(InitialLayout));
            if (PlayerPrefs.HasKey(SaveKey))
            {
                try
                {
                    var saved = JsonUtility.FromJson<Data_RanchSave>(PlayerPrefs.GetString(SaveKey));
                    if (RanchGrid.Validate(Catalog, saved, GridSize)) Layout = saved;
                    else Message?.Invoke("저장된 배치를 읽지 못해 기본 목장을 표시합니다");
                }
                catch (Exception) { Message?.Invoke("저장된 배치를 읽지 못해 기본 목장을 표시합니다"); }
            }
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
            Cancel(); IsBuilding = enabled; Grid.SetActive(enabled); Changed?.Invoke();
        }

        public void BeginNew(string id)
        {
            if (!ready || Catalog.Find(id) == null) return;
            if (Layout.Facilities.Count >= 256) { Message?.Invoke("시설은 최대 256개까지 배치할 수 있어요"); return; }
            Cancel(); IsBuilding = true; Grid.SetActive(true);
            Preview = new Data_RanchPlacement { InstanceId = Guid.NewGuid().ToString("N"), FacilityId = id, X = 10, Y = 6 };
            bool found = false;
            for (int y = 0; y < GridSize.y && !found; y++)
                for (int x = 0; x < GridSize.x && !found; x++)
                { Preview.X = x; Preview.Y = y; found = RanchGrid.CanPlace(Catalog, Layout.Facilities, Preview, GridSize, out _); }
            CreatePreview();
        }

        public void BeginMove(string instanceId)
        {
            if (!ready) return;
            var entry = Layout.Facilities.Find(item => item.InstanceId == instanceId);
            if (entry == null) return;
            Cancel(); IsBuilding = true; Grid.SetActive(true); Preview = entry.Copy();
            if (instances.TryGetValue(instanceId, out hiddenOriginal)) hiddenOriginal.SetActive(false);
            CreatePreview();
        }

        private void CreatePreview()
        {
            previewObject = Create(Preview); previewObject.name = "obj_placement_preview";
            foreach (var collider in previewObject.GetComponentsInChildren<Collider>()) collider.enabled = false;
            UpdatePreview();
        }

        public void MovePreview(int x, int y)
        {
            if (Preview == null) return;
            Preview.X = x; Preview.Y = y; UpdatePreview();
        }

        public void Rotate()
        {
            if (Preview == null) return;
            Preview.Rotation = (Preview.Rotation + 1) % 4; UpdatePreview();
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
            Cancel(); Rebuild(); Save(); Changed?.Invoke(); return true;
        }

        public void Cancel()
        {
            dragging = false;
            if (previewObject != null) { previewObject.SetActive(false); Destroy(previewObject); }
            if (hiddenOriginal != null) hiddenOriginal.SetActive(true);
            hiddenOriginal = null; previewObject = null; Preview = null;
            if (FootprintMarker != null) FootprintMarker.gameObject.SetActive(false);
            Changed?.Invoke();
        }

        public void RemoveSelected()
        {
            if (!IsMoving) return;
            string id = Preview.InstanceId; Cancel();
            Layout.Facilities.RemoveAll(item => item.InstanceId == id); Rebuild(); Save(); Changed?.Invoke();
        }

        public void ResetLayout()
        {
            Cancel(); Layout = JsonUtility.FromJson<Data_RanchSave>(JsonUtility.ToJson(InitialLayout));
            Rebuild(); Save(); Changed?.Invoke();
        }

        private void Save()
        {
            try { PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Layout)); PlayerPrefs.Save(); Message?.Invoke("목장 배치를 저장했어요"); }
            catch (Exception exception) { Debug.LogWarning("Ranch layout save: " + exception.Message); Message?.Invoke("저장 공간을 확인해 주세요. 현재 배치는 유지됩니다"); }
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
            if (!ready || !IsBuilding || InputBlocked) { dragging = false; return; }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame) { Cancel(); return; }
                if (Keyboard.current.rKey.wasPressedThisFrame) Rotate();
                if (Keyboard.current.enterKey.wasPressedThisFrame) Confirm();
            }
            var pointer = Pointer.current;
            if (pointer == null) return;
            var point = pointer.position.ReadValue();
            if (Touchscreen.current != null)
            {
                int touches = 0;
                foreach (var touch in Touchscreen.current.touches) if (touch.press.isPressed) touches++;
                if (touches > 1) { dragging = false; return; }
            }
            if (pointer.press.wasPressedThisFrame && !IsOverUI(point))
            {
                bool selectingExisting = Preview == null;
                if (Preview == null)
                {
                    var ray = WorldCamera.ScreenPointToRay(point);
                    if (Physics.Raycast(ray, out var hit, 200))
                        foreach (var pair in instances)
                            if (hit.transform == pair.Value.transform || hit.transform.IsChildOf(pair.Value.transform))
                            { BeginMove(pair.Key); break; }
                }
                dragging = Preview != null;
                dragOffset = Vector2.zero;
                if (selectingExisting && dragging && GridPoint(point, out var local))
                {
                    var size = Catalog.Find(Preview.FacilityId).Size(Preview.Rotation);
                    dragOffset = local - new Vector2(Preview.X + size.x * .5f, Preview.Y + size.y * .5f);
                }
            }
            if (dragging && pointer.press.isPressed && !IsOverUI(point) && Preview != null)
            {
                if (GridPoint(point, out var local))
                {
                    local -= dragOffset;
                    var size = Catalog.Find(Preview.FacilityId).Size(Preview.Rotation);
                    MovePreview(Mathf.FloorToInt(local.x - size.x * .5f + .5f), Mathf.FloorToInt(local.y - size.y * .5f + .5f));
                }
            }
            if (!pointer.press.isPressed) dragging = false;
        }

        private bool GridPoint(Vector2 point, out Vector2 local)
        {
            var ray = WorldCamera.ScreenPointToRay(point);
            if (new Plane(Vector3.up, GridOrigin).Raycast(ray, out float distance))
            {
                var position = (ray.GetPoint(distance) - GridOrigin) / CellSize;
                local = new Vector2(position.x, position.z); return true;
            }
            local = Vector2.zero; return false;
        }

        private void OnDisable() { Cancel(); }
    }
}
