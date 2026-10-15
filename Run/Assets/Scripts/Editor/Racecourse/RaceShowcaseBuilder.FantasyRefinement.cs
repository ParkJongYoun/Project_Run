using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        [Serializable] private sealed class RefinedRegion
        {
            public string Name, File;
            public int X, Y, Width, Height, Border;
        }
        [Serializable] private sealed class RefinedRegionList { public RefinedRegion[] Items; }
        private static Font heavyFont, bodyFont;

        private static void ImportRefinedArt()
        {
            string directory = fantasyArt + "/V2";
            Ensure(directory + "/Sprites");
            foreach (string file in new[] { "UI_ButtonFrame.png", "UI_PanelFrame.png", "UI_Icons.png", "UI_VictoryCrest.png" })
            {
                string path = directory + "/" + file;
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            var regions = JsonUtility.FromJson<RefinedRegionList>(File.ReadAllText(directory + "/sprite-regions.json"));
            foreach (var region in regions.Items)
            {
                string path = directory + "/Sprites/" + region.Name + ".asset";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/" + region.File);
                    sprite = Sprite.Create(texture, new UnityEngine.Rect(region.X, region.Y, region.Width, region.Height),
                        center, 100, 0, SpriteMeshType.FullRect, Vector4.one * region.Border);
                    sprite.name = region.Name;
                    AssetDatabase.CreateAsset(sprite, path);
                }
                fantasySprites[region.Name] = sprite;
            }
            heavyFont = AssetDatabase.LoadAssetAtPath<Font>(fantasyArt + "/Fonts/NanumGothic-ExtraBold.ttf");
            bodyFont = AssetDatabase.LoadAssetAtPath<Font>(fantasyArt + "/Fonts/NanumGothic-Bold.ttf");
            if (heavyFont == null || bodyFont == null) throw new InvalidOperationException("Fantasy UI fonts must be imported first.");
            font = heavyFont;
        }

        private static void Hide(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (child != null) child.gameObject.SetActive(false);
        }

        private static void RefinedSurface(Transform parent, bool button, float cornerSize = 24)
        {
            var bg = parent.Find("bg_surface").GetComponent<UnityEngine.UI.Image>();
            bg.sprite = fantasySprites["Parchment"];
            bg.type = UnityEngine.UI.Image.Type.Simple;
            bg.rectTransform.sizeDelta = Vector2.one * -Mathf.Min(18, cornerSize * .8f);
            var frame = parent.Find("img_frame").GetComponent<UnityEngine.UI.Image>();
            frame.sprite = fantasySprites[button ? "FrameButtonV2" : "FramePanelV2"];
            frame.type = UnityEngine.UI.Image.Type.Sliced;
            frame.preserveAspect = false;
            frame.pixelsPerUnitMultiplier = frame.sprite.border.x / cornerSize;
        }

        private static UnityEngine.UI.Image RoundPortrait(Transform parent, string name, float x, float y, float size)
        {
            var holder = Fixed(parent, name, x, y, size, size, center);
            var mask = Get<UnityEngine.UI.Image>(holder.gameObject);
            mask.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/UI/Dot.png");
            mask.color = Hex("96D0EA"); mask.raycastTarget = false;
            Get<UnityEngine.UI.Mask>(holder.gameObject).showMaskGraphic = true;
            var portrait = Image(holder, "img_portrait", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            portrait.preserveAspect = true;
            return portrait;
        }

        private static void Reposition(RectTransform rect, Transform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = center;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        private static void ButtonArt(UnityEngine.UI.Button button, string icon, UnityEngine.UI.Text label, float iconX, float iconSize, float labelX, float labelWidth)
        {
            RefinedSurface(button.transform, true, 20);
            Art(button.transform, "img_action", icon, iconX, 0, iconSize, iconSize);
            label.rectTransform.anchoredPosition = new Vector2(labelX, 0);
            label.rectTransform.sizeDelta = new Vector2(labelWidth, 48);
            label.font = heavyFont; label.fontStyle = FontStyle.Normal;
            label.resizeTextForBestFit = false;
            var gem = Art(button.transform, "img_gem", "Gem", 0, -button.GetComponent<RectTransform>().rect.height * .5f + 1, 24, 27);
            gem.transform.SetAsLastSibling();
            button.targetGraphic = button.transform.Find("bg_surface").GetComponent<UnityEngine.UI.Image>();
        }

        private static void RefineFantasyHUD(UIPanel_RaceHUD hud, Sys_Race race, RectTransform root)
        {
            ImportRefinedArt();
            // Retain existing scene references and authored groups while replacing their art.
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if (image.name == "img_frame" && image.transform.parent.Find("bg_surface") != null)
                    RefinedSurface(image.transform.parent, image.transform.parent.GetComponent<UnityEngine.UI.Button>() != null,
                        image.transform.parent.GetComponent<RectTransform>().rect.height < 50 ? 10 : 24);

            var course = root.Find("group_course");
            var title = course.Find("txt_title").GetComponent<UnityEngine.UI.Text>();
            title.fontSize = 29; title.rectTransform.sizeDelta = new Vector2(265, 40);
            hud.DistanceLabel.fontSize = 55;
            hud.DistanceLabel.rectTransform.sizeDelta = new Vector2(137, 69);
            hud.ShotLabel.gameObject.SetActive(false);
            var phase = root.Find("group_phase");
            Art(phase, "img_corner_arrow", "IconCorner", -54, 0, 25, 25);
            hud.PhaseLabel.rectTransform.anchoredPosition = new Vector2(14, 0);
            hud.PhaseLabel.rectTransform.sizeDelta = new Vector2(110, 30);
            hud.PhaseLabel.fontSize = 19;

            var progress = root.Find("group_progress");
            for (int i = 1; i < 7; i++)
                Image(progress, "img_segment_" + i, center, center, new Vector2(-311 + 622f * i / 7, 0),
                    new Vector2(1.3f, 13), new Color(1, 1, 1, .65f));
            var gloss = Image(progress, "img_gloss", center, center, new Vector2(0, 4), new Vector2(620, 3), new Color(1, 1, 1, .25f));
            gloss.raycastTarget = false;
            var handle = hud.ProgressHandle.GetComponent<UnityEngine.UI.Image>();
            handle.sprite = fantasySprites["IconFeather"];
            handle.rectTransform.sizeDelta = new Vector2(33, 43);
            handle.transform.SetAsLastSibling();

            hud.RankFrames = new UnityEngine.UI.Image[hud.RankBadges.Length];
            for (int i = 0; i < hud.RankBadges.Length; i++)
            {
                var badge = hud.RankBadges[i];
                var rim = Image(badge, "img_white_rim", center, center, Vector2.zero, new Vector2(41, 41), Color.white);
                rim.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/UI/Dot.png");
                rim.transform.SetAsFirstSibling();
                hud.RankFrames[i] = badge.Find("img_rank_frame").GetComponent<UnityEngine.UI.Image>();
                hud.RankFrames[i].enabled = i == hud.SelectedRunnerId;
                hud.RankLabels[i].fontSize = 27;
                var my = hud.MyBadges[i].GetComponent<RectTransform>();
                my.anchoredPosition = new Vector2(0, 30); my.sizeDelta = new Vector2(40, 23);
                RefinedSurface(my, true, 7);
                var label = my.Find("txt_my").GetComponent<UnityEngine.UI.Text>();
                label.fontSize = 15; label.rectTransform.sizeDelta = new Vector2(35, 25);
            }

            var status = root.Find("group_selected");
            Art(status, "img_feather", "IconFeather", -162, 22, 30, 43);
            Art(status, "img_right_wing", "WingRight", 330, 31, 45, 76);
            hud.FocusName.fontSize = 29; hud.FocusName.resizeTextMinSize = 22; hud.FocusName.resizeTextMaxSize = 29;
            hud.FocusName.rectTransform.anchoredPosition = new Vector2(6, 26);
            hud.FocusName.rectTransform.sizeDelta = new Vector2(290, 44);
            hud.FocusGap.fontSize = 22;
            hud.FocusRank.fontSize = 82;
            hud.FocusChange.rectTransform.anchoredPosition = new Vector2(285, -38);
            hud.FocusChange.rectTransform.sizeDelta = new Vector2(56, 28);
            hud.FocusChangeIcon = Art(status, "img_rank_change", "IconUp", 250, -38, 19, 24);
            hud.RankUpSprite = fantasySprites["IconUp"]; hud.RankDownSprite = fantasySprites["IconDown"];
            hud.FocusChangeIcon.gameObject.SetActive(false);

            var skill = hud.SkillRoot.transform;
            hud.SkillName.fontSize = 26;
            hud.SkillName.rectTransform.anchoredPosition = new Vector2(-2, -12);
            hud.SkillName.rectTransform.sizeDelta = new Vector2(238, 38);
            hud.SkillRunner.rectTransform.anchoredPosition = new Vector2(-2, 20);
            hud.SkillRunner.rectTransform.sizeDelta = new Vector2(238, 25);
            Caption(skill, "txt_effect", "속도", 142, -9, 51, 32, 21, Hex("925316"));
            Art(skill, "img_effect_up", "IconUp", 180, -9, 22, 31);

            Hide(hud.CameraButton.transform, "img_icon"); Hide(hud.SpeedButton.transform, "img_play");
            Hide(hud.SkipButton.transform, "img_skip"); Hide(hud.MenuButton.transform, "img_menu");
            ButtonArt(hud.CameraButton, "IconCamera", hud.CameraButtonLabel, -45, 43, 24, 86);
            ButtonArt(hud.SpeedButton, "IconFast", hud.SpeedButtonLabel, -41, 42, 27, 77);
            ButtonArt(hud.SkipButton, "IconSkip", hud.SkipButton.transform.Find("txt_label").GetComponent<UnityEngine.UI.Text>(), -53, 31, 17, 111);
            ButtonArt(hud.MenuButton, "IconMenu", hud.MenuButton.transform.Find("txt_label").GetComponent<UnityEngine.UI.Text>(), -42, 34, 24, 86);
            hud.CameraButtonLabel.fontSize = 24; hud.SpeedButtonLabel.fontSize = 27;
            hud.SkipButton.transform.Find("txt_label").GetComponent<UnityEngine.UI.Text>().fontSize = 22;
            Hide(hud.WorldMarker, "img_pointer");
            Art(hud.WorldMarker, "img_pointer_icon", "IconPointer", 0, -28, 23, 20);
            RefinedSurface(hud.WorldMarker, true, 10);
            hud.WorldMarkerText.fontSize = 20;

            var countdown = Fixed(root, "obj_countdown_plate", 0, 70, 340, 142, center);
            PanelSurface(countdown, Hex("213D75")); RefinedSurface(countdown, true, 25);
            Reposition(hud.CountdownLabel.rectTransform, countdown, 0, 0, 310, 110);
            hud.CountdownLabel.fontSize = 65; hud.CountdownRoot = countdown.gameObject;
            RefineResult(hud, race, root);
            RefineMenu(hud);
            RefineRaceOverlays(hud);
            foreach (var text in hud.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                text.font = text.fontSize >= 24 || text.name == "txt_my" ? heavyFont : bodyFont;
                text.fontStyle = FontStyle.Normal;
                text.lineSpacing = 1;
            }
            Ensure(resources + "/Prefabs/UI/Slot");
            var rowTemplate = UnityEngine.Object.Instantiate(hud.ResultRows[0].gameObject);
            rowTemplate.name = "UIItem_RaceResult";
            rowTemplate.GetComponent<RectTransform>().anchoredPosition3D = Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(rowTemplate, resources + "/Prefabs/UI/Slot/UIItem_RaceResult.prefab");
            UnityEngine.Object.DestroyImmediate(rowTemplate);
        }

        public static void RefineRaceOverlays(UIPanel_RaceHUD hud)
        {
            var countdown = hud.CountdownRoot.transform;
            var label = hud.CountdownLabel.rectTransform;
            if (label.parent != countdown) label.SetParent(countdown, false);
            label.anchorMin = label.anchorMax = label.pivot = center;
            label.anchoredPosition3D = Vector3.zero;
            label.sizeDelta = new Vector2(310, 110);
            label.localScale = Vector3.one;
            label.localRotation = Quaternion.identity;
            // uGUI draws siblings in order: background, frame, then the numeral.
            countdown.Find("bg_surface").SetAsFirstSibling();
            countdown.Find("img_frame").SetSiblingIndex(1);
            label.SetAsLastSibling();
            countdown.SetAsLastSibling();
            hud.ResultRoot.transform.SetAsLastSibling();
            hud.MenuRoot.transform.SetAsLastSibling();

            // World labels sit behind the HUD and transient countdown, with crisp text.
            hud.WorldMarker.SetAsFirstSibling();
            hud.WorldMarker.Find("bg_surface").GetComponent<UnityEngine.UI.Image>().color = new Color(1f, .94f, .79f, .76f);
            var markerFrame = hud.WorldMarker.Find("img_frame").GetComponent<UnityEngine.UI.Image>();
            markerFrame.color = new Color(1f, 1f, 1f, .88f);
            // The frame artwork also has an opaque center; omit it so the parchment stays translucent.
            markerFrame.fillCenter = false;
            hud.WorldMarker.Find("img_pointer_icon").GetComponent<UnityEngine.UI.Image>().color = new Color(1f, 1f, 1f, .84f);
            var textColor = hud.WorldMarkerText.color;
            textColor.a = 1f;
            hud.WorldMarkerText.color = textColor;
        }

        private static void RefineResult(UIPanel_RaceHUD hud, Sys_Race race, RectTransform root)
        {
            var popup = hud.ResultRoot.GetComponent<RectTransform>();
            popup.anchorMin = Vector2.zero; popup.anchorMax = Vector2.one;
            popup.offsetMin = popup.offsetMax = Vector2.zero;
            Hide(popup, "bg_surface"); Hide(popup, "img_frame"); Hide(popup, "txt_title");
            hud.ResultDetails.gameObject.SetActive(false);
            var dim = Image(popup, "bg_dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.025f,.055f,.13f,.83f));
            dim.raycastTarget = true; dim.transform.SetAsFirstSibling();
            var sheet = Fixed(popup, "group_result_sheet", 0, 0, 600, 980, center);
            PanelSurface(sheet, Color.white); RefinedSurface(sheet, false, 35);
            var shield = Art(sheet, "img_victory_crest", "VictoryCrest", 0, 451, 390, 139);
            Caption(shield.transform, "txt_ribbon", "RACE RESULT", 0, -40, 200, 28, 19, Color.white);
            Caption(sheet, "txt_course_title", "프론테라 초원컵", 0, 354, 480, 35, 21, goldInk);
            Reposition(hud.ResultName.rectTransform, sheet, 0, 308, 500, 62);
            hud.ResultName.fontSize = 38; hud.ResultName.text = "우승을 축하합니다!";
            hud.WinnerPortrait = RoundPortrait(sheet, "obj_winner_portrait", -172, 216, 104);
            Art(sheet, "img_winner_frame", "PortraitFrame", -172, 216, 137, 142);
            hud.WinnerMount = Caption(sheet, "txt_winner_mount", "우승 주자", 53, 248, 305, 38, 26, ink);
            hud.WinnerTime = Caption(sheet, "txt_winner_time", "0.000 s", 53, 204, 305, 51, 37, ink);
            Caption(sheet, "txt_record_caption", "우승 기록", 53, 170, 260, 25, 16, goldInk);
            Image(sheet, "img_divider", center, center, new Vector2(0,139), new Vector2(518,1.5f), Hex("D5B778"));
            Caption(sheet, "txt_rank_heading", "순위", -224, 112, 60, 25, 16, goldInk);
            Caption(sheet, "txt_runner_heading", "주자", -82, 112, 180, 25, 16, goldInk, TextAnchor.MiddleLeft);
            Caption(sheet, "txt_time_heading", "완주 기록", 180, 112, 135, 25, 16, goldInk);
            hud.ResultRows = new UIItem_RaceResult[race.Entrants.Length];
            for (int i = 0; i < hud.ResultRows.Length; i++)
            {
                var row = Fixed(sheet, "group_result_row_" + i, 0, 60-i*74, 528, 66, center);
                PanelSurface(row, Color.white); RefinedSurface(row, true, 12);
                var item = Get<UIItem_RaceResult>(row.gameObject);
                item.Background = row.Find("bg_surface").GetComponent<UnityEngine.UI.Image>();
                var rank = Image(row, "bg_rank", center, center, new Vector2(-224,0), new Vector2(38,38), i == 0 ? Hex("C99835") : Hex("234675"));
                rank.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art + "/UI/Dot.png");
                item.RankLabel = Caption(row, "txt_rank", (i+1).ToString(), -224, 0, 36, 38, 24, Color.white);
                item.Portrait = RoundPortrait(row, "obj_portrait", -168, 0, 46);
                item.NameLabel = Caption(row, "txt_name", race.Entrants[i].Name, -29, 12, 202, 25, 21, ink, TextAnchor.MiddleLeft);
                item.MountLabel = Caption(row, "txt_mount", race.Entrants[i].Mount, -29, -13, 202, 22, 14, goldInk, TextAnchor.MiddleLeft);
                item.TimeLabel = Caption(row, "txt_time", "0.000 s", 180, 10, 136, 27, 23, ink);
                item.GapLabel = Caption(row, "txt_gap", "", 180, -15, 136, 21, 13, goldInk);
                var my = Fixed(row, "obj_my", 83, 13, 32, 18, center);
                PanelSurface(my, Hex("FFDB75")); RefinedSurface(my, true, 5);
                Caption(my, "txt_my", "MY", 0, 0, 30, 20, 11, Hex("714514"));
                item.MyBadge = my.gameObject; my.gameObject.SetActive(false);
                item.Portrait.sprite = race.Entrants[i].FacePortrait;
                hud.ResultRows[i] = item;
            }
            hud.ResultSummary = Caption(sheet, "txt_my_summary", "내 주자의 최종 기록", 0, -365, 510, 32, 20, ink);
            Reposition(hud.ResultReplay.GetComponent<RectTransform>(), sheet, 0, -423, 350, 72);
            var replayLabel = hud.ResultReplay.transform.Find("txt_label").GetComponent<UnityEngine.UI.Text>();
            replayLabel.text = "다시 달리기"; replayLabel.fontSize = 26;
            ButtonArt(hud.ResultReplay, "IconReplay", replayLabel, -110, 38, 27, 228);
            popup.gameObject.SetActive(false);
        }

        private static void RefineMenu(UIPanel_RaceHUD hud)
        {
            var panel = hud.MenuRoot.transform.Find("group_panel").GetComponent<RectTransform>();
            panel.sizeDelta = new Vector2(500, 620);
            RefinedSurface(panel, false, 33);
            Art(panel, "img_menu_crown", "IconCrown", 0, 270, 88, 72);
            var title = panel.Find("txt_title").GetComponent<UnityEngine.UI.Text>();
            title.rectTransform.anchoredPosition = new Vector2(0,196); title.fontSize = 32;
            Caption(panel, "txt_subtitle", "레이스 관전 설정", 0, 157, 370, 29, 18, goldInk);
            var buttons = new[] { hud.PauseButton, hud.CutsceneButton, hud.RestartButton, hud.MenuCloseButton };
            var icons = new[] { "IconPlay", "IconCamera", "IconReplay", "IconClose" };
            var positions = new[] { 91f, 1f, -89f, -214f };
            for (int i = 0; i < buttons.Length; i++)
            {
                float width = i == 3 ? 240 : 380;
                Reposition(buttons[i].GetComponent<RectTransform>(), panel, 0, positions[i], width, 72);
                var label = buttons[i].transform.Find("txt_label").GetComponent<UnityEngine.UI.Text>();
                label.fontSize = i == 1 ? 23 : 25;
                ButtonArt(buttons[i], icons[i], label, i == 3 ? -65 : -135, 34, 22, i == 3 ? 125 : 267);
            }
            hud.MenuRoot.SetActive(false);
        }
    }
}
