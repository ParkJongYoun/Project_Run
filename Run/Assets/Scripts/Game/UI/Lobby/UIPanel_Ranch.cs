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
            Drawer = Pair(SafeArea, "group_construction", "bottom", 0, -261, 1260, 190);
            var activeBuild=Pair(Drawer,"group_build_selected","menu",-520,42,184,72);
            activeBuild.Find("bg_surface").GetComponent<Image>().color=new Color(1,.78f,.25f);
            Icon(activeBuild,"img_icon","build",-53,2,63,63);
            Label(activeBuild,"txt_label","건설",30,0,104,55,29);
            var closeBuild=Pair(Drawer,"btn_close_build","menu",-520,-40,184,65);
            Click(closeBuild,null);Label(closeBuild,"txt_cross","×",-56,0,45,54,42);
            Label(closeBuild,"txt_label","닫기",25,0,110,52,27);
            var catalog=Pair(Drawer,"group_catalog","panel",96,0,1036,170);
            Label(catalog, "txt_heading", "시설 배치", -420, 61, 140, 28, 22);
            Label(catalog, "txt_help", "시설 선택 → 바닥을 눌러 이동 → 배치 확정", 72, 61, 720, 30, 18);
            PlainButton(catalog, "btn_previous", "◀", -490, -15, 32, 43, null);
            PlainButton(catalog, "btn_next", "▶", 490, -15, 32, 43, null);
            DemoUI.Rect(catalog, "group_cards", 0, -16, 922, 125);
            Actions = Pair(SafeArea, "group_placement_actions", "panel", 0, -126, 660, 66);
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
            if(Placement!=null && Placement.Catalog!=null) BuildCards();
        }

        private void BuildCards()
        {
            var holder = Drawer.Find("group_catalog/group_cards"); DemoUI.Clear(holder);
            int start = catalogPage * 6;
            for (int i = 0; i < 6 && start + i < Placement.Catalog.Facilities.Length; i++)
            {
                var definition = Placement.Catalog.Facilities[start + i]; string id = definition.Id;
                var card = Pair(holder, "btn_facility_" + id, "panel", (i - 2.5f) * 151, 0, 143, 125);
                Click(card, () => Placement.BeginNew(id));
                var portrait = Icon(card, "img_portrait", "", 0, 17, 126, 88);
                portrait.sprite = definition.Portrait != null ? definition.Portrait : Art.Get("build");
                Label(card, "txt_name", definition.Name, 0, -33, 136, 24, 19);
                Label(card, "txt_size", definition.Footprint.x + " × " + definition.Footprint.y, 0, -53, 135, 18, 15);
            }
        }

        private void Refresh()
        {
            if (Drawer == null) return;
            Drawer.gameObject.SetActive(Placement.IsBuilding && !Modal.gameObject.activeSelf);
            Navigation.gameObject.SetActive(!Placement.IsBuilding && !Modal.gameObject.activeSelf);
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
            Label(panel, "txt_body", "건설 메뉴에서 시설이나 흙길을 고르고 바닥을 탭하세요.\n초록색 영역에서 배치를 확정할 수 있어요.\n배치한 시설을 탭하면 이동하거나 회수할 수 있어요.\n몬스터는 서로 연결된 길을 따라 돌아다닙니다.\n\n좌클릭 드래그 / 한 손가락 스와이프: 화면 이동\n우클릭 드래그: 좌우 회전 / 우클릭: 원래 각도\n+/− 버튼·휠·두 손가락 벌리기: 5단계 확대·축소\nR: 시설 회전 / Enter: 배치 / Esc: 취소\n\n데모의 배치는 저장하지 않으며 새 실행에 기본 배치로 시작합니다.", 0, -16, 935, 335, 22);
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
