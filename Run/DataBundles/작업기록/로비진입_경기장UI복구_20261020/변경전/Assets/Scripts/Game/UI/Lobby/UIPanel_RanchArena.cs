using UnityEngine;

namespace ProjectRun.Race
{
    // The scenic exhibition is presentation only; entry uses the shared demo table and rules.
    public sealed class UIPanel_RanchArena : UIPanel_Base
    {
        public RectTransform SafeArea;
        public RectTransform Layout;
        [Range(.5f, 1f)] public float LayoutScale = .94f;
        public UnityEngine.UI.Button BackButton;
        public UnityEngine.UI.Button EntryButton;
        public UnityEngine.UI.Button NotebookButton;
        public UnityEngine.UI.Button SettingsButton;
        public UnityEngine.UI.Text Requirements;
        public GameObject EntryPopup;
        public UnityEngine.UI.Button EntryClose;
        public UnityEngine.UI.Button[] EntrantButtons;
        public UnityEngine.UI.Image[] EntrantFrames;
        public UnityEngine.UI.Image[] EntrantPortraits;
        public UnityEngine.UI.Text[] EntrantNames;
        public UnityEngine.UI.Text[] EntrantStats;
        public UnityEngine.UI.Text[] EntrantStates;
        public UnityEngine.UI.Text TacticDescription;
        public UnityEngine.UI.Button ConfirmEntry;
        public UnityEngine.UI.Text ConfirmLabel;
        public GameObject NotebookPopup;
        public UnityEngine.UI.Button NotebookClose;
        public UnityEngine.UI.Button NotebookCloseX;   // 수첩 오른쪽 위 X 버튼
        public GameObject SettingsPopup;
        public UnityEngine.UI.Button SettingsClose;
        public UnityEngine.UI.Button SoundButton;
        public UnityEngine.UI.Text SoundLabel;

        private UIPanel_Ranch owner;
        private int selectedId = -1;
        public int SelectedId => selectedId;

        public void SetOwner(UIPanel_Ranch value) => owner = value;

        public override void OnOpen()
        {
            if (!Application.isPlaying || BackButton == null) return;
            SkinNotebook();   // 경기장 수첩 팝업을 새 이미지로 꾸민다 (최초 1회)
            SkinEntrants();   // 출전할 친구 팝업을 새 이미지로 꾸민다 (최초 1회)
            Bind(BackButton, ReturnToRanch);
            Bind(EntryButton, ShowEntrants);
            Bind(NotebookButton, () => { ClosePopups(); NotebookPopup.SetActive(true); });
            Bind(SettingsButton, () => { ClosePopups(); RefreshSound(); SettingsPopup.SetActive(true); });
            Bind(EntryClose, ClosePopups);
            Bind(NotebookClose, ClosePopups);
            Bind(NotebookCloseX, ClosePopups);
            Bind(SettingsClose, ClosePopups);
            Bind(ConfirmEntry, ConfirmSelection);
            Bind(SoundButton, ToggleSound);
            for (int i = 0; i < EntrantButtons.Length; i++)
            {
                int id = i;
                Bind(EntrantButtons[i], () => SelectEntrant(id));
            }
            Requirements.text = "훈련 없이도 출전 가능";
            var entryDescription = EntryPopup.transform.Find("group_content/txt_description");
            if (entryDescription != null)
                entryDescription.GetComponent<UnityEngine.UI.Text>().text = "함께 달릴 친구를 선택하고 출전하세요";
            ClosePopups();
        }

        public override void OnClose() => ClosePopups();

        private static void Bind(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        private void Update()
        {
            DemoUI.SafeArea(SafeArea);
            if (SafeArea != null && Layout != null)
                Layout.localScale = Vector3.one * LayoutScale * Mathf.Min(1, SafeArea.rect.width / 1280f, SafeArea.rect.height / 720f);
        }

        // ── 경기장 수첩 스킨 ─────────────────────────────────────
        // 수첩 팝업은 에디터 빌더가 만든 프리팹이고, 빌더는 프리팹이 이미 있으면 다시 만들지 않는다.
        // 그래서 실행 시 코드로 팝업 내용을 새 이미지(프레임/배너/버튼)로 다시 꾸민다.
        private bool notebookSkinned;              // 한 번만 적용하기 위한 표시
        private const float NK = 0.81f;            // 시안(목업) 좌표 → 캔버스(1280x720) 배율
        private static readonly Color NoteInk  = new Color(.27f, .13f, .065f);   // 제목·소제목 글자색
        private static readonly Color NoteBody = new Color(.42f, .27f, .17f);    // 본문 글자색
        private static readonly Color NoteLineColor = new Color(.78f, .62f, .42f, .55f); // 구분선 색

        // 실행 중 팝업을 열 때 호출된다. 이미 프리팹에 구워져 있으면 아무것도 하지 않는다.
        private void SkinNotebook()
        {
            if (notebookSkinned) return;
            ApplyNotebookSkin(Mgr_Bundle.Load<Data_DemoArt>("UI/Ranch/Data_RanchArt"));
        }

        // 수첩 팝업을 새 이미지로 꾸민다. 실행 중에도, 에디터에서 프리팹에 구워 넣을 때도 쓴다. 새로 적용했으면 true.
        public bool ApplyNotebookSkin(Data_DemoArt art)
        {
            if (notebookSkinned || NotebookPopup == null) return false;
            if (art == null || art.Get("arena_notebook_frame") == null) return false;   // 이미지가 아직 임포트되지 않았으면 기존 모양 유지
            var body = NotebookPopup.transform.Find("group_content") as RectTransform;
            if (body == null) return false;
            if (body.Find("img_notebook_frame") != null)   // 이미 프리팹에 구워져 있음: 연결만 확인
            {
                notebookSkinned = true;
                var savedX = body.Find("btn_close_x");
                if (NotebookCloseX == null && savedX != null) NotebookCloseX = savedX.GetComponent<UnityEngine.UI.Button>();
                return false;
            }
            notebookSkinned = true;

            // 기존 팝업에서 재사용할 것(경기장 사진, 글꼴)을 먼저 챙긴다
            var oldArena = body.Find("obj_photo_clip/img_arena");
            Sprite arenaSprite = oldArena != null ? oldArena.GetComponent<UnityEngine.UI.Image>().sprite : (DemoUI.Art != null ? DemoUI.Art.Get("arena") : null);
            var oldText = body.GetComponentInChildren<UnityEngine.UI.Text>(true);
            Font font = oldText != null ? oldText.font : null;

            // 기존 내용(프레임, 제목, 버튼 등)을 모두 치운다
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                body.GetChild(i).gameObject.SetActive(false);
                RemoveObject(body.GetChild(i).gameObject);
            }
            // body를 화면 전체(1280x720) 크기로 만들어 아래 요소를 화면 좌표로 배치한다
            body.anchorMin = body.anchorMax = body.pivot = new Vector2(0, 1);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = new Vector2(1280, 720);

            // 아래 숫자는 모두 시안 좌표(좌상단 x, y, 너비, 높이)이다. 어긋나면 이 숫자만 조정하면 된다.
            NoteImage(body, "img_notebook_frame", art.Get("arena_notebook_frame"), 43f, 156f, 1318f, 759f);   // 종이 묶음

            // 경기장 사진: 프레임 안쪽 칸에 맞춰 잘라서(RectMask2D) 보여 준다
            var clip = NoteRect(body, "obj_photo_clip", 215f, 364f, 578f, 320f);
            clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var photo = NoteImage(clip, "img_arena", arenaSprite, 0, 0, 0, 0);
            var pr = photo.rectTransform;
            pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(.5f, .5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(clip.sizeDelta.x, clip.sizeDelta.x * 9f / 16f);   // 16:9 사진을 칸 너비에 맞춤

            NoteImage(body, "img_flower", art.Get("arena_notebook_flower"), 715f, 606f, 79f, 80f);           // 사진 모서리에 걸친 꽃
            NoteImage(body, "img_title", art.Get("arena_notebook_title"), 431f, 120f, 627f, 151f);           // 제목 배너("경기장 수첩" 글자 포함)

            // 사진 아래 설명 띠
            var plate = NoteImage(body, "bg_photo_note", null, 275f, 733f, 420f, 57f);
            plate.color = new Color(.93f, .82f, .62f, .55f);
            NoteText(body, "txt_photo_note", "흙과 잔디의 순환 코스", font, 21, NoteInk, true, TextAnchor.MiddleCenter, 275f, 733f, 420f, 57f);

            // 오른쪽 설명 영역: 소제목 / 본문 / 구분선
            NoteText(body, "txt_head_character", "코스의 성격", font, 22, NoteInk, true, TextAnchor.MiddleLeft, 890f, 372f, 375f, 32f);
            NoteLine(body, "line_1", 885f, 406f, 380f);
            NoteText(body, "txt_body_character", "완만한 커브 · 마지막 직선의 접전", font, 20, NoteBody, false, TextAnchor.MiddleLeft, 890f, 415f, 375f, 30f);
            NoteLine(body, "line_2", 885f, 466f, 380f);
            NoteText(body, "txt_head_ability", "잘 맞는 능력", font, 22, NoteInk, true, TextAnchor.MiddleLeft, 890f, 490f, 375f, 32f);
            NoteLine(body, "line_3", 885f, 523f, 280f);
            NoteText(body, "txt_body_ability", "스피드: 직선 최고 속도\n근력: 코너를 빠져나오는 재가속", font, 20, NoteBody, false, TextAnchor.MiddleLeft, 890f, 530f, 375f, 72f).lineSpacing = 1.2f;
            NoteLine(body, "line_4", 885f, 619f, 380f);
            NoteText(body, "txt_body_stamina", "체력을 남겨 막판에 힘을 내요", font, 20, NoteBody, false, TextAnchor.MiddleLeft, 890f, 642f, 375f, 30f);

            // 버튼: "수첩 닫기"(아래)와 X(오른쪽 위) 모두 팝업을 닫는다. NotebookClose는 OnOpen에서 ClosePopups에 연결된다
            NotebookClose = NoteButton(body, "btn_close_notebook", art.Get("arena_notebook_close"), 518f, 829f, 406f, 89f);
            NotebookCloseX = NoteButton(body, "btn_close_x", art.Get("arena_notebook_x"), 1216f, 123f, 142f, 126f);   // X 버튼 (OnOpen에서 닫기에 연결)
            return true;
        }

        // 시안 좌표(좌상단 x, y)를 캔버스 좌표로 바꾼다 (시안 중심 702,518 → 화면 중심 640,360)
        private static Vector2 NotePoint(float mx, float my) => new Vector2((mx - 702f) * NK + 640f, (my - 518f) * NK + 360f);

        // 시안 좌표로 위치와 크기를 주어 빈 RectTransform을 만든다 (좌상단 기준)
        private static RectTransform NoteRect(Transform parent, string name, float mx, float my, float mw, float mh)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var p = NotePoint(mx, my);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(p.x, -p.y);
            rect.sizeDelta = new Vector2(mw * NK, mh * NK);
            return rect;
        }

        // 이미지 한 장을 시안 좌표로 배치한다 (클릭은 받지 않는다)
        private static UnityEngine.UI.Image NoteImage(Transform parent, string name, Sprite sprite, float mx, float my, float mw, float mh)
        {
            var image = NoteRect(parent, name, mx, my, mw, mh).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        // 글자 한 덩어리를 시안 좌표로 배치한다 (size는 캔버스 기준 글자 크기)
        private static UnityEngine.UI.Text NoteText(Transform parent, string name, string value, Font font, int size, Color color, bool bold, TextAnchor align, float mx, float my, float mw, float mh)
        {
            var text = NoteRect(parent, name, mx, my, mw, mh).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font; text.fontSize = size; text.text = value; text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = align; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // 가로 구분선(얇은 띠)을 만든다
        private static void NoteLine(Transform parent, string name, float mx, float my, float mw)
        {
            var line = NoteImage(parent, name, null, mx, my, mw, 2.2f);
            line.color = NoteLineColor;
        }

        // 이미지 한 장을 버튼으로 만든다 (눌렀을 때 살짝 어두워짐)
        private static UnityEngine.UI.Button NoteButton(Transform parent, string name, Sprite sprite, float mx, float my, float mw, float mh)
        {
            var image = NoteImage(parent, name, sprite, mx, my, mw, mh);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var colors = button.colors; colors.pressedColor = new Color(.82f, .82f, .82f); button.colors = colors;
            return button;
        }

        // ── 출전할 친구 팝업 스킨 ───────────────────────────────────
        // 이 팝업도 에디터 빌더가 만든 프리팹이라, 실행 시 코드로 새 이미지(프레임/제목/카드/버튼)로 다시 꾸민다.
        // 좌표는 모두 캔버스(1280x720) 기준 좌상단 픽셀이다. 어긋나면 이 숫자만 조정하면 된다.
        private bool entrantsSkinned;                 // 한 번만 적용하기 위한 표시
        private GameObject confirmEnabledLook;        // 확정 버튼: 선택했을 때 모양(기존 갈색 버튼)
        private GameObject confirmDisabledLook;       // 확정 버튼: 선택 전 모양(회색 버튼)
        private static readonly Color EntryCream = new Color(1f, .93f, .76f);    // 이름·버튼 글자색
        private static readonly Color EntryOutline = new Color(.20f, .09f, .03f, .9f); // 글자 테두리색

        // 실행 중 팝업을 열 때 호출된다. 이미 프리팹에 구워져 있으면 연결만 확인한다.
        private void SkinEntrants()
        {
            if (entrantsSkinned) return;
            ApplyEntrantsSkin(Mgr_Bundle.Load<Data_DemoArt>("UI/Ranch/Data_RanchArt"),
                              Mgr_Bundle.Load<Data_DemoArt>("UI/RanchCollection/Data_RanchCollectionArt"));
        }

        // 출전할 친구 팝업을 새 이미지로 꾸민다. 실행 중에도, 에디터에서 프리팹에 구워 넣을 때도 쓴다. 새로 적용했으면 true.
        public bool ApplyEntrantsSkin(Data_DemoArt art, Data_DemoArt collectionArt)
        {
            if (entrantsSkinned || EntryPopup == null) return false;
            if (art == null || art.Get("arena_entry_frame") == null) return false;   // 이미지가 아직 임포트되지 않았으면 기존 모양 유지
            var body = EntryPopup.transform.Find("group_content") as RectTransform;
            if (body == null || ConfirmEntry == null || ConfirmLabel == null) return false;
            if (body.Find("img_entry_frame") != null)   // 이미 프리팹에 구워져 있음: 확정 버튼 모양 연결만 다시 찾는다
            {
                entrantsSkinned = true;
                var savedEnabled = ConfirmEntry.transform.Find("group_enabled");
                var savedDisabled = ConfirmEntry.transform.Find("img_disabled");
                confirmEnabledLook = savedEnabled != null ? savedEnabled.gameObject : null;
                confirmDisabledLook = savedDisabled != null ? savedDisabled.gameObject : null;
                return false;
            }
            entrantsSkinned = true;

            // 기존 팝업에서 재사용할 것(글꼴, 확정 버튼 이미지, 선택 테두리, 제목 책 아이콘)을 먼저 챙긴다
            Font font = ConfirmLabel.font;
            var oldBg = ConfirmEntry.transform.Find("bg_surface");
            var oldFrame = ConfirmEntry.transform.Find("img_frame");
            Sprite confirmBg = oldBg != null ? oldBg.GetComponent<UnityEngine.UI.Image>().sprite : null;
            Sprite confirmFrame = oldFrame != null ? oldFrame.GetComponent<UnityEngine.UI.Image>().sprite : null;
            Sprite selectedSprite = EntrantFrames != null && EntrantFrames.Length > 0 ? EntrantFrames[0].sprite : null;
            Sprite bookIcon = collectionArt != null ? collectionArt.Get("icon_encyclopedia") : null;

            // 기존 내용을 모두 치우고, body를 화면 전체(1280x720) 크기로 만든다
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                body.GetChild(i).gameObject.SetActive(false);
                RemoveObject(body.GetChild(i).gameObject);
            }
            body.anchorMin = body.anchorMax = body.pivot = new Vector2(0, 1);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = new Vector2(1280, 720);

            // 1) 큰 나무 프레임(뒤쪽) → 제목 판자 → 제목 글자
            AbsImage(body, "img_entry_frame", art.Get("arena_entry_frame"), 51, 22, 1160, 690);
            AbsImage(body, "img_entry_title", art.Get("arena_entry_title"), 350, -6, 554, 204);
            if (bookIcon != null) AbsImage(body, "img_book", bookIcon, 418, 58, 92, 88).preserveAspect = true;
            AbsText(body, "txt_title", "출전할 친구", font, 44, EntryCream, true, TextAnchor.MiddleCenter, 520, 60, 330, 92, true);

            // 2) 안내 글귀(종이 띠 위)
            AbsText(body, "txt_description", "함께 달릴 친구를 선택하고 출전하세요", font, 22, NoteInk, false, TextAnchor.MiddleCenter, 340, 166, 600, 34, false);

            // 3) 돌아가기 버튼(왼쪽 위)
            EntryClose = AbsButton(body, "btn_close_entry", art.Get("arena_entry_back"), 13, 47, 298, 102);

            // 4) 친구 카드 3장
            for (int i = 0; i < 3; i++)
            {
                float x = 140 + i * (315 + 28), y = 198;      // 카드 왼쪽 위 좌표 (카드 폭 315, 간격 28)
                const float s = 315f / 455f;                   // 카드 그림 축소 배율
                const float extra = 30f;                       // 이름판 아래에 글자 두 줄이 들어가도록 늘리는 높이
                float topH = 372 * s, midH = 8 * s + extra, botH = 52 * s;

                // 카드 전체를 누르는 버튼 (보이지 않는 이미지로 클릭만 받는다)
                var card = AbsImage(body, "btn_entrant_" + i, null, x, y, 315, topH + midH + botH);
                card.color = new Color(1, 1, 1, 0); card.raycastTarget = true;
                var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = card; button.transition = UnityEngine.UI.Selectable.Transition.None;
                button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                EntrantButtons[i] = button;

                // 카드 그림: 위(top) + 늘어나는 가운데(mid) + 아래(bottom) 세 조각
                AbsImage(card.transform, "img_card_top", art.Get("arena_entry_card_top"), 0, 0, 315, topH);
                AbsImage(card.transform, "img_card_mid", art.Get("arena_entry_card_mid"), 0, topH, 315, midH);
                AbsImage(card.transform, "img_card_bottom", art.Get("arena_entry_card_bottom"), 0, topH + midH, 315, botH);

                // 몬스터 얼굴, 이름(이름판 위), 능력치, 출전 가능 여부
                var face = AbsImage(card.transform, "img_portrait", null, 52, 34, 211, 166);
                face.preserveAspect = true; EntrantPortraits[i] = face;
                // preserveAspect는 RectTransform의 pivot 기준으로 정렬되므로, 중앙정렬되도록 pivot을 가운데로 두고 위치를 중심 좌표로 이동
                face.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                face.rectTransform.anchoredPosition = new Vector2(157.5f, -117f);
                EntrantNames[i] = AbsText(card.transform, "txt_name", "", font, 24, EntryCream, true, TextAnchor.MiddleCenter, 52, 204, 211, 47, true);
                EntrantStats[i] = AbsText(card.transform, "txt_stats", "", font, 19, new Color(.36f, .21f, .11f), false, TextAnchor.MiddleCenter, 10, 256, 295, 26, false);
                EntrantStates[i] = AbsText(card.transform, "txt_status", "", font, 19, new Color(.36f, .21f, .11f), true, TextAnchor.MiddleCenter, 10, 281, 295, 26, false);

                // 선택됐을 때 카드 위에 덮는 테두리 (RefreshSelection이 켜고 끈다)
                var sel = AbsImage(card.transform, "img_selection", selectedSprite, 0, 0, 315, topH + midH + botH);
                sel.type = UnityEngine.UI.Image.Type.Sliced; sel.pixelsPerUnitMultiplier = 1.5f;
                sel.gameObject.SetActive(false);
                EntrantFrames[i] = sel;
            }

            // 5) 추천 전술 설명 + 확정 버튼
            TacticDescription = AbsText(body, "txt_tactic", "", font, 20, NoteInk, false, TextAnchor.MiddleCenter, 290, 530, 700, 50, false);

            var confirm = AbsImage(body, "btn_confirm_entry", null, 440, 586, 400, 72);
            confirm.color = new Color(1, 1, 1, 0); confirm.raycastTarget = true;
            var confirmButton = confirm.gameObject.AddComponent<UnityEngine.UI.Button>();
            confirmButton.targetGraphic = confirm; confirmButton.transition = UnityEngine.UI.Selectable.Transition.None;
            confirmButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            ConfirmEntry = confirmButton;
            // 선택했을 때 모양: 기존 갈색 버튼 이미지를 그대로 사용
            var enabledLook = new GameObject("group_enabled", typeof(RectTransform)).GetComponent<RectTransform>();
            enabledLook.SetParent(confirm.transform, false);
            enabledLook.anchorMin = Vector2.zero; enabledLook.anchorMax = Vector2.one; enabledLook.offsetMin = enabledLook.offsetMax = Vector2.zero;
            AbsStretch(AbsImage(enabledLook, "bg_surface", confirmBg, 0, 0, 0, 0));
            AbsStretch(AbsImage(enabledLook, "img_frame", confirmFrame, 0, 0, 0, 0));
            confirmEnabledLook = enabledLook.gameObject;
            // 선택 전 모양: 새 회색 버튼 이미지
            var disabledLook = AbsImage(confirm.transform, "img_disabled", art.Get("arena_entry_confirm_off"), 0, 0, 0, 0);
            AbsStretch(disabledLook);
            confirmDisabledLook = disabledLook.gameObject;
            // 버튼 글자 (모양 위에 표시)
            var label = AbsText(confirm.transform, "txt_label", "", font, 26, EntryCream, true, TextAnchor.MiddleCenter, 0, 0, 0, 0, true);
            AbsStretch(label);
            ConfirmLabel = label;
            return true;
        }

        // 확정 버튼 모양을 선택 여부에 맞춰 바꾼다 (선택 전: 회색, 선택 후: 갈색)
        private void RefreshConfirmLook()
        {
            if (confirmEnabledLook == null || confirmDisabledLook == null) return;
            bool off = !Mgr_User.CanEnter(selectedId);
            confirmEnabledLook.SetActive(!off);
            confirmDisabledLook.SetActive(off);
            var trainingLook = ConfirmEntry.transform.Find("img_training");
            if (trainingLook != null) trainingLook.gameObject.SetActive(false);
            ConfirmLabel.color = off ? new Color(.45f, .42f, .39f) : EntryCream;
            // Older scene instances override the outline with a thick cream rim.
            // Match the edge to the current light/dark button state after those overrides.
            var outline = ConfirmLabel.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null)
            {
                outline.effectColor = off ? new Color(1, .97f, .86f, .45f) : EntryOutline;
                outline.effectDistance = new Vector2(.6f, -.6f);
            }
        }

        // 오브젝트를 지운다. 실행 중에는 Destroy, 에디터에서 프리팹을 편집할 때는 DestroyImmediate를 쓴다.
        private static void RemoveObject(GameObject target)
        {
            target.SetActive(false);
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }

        // 캔버스 픽셀 좌표(좌상단 x, y, 너비, 높이)로 빈 RectTransform을 만든다
        private static RectTransform AbsRect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        // 이미지 한 장을 만든다 (기본은 클릭을 받지 않는다)
        private static UnityEngine.UI.Image AbsImage(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
        {
            var image = AbsRect(parent, name, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.raycastTarget = false;
            return image;
        }

        // 글자를 만든다 (outline이 true면 어두운 테두리를 입힌다)
        private static UnityEngine.UI.Text AbsText(Transform parent, string name, string value, Font font, int size, Color color, bool bold, TextAnchor align, float x, float y, float w, float h, bool outline)
        {
            var text = AbsRect(parent, name, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = font; text.fontSize = size; text.text = value; text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = align; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var edge = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.effectColor = EntryOutline; edge.effectDistance = new Vector2(1.5f, -1.5f);
            }
            return text;
        }

        // 이미지 한 장을 버튼으로 만든다 (눌렀을 때 살짝 어두워짐)
        private static UnityEngine.UI.Button AbsButton(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
        {
            var image = AbsImage(parent, name, sprite, x, y, w, h);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var colors = button.colors; colors.pressedColor = new Color(.82f, .82f, .82f); button.colors = colors;
            return button;
        }

        // 부모 크기에 꽉 차도록 늘린다
        private static void AbsStretch(UnityEngine.UI.Graphic graphic)
        {
            var r = graphic.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        }

        public void ClosePopups()
        {
            owner?.Guide?.Clear();
            if (EntryPopup != null) EntryPopup.SetActive(false);
            if (NotebookPopup != null) NotebookPopup.SetActive(false);
            if (SettingsPopup != null) SettingsPopup.SetActive(false);
        }

        public void ShowEntrants()
        {
            ClosePopups();
            selectedId = Mgr_User.Data.SelectedMonster;
            if (selectedId >= EntrantButtons.Length) selectedId = -1;
            var art = Mgr_Bundle.Load<Data_DemoArt>("UI/RanchCollection/Data_RanchCollectionArt");
            for (int i = 0; i < EntrantButtons.Length; i++)
            {
                var monster = Mgr_Table.Demo.Monsters[i];
                var progress = Mgr_User.Data.Monsters[i];
                var portrait = art != null ? art.Get(monster.Id + "_face") : null;
                EntrantPortraits[i].sprite = portrait != null ? portrait : DemoUI.Art.Get(monster.Id + "_face");
                // 도안마다 무게중심이 달라서, 카드 중앙에 보이도록 초상화 x 위치를 몬스터별로 보정 (카드 기준 px, 음수=왼쪽)
                float portraitShiftX = monster.Id == "Minorous" ? -1.3f : 0f;
                var portraitRect = EntrantPortraits[i].rectTransform;
                // 앵커·크기·pivot·위치를 모두 명시해서 카드 중앙(가로 157.5, 세로 -117)에 고정
                portraitRect.anchorMin = new Vector2(0f, 1f);
                portraitRect.anchorMax = new Vector2(0f, 1f);
                portraitRect.pivot = new Vector2(0.5f, 0.5f);
                portraitRect.sizeDelta = new Vector2(211f, 166f);
                portraitRect.anchoredPosition = new Vector2(157.5f + portraitShiftX, -117f);
                EntrantNames[i].text = monster.Name;
                EntrantStats[i].text = "Lv." + progress.Level + "  ·  스피드 " + (monster.Stats[0] + progress.SpeedBonus);
                EntrantStates[i].text = "출전 가능";
                EntrantStates[i].color = new Color(.10f,.39f,.17f);
            }
            RefreshSelection();
            EntryPopup.SetActive(true);
        }

        public void SelectEntrant(int id)
        {
            if (id < 0 || id >= EntrantButtons.Length) return;
            selectedId = id;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            RefreshConfirmLook();   // 선택 여부에 따라 확정 버튼 모양(활성/비활성) 전환
            for (int i = 0; i < EntrantFrames.Length; i++) EntrantFrames[i].gameObject.SetActive(i == selectedId);
            ConfirmEntry.interactable = Mgr_User.CanEnter(selectedId);
            if (!ConfirmEntry.interactable)
            {
                TacticDescription.text = "이번 경기에 출전할 친구 한 마리를 선택하세요.";
                ConfirmLabel.text = "친구를 선택하세요";
                return;
            }
            var monster = Mgr_Table.Demo.Monsters[selectedId];
            TacticDescription.text = "추천 전술 · " + monster.Tactic + "\n" + monster.Strength + "에 강한 친구예요.";
            ConfirmLabel.text = "출전하기";
        }

        private void ConfirmSelection()
        {
            if (!Mgr_User.CanEnter(selectedId)) return;
            if (Mgr_Game.Instance == null || Mgr_Game.Instance.IsTransitioning) return;
            // Browsing or cancelling never changes monster/training selections or entry data.
            Mgr_User.Select(selectedId);
            ConfirmEntry.interactable = false;
            Mgr_Game.Instance.EnterRace(Define_Scene.OvalToon);
        }

        private void RefreshSound() => SoundLabel.text = "소리  " + (Mgr_User.Data.Volume > 0 ? "켜짐" : "꺼짐");

        private void ToggleSound()
        {
            Mgr_User.Data.Volume = Mgr_User.Data.Volume > 0 ? 0 : .65f;
            AudioListener.volume = Mgr_User.Data.Volume;
            Mgr_User.Save();
            RefreshSound();
        }

        public void ReturnToRanch()
        {
            if (owner != null) owner.CloseCollection();
        }
    }
}
