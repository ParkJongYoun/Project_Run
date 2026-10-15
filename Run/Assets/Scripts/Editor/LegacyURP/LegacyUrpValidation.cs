using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class LegacyUrpValidation
    {
        [Serializable]
        private sealed class Data_Validation
        {
            public string Status = "running";
            public int Checked;
            public int Total;
            public int Renderers;
            public int SkinnedRenderers;
            public List<string> Errors = new List<string>();
            public List<string> Notes = new List<string>();
        }
        private static Data_Validation result;
        private static string[] paths;

        public static string Start()
        {
            paths = Directory.GetFiles(LegacyUrpImporter.OutputRoot, "*.prefab", SearchOption.AllDirectories).Select(p => p.Replace('\\','/')).ToArray();
            result = new Data_Validation { Total = paths.Length };
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return "Validation started: " + paths.Length;
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            for (int batch = 0; batch < 35 && result.Checked < paths.Length; batch++)
            {
                string path = paths[result.Checked++];
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { result.Errors.Add(path + ": cannot load prefab"); continue; }
                if (go.transform.localPosition != Vector3.zero || go.transform.localRotation != Quaternion.identity || go.transform.localScale != Vector3.one)
                    result.Errors.Add(path + ": non-default root transform");
                foreach (var transform in go.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0) result.Errors.Add(path + ": missing script");
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    result.Renderers++;
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null) result.Errors.Add(path + " / " + renderer.name + ": null material");
                        else if (mat.shader == null || mat.shader.name != "ProjectRun/CharacterSimple" || !mat.shader.isSupported)
                            result.Errors.Add(path + " / " + renderer.name + ": unconverted or unsupported material " + mat.name);
                    }
                    var skin = renderer as SkinnedMeshRenderer;
                    if (skin == null) continue;
                    result.SkinnedRenderers++;
                    if (skin.sharedMesh == null) { result.Notes.Add(path + " / " + skin.name + ": source mesh absent"); continue; }
                    if (skin.sharedMesh.bindposes.Length > 0 && (skin.bones.Length != skin.sharedMesh.bindposes.Length || skin.bones.Any(b => b == null)))
                        result.Errors.Add(path + " / " + skin.name + ": incomplete bone binding");
                }
            }
            if (result.Checked >= paths.Length)
            {
                result.Status = result.Errors.Count == 0 ? "passed" : "failed";
                EditorApplication.update -= Tick;
                Debug.Log("Legacy URP validation: " + result.Status + "; " + result.Checked + " prefabs, " + result.Errors.Count + " errors.");
            }
            File.WriteAllText(Path.Combine(LegacyUrpImporter.ReportDirectory, "Validation.json"), JsonUtility.ToJson(result, true));
        }

        public static string Capture(string assetPath, string name)
        {
            var preview = new PreviewRenderUtility();
            Texture2D picture = null;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (source == null) throw new InvalidOperationException("Preview asset missing: " + assetPath);
                var instance = preview.InstantiatePrefabInScene(source);
                instance.SetActive(true);
                var renderers = instance.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No visible mesh renderers: " + assetPath);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                foreach (var light in preview.lights) light.enabled = false;
                preview.ambientColor = Color.black;
                var camera = preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f,0.1f,0.14f,1);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(0.1f, bounds.extents.magnitude * 0.88f);
                float distance = Mathf.Max(1, bounds.extents.magnitude * 4);
                camera.transform.position = bounds.center + new Vector3(1.6f,0.7f,2.2f).normalized * distance;
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = distance * 3;
                preview.BeginStaticPreview(new Rect(0,0,768,768));
                preview.Render(true,false);
                picture = preview.EndStaticPreview();
                string directory = Path.Combine(LegacyUrpImporter.ReportDirectory, "Previews");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, name + ".png");
                File.WriteAllBytes(path, picture.EncodeToPNG());
                return path;
            }
            finally
            {
                preview.Cleanup();
                if (picture != null) Object.DestroyImmediate(picture);
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
            }
        }
    }
}
