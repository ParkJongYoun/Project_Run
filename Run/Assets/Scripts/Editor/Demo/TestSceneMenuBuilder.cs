using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ProjectRun.Race.Editor
{
    public static class TestSceneMenuBuilder
    {
        public const string Folder = "Assets/Scenes/Test";
        public const string Title = "Scene_TestTitle";
        public const string TitlePath = Folder + "/" + Title + ".unity";
        private static Font font;

        public static string[] ScenePaths() => new[] { TitlePath }.Concat(
            AssetDatabase.FindAssets("t:Scene", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path != TitlePath).OrderBy(path => path, StringComparer.Ordinal)).ToArray();

        [MenuItem("Tools/Project Run/Test Scenes/Connect scene buttons")]
        public static void Build()
        {
            RequireIdleScenes();
            var paths = ScenePaths();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitlePath) == null || paths.Length < 2)
                throw new InvalidOperationException("Test title or test scenes are missing.");
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var titleScene = EditorSceneManager.OpenScene(TitlePath);
                var panel = UnityEngine.Object.FindFirstObjectByType<UIPanel_PlayMode>(FindObjectsInactive.Include);
                if (panel == null) panel = CreatePanel();
                panel.gameObject.SetActive(false);
                var background = GetRect(panel.transform, "bg_title", Vector2.zero, Vector2.zero);
                Stretch(background);
                SetImage(background, new Color(.035f,.055f,.085f));
                background.SetAsFirstSibling();
                Label(panel.SafeArea,"txt_title","테스트 씬 선택",0,248,1080,75,48);
                Label(panel.SafeArea,"txt_description","확인할 씬을 선택하세요",0,177,1080,50,24);
                panel.StatusLabel = Label(panel.SafeArea,"txt_status","",0,-279,1080,45,21);
                var buttons = new System.Collections.Generic.List<UnityEngine.UI.Button>();
                var targets = paths.Skip(1).ToArray();
                int rows = (targets.Length + 1) / 2;
                float spacing = Mathf.Min(124, 370f / Mathf.Max(1,rows));
                foreach (Transform child in panel.SafeArea)
                    if (child.name.StartsWith("btn_",StringComparison.Ordinal)) child.gameObject.SetActive(false);
                for (int i = 0; i < targets.Length; i++)
                {
                    string sceneName = Path.GetFileNameWithoutExtension(targets[i]);
                    var rect = GetRect(panel.SafeArea,"btn_" + sceneName.ToLowerInvariant(),
                        new Vector2(i % 2 == 0 ? -239 : 239, 5 + ((rows - 1) * .5f - i / 2) * spacing),new Vector2(448,spacing - 18));
                    rect.gameObject.SetActive(true);
                    var image = SetImage(rect,new Color(.10f,.23f,.29f)); image.raycastTarget = true;
                    var button = rect.GetComponent<UnityEngine.UI.Button>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                    button.targetGraphic = image;
                    Label(rect,"txt_label",Caption(sceneName),0,15,422,45,26);
                    var detail = Label(rect,"txt_scene",sceneName,0,-24,422,30,17);
                    detail.color = new Color(.66f,.80f,.83f);
                    buttons.Add(button);
                }
                panel.Buttons = buttons.ToArray(); panel.Scenes = targets;
                panel.gameObject.SetActive(true);
                EnsureServices();
                string prefabFolder = "Assets/Resources/Prefabs/UI/Panel/Test";
                Directory.CreateDirectory(prefabFolder);
                PrefabUtility.SaveAsPrefabAsset(panel.gameObject,prefabFolder + "/UIPanel_PlayMode.prefab");
                Save(titleScene);

                foreach (string path in targets)
                {
                    var scene = EditorSceneManager.OpenScene(path);
                    var menus = UnityEngine.Object.FindObjectsByType<UIPanel_PlayMode>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                    if (menus.Length == 0)
                    {
                        var menu = CreatePanel();
                        var rect = GetRect(menu.SafeArea,"btn_title",Vector2.zero,new Vector2(170,48));
                        var image = SetImage(rect,new Color(.08f,.19f,.24f,.95f)); image.raycastTarget = true;
                        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
                        Label(rect,"txt_label","테스트 목록",0,0,166,44,22);
                        menu.Buttons = new[] { button }; menus = new[] { menu };
                    }
                    foreach (var menu in menus)
                    {
                        menu.enabled = true;
                        if (!menu.gameObject.activeInHierarchy)
                        {
                            menu.transform.SetParent(CreateCanvas(), false);
                            Stretch((RectTransform)menu.transform);
                            menu.gameObject.SetActive(true);
                        }
                        menu.Scenes = Enumerable.Repeat(TitlePath,menu.Buttons.Length).ToArray();
                        foreach (var button in menu.Buttons)
                        {
                            var label = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
                            label.text = "테스트 목록";
                            Stretch(label.rectTransform);
                            var rect = (RectTransform)button.transform;
                            rect.anchorMin = rect.anchorMax = new Vector2(1,1);
                            rect.anchoredPosition = new Vector2(-105,-125); rect.sizeDelta = new Vector2(170,48);
                        }
                    }
                    EnsureServices(); Save(scene);
                }
                // Editor Play can visit both sets; each web build supplies its own explicit scene list.
                var known = EditorBuildSettings.scenes.ToList();
                foreach (string path in paths)
                {
                    var entry = known.FirstOrDefault(scene => scene.path == path);
                    if (entry == null) known.Add(new EditorBuildSettingsScene(path,true)); else entry.enabled = true;
                }
                EditorBuildSettings.scenes = known.ToArray();
                AssetDatabase.SaveAssets();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void RequireIdleScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Stop Play and wait for compilation/building.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene edits first.");
        }

        private static string Caption(string name)
        {
            switch(name)
            {
                case "Scene_Oval": return "타원 경기장 · 기존 캐릭터";
                case "Scene_Prontera": return "프론테라 · 기존 캐릭터";
                case "Scene_PronteraToon": return "프론테라 · 카툰 캐릭터";
                case "Scene_MinorousLookDev": return "미노타우루스 외형 테스트";
                default: return name;
            }
        }

        private static UIPanel_PlayMode CreatePanel()
        {
            var canvasRect = CreateCanvas();
            var panelRect = GetRect(canvasRect,"UIPanel_PlayMode",Vector2.zero,Vector2.zero); Stretch(panelRect);
            var panel = panelRect.gameObject.AddComponent<UIPanel_PlayMode>();
            panel.Buttons = Array.Empty<UnityEngine.UI.Button>(); panel.Scenes = Array.Empty<string>();
            panel.SafeArea = GetRect(panelRect,"obj_safe_area",Vector2.zero,Vector2.zero); Stretch(panel.SafeArea);
            return panel;
        }

        private static RectTransform CreateCanvas()
        {
            var root = GameObject.Find("UICanvas_Root") ?? new GameObject("UICanvas_Root",typeof(RectTransform));
            var rect = GetRect(root.transform,"Canvas_TestSceneMenu",Vector2.zero,Vector2.zero); Stretch(rect);
            var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            ProjectRun.Editor.PlayModeLandscapeLayout.ConfigureCanvas(rect.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>());
            rect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return rect;
        }

        private static void EnsureServices()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            if (UnityEngine.Object.FindFirstObjectByType<Mgr_UI>() == null)
            {
                var root = GameObject.Find("[Managers]") ?? new GameObject("[Managers]");
                var manager = new GameObject("Mgr_UI"); manager.transform.SetParent(root.transform); manager.AddComponent<Mgr_UI>();
            }
        }

        private static RectTransform GetRect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null) { rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); }
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = new Vector2(.5f,.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.Image SetImage(RectTransform rect,Color color)
        {
            var image = rect.GetComponent<UnityEngine.UI.Image>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false; return image;
        }
        private static UnityEngine.UI.Text Label(Transform parent,string name,string value,float x,float y,float width,float height,int size)
        {
            var rect = GetRect(parent,name,new Vector2(x,y),new Vector2(width,height));
            var text = rect.GetComponent<UnityEngine.UI.Text>() ?? rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            text.text = value; text.font = font; text.fontSize = size; text.color = new Color(.94f,.95f,.91f);
            text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        private static void Save(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
