using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ProjectRun.Ranch;

namespace ProjectRun.Race
{
    [DefaultExecutionOrder(200)]
    public sealed class UIPanel_Ranch : UIPanel_Base
    {
        public Data_DemoArt Art;
        public Sys_RanchPlacement Placement;
        public Sys_RanchCamera RanchCamera;
        public RectTransform SafeArea;
        public RectTransform Modal;
        public RectTransform Drawer;
        public RectTransform Actions;
        public RectTransform Navigation;
        public Mgr_UI CollectionManager;
        private CanvasGroup collectionVisibility;
        public Text Toast;
        public Button ConfirmButton;
        public Button RemoveButton;
        public Vector3[] LockedPositions;
        public RectTransform[] LockedLabels;
        private int catalogPage;
        private float toastUntil;
        private bool subscribed, catalogDragging;
        private CanvasGroup actionVisibility;
        private readonly Vector3[] uiCorners = new Vector3[4];
        private static readonly Color ink = new Color(.25f, .13f, .065f);

        public override void OnOpen()
        {
            if (!Application.isPlaying || Placement == null || SafeArea == null) return;
            foreach (var surface in new[] { Drawer, Drawer.Find("group_catalog") as RectTransform, Navigation, SafeArea.Find("group_ranch_info") as RectTransform })
                if (surface != null) surface.Find("bg_surface").GetComponent<Image>().raycastTarget = true;
            Bind("btn_home", Home);
            Bind("btn_monsters", ShowMonsters);
            Bind("btn_build", () => { CloseModal(); Placement.SetBuildMode(!Placement.IsBuilding); });
            Bind("btn_close_build", () => Placement.SetBuildMode(false));
            Bind("btn_training", ShowTraining);
            Bind("btn_arena", ShowArena);
            Bind("btn_settings", ShowSettings);
            Bind("btn_notebook", ShowNotebook);
            Bind("btn_zoom_in", RanchCamera.ZoomIn);
            Bind("btn_zoom_out", RanchCamera.ZoomOut);
            Bind("btn_rotate", Placement.Rotate);
            Bind("btn_confirm", () => Placement.Confirm());
            Bind("btn_cancel", Placement.Cancel);
            Bind("btn_remove", Placement.RemoveSelected);
            Bind("btn_previous", () => { catalogPage = Mathf.Max(0, catalogPage - 1); BuildCards(); });
            Bind("btn_next", () => { catalogPage = Mathf.Min((Placement.Catalog.Facilities.Length - 1) / 6, catalogPage + 1); BuildCards(); });
            if (!subscribed) { Placement.Changed += Refresh; Placement.Message += Notify; subscribed = true; }
            BuildCards(); Refresh();
        }

        public override void OnClose()
        {
            if (Placement == null) return;
            catalogDragging = false;
            if (subscribed) { Placement.Changed -= Refresh; Placement.Message -= Notify; subscribed = false; }
            Placement.InputBlocked = false;
            if (Application.isPlaying) Placement.Cancel();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) catalogDragging = false;
        }

        private void Bind(string name, UnityEngine.Events.UnityAction action)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
                if (button.name == name) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); return; }
        }

        private void Update()
        {
            DemoUI.SafeArea(SafeArea);
            if (Toast != null) Toast.transform.parent.gameObject.SetActive(Time.unscaledTime < toastUntil);
            if (Placement == null || Placement.WorldCamera == null || LockedLabels == null) return;
            for (int i = 0; i < LockedLabels.Length; i++)
            {
                var screen = Placement.WorldCamera.WorldToScreenPoint(LockedPositions[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(SafeArea, screen, null, out var local);
                LockedLabels[i].anchoredPosition = local;
                LockedLabels[i].gameObject.SetActive(screen.z > 0 && !Modal.gameObject.activeSelf && !Placement.IsBuilding);
            }
        }

        public void Build()
        {
            DemoUI.Clear(transform);
            SafeArea = DemoUI.Fill(transform, "obj_safe_area");
            var info = Pair(SafeArea, "group_ranch_info", "ranch_info", -469, 323, 330, 65);
            Icon(info, "img_ranch", "home", -132, 4, 76, 68);
            Label(info, "txt_name", "나의 목장", -18, 1, 160, 42, 27);
            Label(info, "txt_level", "Lv. 1", 114, 1, 75, 42, 24, new Color(1,.92f,.73f));
            var notebook = IconButton(SafeArea, "btn_notebook", "notebook", 549, 326, 52, 57);
            Icon(notebook.transform, "img_alert", "alert", 19, 23, 14, 14);
            var settings = Pair(SafeArea, "btn_settings", "round", 603, 326, 57, 60);
            Click(settings, null); Icon(settings, "img_settings", "settings", 0, 1, 41, 41);
            var zoomIn=Pair(SafeArea,"btn_zoom_in","round",435,326,51,54);
            Click(zoomIn,null);Label(zoomIn,"txt_symbol","+",0,1,43,46,36,new Color(1,.94f,.78f));
            var zoomOut=Pair(SafeArea,"btn_zoom_out","round",492,326,51,54);
            Click(zoomOut,null);Label(zoomOut,"txt_symbol","−",0,1,43,46,36,new Color(1,.94f,.78f));
            var bottom = Pair(SafeArea, "group_navigation_base", "bottom", 0, -324, 1200, 81);
            Navigation = bottom;
            string[] names = { "home", "monsters", "build", "training", "arena" };
            string[] titles = { "목장", "몬스터", "건설", "훈련", "경기장" };
            for (int i = 0; i < names.Length; i++)
            {
                var button = Pair(bottom, "btn_" + names[i], "menu", (i - 2) * 201, 14, 198, 65);
                Click(button, null); Icon(button, "img_icon", names[i], -60, 6, 77, 74);
                Label(button, "txt_label", titles[i], 34, 0, 110, 49, 25);
            }
            RebuildConstructionUI();
            var toast = Pair(SafeArea, "group_toast", "panel", 0, 260, 550, 46);
            Toast = Label(toast, "txt_toast", "", 0, 0, 515, 38, 20);
            toast.gameObject.SetActive(false);
            LockedLabels = new RectTransform[LockedPositions.Length];
            for (int i = 0; i < LockedLabels.Length; i++)
            {
                var marker = DemoUI.Rect(SafeArea, "group_locked_" + i, 0, 0, 100, 95);
                Icon(marker, "img_lock", "lock", 0, 25, 40, 47);
                Pair(marker, "group_sign", "sign", 0, -19, 94, 43);
                Label(marker, "txt_label", "확장 예정", 0, -19, 90, 30, 17);
                LockedLabels[i] = marker;
            }
            Modal = DemoUI.Fill(SafeArea, "group_overlay"); Modal.gameObject.SetActive(false);
            Drawer.gameObject.SetActive(false); Actions.gameObject.SetActive(false);
        }


        public void RebuildConstructionUI()
        {
            RemoveConstructionRoot(Drawer); RemoveConstructionRoot(Actions);
            Drawer = Pair(SafeArea, "group_construction", "bottom", 0, 72, 1260, 120);
            Drawer.anchorMin = Drawer.anchorMax = new Vector2(.5f, 0);
            Drawer.SetSiblingIndex(Navigation.GetSiblingIndex() + 1);
            var selected = Pair(Drawer, "group_build_selected", "menu", -520, 28, 184, 50);
            selected.Find("bg_surface").GetComponent<Image>().color = new Color(1, .78f, .25f);
            Icon(selected, "img_icon", "build", -57, 2, 50, 49);
            Label(selected, "txt_label", "건설", 28, 0, 110, 42, 24);
            var close = Pair(Drawer, "btn_close_build", "menu", -520, -27, 184, 50);
            Click(close, null); Label(close, "txt_cross", "×", -57, 0, 40, 43, 34);
            Label(close, "txt_label", "닫기", 27, 0, 110, 42, 24);
            var catalog = Pair(Drawer, "group_catalog", "panel", 96, 0, 1036, 120);
            PlainButton(catalog, "btn_previous", "◀", -490, 0, 32, 43, null);
            PlainButton(catalog, "btn_next", "▶", 490, 0, 32, 43, null);
            DemoUI.Rect(catalog, "group_cards", 0, 0, 922, 108);

            Actions = DemoUI.Rect(SafeArea, "group_placement_actions", 0, 0, 164, 42);
            Actions.SetSiblingIndex(Drawer.GetSiblingIndex() + 1);
            actionVisibility = Actions.gameObject.AddComponent<CanvasGroup>();
            ActionButton("btn_rotate", "action_rotate", -60);
            ConfirmButton = ActionButton("btn_confirm", "action_confirm", -20);
            ActionButton("btn_cancel", "action_cancel", 20);
            RemoveButton = ActionButton("btn_remove", "action_store", 60);
            Drawer.gameObject.SetActive(false); Actions.gameObject.SetActive(false);
            if (Placement != null && Placement.Catalog != null) BuildCards();
        }

        private static void RemoveConstructionRoot(RectTransform root)
        {
            if (root == null) return;
            root.gameObject.SetActive(false); root.SetParent(null);
            if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
        }

        private Button ActionButton(string name, string key, float x)
        {
            var rect = Pair(Actions, name, "round", x, 0, 36, 38);
            var button = Click(rect, null);
            Icon(rect, "img_symbol", key, 0, 1, 30, 30);
            rect.gameObject.AddComponent<CanvasGroup>();
            return button;
        }

        private void LateUpdate()
        {
            if (Placement == null || RanchCamera == null || !Placement.IsBuilding || Drawer == null) return;
            UpdateConstructionView();
        }

        private void UpdateConstructionView()
        {
            // Match the actual compact dock and safe area instead of reserving an empty action bar.
            var canvas = SafeArea.GetComponentInParent<Canvas>();
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Drawer.GetWorldCorners(uiCorners);
            var dockTop = RectTransformUtility.WorldToScreenPoint(uiCamera, uiCorners[1]);
            SafeArea.GetWorldCorners(uiCorners);
            var safeMin = RectTransformUtility.WorldToScreenPoint(uiCamera, uiCorners[0]);
            var safeMax = RectTransformUtility.WorldToScreenPoint(uiCamera, uiCorners[2]);
            float scale = canvas.scaleFactor;
            // Keep one small toolbar row below even the bottom-most building's roof overhang.
            var viewportMin = Placement.WorldCamera.ScreenToViewportPoint(new Vector3(safeMin.x + 8 * scale, dockTop.y + 60 * scale, 0));
            var viewportMax = Placement.WorldCamera.ScreenToViewportPoint(new Vector3(safeMax.x - 8 * scale, safeMax.y - 10 * scale, 0));
            RanchCamera.SetBuildViewport(Rect.MinMaxRect(viewportMin.x, viewportMin.y, viewportMax.x, viewportMax.y));

            if (Placement.Preview == null || !Actions.gameObject.activeSelf) return;
            if (!Placement.TryGetPreviewBounds(out var bounds)) return;
            var screen = Placement.WorldCamera.WorldToScreenPoint(bounds.center);
            float bottomY = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                bottomY = Mathf.Min(bottomY, Placement.WorldCamera.WorldToScreenPoint(corner).y);
            }
            screen.y = bottomY;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(SafeArea, screen, uiCamera, out var local);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(SafeArea, dockTop, uiCamera, out var dockLocal);
            float halfWidth = Actions.rect.width * .5f, halfHeight = Actions.rect.height * .5f;
            local.y -= halfHeight + 2;
            local.x = Mathf.Clamp(local.x, SafeArea.rect.xMin + halfWidth + 8, SafeArea.rect.xMax - halfWidth - 8);
            local.y = Mathf.Clamp(local.y, dockLocal.y + halfHeight + 5, SafeArea.rect.yMax - halfHeight - 8);
            Actions.anchoredPosition = local;
            // The moving toolbar cannot catch the pointer during a facility drag near an edge.
            if (actionVisibility == null) actionVisibility = Actions.GetComponent<CanvasGroup>();
            actionVisibility.blocksRaycasts = !Placement.IsDraggingPreview && !catalogDragging;
        }

        private void BuildCards()
        {
            var holder = Drawer.Find("group_catalog/group_cards"); DemoUI.Clear(holder);
            int start = catalogPage * 6;
            for (int i = 0; i < 6 && start + i < Placement.Catalog.Facilities.Length; i++)
            {
                var definition = Placement.Catalog.Facilities[start + i]; string id = definition.Id;
                var card = Pair(holder, "btn_facility_" + id, "panel", (i - 2.5f) * 151, 0, 143, 108);
                bool dragged = false;
                Click(card, () => { if (!dragged) Placement.BeginNew(id); });
                var drag = card.gameObject.AddComponent<EventTrigger>();
                AddDragEvent(drag, EventTriggerType.PointerDown, data => { dragged = false; catalogDragging = false; });
                AddDragEvent(drag, EventTriggerType.BeginDrag, data =>
                {
                    if (((PointerEventData)data).button != PointerEventData.InputButton.Left) return;
                    dragged = catalogDragging = true; Placement.BeginNew(id);
                });
                AddDragEvent(drag, EventTriggerType.Drag, data =>
                {
                    if (dragged) Placement.DragPreviewToScreen(((PointerEventData)data).position);
                });
                AddDragEvent(drag, EventTriggerType.EndDrag, data =>
                {
                    if (dragged) Placement.DragPreviewToScreen(((PointerEventData)data).position);
                    catalogDragging = false;
                });
                var portrait = Icon(card, "img_portrait", "", 0, 18, 110, 68);
                portrait.sprite = definition.Portrait != null ? definition.Portrait : Art.Get("build");
                Label(card, "txt_name", definition.Name, 0, -24, 136, 22, 17);
                Label(card, "txt_size", definition.Footprint.x + " × " + definition.Footprint.y, 0, -43, 135, 16, 13);
            }
        }

        private static void AddDragEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action); trigger.triggers.Add(entry);
        }

        private void Refresh()
        {
            if (Drawer == null) return;
            Drawer.gameObject.SetActive(Placement.IsBuilding && !Modal.gameObject.activeSelf);
            Navigation.gameObject.SetActive(!Placement.IsBuilding && !Modal.gameObject.activeSelf);
            Actions.gameObject.SetActive(Placement.Preview != null && !Modal.gameObject.activeSelf);
            ConfirmButton.interactable = Placement.CanConfirm;
            RemoveButton.interactable = Placement.IsMoving;
            ConfirmButton.GetComponent<CanvasGroup>().alpha = Placement.CanConfirm ? 1 : .4f;
            RemoveButton.GetComponent<CanvasGroup>().alpha = Placement.IsMoving ? 1 : .4f;
            foreach (string name in new[] { "btn_zoom_in", "btn_zoom_out" })
            {
                var button = SafeArea.Find(name);
                if (button != null) button.GetComponent<Button>().interactable = !Placement.IsBuilding;
            }
            if (Placement.IsBuilding) UpdateConstructionView();
        }

        private void Home() { CloseModal(); Placement.SetBuildMode(false); RanchCamera.ResetView(); }
        private void Notify(string message) { Toast.text = message; toastUntil = Time.unscaledTime + 3; }
        private void CloseModal() { Modal.gameObject.SetActive(false); Placement.InputBlocked = false; Navigation.gameObject.SetActive(true); Refresh(); }

        private RectTransform OpenModal(string title)
        {
            Placement.Cancel(); Placement.InputBlocked = true; Navigation.gameObject.SetActive(false);
            DemoUI.Clear(Modal); Modal.gameObject.SetActive(true);
            var shade = DemoUI.Shade(Modal, "bg_dim", new Color(.10f,.15f,.10f,.62f)); shade.raycastTarget = true;
            var panel = Pair(Modal, "popup_content", "panel", 0, -4, 1040, 540);
            Label(panel, "txt_title", title, 0, 211, 850, 55, 32);
            PlainButton(panel, "btn_close", "뒤로", -423, 211, 121, 45, CloseModal);
            Refresh(); return panel;
        }

        private void ShowMonsters()
        {
            if (CollectionManager != null) { OpenCollection(E_PanelType.RanchMonsters); return; }
            var panel = OpenModal("목장의 몬스터");
            for (int i = 0; i < Mgr_Table.Demo.Monsters.Length; i++)
            {
                var monster = Mgr_Table.Demo.Monsters[i]; float x = (i - 1) * 315;
                var card = Pair(panel, "group_monster_" + i, "panel", x, -10, 293, 332);
                var face = Icon(card, "img_portrait", "", 0, 77, 132, 132); face.sprite = DemoUI.Art.Get(monster.Id + "_face");
                Label(card, "txt_name", monster.Name, 0, -12, 265, 43, 26);
                Label(card, "txt_strength", monster.Strength, 0, -53, 270, 35, 19);
                Label(card, "txt_skill", monster.Skill, 0, -95, 260, 38, 20);
                Label(card, "txt_speed", "기본 스피드 " + monster.Stats[0], 0, -132, 260, 30, 17);
            }
            Label(panel, "txt_description", "몬스터 축사와 방목 공간을 배치해 친구들의 목장을 꾸며 보세요", 0, -211, 930, 42, 20);
        }

        private void ShowTraining()
        {
            if (CollectionManager != null) { OpenCollection(E_PanelType.RanchTraining); return; }
            var panel = OpenModal("몬스터 훈련장");
            var image = Icon(panel, "img_training", "training", -305, 10, 260, 215);
            var definition = Placement.Catalog.Find("training"); if (definition.Portrait != null) image.sprite = definition.Portrait;
            Label(panel, "txt_body", "달리기 · 장애물 · 근력 훈련\n\n포링의 탄성 점프, 미노타우르스의 힘, 페코페코의 질주\n\n훈련 완료: Lv.5 · 스피드 +30 · 체력 소모 감소", 135, 18, 615, 240, 22);
            PlainButton(panel, "btn_place_training", "훈련장 배치하기", 0, -196, 286, 55, () => { CloseModal(); Placement.BeginNew("training"); });
        }

        private void ShowArena()
        {
            if (CollectionManager != null) { OpenCollection(E_PanelType.RanchArena); return; }
            var panel = OpenModal("프론테라컵 경기장");
            Icon(panel, "img_arena", "arena", -305, 20, 228, 200);
            Label(panel, "txt_body", "몬스터 1마리 출전 · 자동 레이스\n\n출전 조건  Lv.5 / 스피드 260\n카툰 경기장\n\n훈련을 마친 친구와 함께 출전해 보세요", 135, 17, 615, 252, 23);
            PlainButton(panel, "btn_dismiss", "목장으로", 0, -195, 240, 52, CloseModal);
        }

        private void OpenCollection(E_PanelType type)
        {
            CloseModal();
            Placement.SetBuildMode(false);
            Placement.Cancel();
            Placement.InputBlocked = true;
            collectionVisibility = GetComponent<CanvasGroup>();
            if (collectionVisibility == null) collectionVisibility = gameObject.AddComponent<CanvasGroup>();
            collectionVisibility.alpha = 0;
            collectionVisibility.interactable = false;
            collectionVisibility.blocksRaycasts = false;
            CollectionManager.Open(type);
        }

        public void CloseCollection()
        {
            if (CollectionManager != null)
            {
                CollectionManager.Close(E_PanelType.RanchMonsters);
                CollectionManager.Close(E_PanelType.RanchTraining);
                CollectionManager.Close(E_PanelType.RanchArena);
            }
            if (collectionVisibility != null)
            {
                collectionVisibility.alpha = 1;
                collectionVisibility.interactable = collectionVisibility.blocksRaycasts = true;
            }
            CloseModal();
        }

        public void ShowTrainingFromArena()
        {
            CloseCollection();
            ShowTraining();
        }

        private void ShowNotebook()
        {
            var panel = OpenModal("목장 수첩");
            Label(panel, "txt_body", "건설 모드에서는 농장 전체를 위에서 내려다봅니다.\n목록의 시설을 농장으로 끌어 놓거나 선택 후 드래그하세요.\n설치된 시설도 직접 잡아 이동하거나 회수할 수 있어요.\n시설 아래 아이콘: 회전 · 체크(배치) · X(취소) · 수납(회수)\n초록색일 때 배치 확정 · 빨간색은 배치 불가입니다.\n건설 중 카메라는 고정되며 닫으면 이전 시점으로 돌아갑니다.\n\n일반 목장: 좌클릭 드래그 / 한 손가락으로 화면 이동\n우클릭 드래그: 좌우 회전 / 우클릭: 원래 각도\n+/− 버튼·휠·두 손가락 벌리기: 확대·축소\n건설 중 R: 시설 회전 / Enter: 배치 / Esc: 선택 취소\n\n데모의 배치는 저장하지 않으며 새 실행에 기본 배치로 시작합니다.", 0, -16, 935, 335, 22);
        }

        private void ShowSettings()
        {
            var panel = OpenModal("목장 설정");
            Label(panel, "txt_body", "이번 데모에서는 시설과 길의 배치를 저장하지 않습니다.\n\n새 실행에는 가로지르는 길 하나와 축사·먹이창고로 시작합니다.", 0, 65, 900, 135, 23);
            PlainButton(panel, "btn_reset_view", "카메라 원위치", -205, -76, 310, 57, () => { RanchCamera.ResetView(); CloseModal(); });
            PlainButton(panel, "btn_reset_layout", "기본 배치로", 205, -76, 310, 57, () =>
            {
                var confirmation = OpenModal("기본 배치로 돌아갈까요?");
                Label(confirmation, "txt_warning", "현재 배치한 시설과 길이 기본 배치로 바뀝니다.", 0, 37, 900, 96, 25);
                PlainButton(confirmation, "btn_keep", "유지하기", -173, -98, 260, 58, ShowSettings);
                PlainButton(confirmation, "btn_reset_confirm", "초기화", 173, -98, 260, 58, () => { Placement.ResetLayout(); CloseModal(); });
            });
        }

        private RectTransform Pair(Transform parent, string name, string key, float x, float y, float w, float h)
        {
            var rect = DemoUI.Rect(parent, name, x, y, w, h);
            Image(rect, "bg_surface", key + "_bg", 0, 0, w, h, false);
            Image(rect, "img_frame", key + "_frame", 0, 0, w, h, false);
            return rect;
        }

        private Image Image(Transform parent, string name, string key, float x, float y, float w, float h, bool preserve)
        {
            var image = DemoUI.Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = Art.Get(key); image.raycastTarget = false; image.preserveAspect = preserve;
            if (image.sprite != null && image.sprite.border.sqrMagnitude > 0 && !key.StartsWith("menu_")) { image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4; }
            return image;
        }
        private Image Icon(Transform parent, string name, string key, float x, float y, float w, float h) => Image(parent, name, key, x, y, w, h, true);
        private Button IconButton(Transform parent, string name, string key, float x, float y, float w, float h)
        {
            var image = Icon(parent, name, key, x, y, w, h); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; return button;
        }
        private Button Click(RectTransform rect, UnityEngine.Events.UnityAction action)
        {
            var image = rect.Find("bg_surface").GetComponent<Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1,.94f,.76f); colors.pressedColor = new Color(.84f,.73f,.52f); button.colors = colors;
            button.navigation = new Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            if (action != null) button.onClick.AddListener(action); return button;
        }
        private Button PlainButton(Transform parent, string name, string label, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var rect = Pair(parent, name, "sign", x, y, w, h); var button = Click(rect, action);
            Label(rect, "txt_label", label, 0, 0, w - 10, h - 6, h < 50 ? 18 : 22); return button;
        }
        private Text Label(Transform parent, string name, string value, float x, float y, float w, float h, int size, Color? color = null)
        {
            var text = DemoUI.Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = Art.BodyFont; text.fontSize = size; text.text = value; text.color = color ?? ink;
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
        }
    }
}
