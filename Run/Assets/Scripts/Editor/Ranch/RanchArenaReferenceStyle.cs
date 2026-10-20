using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    // Reuse the approved collection art; update the arena prefab without saving the open scene.
    public static class RanchArenaReferenceStyle
    {
        private const string prefabPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_RanchArena.prefab";
        private static readonly Color ink = new Color(.24f, .045f, .035f);
        private static readonly Color cream = new Color(1, .95f, .78f);

        [MenuItem("Tools/Project Run/Ranch/Match Arena To Monster Notebook")]
        public static void Refresh()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before updating the arena prefab.");
            var art = AssetDatabase.LoadAssetAtPath<Data_DemoArt>("Assets/Resources/UI/RanchCollection/Data_RanchCollectionArt.asset");
            var ranch = AssetDatabase.LoadAssetAtPath<Data_DemoArt>("Assets/Resources/UI/Ranch/Data_RanchArt.asset");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Apply(root.GetComponent<UIPanel_RanchArena>(), art, ranch);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("Arena notebook style and common-start exhibition saved to the arena prefab.");
        }

        public static void Apply(UIPanel_RanchArena panel, Data_DemoArt art, Data_DemoArt ranch)
        {
            foreach (string key in new[] { "ui_paper", "ui_frame", "ui_preview", "ui_title", "screen_frame",
                "reference_back_bg", "reference_BackFrame", "title_post_left", "title_post_right" })
                if (art == null || art.Get(key) == null) throw new InvalidOperationException("Missing collection sprite: " + key);
            var layout = panel.Layout;
            panel.LayoutScale = .94f;
            layout.localScale = Vector3.one * panel.LayoutScale;
            var cover = panel.transform.Find("bg_fullscreen").GetComponent<Image>();
            cover.sprite = null;
            cover.color = new Color(0, 0, 0, .48f);
            var page = Pic(layout, "bg_page", art.Get("ui_paper"), 42, 72, 1196, 614, true);
            page.transform.SetAsFirstSibling();
            Pic(layout, "img_outer_frame", art.Get("screen_frame"), 16, 26, 1248, 688);

            var title = layout.Find("group_title");
            Rect(title, 450, 14, 380, 84);
            Pic(title, "bg_surface", art.Get("ui_title"), 0, 0, 380, 84, true);
            Pic(title, "img_frame", art.Get("ui_frame"), 4, 4, 372, 76, true);
            TextStyle(layout.Find("txt_title"), art, 39, cream, 494, 22, 292, 62, true);
            Shadow(layout.Find("txt_title").GetComponent<Text>());
            Pic(layout, "img_paw_left", art.Get("icon_paw"), 459, 37, 34, 35);
            Pic(layout, "img_paw_right", art.Get("icon_paw"), 787, 37, 34, 35);
            Pic(layout, "img_lantern_left", art.Get("title_post_left"), 412, 10, 43, 94);
            Pic(layout, "img_lantern_right", art.Get("title_post_right"), 827, 10, 45, 94);
            for (int i = 0; i < 4; i++)
                Pic(title, "img_rivet_" + i, art.Get("ui_rivet"), i % 2 == 0 ? 10 : 362, i < 2 ? 8 : 66, 8, 8);

            WoodButton(panel.BackButton, art, 66, 29, 182, 64, 24);
            Rect(panel.BackButton.transform.Find("img_arrow"), 17, 16, 33, 32);
            Rect(panel.BackButton.GetComponentInChildren<Text>().transform, 52, 10, 115, 44);
            WoodButton(panel.NotebookButton, art, 1019, 32, 134, 58, 22);
            Rect(panel.NotebookButton.transform.Find("img_book"), 14, 13, 31, 31);
            Rect(panel.NotebookButton.GetComponentInChildren<Text>().transform, 49, 10, 72, 38);

            Rect(panel.SettingsButton.transform, 1164, 32, 58, 58);
            var settingsSurface = Pic(panel.SettingsButton.transform, "bg_surface", ranch.Get("round_bg"), 0, 0, 58, 58);
            Pic(panel.SettingsButton.transform, "img_frame", ranch.Get("round_frame"), 0, 0, 58, 58);
            Pic(panel.SettingsButton.transform, "img_settings", ranch.Get("settings"), 13, 13, 32, 32);
            settingsSurface.raycastTarget = true;
            panel.SettingsButton.targetGraphic = settingsSurface;

            var preview = layout.Find("group_arena_preview");
            Rect(preview, 66, 111, 820, 506);
            Rect(preview.Find("obj_clip"), 4, 4, 812, 498);
            Pic(preview, "img_preview_frame", art.Get("ui_preview"), 0, 0, 820, 506, true);
            Pic(layout, "img_preview_paw", art.Get("icon_paw"), 222, 632, 24, 24);
            TextStyle(layout.Find("txt_exhibition"), art, 20, ink, 254, 628, 548, 32);
            var exhibition = panel.GetComponentInChildren<UIItem_ArenaExhibition>(true);
            exhibition.ConfigurePresentation(38, 45, 74, 11);
            exhibition.SetStatusLabel(layout.Find("txt_exhibition").GetComponent<Text>());
            layout.Find("txt_exhibition").GetComponent<Text>().text = "같은 출발선에서 함께 달려요";

            var note = layout.Find("group_course_notebook");
            Rect(note, 904, 111, 309, 447);
            Paper(note, art, 309, 447);
            Corners(note, art, 309, 447);
            Rect(note.Find("img_trophy"), 124, 18, 61, 58);
            TextStyle(note.Find("txt_cup"), art, 29, ink, 20, 81, 269, 43, true);
            Rule(note, "img_heading_rule", 22, 129, 265);
            Rect(note.Find("obj_map_clip"), 21, 144, 267, 150);
            Rect(note.Find("obj_map_clip/img_course"), 0, 0, 267, 150);
            Pic(note, "img_map_frame", art.Get("ui_preview"), 17, 140, 275, 158, true);
            Rect(note.Find("img_memo"), 25, 313, 28, 28);
            TextStyle(note.Find("txt_memo_heading"), art, 22, ink, 64, 309, 222, 33, true, TextAnchor.MiddleLeft);
            TextStyle(note.Find("txt_course_memo"), art, 19, ink, 27, 349, 258, 48, false, TextAnchor.UpperLeft);
            Rule(note, "img_requirement_rule", 23, 401, 263);
            Rect(note.Find("img_entry"), 26, 413, 25, 25);
            TextStyle(note.Find("txt_requirement_heading"), art, 17, ink, 61, 412, 89, 27, true, TextAnchor.MiddleLeft);
            TextStyle(panel.Requirements.transform, art, 17, ink, 148, 412, 141, 27, false, TextAnchor.MiddleRight);
            WoodButton(panel.EntryButton, art, 904, 575, 309, 78, 29);
            Rect(panel.EntryButton.transform.Find("img_flag"), 28, 21, 38, 38);
            Rect(panel.EntryButton.GetComponentInChildren<Text>().transform, 76, 14, 210, 50);

            // Keep the existing notebook/entrant designs and their event references.
            // Settings shares the same separate paper surface and frame as the main page.
            var settings = panel.SettingsPopup.transform.Find("group_content");
            Paper(settings, art, 620, 352);
            Corners(settings, art, 620, 352);
            TextStyle(settings.Find("txt_title"), art, 32, ink, 50, 34, 520, 55, true);
            Paper(panel.SoundButton.transform, art, 420, 74);
            TextStyle(panel.SoundLabel.transform, art, 26, ink, 12, 8, 396, 58, true);
            WoodButton(panel.SettingsClose, art, 185, 248, 250, 65, 23);
            RepairConfirmation(panel, art, ranch);
        }

        private static void RepairConfirmation(UIPanel_RanchArena panel, Data_DemoArt art, Data_DemoArt ranch)
        {
            panel.ApplyEntrantsSkin(ranch, art);
            var button = panel.ConfirmEntry.transform;
            var enabled = button.Find("group_enabled");
            if (enabled == null) return;
            var size = ((RectTransform)button).sizeDelta;
            Pic(enabled, "bg_surface", art.Get("reference_back_bg"), 6, 6, size.x - 12, size.y - 12, true);
            Pic(enabled, "img_frame", art.Get("reference_BackFrame"), 0, 0, size.x, size.y);
            var training = Pic(button, "img_training", null, 0, 0, size.x, size.y);
            training.color = Color.clear;
            var paper = Pic(training.transform, "bg_surface", art.Get("ui_paper"), 6, 6, size.x - 12, size.y - 12, true);
            paper.color = new Color(1, .85f, .53f);
            Pic(training.transform, "img_frame", art.Get("reference_BackFrame"), 0, 0, size.x, size.y);
            training.transform.SetSiblingIndex(panel.ConfirmLabel.transform.GetSiblingIndex());
            panel.ConfirmLabel.transform.SetAsLastSibling();
            var outline = panel.ConfirmLabel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(.19f, .055f, .01f, .85f);
                outline.effectDistance = new Vector2(.7f, -.7f);
            }
            training.gameObject.SetActive(false);
        }

        private static void WoodButton(Button button, Data_DemoArt art, float x, float y, float width, float height, int size)
        {
            Rect(button.transform, x, y, width, height);
            var surface = Pic(button.transform, "bg_surface", art.Get("reference_back_bg"), 6, 6, width - 12, height - 12, true);
            var frame = Pic(button.transform, "img_frame", art.Get("reference_BackFrame"), 0, 0, width, height);
            surface.raycastTarget = true;
            frame.raycastTarget = true;
            button.targetGraphic = surface;
            var colors = button.colors;
            colors.highlightedColor = new Color(1, .95f, .85f);
            colors.pressedColor = new Color(.78f, .69f, .56f);
            button.colors = colors;
            var label = button.GetComponentInChildren<Text>(true);
            TextStyle(label.transform, art, size, cream, 12, 8, width - 24, height - 16, true);
            Shadow(label);
        }

        private static void Paper(Transform group, Data_DemoArt art, float width, float height)
        {
            Pic(group, "bg_surface", art.Get("ui_paper"), 0, 0, width, height, true);
            Pic(group, "img_frame", art.Get("ui_frame"), 0, 0, width, height, true);
            var button = group.GetComponent<Button>();
            if (button != null)
            {
                var surface = group.Find("bg_surface").GetComponent<Image>();
                surface.raycastTarget = true;
                button.targetGraphic = surface;
            }
        }

        private static void Corners(Transform group, Data_DemoArt art, float width, float height)
        {
            for (int i = 0; i < 4; i++)
                Pic(group, "img_corner_" + i, art.Get("ui_corner"), i % 2 == 0 ? 11 : width - 21, i < 2 ? 10 : height - 20, 10, 10);
        }

        private static void Rule(Transform group, string name, float x, float y, float width)
        {
            Pic(group, name, null, x, y, width, 1).color = new Color(.75f, .57f, .36f, .6f);
        }

        private static Image Pic(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, bool sliced = false)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform), typeof(Image)).transform;
                child.SetParent(parent, false);
            }
            var image = child.GetComponent<Image>();
            Rect(child, x, y, width, height);
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = false;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            child.gameObject.SetActive(true);
            return image;
        }

        private static void TextStyle(Transform target, Data_DemoArt art, int size, Color color, float x, float y, float width, float height, bool bold = false, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            Rect(target, x, y, width, height);
            var text = target.GetComponent<Text>();
            text.font = bold ? art.TitleFont : art.BodyFont;
            text.fontSize = size;
            text.fontStyle = FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
        }

        private static void Shadow(Text text)
        {
            var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.19f, .055f, .01f, .85f);
            shadow.effectDistance = new Vector2(0, -2);
        }

        private static void Rect(Transform target, float x, float y, float width, float height)
        {
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
