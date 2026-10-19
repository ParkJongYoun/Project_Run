using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectRun.Race;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    public static class RanchArenaBuilder
    {
        private const string prefabPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_RanchArena.prefab";
        private const string scenePath = "Assets/Scenes/Scene_LobbyTest.unity";
        private static Data_DemoArt art;
        private static Data_DemoArt ranchArt;
        private static readonly Color ink = new Color(.27f, .13f, .065f);
        private static readonly Color cream = new Color(1, .95f, .78f);

        [MenuItem("Tools/Project Run/Ranch/Build Arena UI")]
        public static void BuildAndConnect()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before building the arena UI.");
            art = AssetDatabase.LoadAssetAtPath<Data_DemoArt>("Assets/Resources/UI/RanchCollection/Data_RanchCollectionArt.asset");
            if (art == null) throw new InvalidOperationException("Ranch collection art is required.");
            ranchArt = Resources.Load<Data_DemoArt>("UI/Ranch/Data_RanchArt");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) CreatePrefab();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            bool temporary = !scene.isLoaded;
            if (temporary) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var bootstrap = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Sys_RanchUI>(true)).Single();
            var canvas = bootstrap.Panel.GetComponentInParent<Canvas>().transform;
            var existing = canvas.Find("UIPanel_RanchArena");
            if (existing == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
                instance.name = "UIPanel_RanchArena";
                instance.SetActive(false);
                Undo.RegisterCreatedObjectUndo(instance, "Add ranch arena panel");
                existing = instance.transform;
            }
            Undo.RecordObject(bootstrap, "Connect ranch arena panel");
            bootstrap.ArenaPanel = existing.GetComponent<UIPanel_RanchArena>();
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (temporary) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("Ranch arena UI: wood frame, course notebook and animated sprite exhibition connected.");
        }

        private static void CreatePrefab()
        {
            var root = new GameObject("UIPanel_RanchArena", typeof(RectTransform));
            root.SetActive(false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var panel = root.AddComponent<UIPanel_RanchArena>();
            var cover = DemoUI.Fill(root.transform, "bg_fullscreen").gameObject.AddComponent<UnityEngine.UI.Image>();
            cover.sprite = art.Get("screen_bg"); cover.raycastTarget = true;
            panel.SafeArea = DemoUI.Fill(root.transform, "obj_safe_area");
            panel.Layout = DemoUI.Rect(panel.SafeArea, "group_layout", 0, 0, 1280, 720);
            var layout = panel.Layout;
            Pic(layout, "img_outer_frame", art.Get("screen_frame"), 0, 0, 1280, 720);
            Pair(layout, "group_title", "title", 416, -5, 448, 110);
            Label(layout, "txt_title", "경기장", 480, 23, 320, 51, 35, cream, true);
            Pic(layout, "img_paw_left", art.Get("icon_paw"), 439, 34, 34, 34, true);
            Pic(layout, "img_paw_right", art.Get("icon_paw"), 807, 34, 34, 34, true);
            Pic(layout, "img_lantern_left", art.Get("icon_lantern"), 388, 18, 34, 58, true);
            Pic(layout, "img_lantern_right", art.Get("icon_lantern"), 860, 18, 34, 58, true);
            panel.BackButton = Button(layout, "btn_back", "back_button", "목장으로", 37, 24, 216, 59, 24);
            Pic(panel.BackButton.transform, "img_arrow", art.Get("icon_back"), 16, 18, 31, 24, true);
            var backLabel = panel.BackButton.GetComponentInChildren<UnityEngine.UI.Text>();
            SetRect(backLabel.rectTransform, 48, 8, 150, 43);
            panel.NotebookButton = Button(layout, "btn_notebook", "info", "수첩", 1024, 25, 137, 55, 21);
            Pic(panel.NotebookButton.transform, "img_book", art.Get("icon_encyclopedia"), 11, 12, 32, 31, true);
            SetRect(panel.NotebookButton.GetComponentInChildren<UnityEngine.UI.Text>().rectTransform, 47, 9, 77, 36);
            panel.SettingsButton = Button(layout, "btn_settings", "info", "", 1176, 25, 65, 55, 21);
            var settings = ranchArt != null ? ranchArt.Get("settings") : null;
            if (settings != null) Pic(panel.SettingsButton.transform, "img_settings", settings, 14, 9, 37, 37, true);
            else Label(panel.SettingsButton.transform, "txt_settings", "설정", 6, 9, 53, 37, 18, ink, true);
            BuildPreview(layout);
            BuildNotebook(panel, layout);
            BuildEntrants(panel, layout);
            BuildNotebookPopup(panel, layout);
            BuildSettings(panel, layout);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BuildPreview(Transform layout)
        {
            var preview = Rect(layout, "group_arena_preview", 28, 103, 882, 520);
            var clip = Rect(preview, "obj_clip", 12, 12, 858, 496);
            clip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var arena = DemoUI.Image(clip, "bg_arena", "arena", 0, 0, 858, 496);
            arena.type = UnityEngine.UI.Image.Type.Simple;
            var fit = arena.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = arena.sprite.rect.width / arena.sprite.rect.height;
            var exhibition = DemoUI.Fill(arena.transform, "group_exhibition").gameObject.AddComponent<UIItem_ArenaExhibition>();
            exhibition.ConfigurePresentation(38, 45, 74, .145f);
            Pic(preview, "img_preview_frame", art.Get("preview_frame"), 0, 0, 882, 520, false, true);
            Pic(layout, "img_preview_paw", art.Get("icon_paw"), 187, 631, 24, 24, true);
            Label(layout, "txt_exhibition", "몬스터들이 몸을 풀고 있어요", 222, 629, 515, 29, 20, ink);
        }

        private static void BuildNotebook(UIPanel_RanchArena panel, Transform layout)
        {
            var note = Pair(layout, "group_course_notebook", "info", 926, 104, 323, 465, true);
            var trophy = ranchArt != null ? ranchArt.Get("arena") : null;
            if (trophy == null) trophy = art.Get("icon_entry");
            Pic(note, "img_trophy", trophy, 127, 20, 70, 64, true);
            Label(note, "txt_cup", "프론테라컵", 24, 89, 275, 42, 29, ink, true);
            var mapClip = Rect(note, "obj_map_clip", 37, 151, 250, 122);
            mapClip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            Pic(mapClip, "img_course", DemoUI.Art.Get("arena"), 0, -9, 250, 141);
            Pic(note, "img_map_frame", art.Get("preview_frame"), 23, 137, 278, 150, false, true);
            Pic(note, "img_memo", art.Get("icon_encyclopedia"), 27, 295, 29, 30, true);
            Label(note, "txt_memo_heading", "코스 메모", 68, 295, 224, 30, 23, ink, true, TextAnchor.MiddleLeft);
            Label(note, "txt_course_memo", "완만한 커브\n마지막 직선 승부", 30, 332, 267, 50, 20, ink, false, TextAnchor.MiddleLeft);
            Pic(note, "img_entry", art.Get("icon_entry"), 27, 390, 28, 27, true);
            Label(note, "txt_requirement_heading", "참가 조건", 66, 390, 222, 27, 21, ink, true, TextAnchor.MiddleLeft);
            panel.Requirements = Label(note, "txt_requirements", "Lv.5 · 스피드 260", 30, 421, 267, 24, 19, ink, true);
            panel.EntryButton = Button(layout, "btn_entry", "train_button", "출전 준비", 926, 580, 323, 80, 30);
            Pic(panel.EntryButton.transform, "img_flag", art.Get("icon_entry"), 24, 20, 40, 39, true);
            SetRect(panel.EntryButton.GetComponentInChildren<UnityEngine.UI.Text>().rectTransform, 76, 14, 216, 49);
        }

        private static void BuildEntrants(UIPanel_RanchArena panel, Transform layout)
        {
            var layer = Popup(layout, "popup_entry", out var body, 1040, 572);
            panel.EntryPopup = layer.gameObject;
            Label(body, "txt_title", "출전할 친구", 160, 24, 720, 45, 31, ink, true);
            Label(body, "txt_description", "한 마리를 선택하고 추천 전술을 확인하세요", 110, 75, 820, 35, 21, ink);
            panel.EntryClose = Button(body, "btn_close_entry", "back_button", "돌아가기", 26, 24, 143, 49, 19);
            panel.EntrantButtons = new UnityEngine.UI.Button[3];
            panel.EntrantFrames = new UnityEngine.UI.Image[3];
            panel.EntrantPortraits = new UnityEngine.UI.Image[3];
            panel.EntrantNames = new UnityEngine.UI.Text[3];
            panel.EntrantStats = new UnityEngine.UI.Text[3];
            panel.EntrantStates = new UnityEngine.UI.Text[3];
            for (int i = 0; i < 3; i++)
            {
                var button = Button(body, "btn_entrant_" + i, "info", "", 40 + i * 328, 125, 304, 260, 20, true);
                panel.EntrantButtons[i] = button;
                panel.EntrantPortraits[i] = Pic(button.transform, "img_portrait", art.Get(Mgr_Table.Demo.Monsters[i].Id + "_face"), 86, 17, 132, 118, true);
                panel.EntrantNames[i] = Label(button.transform, "txt_name", Mgr_Table.Demo.Monsters[i].Name, 15, 135, 274, 37, 25, ink, true);
                panel.EntrantStats[i] = Label(button.transform, "txt_stats", "", 15, 178, 274, 28, 21, ink);
                panel.EntrantStates[i] = Label(button.transform, "txt_status", "", 15, 208, 274, 24, 20, ink);
                panel.EntrantFrames[i] = Pic(button.transform, "img_selection", art.Get("portrait_selected"), 0, 0, 304, 260, false, true);
                panel.EntrantFrames[i].pixelsPerUnitMultiplier = 1.5f;
                panel.EntrantFrames[i].gameObject.SetActive(false);
            }
            panel.TacticDescription = Label(body, "txt_tactic", "이번 경기에 출전할 친구 한 마리를 선택하세요.", 50, 402, 940, 68, 23, ink);
            panel.ConfirmEntry = Button(body, "btn_confirm_entry", "train_button", "친구를 선택하세요", 280, 484, 480, 66, 24);
            panel.ConfirmLabel = panel.ConfirmEntry.GetComponentInChildren<UnityEngine.UI.Text>();
            layer.gameObject.SetActive(false);
        }

        private static void BuildNotebookPopup(UIPanel_RanchArena panel, Transform layout)
        {
            var layer = Popup(layout, "popup_course_notes", out var body, 1000, 568);
            panel.NotebookPopup = layer.gameObject;
            Pic(body, "img_book", art.Get("icon_encyclopedia"), 310, 27, 48, 43, true);
            Label(body, "txt_title", "경기장 수첩", 366, 23, 350, 50, 32, ink, true);
            var photoClip = Rect(body, "obj_photo_clip", 42, 116, 467, 256);
            photoClip.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            Pic(photoClip, "img_arena", DemoUI.Art.Get("arena"), -8, -8, 483, 272);
            Pic(body, "img_arena_frame", art.Get("preview_frame"), 28, 102, 495, 284, false, true);
            Label(body, "txt_photo_note", "흙과 잔디의 순환 코스", 42, 401, 467, 45, 22, ink, true);
            Label(body, "txt_notes", "코스의 성격\n완만한 커브 · 마지막 직선의 접전\n\n잘 맞는 능력\n스피드: 직선 최고 속도\n근력: 코너를 빠져나오는 재가속\n\n체력을 남겨 막판에 힘을 내요", 558, 103, 398, 349, 23, ink, false, TextAnchor.UpperLeft);
            panel.NotebookClose = Button(body, "btn_close_notebook", "back_button", "수첩 닫기", 355, 483, 290, 62, 23);
            layer.gameObject.SetActive(false);
        }

        private static void BuildSettings(UIPanel_RanchArena panel, Transform layout)
        {
            var layer = Popup(layout, "popup_arena_settings", out var body, 620, 352);
            panel.SettingsPopup = layer.gameObject;
            Label(body, "txt_title", "설정", 50, 34, 520, 55, 32, ink, true);
            panel.SoundButton = Button(body, "btn_sound", "info", "소리  켜짐", 100, 121, 420, 74, 26, true);
            panel.SoundLabel = panel.SoundButton.GetComponentInChildren<UnityEngine.UI.Text>();
            panel.SettingsClose = Button(body, "btn_close_settings", "back_button", "닫기", 185, 248, 250, 65, 23);
            layer.gameObject.SetActive(false);
        }

        private static RectTransform Popup(Transform parent, string name, out RectTransform body, float width, float height)
        {
            var root = DemoUI.Fill(parent, name);
            var shade = DemoUI.Shade(root, "bg_dim", new Color(.12f, .08f, .04f, .64f));
            shade.raycastTarget = true;
            body = Pair(root, "group_content", "info", (1280 - width) / 2, (720 - height) / 2, width, height, true);
            return root;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, x, y, width, height);
            return rect;
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static UnityEngine.UI.Image Pic(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, bool preserve = false, bool sliced = false)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite; image.raycastTarget = false; image.preserveAspect = preserve;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            return image;
        }

        private static RectTransform Pair(Transform parent, string name, string key, float x, float y, float width, float height, bool sliced = false)
        {
            var rect = Rect(parent, name, x, y, width, height);
            Pic(rect, "bg_surface", art.Get(key + "_bg"), 0, 0, width, height, false, sliced);
            Pic(rect, "img_frame", art.Get(key + "_frame"), 0, 0, width, height, false, sliced);
            return rect;
        }

        private static UnityEngine.UI.Button Button(Transform parent, string name, string key, string label, float x, float y, float width, float height, int fontSize, bool sliced = false)
        {
            var rect = Pair(parent, name, key, x, y, width, height, sliced);
            var surface = rect.Find("bg_surface").GetComponent<UnityEngine.UI.Image>();
            surface.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = surface;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            Label(rect, "txt_label", label, 12, 8, width - 24, height - 16, fontSize, key == "info" ? ink : cream, true);
            return button;
        }

        private static UnityEngine.UI.Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.Text>();
            text.font = art.BodyFont; text.fontSize = size; text.text = value; text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = alignment; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
    }
}
