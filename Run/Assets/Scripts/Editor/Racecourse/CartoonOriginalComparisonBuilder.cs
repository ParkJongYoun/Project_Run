using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ProjectRun.Race;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.EditorTools
{
    public static class CartoonOriginalComparisonBuilder
    {
        private const string records = "DataBundles/검증/카툰테스트_원본전환_20261019";

        [MenuItem("Tools/Project Run/Cartoon monsters/Connect original comparison")]
        public static void Connect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = SceneManager.GetSceneByPath(CartoonShowcaseBuilder.ScenePath);
            if (!scene.isLoaded) throw new InvalidOperationException("Open the cartoon test scene first.");
            if (scene.isDirty) throw new InvalidOperationException("Cartoon scene has unsaved changes.");
            var show = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Sys_CartoonShowcase>(true)).Single();
            Configure(show);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Configure(Sys_CartoonShowcase show)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var report = new System.Collections.Generic.List<object>();
            try
            {
                foreach (var entry in show.Exhibits)
                {
                    var view = entry.Root.GetComponentInChildren<RaceRunnerView>(true);
                    entry.CartoonRoot = view.transform;
                    string id = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject))).Replace("P_Runner_Cartoon_", "");
                    string path = SourcePath(id);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) throw new InvalidOperationException("Missing original: " + path);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                    try
                    {
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                            if (renderer.name == "shadow") renderer.gameObject.SetActive(false);
                        var animator = model.GetComponentInChildren<Animator>(true);
                        var clips = animator.runtimeAnimatorController.animationClips;
                        entry.OriginalPrefab = prefab;
                        entry.OriginalIdle = clips.First(c => c.name.EndsWith("_stand_01", StringComparison.Ordinal) || c.name.EndsWith("_stand", StringComparison.Ordinal));
                        entry.OriginalRun = clips.First(c => c.name.EndsWith("_run_01", StringComparison.Ordinal) || c.name.EndsWith("_run", StringComparison.Ordinal));
                        animator.runtimeAnimatorController = null;
                        animator.fireEvents = false;
                        entry.OriginalIdle.SampleAnimation(animator.gameObject, .15f);
                        var bounds = BoundsOf(model);
                        float scale = Mathf.Min(3.35f / Mathf.Max(bounds.size.x, bounds.size.y), 3.8f / bounds.size.z);
                        model.transform.localScale *= scale;
                        bounds = BoundsOf(model);
                        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                        entry.OriginalPosition = model.transform.localPosition;
                        entry.OriginalRotation = model.transform.localRotation;
                        entry.OriginalScale = model.transform.localScale;
                        // Fit either model without changing the camera when the button is pressed.
                        bounds = BoundsOf(model);
                        var world = new Bounds(entry.Root.TransformPoint(bounds.center), Vector3.zero);
                        for (int corner = 0; corner < 8; corner++)
                        {
                            var p = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                            world.Encapsulate(entry.Root.TransformPoint(p));
                        }
                        var cartoonBounds = BoundsOf(view.gameObject);
                        world.Encapsulate(cartoonBounds);
                        entry.Focus = world.center;
                        entry.Radius = Mathf.Max(world.extents.x, world.extents.y, world.extents.z);
                        var materials = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
                        if (materials.Any(m => m.shader == null || !m.shader.isSupported)) throw new InvalidOperationException("Unsupported original material: " + id);
                        report.Add(new { Id = id, Source = path, Idle = entry.OriginalIdle.name, Run = entry.OriginalRun.name, Materials = materials.Select(AssetDatabase.GetAssetPath).ToArray() });
                    }
                    finally { Object.DestroyImmediate(model); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var safe = show.SafeArea;
            var existing = safe.Find("btn_appearance");
            if (existing == null)
            {
                var rect = new GameObject("btn_appearance", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(safe, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
                rect.sizeDelta = new Vector2(210,42); rect.anchoredPosition = new Vector2(485,316);
                var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(.13f,.26f,.30f,.97f);
                var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
                UnityEventTools.AddPersistentListener(button.onClick, show.ToggleAppearance);
                var textRect = new GameObject("txt_label",typeof(RectTransform)).GetComponent<RectTransform>();
                textRect.SetParent(rect,false); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                var text = textRect.gameObject.AddComponent<UnityEngine.UI.Text>(); text.text = "원본 ↔ 카툰"; text.fontSize = 22;
                text.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/ArtBundles/UI/SGLReference/Fonts/NanumMyeongjo-Bold.ttf");
                text.color = Color.white; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            }
            show.Status.text = "전체 " + show.Exhibits.Length + "종 · 카툰 · 대기";
            EditorUtility.SetDirty(show);
            Directory.CreateDirectory(records);
            File.WriteAllText(records + "/원본17종_연결목록.json", JsonConvert.SerializeObject(report,Formatting.Indented));
        }

        private static string SourcePath(string id)
        {
            string name;
            switch (id)
            {
                case "Minorous": return Monster("morocc_mob", "minorous_001");
                case "Poring": return Monster("prontera_mob", "poring_001");
                case "Pecopeco": return Monster("morocc_mob", "pecopeco_001");
                case "Lunatic": return Monster("prontera_mob", "lunatic_001");
                case "Deviruchi": return Monster("geffen_mob", "deviruchi_001");
                case "BaphometJr": return Monster("mjolnir_mob", "bapojunior_001");
                case "Smokie": return Monster("geffen_mob", "smokie_001");
                case "Savage": return Monster("mjolnir_mob", "savage_001");
                case "Alpaca": name = "rides_alpaca01"; break;
                case "BlueDragon": name = "vehicle_bluedragon_001"; break;
                case "Horse": name = "horse_001"; break;
                case "ButlerWhisper": name = "butlerwhisper_001"; break;
                case "FlameBird": name = "vehicle_flamebird_001"; break;
                case "PorterPuppy": name = "vehicle_porterpuppy_001"; break;
                case "Fox": name = "fox_001"; break;
                case "GiantPoring": name = "giantporing_001"; break;
                case "Edgar": name = "vehicle_edgar_001"; break;
                default: throw new InvalidOperationException("Unmapped original: " + id);
            }
            return "Assets/ArtBundles/LegacyURP/Mounts/Prefabs/M_" + name + "_pawn.prefab";
        }
        private static string Monster(string group, string name) { return "Assets/ArtBundles/LegacyURP/Monsters/Prefabs/" + group + "/M_" + name + "_pawn.prefab"; }
        private static Bounds BoundsOf(GameObject model)
        {
            var baked = new Mesh(); bool first = true; var bounds = new Bounds();
            try
            {
                foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.name.StartsWith("obj_outline",StringComparison.Ordinal)))
                {
                    skin.BakeMesh(baked,true);
                    foreach (var vertex in baked.vertices)
                    {
                        var p = skin.transform.TransformPoint(vertex);
                        if (first) { bounds = new Bounds(p,Vector3.zero); first = false; } else bounds.Encapsulate(p);
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); }
            if (first) throw new InvalidOperationException("No original geometry: " + model.name);
            return bounds;
        }
    }
}
