using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Ranch;

namespace ProjectRun.Race
{
    // Presentation-only guidance. Progress belongs to Mgr_Guide's current session.
    public sealed class UIPanel_RanchGuide : UIPanel_Base
    {
        public UIPanel_Ranch Owner;
        public UIPanel_RanchCollection Monsters;
        public UIPanel_RanchCollection Training;
        public UIPanel_RanchArena Arena;
        public Sprite CharacterSprite;
        public RectTransform SafeArea;
        public RectTransform Layout;
        public GameObject Dialogue;
        public Image Character;
        public Text Message;
        public Text ContinueLabel;
        public Button AdvanceButton;
        public Button SkipButton;
        public RectTransform Highlight;
        public Image HighlightFrame;
        public Text Hint;
        public bool IsDialogueOpen => Dialogue != null && Dialogue.activeSelf;
        public string CurrentKey { get; private set; }
        private string[] lines;
        private int lineIndex;
        private float charactersShown;
        private float openedAt;
        private string fullMessage;
        private RectTransform target;
        private RectTransform afterTarget;
        private string afterHint;
        private readonly Vector3[] corners = new Vector3[4];

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
            var box = DemoUI.Rect(Layout, "btn_dialogue", 0, -237, 1060, 164);
            var paper = DemoUI.Image(box, "bg_paper", "dialog_bg", 0, 0, 1060, 164);
            paper.color = new Color(1, 1, 1, .95f); paper.raycastTarget = true;
            var frame = DemoUI.Image(box, "img_frame", "dialog_frame", 0, 0, 1060, 164);
            frame.pixelsPerUnitMultiplier = 2;
            var name = DemoUI.Image(box, "bg_name", "dialog_bg", -355, 87, 230, 36);
            name.color = new Color(.98f, .65f, .76f);
            var nameFrame = DemoUI.Image(box, "img_name_frame", "dialog_frame", -355, 87, 230, 36);
            nameFrame.pixelsPerUnitMultiplier = 3;
            DemoUI.Text(box, "txt_name", "목장 안내인", -355, 87, 212, 31, 21, new Color(.38f, .18f, .22f));
            Message = DemoUI.Text(box, "txt_dialogue", "", 0, 9, 946, 96, 26, new Color(.20f, .23f, .29f), TextAnchor.MiddleLeft);
            ContinueLabel = DemoUI.Text(box, "txt_continue", "", 305, -58, 330, 30, 17, new Color(.47f, .42f, .37f), TextAnchor.MiddleRight);
            AdvanceButton = box.gameObject.AddComponent<Button>(); AdvanceButton.targetGraphic = paper;
            AdvanceButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var skip = DemoUI.Image(Layout, "btn_skip_all", "dialog_bg", 511, 311, 174, 42);
            skip.color = new Color(1, 1, 1, .92f); skip.raycastTarget = true;
            DemoUI.Image(skip.transform, "img_frame", "dialog_frame", 0, 0, 174, 42).pixelsPerUnitMultiplier = 3;
            DemoUI.Text(skip.transform, "txt_label", "전부 스킵  »", 0, 0, 154, 38, 19, new Color(.35f, .29f, .26f));
            SkipButton = skip.gameObject.AddComponent<Button>(); SkipButton.targetGraphic = skip;
            SkipButton.navigation = new Navigation { mode = Navigation.Mode.None };
            Highlight = DemoUI.Rect(transform, "group_highlight", 0, 0, 200, 80);
            HighlightFrame = DemoUI.Shade(Highlight, "img_target_frame", Color.white);
            HighlightFrame.sprite = DemoUI.Art.Get("card_selected"); HighlightFrame.type = Image.Type.Sliced;
            var hintBg = DemoUI.Image(Highlight, "bg_hint", "dialog_bg", 0, 65, 272, 38);
            hintBg.color = new Color(1, .98f, .91f);
            Hint = DemoUI.Text(hintBg.transform, "txt_hint", "", 0, 0, 252, 34, 18, DemoUI.Ink);
            Dialogue.SetActive(false); Highlight.gameObject.SetActive(false);
        }

        public override void OnOpen()
        {
            if (!Application.isPlaying || AdvanceButton == null) return;
            AdvanceButton.onClick.RemoveAllListeners(); AdvanceButton.onClick.AddListener(Advance);
            SkipButton.onClick.RemoveAllListeners(); SkipButton.onClick.AddListener(SkipAll);
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
            if (!Mgr_Guide.HasShown("ranch_welcome"))
                ShowOnce("ranch_welcome", new[] {
                    "우리 목장에 오신 걸 환영해요!\n저와 함께 친구들을 돌보고 첫 레이스를 준비해 볼까요?",
                    "포링, 미노타우르스, 페코페코가 기다리고 있어요.\n먼저 아래 ‘몬스터’를 눌러 친구들의 능력을 살펴봐요."
                }, HomeButton("btn_monsters"), "몬스터 버튼을 눌러 보세요");
            else if (AnyTrained && !Mgr_Guide.HasShown("ranch_ready"))
                ShowOnce("ranch_ready", new[] { "이제 레이스에 출전할 준비가 됐어요!\n‘경기장’에서 코스를 보고 함께 달릴 친구를 골라 주세요." }, HomeButton("btn_arena"), "경기장으로 출발해요");
            else if (Mgr_Guide.HasShown("ranch_monsters") && !Mgr_Guide.HasShown("ranch_training"))
            {
                if (!ShowOnce("ranch_open_training", new[] { "친구들의 능력을 확인했나요?\n이번에는 ‘훈련’에서 성장시킬 친구를 직접 골라 봐요." }, HomeButton("btn_training"), "훈련 버튼을 눌러 보세요"))
                    PointTo(HomeButton("btn_training"), "훈련 버튼을 눌러 보세요");
            }
            else if (!Mgr_Guide.HasShown("ranch_monsters")) PointTo(HomeButton("btn_monsters"), "몬스터 버튼을 눌러 보세요");
        }

        public void EnterCollection(UIPanel_RanchCollection panel)
        {
            Clear();
            if (panel.TrainingMode)
                ShowOnce("ranch_training", new[] { "훈련할 친구를 고른 뒤 ‘훈련 시작’을 눌러 주세요.\n훈련을 끝까지 마치면 Lv.5, 스피드 +30과 후천 스킬을 얻어요." });
            else ShowOnce("ranch_monsters", new[] { "초상을 누르면 친구의 능력치와 스킬을 볼 수 있어요.\n여기서 살펴본 친구와 훈련·출전할 친구는 따로 고를 수 있답니다." });
        }

        public void TrainingCompleted()
        {
            ShowOnce("ranch_training_complete", new[] { "잘했어요! 훈련을 끝까지 마쳐서 한층 성장했어요.\n목장으로 돌아가 ‘경기장’에서 첫 레이스를 준비해 봐요." }, Training.BackButton.transform as RectTransform, "목장으로 돌아가요");
        }

        public void EnterArena()
        {
            Clear();
            ShowOnce("ranch_arena", new[] { "여기가 프론테라컵 경기장이에요.\n먼저 오른쪽 위 ‘수첩’에서 코스를 살펴보고 출전을 준비해요." }, Arena.NotebookButton.transform as RectTransform, "수첩에서 코스를 확인해요");
        }

        public void EnterNotebook()
        {
            Clear(); Mgr_Guide.TryShow("ranch_notebook");
        }

        public void EnterEntry()
        {
            Clear();
            ShowOnce("ranch_entry", new[] { "이번 경기에 출전할 친구 한 마리를 골라 주세요.\n추천 전술을 확인하고 확정하면 여섯 친구의 레이스가 시작돼요." });
        }

        public bool ShowOnce(string key, string[] messages, RectTransform nextTarget = null, string nextHint = null)
        {
            if (!Mgr_Guide.TryShow(key)) return false;
            Clear(); CurrentKey = key; lines = messages; lineIndex = 0;
            afterTarget = nextTarget; afterHint = nextHint;
            Dialogue.SetActive(true);
            if (Owner != null) { Owner.Placement.InputBlocked = true; Owner.RanchCamera.StopMotion(); }
            ShowLine(); return true;
        }

        private void ShowLine()
        {
            fullMessage = lines[lineIndex]; charactersShown = 0; Message.text = ""; openedAt = Time.unscaledTime;
            ContinueLabel.text = lines.Length > 1 ? (lineIndex + 1) + " / " + lines.Length + "     눌러서 계속  ▸" : "눌러서 계속  ▸";
        }

        public void Advance()
        {
            if (!IsDialogueOpen) return;
            if (charactersShown < fullMessage.Length) { charactersShown = fullMessage.Length; Message.text = fullMessage; return; }
            if (++lineIndex < lines.Length) { ShowLine(); return; }
            var next = afterTarget; string caption = afterHint;
            Clear(); PointTo(next, caption);
        }

        public void SkipAll() { Mgr_Guide.SetEnabled(false); Clear(); }

        public void Clear()
        {
            StopAllCoroutines(); target = null; afterTarget = null; lines = null; CurrentKey = null;
            if (Dialogue != null) Dialogue.SetActive(false);
            if (Highlight != null) Highlight.gameObject.SetActive(false);
            if (Owner != null && Owner.Placement != null)
                Owner.Placement.InputBlocked = CollectionOpen || (Owner.Modal != null && Owner.Modal.gameObject.activeSelf);
        }

        private bool CollectionOpen => (Monsters != null && Monsters.gameObject.activeInHierarchy)
            || (Training != null && Training.gameObject.activeInHierarchy) || (Arena != null && Arena.gameObject.activeInHierarchy);
        private bool AnyTrained
        {
            get { foreach (var progress in Mgr_User.Data.Monsters) if (progress.Trained) return true; return false; }
        }
        private RectTransform HomeButton(string name) => Owner.Navigation.Find(name) as RectTransform;
        private void PointTo(RectTransform value, string caption)
        {
            if (!Mgr_Guide.Enabled || value == null || !value.gameObject.activeInHierarchy) return;
            target = value; Hint.text = caption; Highlight.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!Mgr_Guide.Enabled) { if (IsDialogueOpen || target != null) Clear(); return; }
            DemoUI.SafeArea(SafeArea);
            if (Layout != null) Layout.localScale = Vector3.one * Mathf.Min(1, SafeArea.rect.width / 1280f, SafeArea.rect.height / 720f);
            if (IsDialogueOpen)
            {
                charactersShown = Mathf.Min(fullMessage.Length, charactersShown + Time.unscaledDeltaTime * 45);
                Message.text = fullMessage.Substring(0, Mathf.FloorToInt(charactersShown));
                // Small arrival movement, then a quiet breathing motion.
                float arrival = Mathf.SmoothStep(0, 1, (Time.unscaledTime - openedAt) / .3f);
                Character.rectTransform.anchoredPosition = new Vector2(0, -188 - (1 - arrival) * 12 + Mathf.Sin(Time.unscaledTime * 1.4f) * 1.4f);
                return;
            }
            if (target == null) return;
            if (!target.gameObject.activeInHierarchy || (Owner.Modal != null && Owner.Modal.gameObject.activeSelf)) { Clear(); return; }
            target.GetWorldCorners(corners);
            var min = transform.InverseTransformPoint(corners[0]); var max = transform.InverseTransformPoint(corners[2]);
            Highlight.anchoredPosition = (min + max) * .5f;
            Highlight.sizeDelta = new Vector2(max.x - min.x + 14, max.y - min.y + 12);
            HighlightFrame.color = new Color(1, 1, 1, .72f + .28f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.6f)));
            var hintRect = Hint.transform.parent as RectTransform;
            float offset = Highlight.sizeDelta.y * .5f + 27;
            hintRect.anchoredPosition = new Vector2(0, max.y > 220 ? -offset : offset);
        }
    }
}
