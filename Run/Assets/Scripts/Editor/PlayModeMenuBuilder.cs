using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class PlayModeMenuBuilder
    {
        public static readonly string[] ScenePaths = {
            Define_Scene.Path(Define_Scene.Title), Define_Scene.Path(Define_Scene.Oval),
            Define_Scene.Path(Define_Scene.Prontera), Define_Scene.Path(Define_Scene.OvalToon),
            Define_Scene.Path(Define_Scene.PronteraToon), Define_Scene.Path(Define_Scene.Lobby), Define_Scene.Path(Define_Scene.Sprite2D)
        };
        private static Font font;
        [MenuItem("Tools/Project Run/Build play mode menu")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play mode first.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            for (int i = 0; i < ScenePaths.Length; i++)
            {
                // The lobby owns its own UI; only include it in scene navigation and the build.
                if (ScenePaths[i] == Define_Scene.Path(Define_Scene.Lobby) || ScenePaths[i] == Define_Scene.Path(Define_Scene.Sprite2D)) continue;
                var scene = EditorSceneManager.OpenScene(ScenePaths[i]);
                var existing = Object.FindFirstObjectByType<UIPanel_PlayMode>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    if (i == 0) ConfigureTitle(existing);
                    else { existing.Scenes = new[] { Define_Scene.Title }; PlayModeLandscapeLayout.ApplyPlayMode(existing, false); }
                    SaveMenuScene(scene);
                    continue;
                }
                var root = GameObject.Find("UICanvas_Root") ?? new GameObject("UICanvas_Root", typeof(RectTransform));
                var canvasRect = Rect(root.transform, "Canvas_PlayMode", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
                var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>(); PlayModeLandscapeLayout.ConfigureCanvas(scaler);
                canvasRect.gameObject.AddComponent<GraphicRaycaster>();
                var panelRect = Rect(canvasRect, "UIPanel_PlayMode", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                panelRect.gameObject.SetActive(false);
                var panel = panelRect.gameObject.AddComponent<UIPanel_PlayMode>();
                if (i == 0)
                {
                    var bg = Rect(panelRect,"bg_title",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
                    bg.color = new Color(.035f,.06f,.11f); bg.raycastTarget = false;
                }
                panel.SafeArea = Rect(panelRect,"obj_safe_area",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
                if (i == 0)
                {
                    Label(panel.SafeArea,"txt_title","PROJECT RUN",new Vector2(0,310),new Vector2(620,90),54);
                    Label(panel.SafeArea,"txt_description","같은 6종 몬스터의 외형을 비교해 보세요",new Vector2(0,230),new Vector2(620,60),28);
                    Button(panel.SafeArea,"btn_all3d","기존 경기장",30);
                    Button(panel.SafeArea,"btn_lobby","로비",-110);
                    panel.StatusLabel = Label(panel.SafeArea,"txt_status","",new Vector2(0,-310),new Vector2(620,80),21);
                    ConfigureTitle(panel);
                }
                else
                {
                    var button = Button(panel.SafeArea,"btn_title","타이틀",0);
                    var rect = (RectTransform)button.transform; rect.anchorMin = rect.anchorMax = new Vector2(1,1); rect.sizeDelta = new Vector2(120,48); rect.anchoredPosition = new Vector2(-72,-174);
                    button.GetComponentInChildren<Text>().fontSize = 22;
                    panel.Buttons = new[] { button }; panel.Scenes = new[] { "Scene_Title" };
                }
                PlayModeLandscapeLayout.ApplyPlayMode(panel, i == 0);
                panelRect.gameObject.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(panelRect.gameObject,"Assets/Resources/Prefabs/UI/Panel/" + (i == 0 ? "UIPanel_PlayMode" : "UIPanel_PlayModeReturn") + ".prefab");
                if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                if (Object.FindFirstObjectByType<Mgr_UI>() == null)
                {
                    var managers = GameObject.Find("[Managers]") ?? new GameObject("[Managers]");
                    var manager = new GameObject("Mgr_UI"); manager.transform.SetParent(managers.transform); manager.AddComponent<Mgr_UI>();
                }
                EditorSceneManager.SaveScene(scene);
            }
            EnsureBuildScenes();
            EditorSceneManager.OpenScene(ScenePaths[0]);
            AssetDatabase.SaveAssets();
        }

        // Reuse existing menu objects and their art; only add missing course/character combinations.
        public static void ConfigureTitle(UIPanel_PlayMode panel)
        {
            var root = panel.SafeArea;
            var original = root.Find("btn_oval") ?? root.Find("btn_all3d");
            if (original == null) throw new System.InvalidOperationException("Title button template is missing.");
            original.name = "btn_oval";
            var toon = root.Find("btn_oval_toon") ?? root.Find("btn_lookdev_race");
            if (toon != null) toon.name = "btn_oval_toon";
            string[] names = { "btn_oval", "btn_prontera", "btn_oval_toon", "btn_prontera_toon" };
            string[] labels = { "기존 경기장 · 타원\n기존 캐릭터", "신규 경기장 · 프론테라\n기존 캐릭터", "기존 경기장 · 타원\n신규 카툰 캐릭터", "신규 경기장 · 프론테라\n신규 카툰 캐릭터" };
            var buttons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
            for (int i = 0; i < names.Length; i++)
            {
                var item = root.Find(names[i]);
                if (item == null) { item = Object.Instantiate(original.gameObject, root, false).transform; item.name = names[i]; }
                var button = item.GetComponent<UnityEngine.UI.Button>();
                button.targetGraphic.color = i < 2 ? new Color(.12f,.3f,.46f) : new Color(.18f,.40f,.34f);
                var label = item.GetComponentInChildren<UnityEngine.UI.Text>(true);
                label.text = labels[i]; label.fontSize = 28; label.lineSpacing = 1.15f;
                buttons.Add(button);
            }
            var scenes = new System.Collections.Generic.List<string> { Define_Scene.Oval, Define_Scene.Prontera, Define_Scene.OvalToon, Define_Scene.PronteraToon };
            var spriteRace = root.Find("btn_sprite_2d");
            if (spriteRace == null) { spriteRace = Object.Instantiate(original.gameObject, root, false).transform; spriteRace.name = "btn_sprite_2d"; }
            var spriteButton = spriteRace.GetComponent<UnityEngine.UI.Button>();
            spriteButton.targetGraphic.color = new Color(.25f,.43f,.18f);
            spriteRace.GetComponentInChildren<UnityEngine.UI.Text>(true).text = "2D 경기장 · 2D 몬스터";
            buttons.Add(spriteButton); scenes.Add(Define_Scene.Sprite2D);
            var lobby = root.Find("btn_lobby");
            if (lobby == null) throw new System.InvalidOperationException("Title lobby button is missing.");
            lobby.GetComponentInChildren<UnityEngine.UI.Text>(true).text = "로비";
            buttons.Add(lobby.GetComponent<UnityEngine.UI.Button>()); scenes.Add(Define_Scene.Lobby);
            panel.Buttons = buttons.ToArray(); panel.Scenes = scenes.ToArray();
            root.Find("txt_description").GetComponent<UnityEngine.UI.Text>().text = "경기장과 캐릭터 조합을 선택하세요";
            PlayModeLandscapeLayout.ApplyPlayMode(panel, true);
            EditorUtility.SetDirty(panel);
        }

        public static void ConnectTitle()
        {
            string path = "Assets/Resources/Prefabs/UI/Panel/UIPanel_PlayMode.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureTitle(contents.GetComponent<UIPanel_PlayMode>()); PrefabUtility.SaveAsPrefabAsset(contents, path); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var scene = EditorSceneManager.OpenScene(Define_Scene.Path(Define_Scene.Title));
            ConfigureTitle(Object.FindFirstObjectByType<UIPanel_PlayMode>(FindObjectsInactive.Include));
            SaveMenuScene(scene);
            EnsureBuildScenes();
            AssetDatabase.SaveAssets();
        }

        public static void EnsureBuildScenes()
        {
            var known = ScenePaths.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            known.AddRange(EditorBuildSettings.scenes.Where(scene => !ScenePaths.Contains(scene.path)));
            EditorBuildSettings.scenes = known.ToArray();
        }

        private static void SaveMenuScene(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); rect.anchorMin=min;rect.anchorMax=max;rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
        }
        private static Text Label(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize)
        {
            var text=Rect(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),position,size).gameObject.AddComponent<Text>();text.font=font;text.text=value;text.fontSize=fontSize;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;
        }
        private static Button Button(Transform parent,string name,string value,float y)
        {
            var rect=Rect(parent,name,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,y),new Vector2(540,110));
            var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.12f,.3f,.46f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            var label=Label(rect,"txt_label",value,Vector2.zero,Vector2.zero,34);var labelRect=label.rectTransform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;return button;
        }
    }
}
