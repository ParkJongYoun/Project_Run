using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectRun.Ranch;

namespace ProjectRun.Race
{
    // A short, presentation-only menu tour. No menu actions or growth are required.
    public sealed class UIPanel_RanchGuide : UIPanel_Base
    {
        public UIPanel_Ranch Owner;
        public UIPanel_RanchCollection Monsters;
        public UIPanel_RanchCollection Training;
        public UIPanel_RanchArena Arena;
        public Sprite CharacterSprite;
        public Sprite PaperSprite;
        public Sprite FrameSprite;
        public RectTransform SafeArea;
        public RectTransform Layout;
        public GameObject Dialogue;
        public Image Character;
        public Text Message;
        public Text ContinueLabel;
        public Button AdvanceButton;
        public RectTransform Tour;
        public RectTransform TourSafeArea;
        public RectTransform Spotlight;
        public RectTransform DimTop;
        public RectTransform DimBottom;
        public RectTransform DimLeft;
        public RectTransform DimRight;
        public RectTransform Tooltip;
        public RectTransform Pointer;
        public Text TooltipTitle;
        public Text TooltipMessage;
        public Text TooltipProgress;
        public Button NextButton;
        public Text NextLabel;
        public bool IsTourOpen => Tour != null && Tour.gameObject.activeSelf;
        public bool IsDialogueOpen => (Dialogue != null && Dialogue.activeSelf) || IsTourOpen;
        public string CurrentKey { get; private set; }
        public RectTransform CurrentTarget => target;
        private int stepIndex = -1;
        private float charactersShown;
        private float openedAt;
        private RectTransform target;
        private readonly Vector3[] corners = new Vector3[4];
        private const string welcome = "우리 목장에 오신 걸 환영해요!\n친구들과 함께할 목장의 메뉴를 간단히 소개할게요.";
        private static readonly string[] buttonNames = { "btn_monsters", "btn_build", "btn_training", "btn_arena" };
        private static readonly string[] titles = { "몬스터", "시설 · 건설", "훈련", "경기장" };
        private static readonly string[] descriptions = {
            "함께하는 몬스터들의 능력치와\n각자의 스킬을 살펴볼 수 있어요.",
            "시설과 길을 배치하고 옮겨서\n나만의 목장을 꾸밀 수 있어요.",
            "훈련할 친구를 골라 성장시키고\n새로운 능력을 얻을 수 있어요.",
            "코스를 살펴보고 출전할 친구와\n전술을 정해 레이스에 참가해요."
        };
        private static readonly Color ink = new Color(.25f, .13f, .065f);
        private static readonly Color cream = new Color(1, .92f, .75f);
        private static readonly Color wood = new Color(.38f, .20f, .12f);

        public void Build()
        {
            DemoUI.Clear(transform);
            Dialogue = DemoUI.Fill(transform, "group_dialogue").gameObject;
            var dim = DemoUI.Shade(Dialogue.transform, "bg_dim", new Color(.08f, .12f, .19f, .48f));
            dim.raycastTarget = true;
            SafeArea = DemoUI.Fill(Dialogue.transform, "obj_safe_area");
            Layout = DemoUI.Rect(SafeArea, "group_composition", 0, 0, 1280, 720);
            Character = DemoUI.Image(Layout, "img_guide", null, 0, -188, 664, 996, true);
            Character.sprite = CharacterSprite;
            var box = DemoUI.Rect(Layout, "btn_dialogue", 0, -230, 1060, 180);
            var paper = Surface(box, "bg_paper", PaperSprite, 0, 0, 1060, 180, 8);
            paper.raycastTarget = true;
            Surface(box, "img_frame", FrameSprite, 0, 0, 1060, 180, 8);
            Surface(box, "bg_name", PaperSprite, -364, 94, 244, 44, 12).color = wood;
            Surface(box, "img_name_frame", FrameSprite, -364, 94, 244, 44, 12);
            DemoUI.Text(box, "txt_name", "목장 안내인", -364, 94, 216, 32, 21, cream);
            Message = DemoUI.Text(box, "txt_dialogue", "", 0, 7, 946, 96, 26, ink, TextAnchor.MiddleLeft);
            ContinueLabel = DemoUI.Text(box, "txt_continue", "메뉴 둘러보기  ▸", 305, -59, 330, 30, 17, ink, TextAnchor.MiddleRight);
            AdvanceButton = BindButton(box, paper);

            Tour = DemoUI.Fill(transform, "group_menu_tour");
            // The hole reveals the actual button, but never forwards clicks to the game.
            var blocker = DemoUI.Shade(Tour, "bg_input_blocker", Color.clear);
            blocker.raycastTarget = true;
            blocker.canvasRenderer.cullTransparentMesh = false;
            DimTop = Dim("bg_dim_top");
            DimBottom = Dim("bg_dim_bottom");
            DimLeft = Dim("bg_dim_left");
            DimRight = Dim("bg_dim_right");
            TourSafeArea = DemoUI.Fill(Tour, "obj_safe_area");
            Spotlight = DemoUI.Rect(Tour, "img_spotlight_frame", 0, 0, 200, 80);
            var spotlightFrame = Spotlight.gameObject.AddComponent<Image>();
            spotlightFrame.sprite = FrameSprite;
            spotlightFrame.type = Image.Type.Sliced;
            spotlightFrame.fillCenter = false;
            spotlightFrame.pixelsPerUnitMultiplier = 12;
            spotlightFrame.raycastTarget = false;

            Tooltip = DemoUI.Rect(Tour, "popup_menu_description", 0, 0, 352, 176);
            Pointer = DemoUI.Rect(Tooltip, "obj_pointer", 0, -87, 18, 18);
            Pointer.localRotation = Quaternion.Euler(0, 0, 45);
            Surface(Pointer, "bg_pointer", PaperSprite, 0, 0, 18, 18, 30).color = cream;
            Surface(Pointer, "img_pointer_frame", FrameSprite, 0, 0, 18, 18, 30);
            Surface(Tooltip, "bg_paper", PaperSprite, 0, 0, 352, 176, 10);
            Surface(Tooltip, "img_frame", FrameSprite, 0, 0, 352, 176, 10);
            Surface(Tooltip, "bg_title", PaperSprite, -68, 76, 180, 36, 14).color = wood;
            Surface(Tooltip, "img_title_frame", FrameSprite, -68, 76, 180, 36, 14);
            TooltipTitle = DemoUI.Text(Tooltip, "txt_title", "", -68, 76, 160, 30, 20, cream);
            TooltipMessage = DemoUI.Text(Tooltip, "txt_description", "", 0, 12, 308, 76, 21, ink, TextAnchor.MiddleLeft);
            TooltipProgress = DemoUI.Text(Tooltip, "txt_progress", "", -122, -56, 60, 30, 17, ink, TextAnchor.MiddleLeft);
            var next = DemoUI.Rect(Tooltip, "btn_next", 105, -55, 104, 38);
            var nextPaper = Surface(next, "bg_paper", PaperSprite, 0, 0, 104, 38, 14);
            nextPaper.color = wood; nextPaper.raycastTarget = true;
            Surface(next, "img_frame", FrameSprite, 0, 0, 104, 38, 14);
            NextLabel = DemoUI.Text(next, "txt_label", "다음  ▸", 0, 0, 86, 30, 18, cream);
            NextButton = BindButton(next, nextPaper);
            Dialogue.SetActive(false);
            Tour.gameObject.SetActive(false);
        }

        private static Image Surface(Transform parent, string name, Sprite sprite, float x, float y,
            float width, float height, float pixelsPerUnit)
        {
            var image = DemoUI.Image(parent, name, null, x, y, width, height);
            image.sprite = sprite; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
            return image;
        }

        private RectTransform Dim(string name)
        {
            var image = DemoUI.Shade(Tour, name, new Color(.08f, .06f, .04f, .74f));
            image.raycastTarget = true;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            return rect;
        }

        private static Button BindButton(RectTransform rect, Image graphic)
        {
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        public override void OnOpen()
        {
            if (!Application.isPlaying || AdvanceButton == null) return;
            AdvanceButton.onClick.RemoveAllListeners(); AdvanceButton.onClick.AddListener(Advance);
            NextButton.onClick.RemoveAllListeners(); NextButton.onClick.AddListener(Advance);
            Clear();
        }

        public override void OnClose() { if (Application.isPlaying) Clear(); }

        public void EnterHome()
        {
            Clear();
            if (isActiveAndEnabled && Mgr_Guide.Enabled) StartCoroutine(WelcomeWhenReady());
        }

        private IEnumerator WelcomeWhenReady()
        {
            yield return null;
            while (Mgr_Game.Instance != null && Mgr_Game.Instance.IsTransitioning) yield return null;
            if (CollectionOpen || Owner == null || Owner.Placement.IsBuilding || Owner.Modal.gameObject.activeSelf) yield break;
            if (!Mgr_Guide.TryShow("ranch_welcome")) yield break;
            CurrentKey = "ranch_welcome"; stepIndex = -1;
            charactersShown = 0; Message.text = ""; openedAt = Time.unscaledTime;
            Dialogue.SetActive(true);
            Owner.Placement.InputBlocked = true;
            Owner.RanchCamera.StopMotion();
            EventSystem.current?.SetSelectedGameObject(AdvanceButton.gameObject);
        }

        public void Advance()
        {
            if (!IsDialogueOpen) return;
            if (!IsTourOpen && charactersShown < welcome.Length)
            {
                charactersShown = welcome.Length; Message.text = welcome; return;
            }
            if (++stepIndex >= buttonNames.Length) { Clear(); return; }
            target = Owner.Navigation.Find(buttonNames[stepIndex]) as RectTransform;
            if (target == null || !target.gameObject.activeInHierarchy) { Clear(); return; }
            Dialogue.SetActive(false);
            Tour.gameObject.SetActive(true);
            CurrentKey = "ranch_tour_" + buttonNames[stepIndex].Substring(4);
            TooltipTitle.text = titles[stepIndex];
            TooltipMessage.text = descriptions[stepIndex];
            TooltipProgress.text = (stepIndex + 1) + " / " + buttonNames.Length;
            NextLabel.text = stepIndex == buttonNames.Length - 1 ? "완료  ▸" : "다음  ▸";
            EventSystem.current?.SetSelectedGameObject(NextButton.gameObject);
            Canvas.ForceUpdateCanvases();
            PositionTour();
        }

        public void Clear()
        {
            StopAllCoroutines(); target = null; stepIndex = -1; CurrentKey = null;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && selected.transform.IsChildOf(transform))
                EventSystem.current.SetSelectedGameObject(null);
            if (Dialogue != null) Dialogue.SetActive(false);
            if (Tour != null) Tour.gameObject.SetActive(false);
            if (Owner != null && Owner.Placement != null)
                Owner.Placement.InputBlocked = CollectionOpen || (Owner.Modal != null && Owner.Modal.gameObject.activeSelf);
        }

        private bool CollectionOpen => (Monsters != null && Monsters.gameObject.activeInHierarchy)
            || (Training != null && Training.gameObject.activeInHierarchy) || (Arena != null && Arena.gameObject.activeInHierarchy);

        private void LateUpdate()
        {
            if (!Mgr_Guide.Enabled) { if (IsDialogueOpen) Clear(); return; }
            DemoUI.SafeArea(SafeArea);
            if (Layout != null) Layout.localScale = Vector3.one * Mathf.Min(1, SafeArea.rect.width / 1280f, SafeArea.rect.height / 720f);
            if (Dialogue != null && Dialogue.activeSelf)
            {
                charactersShown = Mathf.Min(welcome.Length, charactersShown + Time.unscaledDeltaTime * 45);
                Message.text = welcome.Substring(0, Mathf.FloorToInt(charactersShown));
                float arrival = Mathf.SmoothStep(0, 1, (Time.unscaledTime - openedAt) / .3f);
                Character.rectTransform.anchoredPosition = new Vector2(0, -188 - (1 - arrival) * 12 + Mathf.Sin(Time.unscaledTime * 1.4f) * 1.4f);
            }
            if (!IsTourOpen) return;
            if (target == null || !target.gameObject.activeInHierarchy || CollectionOpen || Owner.Modal.gameObject.activeSelf) { Clear(); return; }
            PositionTour();
        }

        private void PositionTour()
        {
            DemoUI.SafeArea(TourSafeArea);
            var bounds = Tour.rect;
            target.GetWorldCorners(corners);
            var min = Tour.InverseTransformPoint(corners[0]);
            var max = Tour.InverseTransformPoint(corners[2]);
            float left = Mathf.Clamp(min.x - 4, bounds.xMin, bounds.xMax);
            float right = Mathf.Clamp(max.x + 4, left, bounds.xMax);
            float bottom = Mathf.Clamp(min.y - 4, bounds.yMin, bounds.yMax);
            float top = Mathf.Clamp(max.y + 14, bottom, bounds.yMax);
            SetBounds(DimTop, bounds.xMin, top, bounds.xMax, bounds.yMax);
            SetBounds(DimBottom, bounds.xMin, bounds.yMin, bounds.xMax, bottom);
            SetBounds(DimLeft, bounds.xMin, bottom, left, top);
            SetBounds(DimRight, right, bottom, bounds.xMax, top);
            SetBounds(Spotlight, left, bottom, right, top);

            TourSafeArea.GetWorldCorners(corners);
            var safeMin = Tour.InverseTransformPoint(corners[0]);
            var safeMax = Tour.InverseTransformPoint(corners[2]);
            float scale = Mathf.Min(1, (safeMax.x - safeMin.x) / 1280f, (safeMax.y - safeMin.y) / 720f);
            Tooltip.localScale = Vector3.one * scale;
            float halfWidth = Tooltip.sizeDelta.x * .5f * scale;
            float halfHeight = Tooltip.sizeDelta.y * .5f * scale;
            float center = (left + right) * .5f;
            float x = Mathf.Clamp(center, safeMin.x + halfWidth + 12 * scale, safeMax.x - halfWidth - 12 * scale);
            float y = Mathf.Clamp(top + halfHeight + 20 * scale, safeMin.y + halfHeight + 12 * scale, safeMax.y - halfHeight - 20 * scale);
            Tooltip.anchoredPosition = new Vector2(x, y);
            Pointer.anchoredPosition = new Vector2(Mathf.Clamp((center - x) / scale, -150, 150), -87);
        }

        private static void SetBounds(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchoredPosition = new Vector2((left + right) * .5f, (bottom + top) * .5f);
            rect.sizeDelta = new Vector2(right - left, top - bottom);
        }
    }
}
