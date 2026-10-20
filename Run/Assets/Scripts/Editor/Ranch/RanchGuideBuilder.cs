using System;
using System.Linq;
using ProjectRun.Race;
using ProjectRun.Ranch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRun.Editor
{
    public static class RanchGuideBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/UI/Panel/UIPanel_RanchGuide.prefab";

        [MenuItem("Tools/Project Run/Ranch/Build character tutorial")]
        public static void BuildAndConnect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            const string imagePath = "Assets/ArtBundles/UI/RanchGuide/RanchGuide_Welcome.png";
            AssetDatabase.ImportAsset(imagePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048; importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("UIPanel_RanchGuide", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, preview);
            try
            {
                var rect = (RectTransform)root.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var guide = root.AddComponent<UIPanel_RanchGuide>();
                guide.CharacterSprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                guide.Build();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(preview); }
            var scene = SceneManager.GetSceneByPath(RanchSceneBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(RanchSceneBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save lobby edits first.");
                var bootstrap = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Sys_RanchUI>(true)).Single();
                var uiRoot = bootstrap.Panel.GetComponentInParent<Canvas>().transform.parent;
                var canvas = uiRoot.Find("Canvas_Guide");
                if (canvas == null) canvas = DemoUI.Canvas(uiRoot, "Canvas_Guide", (int)E_CanvasLayer.Popup + 10).transform;
                var old = canvas.GetComponentInChildren<UIPanel_RanchGuide>(true);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), canvas);
                var guide = instance.GetComponent<UIPanel_RanchGuide>();
                guide.Owner = bootstrap.Panel; guide.Monsters = bootstrap.MonsterPanel;
                guide.Training = bootstrap.TrainingPanel; guide.Arena = bootstrap.ArenaPanel;
                bootstrap.GuidePanel = guide; bootstrap.Panel.Guide = guide;
                PrefabUtility.RecordPrefabInstancePropertyModifications(guide);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bootstrap.Panel);
                EditorUtility.SetDirty(bootstrap); EditorUtility.SetDirty(bootstrap.Panel); EditorUtility.SetDirty(guide);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
