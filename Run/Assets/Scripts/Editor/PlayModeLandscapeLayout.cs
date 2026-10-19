using System;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    // Shared by the existing builders and the in-place migration. Never rebuilds scene objects.
    public static class PlayModeLandscapeLayout
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1280, 720);
        private static readonly Vector2 center = new Vector2(.5f, .5f);
        public static void ConfigureCanvas(UnityEngine.UI.CanvasScaler scaler)
        {
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            // Fit the whole layout on narrower windows; ultrawide screens gain horizontal race space.
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            EditorUtility.SetDirty(scaler);
        }

        public static void ApplyPlayMode(UIPanel_PlayMode panel, bool title)
        {
            var root = panel.SafeArea;
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.sortingOrder = title ? (int)E_CanvasLayer.System : (int)E_CanvasLayer.Game - 1;
            if (title)
            {
                if (root.Find("btn_prontera") != null)
                {
                    Place(root, "txt_title", center, 0, 250, 1000, 76);
                    root.Find("txt_title").GetComponent<UnityEngine.UI.Text>().fontSize = 56;
                    Place(root, "txt_description", center, 0, 184, 900, 48);
                    root.Find("txt_description").GetComponent<UnityEngine.UI.Text>().fontSize = 24;
                    Place(root, "btn_oval", center, -235, 75, 420, 116);
                    Place(root, "btn_prontera", center, 235, 75, 420, 116);
                    Place(root, "btn_oval_toon", center, -235, -65, 420, 116);
                    Place(root, "btn_prontera_toon", center, 235, -65, 420, 116);
                    if (root.Find("btn_lobby") != null)
                    { Place(root, "btn_lobby", center, root.Find("btn_sprite_2d") != null ? 235 : 0, -190, root.Find("btn_sprite_2d") != null ? 420 : 260, 60); TextSize(root.Find("btn_lobby"), "txt_label", 22); }
                    if (root.Find("btn_sprite_2d") != null)
                    { Place(root, "btn_sprite_2d", center, -235, -190, 420, 60); TextSize(root.Find("btn_sprite_2d"), "txt_label", 24); }
                    Place(root, "txt_status", center, 0, -264, 1040, 50);
                    return;
                }
                Place(root, "txt_title", center, 0, 190, 1000, 90);
                root.Find("txt_title").GetComponent<UnityEngine.UI.Text>().fontSize = 64;
                Place(root, "txt_description", center, 0, 110, 900, 50);
                Place(root, "btn_all3d", center, -235, -30, 420, 100);
                if (root.Find("btn_lookdev_race") != null)
                {
                    Place(root, "btn_lookdev_race", center, 235, -30, 420, 100);
                    Place(root, "btn_lobby", center, 0, -150, 420, 64);
                    TextSize(root.Find("btn_lobby"), "txt_label", 26);
                    Place(root, "txt_status", center, 0, -240, 1040, 70);
                }
                else
                {
                    Place(root, "btn_lobby", center, 235, -30, 420, 100);
                    Place(root, "txt_status", center, 0, -150, 1040, 70);
                }
            }
            else Place(root, "btn_title", new Vector2(1, 1), -84, -122, 128, 48);
        }

        public static void ApplyHUD(UIPanel_RaceHUD hud)
        {
            var root = hud.SafeArea.Find("group_fantasy");
            Place(root, "group_course", new Vector2(0, 1), 190, -54, 320, 76);
            Place(root, "obj_pennant", new Vector2(0, 1), 46, -54, 70, 92);
            Place(root, "group_distance", new Vector2(1, 1), -180, -54, 262, 76);
            Place(root, "group_progress", new Vector2(.5f, 1), 10, -42, 450, 24);
            var progress = root.Find("group_progress");
            Place(progress, "img_start", new Vector2(0, .5f), -6, 0, 32, 38);
            Place(progress, "img_finish", new Vector2(1, .5f), 6, 0, 32, 38);
            Place(progress, "img_gloss", center, 0, 4, 428, 3);
            for (int i = 1; i < 7; i++) Place(progress, "img_segment_" + i, center, -214 + 428f * i / 7, 0, 1.3f, 13);
            Place(root, "group_ranking", new Vector2(.5f, 1), 10, -105, 420, 50);
            Place(root, "group_phase", new Vector2(0, 1), 110, -122, 157, 36);
            Place(root, "group_skill", new Vector2(1, 1), -235, -211, 414, 87);

            Place(root, "group_selected", new Vector2(0, 0), 295, 77, 550, 110);
            var selected = root.Find("group_selected");
            Place(selected, "obj_portrait", center, -220, 6, 86, 86);
            Place(selected, "img_portrait_frame", center, -220, 6, 113, 117);
            Place(selected, "obj_number", center, -220, -43, 62, 24);
            Place(selected, "img_feather", center, -151, 22, 24, 33);
            Place(selected, "txt_name", center, -13, 22, 260, 36);
            hud.FocusName.resizeTextMinSize = 20; hud.FocusName.resizeTextMaxSize = 26;
            Place(selected, "txt_gap", center, -13, -18, 260, 29);
            hud.FocusGap.fontSize = 21;
            Place(selected, "txt_rank", center, 170, 5, 70, 80); hud.FocusRank.fontSize = 64;
            Place(selected, "txt_rank_unit", center, 225, 24, 36, 34); TextSize(selected, "txt_rank_unit", 26);
            Place(selected, "txt_count", center, 225, -5, 54, 27); hud.FocusCount.fontSize = 22;
            Place(selected, "txt_change", center, 232, -32, 48, 24);
            Place(selected, "img_rank_change", center, 200, -32, 16, 20);
            Place(selected, "img_right_wing", center, 268, 22, 32, 58);

            Place(root, "group_controls", new Vector2(1, 0), -295, 57, 550, 76);
            var buttons = new[] { hud.CameraButton, hud.SpeedButton, hud.SkipButton, hud.MenuButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i].transform;
                Place((RectTransform)button, center, -204 + i * 136, 0, 128, 68);
                Place(button, "img_action", center, -40, 0, 27, 29);
                Place(button, "txt_label", center, 16, 0, 84, 44);
                TextSize(button, "txt_label", 20);
                Place(button, "img_gem", center, 0, -32, 19, 21);
            }
            Place(root, "group_announcement", new Vector2(1, 0), -295, 124, 550, 40);
            var announcement = root.Find("group_announcement");
            Place(announcement, "img_trumpet", center, -245, 0, 26, 26);
            Place(announcement, "txt_caption", center, -203, 0, 52, 28); TextSize(announcement, "txt_caption", 20);
            Place(announcement, "txt_announcement", center, 43, 0, 420, 30); hud.Announcement.fontSize = 20;
            hud.Announcement.resizeTextForBestFit = true; hud.Announcement.resizeTextMinSize = 16; hud.Announcement.resizeTextMaxSize = 20;
            Place(root, "obj_countdown_plate", center, 0, 0, 300, 125);
            hud.WorldMarkerInsets = new Vector4(40, 198, 40, 150);
            var styles = hud.GetComponentInChildren<UIItem_RenderStyle>(true);
            if (styles != null) ApplyRenderStyles(styles);
            ApplyResult(hud);
            ApplyMenu(hud);
        }

        public static void ApplyRenderStyles(UIItem_RenderStyle item)
        {
            Place((RectTransform)item.transform, new Vector2(0, 0), 295, 171, 550, 44);
            for (int i = 0; i < item.Buttons.Length; i++)
            {
                Place((RectTransform)item.Buttons[i].transform, center, (i - 1) * 184, 0, 178, 44);
                item.Labels[i].fontSize = 20;
            }
            // Behind the fantasy group, whose modal backdrop must cover these controls.
            item.transform.SetSiblingIndex(1);
        }

        private static void ApplyResult(UIPanel_RaceHUD hud)
        {
            var sheet = hud.ResultRoot.transform.Find("group_result_sheet");
            Place((RectTransform)sheet, center, 0, 0, 1080, 610);
            Place(sheet, "img_victory_crest", center, -300, 236, 310, 110);
            Place(sheet.Find("img_victory_crest"), "txt_ribbon", center, 0, -31, 190, 24);
            Place(sheet, "txt_course_title", center, -300, 158, 430, 30);
            Place(sheet, "txt_winner", center, -300, 112, 430, 54); hud.ResultName.fontSize = 32;
            Place(sheet, "obj_winner_portrait", center, -300, 16, 130, 130);
            Place(sheet, "img_winner_frame", center, -300, 16, 165, 172);
            Place(sheet, "txt_winner_mount", center, -300, -88, 400, 34);
            Place(sheet, "txt_winner_time", center, -300, -131, 400, 47);
            Place(sheet, "txt_record_caption", center, -300, -169, 300, 25);
            Place(sheet, "txt_my_summary", center, -300, -209, 430, 30); hud.ResultSummary.fontSize = 19;
            Place(sheet, "btn_replay", center, -300, -257, 350, 64);
            Place(hud.ResultReplay.transform, "img_gem", center, 0, -30, 24, 27);
            Place(sheet, "img_divider", center, -45, 0, 1.5f, 510);
            Place(sheet, "txt_rank_heading", center, 6, 224, 60, 25);
            Place(sheet, "txt_runner_heading", center, 148, 224, 180, 25);
            Place(sheet, "txt_time_heading", center, 410, 224, 135, 25);
            for (int i = 0; i < hud.ResultRows.Length; i++)
                Place((RectTransform)hud.ResultRows[i].transform, center, 230, 174 - i * 72, 528, 66);
        }

        private static void ApplyMenu(UIPanel_RaceHUD hud)
        {
            var panel = hud.MenuRoot.transform.Find("group_panel");
            Place((RectTransform)panel, center, 0, 0, 500, 560);
            Place(panel, "img_menu_crown", center, 0, 235, 78, 64);
            Place(panel, "txt_title", center, 0, 177, 370, 44);
            Place(panel, "txt_subtitle", center, 0, 140, 370, 29);
            var buttons = new[] { hud.PauseButton, hud.CutsceneButton, hud.RestartButton, hud.MenuCloseButton };
            var positions = new[] { 68f, -14f, -96f, -204f };
            for (int i = 0; i < buttons.Length; i++)
                Place((RectTransform)buttons[i].transform, center, 0, positions[i], i == 3 ? 240 : 380, 72);
        }

        public static void ApplyVideo(UIPanel_RaceSkillVideo panel)
        {
            var root = panel.SafeArea;
            var zone = (RectTransform)root.Find("group_video");
            zone.anchorMin = Vector2.zero; zone.anchorMax = new Vector2(.65f, 1);
            zone.offsetMin = new Vector2(20, 20); zone.offsetMax = new Vector2(-20, -20);
            var fit = panel.VideoImage.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
            Place(root, "group_title", new Vector2(.82f, .65f), 0, 0, 370, 90);
            Place(root, "btn_menu", new Vector2(.82f, .37f), 0, 0, 244, 65);
            Place(panel.MenuButton.transform, "img_action", center, -87, 0, 28, 32);
            Place(root, "btn_skip", new Vector2(.82f, .23f), 0, 0, 244, 65);
        }

        private static void TextSize(Transform parent, string name, int size)
            => parent.Find(name).GetComponent<UnityEngine.UI.Text>().fontSize = size;

        private static void Place(Transform parent, string name, Vector2 anchor, float x, float y, float width, float height)
            => Place((RectTransform)parent.Find(name), anchor, x, y, width, height);

        private static void Place(RectTransform rect, Vector2 anchor, float x, float y, float width, float height)
        {
            if (rect == null) throw new InvalidOperationException("The existing UI layout is incomplete.");
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = center; rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
