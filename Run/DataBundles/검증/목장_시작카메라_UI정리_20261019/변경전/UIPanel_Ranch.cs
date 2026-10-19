using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Ranch;

namespace ProjectRun.Race
{
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
        public Text PlacementLabel;
        public Text Toast;
        public Button ConfirmButton;
        public Button RemoveButton;
        public Vector3[] LockedPositions;
        public RectTransform[] LockedLabels;
        private int catalogPage;
        private float toastUntil;
        private bool subscribed;
        private static readonly Color ink = new Color(.25f, .13f, .065f);

        public override void OnOpen()
        {
            if (!Application.isPlaying || Placement == null || SafeArea == null) return;
            foreach (var surface in new[] { Drawer, Actions, Navigation, SafeArea.Find("group_ranch_info") as RectTransform })
                if (surface != null) surface.Find("bg_surface").GetComponent<Image>().raycastTarget = true;
            Bind("btn_home", Home);
            Bind("btn_monsters", ShowMonsters);
            Bind("btn_build", () => { CloseModal(); Placement.SetBuildMode(!Placement.IsBuilding); });
            Bind("btn_training", ShowTraining);
            Bind("btn_arena", ShowArena);
            Bind("btn_settings", ShowSettings);
            Bind("btn_notebook", ShowNotebook);
            Bind("btn_gold", () => Notify("이번 목장은 자유 배치 체험입니다"));
            Bind("btn_gems", () => Notify("재화 구매는 준비 중입니다"));
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
            if (subscribed) { Placement.Changed -= Refresh; Placement.Message -= Notify; subscribed = false; }
            Placement.InputBlocked = false;
            if (Application.isPlaying) Placement.Cancel();
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
            Currency("gold", "12,800", 255, 326, 178);
            Currency("gems", "120", 426, 326, 163);
            var notebook = IconButton(SafeArea, "btn_notebook", "notebook", 549, 326, 52, 57);
            Icon(notebook.transform, "img_alert", "alert", 19, 23, 14, 14);
            var settings = Pair(SafeArea, "btn_settings", "round", 603, 326, 57, 60);
            Click(settings, null); Icon(settings, "img_settings", "settings", 0, 1, 41, 41);
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
            Drawer = Pair(SafeArea, "group_construction", "panel", 0, -188, 1150, 165);
            Label(Drawer, "txt_heading", "시설 배치", -479, 58, 170, 28, 20);
            Label(Drawer, "txt_help", "시설 선택 → 바닥을 눌러 이동 → 배치 확정", 0, 58, 650, 30, 17);
            PlainButton(Drawer, "btn_previous", "◀", -539, -15, 34, 50, null);
            PlainButton(Drawer, "btn_next", "▶", 539, -15, 34, 50, null);
            DemoUI.Rect(Drawer, "group_cards", 0, -13, 1030, 113);
            Actions = Pair(SafeArea, "group_placement_actions", "panel", 0, -65, 660, 66);
            PlacementLabel = Label(Actions, "txt_placement", "", -146, 0, 320, 54, 18);
            PlainButton(Actions, "btn_rotate", "회전", 63, 0, 74, 43, null);
            ConfirmButton = PlainButton(Actions, "btn_confirm", "배치", 143, 0, 74, 43, null);
            PlainButton(Actions, "btn_cancel", "취소", 223, 0, 74, 43, null);
            RemoveButton = PlainButton(Actions, "btn_remove", "회수", 295, 0, 62, 43, null);
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

        private void Currency(string key, string value, float x, float y, float width)
        {
            var bar = Pair(SafeArea, "btn_" + key, "currency", x, y, width, 45); Click(bar, null);
            Icon(bar, "img_currency", key, -width / 2 + 19, 0, 39, 39);
            Label(bar, "txt_amount", value, 6, 0, width - 69, 34, 22);
            Icon(bar, "img_add", "plus", width / 2 - 17, 0, 19, 19);
        }

        private void BuildCards()
        {
            var holder = Drawer.Find("group_cards"); DemoUI.Clear(holder);
            int start = catalogPage * 6;
            for (int i = 0; i < 6 && start + i < Placement.Catalog.Facilities.Length; i++)
            {
                var definition = Placement.Catalog.Facilities[start + i]; string id = definition.Id;
                var card = Pair(holder, "btn_facility_" + id, "panel", (i - 2.5f) * 169, 0, 160, 108);
                Click(card, () => Placement.BeginNew(id));
                var portrait = Icon(card, "img_portrait", "", 0, 17, 64, 64);
                portrait.sprite = definition.Portrait != null ? definition.Portrait : Art.Get("build");
                Label(card, "txt_name", definition.Name, 0, -27, 150, 23, 16);
                Label(card, "txt_size", definition.Footprint.x + " × " + definition.Footprint.y, 0, -46, 145, 17, 12);
            }
        }

        private void Refresh()
        {
            if (Drawer == null) return;
            Drawer.gameObject.SetActive(Placement.IsBuilding && !Modal.gameObject.activeSelf);
            Actions.gameObject.SetActive(Placement.Preview != null && !Modal.gameObject.activeSelf);
            ConfirmButton.interactable = Placement.CanConfirm;
            RemoveButton.interactable = Placement.IsMoving;
            PlacementLabel.text = Placement.Preview == null ? "" : Placement.Catalog.Find(Placement.Preview.FacilityId).Name + "\n" + Placement.PreviewReason;
            PlacementLabel.color = Placement.CanConfirm ? new Color(.14f,.35f,.13f) : new Color(.65f,.15f,.10f);
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
            var panel = OpenModal("몬스터 훈련장");
            var image = Icon(panel, "img_training", "training", -305, 10, 260, 215);
            var definition = Placement.Catalog.Find("training"); if (definition.Portrait != null) image.sprite = definition.Portrait;
            Label(panel, "txt_body", "달리기 · 장애물 · 근력 훈련\n\n포링의 탄성 점프, 우로스의 힘, 에드가의 질주를 위한 공간\n\n현재 데모 훈련: Lv.5 · 스피드 +30 · 체력 소모 감소\n이 목장에서는 훈련장의 설치와 배치를 먼저 체험합니다", 135, 18, 615, 240, 22);
            PlainButton(panel, "btn_place_training", "훈련장 배치하기", 0, -196, 286, 55, () => { CloseModal(); Placement.BeginNew("training"); });
        }

        private void ShowArena()
        {
            var panel = OpenModal("프론테라컵 경기장");
            Icon(panel, "img_arena", "arena", -305, 20, 228, 200);
            Label(panel, "txt_body", "몬스터 1마리 출전 · 자동 레이스\n\n출전 조건  Lv.5 / 스피드 260\n카툰 경기장 · 초원 경기장 2D\n\n정식 출전과 전술 선택은 기존 로비에서 진행합니다", 135, 17, 615, 252, 23);
            PlainButton(panel, "btn_dismiss", "목장으로", 0, -195, 240, 52, CloseModal);
        }

        private void ShowNotebook()
        {
            var panel = OpenModal("목장 수첩");
            Label(panel, "txt_body", "건설 메뉴에서 시설을 고르고 바닥을 누르거나 끌어 배치하세요.\n회전 후 초록색 영역에서 배치를 확정할 수 있어요.\n배치한 시설도 건설 모드에서 눌러 이동하거나 회수할 수 있어요.\n\n우클릭 드래그: 이동 / 가운데 휠 드래그: 각도 조정\n휠: 빠른 확대·축소 / 터치: 두 손가락 이동·확대·축소\nR: 회전 / Enter: 배치 / Esc: 취소\n\n배치는 이 기기에 저장됩니다. 점선 밖 구역의 확장은 준비 중입니다.", 0, -16, 935, 335, 22);
        }

        private void ShowSettings()
        {
            var panel = OpenModal("목장 설정");
            Label(panel, "txt_body", "현재 목장의 배치는 변경할 때 자동으로 저장됩니다.\n\n기본 배치로 돌리면 이 목장에서 변경한 시설 배치가 초기화됩니다.", 0, 65, 900, 135, 23);
            PlainButton(panel, "btn_reset_view", "카메라 원위치", -205, -76, 310, 57, () => { RanchCamera.ResetView(); CloseModal(); });
            PlainButton(panel, "btn_reset_layout", "기본 배치로", 205, -76, 310, 57, () =>
            {
                var confirmation = OpenModal("기본 배치로 돌아갈까요?");
                Label(confirmation, "txt_warning", "지금 저장된 목장 배치가 기본 배치로 바뀝니다.", 0, 37, 900, 96, 25);
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
