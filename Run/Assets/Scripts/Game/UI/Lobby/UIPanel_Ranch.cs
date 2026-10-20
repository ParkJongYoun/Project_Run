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
        public UIPanel_RanchGuide Guide;
        private CanvasGroup collectionVisibility;
        public Text Toast;
        public Button ConfirmButton;
        public Button RemoveButton;
        public Vector3[] LockedPositions;
        public RectTransform[] LockedLabels;
        private int catalogPage;
        private const int CardsPerPage = 6;   // 카탈로그 한 페이지에 보이는 시설 카드 수
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
            Bind("btn_build", () => { Guide?.Clear(); CloseModal(); Placement.SetBuildMode(!Placement.IsBuilding); });
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
            Bind("btn_next", () => { catalogPage = Mathf.Min((Placement.Catalog.Facilities.Length - 1) / CardsPerPage, catalogPage + 1); BuildCards(); });
            if (!subscribed) { Placement.Changed += Refresh; Placement.Message += Notify; subscribed = true; }
            BuildCards(); Refresh();
            Guide?.EnterHome();
        }

        public override void OnClose()
        {
            Guide?.Clear();
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
            // 우측 상단 버튼 4개: 목업의 이미지 한 장씩 사용한다 (프레임·기호·알림 핀이 이미지에 이미 포함되어 있다)
            IconButton(SafeArea, "btn_notebook", "hud_notebook", 546, 326, 56, 54);   // 목장 수첩
            ArtButton(SafeArea, "btn_settings", "hud_settings", 603, 326, 55, 54, null);   // 설정
            ArtButton(SafeArea, "btn_zoom_in", "hud_zoom_in", 432, 326, 52, 52, null);     // 확대 (+)
            ArtButton(SafeArea, "btn_zoom_out", "hud_zoom_out", 489, 326, 52, 52, null);   // 축소 (−)
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
            // 건설 도크 전체 틀: 화면 아래에서 102 위, 크기 1260x180 (배경 그림은 없다)
            Drawer = DemoUI.Rect(SafeArea, "group_construction", 0, 102, 1260, 180);
            Drawer.anchorMin = Drawer.anchorMax = new Vector2(.5f, 0);
            Drawer.SetSiblingIndex(Navigation.GetSiblingIndex() + 1);
            // 투명 차단막: 도크 영역을 눌러도 뒤의 목장 월드로 클릭이 새지 않게 한다 (OnOpen이 raycastTarget을 켠다)
            Image(Drawer, "bg_surface", "", 0, 0, 1260, 180, false).color = new Color(1, 1, 1, 0);
            // 왼쪽 위: 지금 건설 모드임을 보여 주는 '건설' 표시 (망치 그림 포함, 클릭 불가)
            var selected = ArtPair(Drawer, "group_build_selected", "construct_build", -516, 40, 214, 88);
            Label(selected, "txt_label", "건설", 34, -2, 110, 42, 26);
            // 왼쪽 아래: '닫기' 버튼 (X 그림 포함) - 누르면 건설 모드 종료
            var close = ArtButton(Drawer, "btn_close_build", "construct_close", -509, -42, 205, 77, null);
            Label(close, "txt_label", "닫기", 28, 2, 110, 42, 26);
            // 오른쪽: 시설 카탈로그 패널 (좌우 금속 끝단은 9-slice로 보존, 가운데만 가로로 늘어난다)
            var catalog = DemoUI.Rect(Drawer, "group_catalog", 128, 0, 996, 176);
            var panelImage = Image(catalog, "bg_surface", "construct_panel", 0, 0, 996, 176, false);
            if (panelImage.sprite != null) panelImage.pixelsPerUnitMultiplier = panelImage.sprite.rect.height / 176f;   // 세로는 원본 비율 그대로
            ArtButton(catalog, "btn_previous", "construct_prev", -497, 2, 55, 68, null);   // ◀ 이전 페이지
            ArtButton(catalog, "btn_next", "construct_next", 469, 2, 54, 68, null);        // ▶ 다음 페이지
            var cardHolder = DemoUI.Rect(catalog, "group_cards", -13, 0, 930, 150);         // 카드가 들어갈 자리
            CreateCardSlots(cardHolder);                                                     // 카드 슬롯 6장 (프리팹에서 위치 조정 가능)

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

        // 시설 카드 채우기: 프리팹에 미리 놓인 카드 슬롯(group_cards의 자식)을 그대로 쓰고 내용만 바꾼다.
        // 그래서 프리팹에서 슬롯 위치·크기를 옮기면 게임에도 그대로 반영된다. 슬롯이 하나도 없으면 6장을 새로 만든다.
        private void BuildCards()
        {
            var holder = Drawer.Find("group_catalog/group_cards");
            if (holder.childCount == 0) CreateCardSlots(holder);
            int start = catalogPage * CardsPerPage;   // 이번 페이지 첫 시설의 번호
            for (int i = 0; i < holder.childCount; i++)
            {
                var card = (RectTransform)holder.GetChild(i);
                int index = start + i;
                bool visible = i < CardsPerPage && index < Placement.Catalog.Facilities.Length;
                card.gameObject.SetActive(visible);   // 마지막 페이지에서 남는 슬롯은 숨긴다
                if (!visible) continue;

                var definition = Placement.Catalog.Facilities[index]; string id = definition.Id;
                card.name = "btn_facility_" + id;     // 검증 도구가 이 이름으로 카드를 찾는다
                bool dragged = false;

                // 클릭: 드래그가 아니었으면 시설 배치 시작 (페이지를 넘길 때마다 이전 연결은 지운다)
                var button = card.GetComponent<Button>();
                if (button == null) button = Click(card, null);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { if (!dragged) Placement.BeginNew(id); });

                // 드래그: 카드를 끌어서 목장 위에 바로 놓기 (기존 이벤트를 비우고 다시 연결)
                var drag = card.GetComponent<EventTrigger>();
                if (drag == null) drag = card.gameObject.AddComponent<EventTrigger>();
                drag.triggers.Clear();
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

                // 내용 채우기: 시설 그림 · 이름 · 차지하는 칸 수
                card.Find("img_portrait").GetComponent<Image>().sprite = definition.Portrait != null ? definition.Portrait : Art.Get("build");
                card.Find("txt_name").GetComponent<Text>().text = definition.Name;
                card.Find("txt_size").GetComponent<Text>().text = definition.Footprint.x + " × " + definition.Footprint.y;
            }
        }

        // 카드 슬롯 6장을 만든다 (Build로 프리팹을 새로 만들 때, 또는 슬롯이 없을 때만 쓰임)
        // 슬롯 이미지 146x142, 간격 153. 만든 뒤에는 프리팹에서 위치를 자유롭게 조정해도 된다
        private void CreateCardSlots(Transform holder)
        {
            for (int i = 0; i < CardsPerPage; i++)
            {
                var card = ArtPair(holder, "btn_facility_" + i, "construct_slot", (i - 2.5f) * 153, 0, 146, 142, false);
                Click(card, null);                                                               // 클릭 반응 (실제 동작은 BuildCards가 연결)
                Icon(card, "img_portrait", "", 0, 21, 110, 80);                                  // 시설 그림 자리
                Label(card, "txt_name", "", 0, -37, 136, 24, 18, new Color(.30f, .23f, .18f));   // 시설 이름 (흰색 아웃라인 자동)
                Label(card, "txt_size", "", 0, -57, 136, 18, 15, new Color(.33f, .26f, .21f));   // 칸 수
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

        private void Home() { CloseModal(); Placement.SetBuildMode(false); RanchCamera.ResetView(); Guide?.EnterHome(); }
        private void Notify(string message) { Toast.text = message; toastUntil = Time.unscaledTime + 3; }
        private void CloseModal() { Modal.gameObject.SetActive(false); Placement.InputBlocked = false; Navigation.gameObject.SetActive(true); Refresh(); }

        private RectTransform OpenModal(string title)
        {
            Guide?.Clear();
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
            Guide?.Clear();
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
            Guide?.EnterHome();
        }

        public void ShowTrainingFromArena()
        {
            CloseCollection();
            ShowTraining();
        }

        // ── 목장 수첩 ────────────────────────────────────────────
        // 수첩 팝업을 연다. 수첩 전용 프레임/뒤로 버튼 이미지를 쓴다.
        private void ShowNotebook()
        {
            var panel = OpenNotebookModal();

            // 본문을 3묶음(기본 조작 / 화면·카메라 조작 / 데모 안내)으로 나누고, 묶음 사이에 점선을 넣는다.
            // 좌표(x, y)는 프레임 중앙 기준이다. 위치가 어긋나면 이 숫자만 조정하면 된다.
            Label(panel, "txt_guide_basic",  "건설 모드에서는 농장 전체를 위에서 내려다봅니다.\n목록의 시설을 농장으로 끌어 놓거나 선택 후 드래그하세요.\n설치된 시설도 직접 잡아 이동하거나 회수할 수 있어요.\n초록색은 배치 가능 · 빨간색은 배치 불가입니다.", -19,   85, 800, 130, 23).lineSpacing = 1.15f;
            DashedLine(panel, "line_1", 10, 540);
            Label(panel, "txt_guide_input",  "일반 목장: 좌클릭 드래그 / 한 손가락으로 화면 이동\n우클릭 드래그: 좌우 회전 / 우클릭: 원래 각도\n+/− 버튼·휠·두 손가락 벌리기: 확대·축소\n건설 중 R: 회전 / Enter: 배치 / Esc: 선택 취소", -19,  -67, 800, 130, 23).lineSpacing = 1.15f;
            DashedLine(panel, "line_2", -139, 540);
            Label(panel, "txt_guide_demo",   "시설 아래 아이콘: 회전 · 배치 · 취소 · 회수\n건설을 닫으면 이전 시점으로 돌아갑니다. 새 실행에는 기본 배치로 시작합니다.", -19, -180, 940,  62, 21).lineSpacing = 1.15f;
        }

        // 수첩 전용 팝업 틀을 만든다.
        // 공용 OpenModal은 panel 프레임 + 제목 글자를 쓰지만, 수첩 프레임 이미지에는
        // "목장 수첩" 제목이 이미 그려져 있어서 제목 Label은 만들지 않는다.
        private RectTransform OpenNotebookModal()
        {
            Guide?.Clear();
            Placement.Cancel(); Placement.InputBlocked = true; Navigation.gameObject.SetActive(false); // 배치 중이면 취소하고 뒤쪽 화면 입력을 막는다
            DemoUI.Clear(Modal); Modal.gameObject.SetActive(true);                                       // 이전 팝업 내용을 지우고 팝업 영역을 켠다
            var shade = DemoUI.Shade(Modal, "bg_dim", new Color(.10f,.15f,.10f,.62f)); shade.raycastTarget = true; // 뒷배경을 어둡게 + 클릭 차단

            // 프레임 원본 비율(1511x814)을 유지한 크기 1120x603
            var panel = DemoUI.Rect(Modal, "popup_content", 0, -4, 1120, 603);
            Image(panel, "img_frame", "notebook_frame", 0, 0, 1120, 603, false);

            // 뒤로 버튼: 프레임 좌상단에 걸치게 배치 (누르면 팝업 닫기)
            var back = IconButton(panel, "btn_close", "notebook_back", -428, 215, 175, 85);
            back.onClick.AddListener(CloseModal);
            Refresh(); return panel;
        }

        // 점선 한 줄을 만든다. 작은 가로 막대(dash)를 일정 간격으로 나열한다.
        private void DashedLine(Transform parent, string name, float y, float width)
        {
            var line = DemoUI.Rect(parent, name, -19, y, width, 2);          // 점선 전체를 묶는 빈 오브젝트
            const float dash = 12f, gap = 8f;                                // 점 길이 / 점 사이 간격
            int count = Mathf.FloorToInt(width / (dash + gap));              // 폭 안에 들어가는 점 개수
            float startX = -(count * (dash + gap) - gap) / 2f + dash / 2f;   // 가운데 정렬을 위한 첫 점 위치
            for (int i = 0; i < count; i++)
            {
                var d = DemoUI.Rect(line, "dash_" + i, startX + i * (dash + gap), 0, dash, 2).gameObject.AddComponent<UnityEngine.UI.Image>();
                d.color = new Color(.86f,.66f,.42f,.9f); d.raycastTarget = false;
            }
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

        // 이미지 한 장짜리 틀: bg_surface 하나에 합본 이미지를 넣고 img_frame은 만들지 않는다 (목업 파츠가 프레임+배경 합본이라서)
        private RectTransform ArtPair(Transform parent, string name, string key, float x, float y, float w, float h, bool preserve = true)
        {
            var rect = DemoUI.Rect(parent, name, x, y, w, h);
            Image(rect, "bg_surface", key, 0, 0, w, h, preserve);
            return rect;
        }

        // 이미지 한 장짜리 버튼: ArtPair에 클릭(Button)을 연결한다. action이 null이면 OnOpen의 Bind가 나중에 연결한다
        private RectTransform ArtButton(Transform parent, string name, string key, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var rect = ArtPair(parent, name, key, x, y, w, h);
            Click(rect, action);
            return rect;
        }

        private Image Image(Transform parent, string name, string key, float x, float y, float w, float h, bool preserve)
        {
            var image = DemoUI.Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.sprite = Art.Get(key); image.raycastTarget = false; image.preserveAspect = preserve;
            if (image.sprite != null && image.sprite.border.sqrMagnitude > 0 && !key.StartsWith("menu_")) { image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4; }
            if (key.StartsWith("construction_card_")) image.pixelsPerUnitMultiplier = 10;
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            if (name == "txt_name") WhiteOutline(text);   // 이름 글자(txt_name)는 모두 흰색 아웃라인
            return text;
        }

        // 글자에 흰색 아웃라인을 붙인다. 프리팹에 넣은 Outline 컴포넌트와 같은 값(흰색, 1.5px)이다
        private static void WhiteOutline(Text text)
        {
            var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }
}
