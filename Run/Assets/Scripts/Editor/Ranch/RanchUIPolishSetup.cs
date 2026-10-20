using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ProjectRun.Race;

namespace ProjectRun.Editor
{
    public static class RanchUIPolishSetup
    {
        [MenuItem("Tools/Project Run/Ranch/Apply collection and construction polish")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            const string root = "Assets/ArtBundles/UI/Ranch/Construction";
            Directory.CreateDirectory(root);
            AssetDatabase.Refresh();
            var art = AssetDatabase.LoadAssetAtPath<Data_DemoArt>("Assets/Resources/UI/Ranch/Data_RanchArt.asset");
            var entries = art.Images.ToList();
            foreach (string suffix in new[] { "bg", "frame" })
            {
                var source = art.Get("menu_" + suffix);
                string key = "construction_card_" + suffix;
                string path = root + "/" + key + ".asset";
                var sprite = Sprite.Create(source.texture, source.rect, new Vector2(.5f, .5f),
                    100, 0, SpriteMeshType.FullRect, new Vector4(160, 110, 160, 110));
                sprite.name = key;
                var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (existing == null) { AssetDatabase.CreateAsset(sprite, path); existing = sprite; }
                else { EditorUtility.CopySerialized(sprite, existing); UnityEngine.Object.DestroyImmediate(sprite); }
                entries.RemoveAll(entry => entry.Key == key);
                entries.Add(new Data_DemoSprite { Key = key, Sprite = existing });
            }
            art.Images = entries.ToArray();
            EditorUtility.SetDirty(art);
            const string prefabPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_Ranch.prefab";
            var rootObject = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var panel = rootObject.GetComponent<UIPanel_Ranch>();
                panel.Art = art;
                panel.RebuildConstructionUI();
                PrefabUtility.SaveAsPrefabAsset(rootObject, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rootObject); }
            RanchCollectionBuilder.RefreshPolishedPanels();
            AssetDatabase.SaveAssets();
            ApplyConstructionScene();
            Debug.Log("Ranch UI: construction dock, collection art, inset layout and training progress updated.");
        }

        public static void ApplyConstructionScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            const string path = "Assets/Scenes/Scene_LobbyTest.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("The lobby has unsaved edits; apply the dock after those edits are saved.");
            try
            {
                var panel = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<UIPanel_Ranch>(true)).Single();
                Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Polish ranch construction dock");
                panel.RebuildConstructionUI();
                EditorUtility.SetDirty(panel);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
