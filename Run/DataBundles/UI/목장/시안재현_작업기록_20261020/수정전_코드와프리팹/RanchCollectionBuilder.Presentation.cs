using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static partial class RanchCollectionBuilder
    {
        // Repair the existing prefab in place. Keep its objects, references and scene instances.
        private static void ApplyPresentation(UIPanel_RanchCollection panel)
        {
            panel.CollectionArt = art;
            var layout = panel.Layout;
            foreach (var image in panel.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.sprite == null || image.type == UnityEngine.UI.Image.Type.Filled) continue;
                string key = image.sprite.name;
                if (key.StartsWith("info_") || key.StartsWith("portrait_") ||
                    key.StartsWith("skill_") || key == "preview_frame")
                    image.type = UnityEngine.UI.Image.Type.Sliced;
                // These painted parts include large inset areas; slicing collapses their silhouettes.
                if (key.EndsWith("_selected") || key.StartsWith("ticket_") || key.StartsWith("note_") ||
                    key == "gauge_bg" || key == "gauge_frame" || key.StartsWith("scroll_horizontal") || key.StartsWith("scroll_vertical"))
                    image.type = UnityEngine.UI.Image.Type.Simple;
            }
            SetRect(layout.Find("group_title"), 416, -5, 448, 110);
            foreach (RectTransform child in layout.Find("group_title")) SetRect(child, 0, 0, 448, 110);
            SetRect(layout.Find("txt_title"), 480, 23, 320, 51);
            var title = layout.Find("txt_title").GetComponent<Text>();
            title.font = art.BodyFont; title.fontStyle = FontStyle.Bold; title.fontSize = 35;
            EnsurePic(layout, "img_paw_left", art.Get("icon_paw"), 439, 34, 34, 34, true);
            EnsurePic(layout, "img_paw_right", art.Get("icon_paw"), 807, 34, 34, 34, true);
            SetRect(layout.Find("img_lantern_left"), 388, 18, 34, 58);
            SetRect(layout.Find("img_lantern_right"), 860, 18, 34, 58);
            panel.BackButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            RepairRoster(panel);
            panel.CharacterSprites = Mgr_Table.Demo.Monsters.Select(m => art.Get(m.Id + "_body")).ToArray();
            if (panel.TrainingMode) RepairTraining(panel);
            else RepairMonsterDetails(panel);
            PreviewDefaults(panel);
        }

        private static void RepairRoster(UIPanel_RanchCollection panel)
        {
            bool horizontal = panel.TrainingMode;
            int count = panel.RosterButtons.Length;
            panel.RosterPortraits = new UnityEngine.UI.Image[count];
            var roster = panel.Roster;
            float cardWidth = horizontal ? 225 : 188;
            float cardHeight = horizontal ? 94 : 174;
            SetRect(roster.transform, horizontal ? 32 : 0, 0, horizontal ? 697 : 188, horizontal ? 94 : 554);
            SetRect(roster.viewport, 0, 0, horizontal ? 697 : 188, horizontal ? 94 : 554);
            SetRect(roster.content, 0, 0, horizontal ? count * 235 - 10 : 188, horizontal ? 94 : count * 184 - 10);
            for (int i = 0; i < count; i++)
            {
                var card = panel.RosterButtons[i].transform;
                SetRect(card, horizontal ? i * 235 : 0, horizontal ? 0 : i * 184, cardWidth, cardHeight);
                foreach (string name in new[] { "bg_surface", "img_frame", "img_selected" })
                    SetRect(card.Find(name), 0, 0, cardWidth, cardHeight);
                var face = card.Find("img_face").GetComponent<UnityEngine.UI.Image>();
                face.sprite = art.Get(Mgr_Table.Demo.Monsters[i].Id + "_face");
                panel.RosterPortraits[i] = face;
                if (horizontal)
                {
                    SetRect(face.transform, 11, 10, 77, 74);
                    SetRect(panel.RosterNames[i].transform, 91, 16, 123, 31);
                    SetRect(panel.RosterLevels[i].transform, 93, 49, 119, 27);
                    panel.RosterNames[i].fontSize = 16;
                }
                else
                {
                    var scenery = EnsurePic(card, "bg_portrait_scene", DemoUI.Art.Get("training_bg"), 18, 18, 152, 111);
                    scenery.transform.SetSiblingIndex(1);
                    SetRect(face.transform, 26, 8, 136, 113);
                    var plate = card.Find("group_nameplate");
                    if (plate == null) plate = Pair(card, "group_nameplate", "nameplate", 7, 105, 174, 51);
                    SetRect(plate, 7, 105, 174, 51);
                    foreach (Transform layer in plate) SetRect(layer, 0, 0, 174, 51);
                    plate.SetSiblingIndex(face.transform.GetSiblingIndex() + 1);
                    SetRect(panel.RosterNames[i].transform, 12, 109, 164, 22);
                    SetRect(panel.RosterLevels[i].transform, 12, 130, 164, 20);
                    panel.RosterNames[i].fontSize = 18;
                    panel.RosterLevels[i].fontSize = 16;
                    panel.RosterNames[i].color = panel.RosterLevels[i].color = paper;
                }
                panel.RosterNames[i].fontStyle = FontStyle.Bold;
                panel.SelectionFrames[i].transform.SetAsLastSibling();
            }
            // ScrollRect still handles overflow if more cards are authored later.
            roster.horizontalNormalizedPosition = 0;
            roster.verticalNormalizedPosition = 1;
            Canvas.ForceUpdateCanvases();
        }

        private static void RepairMonsterDetails(UIPanel_RanchCollection panel)
        {
            var layout = panel.Layout;
            var display = layout.Find("group_character");
            SetRect(display.Find("obj_clip"), 14, 14, 407, 411);
            var backdrop = display.Find("obj_clip/bg_illustration").GetComponent<UnityEngine.UI.Image>();
            backdrop.sprite = DemoUI.Art.Get("training_bg"); backdrop.type = UnityEngine.UI.Image.Type.Simple;
            SetRect(backdrop.transform, -162, 0, 731, 411);
            SetRect(panel.Character.transform, 26, 68, 355, 330);
            var stats = layout.Find("group_stats");
            panel.HeaderPortrait = EnsurePic(stats, "img_monster", art.Get("Poring_face"), 20, 13, 57, 49, true);
            SetRect(panel.MonsterName.transform, 88, 17, 382, 43);
            panel.MonsterName.fontStyle = FontStyle.Bold;
            for (int i = 0; i < 5; i++)
            {
                float y = 73 + i * 36;
                SetRect(stats.Find("img_stat_" + i), 24, y, 29, 29);
                SetRect(stats.Find("txt_stat_" + i), 62, y, 83, 30);
                SetRect(stats.Find("bg_gauge_" + i), 152, y + 5, 251, 22);
                SetRect(panel.StatFills[i].transform, 159, y + 9, 237, 14);
                SetRect(stats.Find("img_gauge_frame_" + i), 152, y + 5, 251, 22);
                SetRect(panel.StatValues[i].transform, 409, y, 65, 30);
                panel.StatValues[i].fontStyle = FontStyle.Bold;
            }
            SetRect(panel.Story.transform, 27, 59, 439, 86);
            panel.Story.fontSize = 19; panel.Story.lineSpacing = 1.16f;
            layout.Find("group_story/txt_heading").GetComponent<Text>().fontStyle = FontStyle.Bold;
            for (int i = 0; i < panel.SkillButtons.Length; i++)
            {
                var card = panel.SkillButtons[i].transform;
                SetRect(card.Find("img_icon"), 19, 48, 49, 49);
                SetRect(panel.SkillLabels[i].transform, 76, 45, 219, 54);
                panel.SkillLabels[i].fontSize = 20;
                panel.SkillLabels[i].fontStyle = FontStyle.Bold;
                card.Find("txt_heading").GetComponent<Text>().fontStyle = FontStyle.Bold;
            }
            panel.UniqueSkillIcon = panel.SkillButtons[0].transform.Find("img_icon").GetComponent<UnityEngine.UI.Image>();
        }

        private static void RepairTraining(UIPanel_RanchCollection panel)
        {
            var preview = panel.Layout.Find("group_training_preview");
            var clip = preview.Find("obj_clip");
            SetRect(clip, 14, 14, 756, 398);
            var backdrop = EnsurePic(clip, "bg_training", DemoUI.Art.Get("training_bg"), 0, 0, 756, 398);
            backdrop.transform.SetAsFirstSibling();
            SetRect(panel.TrainingFilm.transform, 0, 0, 756, 398);
            panel.TrainingClips = Mgr_Table.Demo.Monsters.Select(m => Resources.Load<Data_LobbyAnimation>("UI/Demo/Training/" + m.Id)).ToArray();
            panel.TrainingFilm.ConfigureStates(panel.TrainingClips[0], null);
            panel.TrainingFilm.SetPlaying(false);
            panel.TrainingActor = EnsurePic(clip, "img_training_actor", art.Get("Poring_body"), 248, 98, 260, 276, true);
            panel.TrainingActor.gameObject.SetActive(false);
            panel.GrowthLevel.supportRichText = true;
            panel.GrowthLevel.fontStyle = FontStyle.Bold;
            panel.GrowthSpeed.fontStyle = FontStyle.Bold;
            panel.GrowthPassive.fontStyle = FontStyle.Bold;
            panel.TrainLabel.fontStyle = FontStyle.Bold;
            panel.Requirements.fontStyle = FontStyle.Bold;
            foreach (string group in new[] { "group_growth", "group_requirements" })
                panel.Layout.Find(group + "/txt_heading").GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        private static UnityEngine.UI.Image EnsurePic(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, bool preserve = false)
        {
            var existing = parent.Find(name);
            var image = existing != null ? existing.GetComponent<UnityEngine.UI.Image>() : Pic(parent, name, null, x, y, width, height, preserve);
            image.sprite = sprite; image.preserveAspect = preserve; image.raycastTarget = false;
            SetRect(image.transform, x, y, width, height);
            return image;
        }

        private static void SetRect(Transform transform, float x, float y, float width, float height)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
