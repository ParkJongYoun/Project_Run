using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectRun.Race.Editor
{
    /// <summary>One-time migration of the empty shell. Existing authored layouts are never rebuilt.</summary>
    public static class DemoRacePrefabBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_DemoRace.prefab";

        [MenuItem("Tools/Project Run/Demo/Open Race UI Prefab")]
        public static void OpenPrefab()
        {
            EnsurePrefab();
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        }

        public static void EnsurePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before editing the race UI prefab.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing race UI prefab: " + PrefabPath);
            // Reinstalling or opening a completed prefab must preserve all designer changes.
            if (prefab.transform.childCount > 0)
            {
                MigrateVideoOnly();
                Validate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.SetActive(false);
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1280, 720);
                var view = new SerializedObject(root.GetComponent<UIPanel_DemoRace>());
                BuildHUD(root.transform, view);
                BuildVideo(root.transform, view);
                view.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                Validate(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("DEMO_RACE_PREFAB_READY " + PrefabPath);
        }

        public static void Validate(GameObject root)
        {
            var panel = root.GetComponent<UIPanel_DemoRace>();
            if (panel == null) throw new InvalidOperationException("Missing UIPanel_DemoRace component.");
            var serialized = new SerializedObject(panel);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                if (property.objectReferenceValue == null)
                    throw new InvalidOperationException("Unassigned race UI reference: " + property.name);
                var component = property.objectReferenceValue as Component;
                if (component != null && !component.transform.IsChildOf(root.transform))
                    throw new InvalidOperationException("External race UI reference: " + property.name);
            }
            var feed = root.GetComponentInChildren<UIItem_RaceCommentary>(true);
            if (feed == null || feed.Lines == null || feed.Lines.Length != 3 || Array.Exists(feed.Lines, line => line == null))
                throw new InvalidOperationException("Missing authored commentary rows.");
        }

        public static void MigrateVideoOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play before migrating the race UI prefab.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var legacy = root.transform.Find("obj_safe_area/group_comic");
                if (legacy == null) return;
                UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                Validate(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static T Bind<T>(SerializedObject view, string field, T component) where T : UnityEngine.Object
        {
            var property = view.FindProperty(field);
            if (property == null) throw new InvalidOperationException("Unknown race UI field: " + field);
            property.objectReferenceValue = component;
            return component;
        }

        private static void Anchor(Component target, float x, float y, float offsetX, float offsetY)
        {
            var rect = (RectTransform)target.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            rect.anchoredPosition = new Vector2(offsetX, offsetY);
        }

        private static Button Button(SerializedObject view, string field, Transform parent, string name, string caption,
            float x, float y, float width, float height, bool primary = true, string labelField = null)
        {
            var button = Bind(view, field, DemoUI.Button(parent, name, caption, x, y, width, height, null, primary));
            if (labelField != null) Bind(view, labelField, button.GetComponentInChildren<Text>());
            return button;
        }

        private static void BuildHUD(Transform root, SerializedObject view)
        {
            var safe = Bind(view, "SafeArea", DemoUI.Fill(root, "obj_safe_area"));
            var header = DemoUI.Image(safe, "bg_header", null, 0, 315, 1216, 66);
            header.color = new Color(.04f, .14f, .12f, .88f);
            Anchor(header, .5f, 1, 0, -45);
            DemoUI.Text(header.transform, "txt_cup", "프론테라컵", -447, 0, 280, 55, 29, DemoUI.Paper, TextAnchor.MiddleLeft, true);
            Bind(view, "RemainingLabel", DemoUI.Text(header.transform, "txt_distance", "남은 거리  780m", 340, 0, 470, 55, 26, DemoUI.Paper, TextAnchor.MiddleRight));
            var standings = DemoUI.Image(safe, "bg_standings", null, -509, 133, 206, 275);
            standings.color = new Color(.04f, .14f, .12f, .78f);
            Anchor(standings, 0, 1, 131, -227);
            Bind(view, "StandingsLabel", DemoUI.Text(standings.transform, "txt_standings",
                "1  마이너 우로스\n<color=#FFE99A>2  포링 ◀</color>\n3  스모키\n4  새비지\n5  루나틱\n6  에드가", 0, 0, 183, 252, 21, DemoUI.Paper, TextAnchor.MiddleLeft));
            var footer = DemoUI.Image(safe, "bg_footer", null, 0, -296, 1216, 106);
            footer.color = new Color(.04f, .14f, .12f, .9f);
            Anchor(footer, .5f, 0, 0, 64);
            Bind(view, "HeroImage", DemoUI.Image(footer.transform, "img_hero", "Poring_face", -550, 0, 84, 84, true));
            Bind(view, "RankLabel", DemoUI.Text(footer.transform, "txt_rank", "2위 / 6", -425, 17, 160, 53, 32, DemoUI.Paper));
            Bind(view, "HeroNameLabel", DemoUI.Text(footer.transform, "txt_name", "포링", -425, -29, 190, 35, 18, DemoUI.Paper));
            var commentary = Bind(view, "CommentaryLabel", DemoUI.Text(footer.transform, "txt_commentary",
                "선택한 친구의 첫 레이스를 응원해 주세요", 7, 11, 600, 65, 24, DemoUI.Paper));
            var feed = Bind(view, "CommentaryFeed", UIItem_RaceCommentary.Create(null, commentary));
            feed.Lines[0].text = "출발! 첫 번째 코너를 향해 달립니다";
            feed.Lines[1].text = "잠시 후 출발! 여섯 몬스터가 준비합니다";
            DemoUI.Image(footer.transform, "bg_stamina", "gauge_bg", 2, -34, 456, 13);
            var stamina = Bind(view, "StaminaImage", DemoUI.Image(footer.transform, "img_stamina", "gauge_1", 2, -34, 456, 11));
            stamina.type = Image.Type.Filled;
            stamina.fillMethod = Image.FillMethod.Horizontal;
            Button(view, "SpeedButton", footer.transform, "btn_speed", "1×", 374, 11, 90, 52, labelField: "SpeedLabel");
            Button(view, "PauseButton", footer.transform, "btn_pause", "정지", 478, 11, 80, 52, labelField: "PauseLabel");
            Button(view, "ResultButton", footer.transform, "btn_skip", "결과", 559, 11, 76, 52);
            Anchor(Button(view, "LobbyButton", safe, "btn_lobby", "로비", 535, 230, 136, 45, false), 1, 1, -105, -130);
            safe.Find("btn_lobby").gameObject.SetActive(false);
            var countdown = Bind(view, "CountdownLabel", DemoUI.Text(safe, "txt_countdown", "", 0, 20, 800, 150, 88, DemoUI.Paper, TextAnchor.MiddleCenter, true));
            countdown.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3, -4);

            BuildResult(safe, view);
        }

        private static void BuildResult(Transform safe, SerializedObject view)
        {
            var result = Bind(view, "ResultRoot", DemoUI.Fill(safe, "group_result"));
            DemoUI.Shade(result, "bg_result_dim", new Color(.03f, .10f, .08f, .78f)).raycastTarget = true;
            var panel = DemoUI.Panel(result, "group_result_card", 1060, 580);
            Bind(view, "ResultTitle", DemoUI.Text(panel, "txt_title", "프론테라컵 우승!", 0, 224, 940, 75, 45, DemoUI.Green, TextAnchor.MiddleCenter, true));
            Bind(view, "ResultHero", DemoUI.Image(panel, "img_winner", "Poring_body", -316, -5, 305, 350, true));
            Bind(view, "ResultName", DemoUI.Text(panel, "txt_name", "포링", 164, 123, 550, 65, 38));
            Bind(view, "ResultSummary", DemoUI.Text(panel, "txt_summary",
                "1위  ·  38.00초\nLv.5  ·  스피드 270\n전술  추입\n고유  포링 점프\n후천  지치지 않는 발", 164, -26, 560, 224, 25, DemoUI.Ink, TextAnchor.MiddleLeft));
            Button(view, "ReplayButton", panel, "btn_new_session", "처음부터 다시 체험", -197, -226, 390, 64);
            Button(view, "EndDemoButton", panel, "btn_end_demo", "데모 종료", 248, -226, 310, 64, false);
            result.gameObject.SetActive(false);
        }

        private static void BuildVideo(Transform root, SerializedObject view)
        {
            var video = Bind(view, "VideoRoot", DemoUI.Fill(root, "group_skill_video"));
            Bind(view, "VideoOpacity", video.gameObject.AddComponent<CanvasGroup>());
            DemoUI.Shade(video, "bg_video", Color.black).raycastTarget = true;
            var image = DemoUI.Fill(video, "img_video");
            Bind(view, "VideoImage", image.gameObject.AddComponent<RawImage>()).raycastTarget = false;
            var fit = image.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9f;
            var safe = Bind(view, "VideoSafeArea", DemoUI.Fill(video, "obj_safe_area"));
            var title = Bind(view, "VideoTitle", DemoUI.Text(safe, "txt_skill", "포링 점프", 0, -45, 900, 65, 36, DemoUI.Paper, TextAnchor.MiddleCenter, true));
            Anchor(title, .5f, 1, 0, -45);
            title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2, -2);
            video.gameObject.SetActive(false);
        }
    }
}
