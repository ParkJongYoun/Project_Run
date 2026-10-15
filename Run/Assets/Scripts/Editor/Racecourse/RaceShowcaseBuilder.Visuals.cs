using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static partial class RaceShowcaseBuilder
    {
        private static void PrepareVisualAssets()
        {
            MakeTexture("SoftDisc", true, false);
            MakeTexture("RoundPanel", false, true);
            MakeTexture("Dot", false, false);
            TransparentMaterial("Mat_Dust", new Color(0.62f, 0.54f, 0.34f, 0.28f), true);
            TransparentMaterial("Mat_Shadow", new Color(0.04f, 0.12f, 0.10f, 0.45f), false);
            for (int i = 0; i < mounts.Length; i++)
            {
                var color = colors[i]; color.a = 0.6f;
                TransparentMaterial("Mat_Energy" + i, color, true);
            }
        }
        private static void MakeTexture(string name, bool soft, bool rounded)
        {
            string path = art + "/UI/" + name + ".png";
            if (File.Exists(path)) return;
            int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f - size / 2f) / (size / 2f), v = (y + 0.5f - size / 2f) / (size / 2f);
                float distance = Mathf.Sqrt(u * u + v * v);
                float alpha = soft ? Mathf.Pow(Mathf.Clamp01(1 - distance), 1.4f) : Mathf.Clamp01((1 - distance) * 32);
                if (rounded)
                {
                    float qx = Mathf.Max(Mathf.Abs(u) - 0.66f, 0), qy = Mathf.Max(Mathf.Abs(v) - 0.66f, 0);
                    alpha = Mathf.Clamp01((0.33f - Mathf.Sqrt(qx * qx + qy * qy)) * 32);
                }
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.spriteBorder = rounded ? new Vector4(15, 15, 15, 15) : Vector4.zero;
            importer.SaveAndReimport();
        }
        private static Material TransparentMaterial(string name, Color color, bool vertex)
        {
            string path = art + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("ProjectRun/CharacterSimple")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(art + "/UI/SoftDisc.png"));
            m.SetFloat("_SurfaceMode", 2); m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", 1); m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0); m.SetFloat("_VertexColor", vertex ? 1 : 0);
            m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000; EditorUtility.SetDirty(m); return m;
        }
        private static void AddEffects(RaceRunnerView view, Color color)
        {
            var root = view.transform;
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "obj_contact_shadow"; shadow.transform.SetParent(root, false);
            shadow.transform.localPosition = new Vector3(0, 0.035f, -0.2f);
            shadow.transform.localRotation = Quaternion.Euler(90, 0, 0);
            shadow.transform.localScale = new Vector3(3.2f, 5.8f, 1);
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(art + "/Materials/Mat_Shadow.mat");
            view.Shadow = shadow.transform;
            view.Dust = Particle(root, "obj_dust", new Vector3(0, 0.2f, -1.3f), art + "/Materials/Mat_Dust.mat", false);
            view.Sparks = Particle(root, "obj_skill_sparks", new Vector3(0, 1.1f, -1), art + "/Materials/Mat_Energy" + view.RunnerId + ".mat", true);
            view.Trails = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var trail = new GameObject("obj_speed_trail_" + i).AddComponent<TrailRenderer>();
                trail.transform.SetParent(root, false); trail.transform.localPosition = new Vector3(i == 0 ? -0.8f : 0.8f, 1.2f, -1.2f);
                trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(art + "/Materials/Mat_Energy" + view.RunnerId + ".mat");
                trail.time = 0.25f; trail.startWidth = 0.3f; trail.endWidth = 0.015f;
                trail.minVertexDistance = 0.18f; trail.emitting = false; trail.numCapVertices = 2;
                trail.startColor = Color.white; trail.endColor = new Color(1, 1, 1, 0);
                trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
                view.Trails[i] = trail;
            }
        }
        private static ParticleSystem Particle(Transform parent, string name, Vector3 position, string material, bool spark)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = position;
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.duration = 3;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = spark ? 90 : 70;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, spark ? 0.6f : 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, spark ? 2.3f : 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(spark ? 0.08f : 0.35f, spark ? 0.22f : 0.9f);
            main.startColor = Color.white; main.gravityModifier = spark ? 0 : -0.07f;
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = spark ? 1.1f : 0.8f; shape.angle = 30; shape.rotation = new Vector3(-90, 0, 0);
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.8f, 0.12f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.45f, 1, 1.3f));
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(material); renderer.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        private static void CapturePortrait(GameObject runner, int index, bool faceOnly = false)
        {
            // Bake once so an immediate editor render uses this sampled pose, not a stale skinning buffer.
            var bakedRoot = new GameObject("portrait_pose");
            var meshes = new System.Collections.Generic.List<Mesh>();
            Bounds bounds = new Bounds(); bool first = true;
            Vector3 faceCenter = Vector3.zero;
            foreach (var skin in runner.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (faceOnly && !skin.transform.IsChildOf(runner.GetComponent<RaceRunnerView>().RiderRoot)) continue;
                var mesh = new Mesh(); skin.BakeMesh(mesh); meshes.Add(mesh);
                var part = new GameObject("portrait_part"); part.layer = 31;
                part.transform.SetParent(bakedRoot.transform, false);
                part.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                // BakeMesh already incorporates the skeleton's scale in these legacy rigs.
                part.transform.localScale = Vector3.one;
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterials = skin.sharedMaterials;
                renderer.renderingLayerMask = 3;
                if (skin.name == "obj_face") faceCenter = renderer.bounds.center;
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            var camera = new GameObject("portrait_camera").AddComponent<Camera>(); camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, 0); camera.cullingMask = 1 << 31;
            var direction = new Vector3(0.45f, 0.2f, 1).normalized;
            var rotation = Quaternion.LookRotation(-direction, Vector3.up);
            float distance = 0;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                var corner = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                var projected = Quaternion.Inverse(rotation) * corner;
                float plane = Mathf.Max(Mathf.Abs(projected.x), Mathf.Abs(projected.y));
                distance = Mathf.Max(distance, plane / Mathf.Tan(20 * Mathf.Deg2Rad) - projected.z);
            }
            camera.transform.position = bounds.center + direction * distance * 1.06f;
            camera.transform.LookAt(bounds.center); camera.fieldOfView = 40;
            if (faceOnly)
            {
                var view = runner.GetComponent<RaceRunnerView>();
                var face = view.RiderHead;
                Vector3 faceForward = face.TransformDirection(view.FaceForwardAxis).normalized;
                Vector3 faceUp = (face.position - view.RiderPelvis.position).normalized;
                Vector3 focus = faceCenter + faceUp * .04f;
                camera.transform.position = focus + faceForward * 1.05f;
                camera.transform.rotation = Quaternion.LookRotation(-faceForward, faceUp);
                camera.fieldOfView = 36;
            }
            var rt = RenderTexture.GetTemporary(256, 256, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try { ShaderUtil.allowAsyncCompilation = false; camera.Render(); }
            finally { ShaderUtil.allowAsyncCompilation = asyncCompilation; }
            var old = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); texture.Apply();
            string path = art + "/UI/" + (faceOnly ? "FacePortrait_" : "Portrait_") + index + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
            Object.DestroyImmediate(bakedRoot);
            foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.SaveAndReimport();
        }
        private static Material CourseMaterial(string name, string color)
        {
            string path = art + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", Hex(color)); material.SetFloat("_Smoothness", 0.18f); return material;
        }
        private static void BuildMapDress()
        {
            var root = GetRoot("P_RaceBroadcastDress");
            // A separate additive environment root keeps the user's racecourse prefab untouched.
            if (root.transform.childCount > 0) { UpdateStartGrid(root); RepairWorldLabels(root); return; }
            var teal = CourseMaterial("Mat_TrackTeal", "125B5B"); var ivory = CourseMaterial("Mat_TrackIvory", "EAF6E2"); var gold = CourseMaterial("Mat_TrackGold", "EDC879");
            for (int i = 0; i < mounts.Length; i++)
            {
                Vector3 p = Data_RaceSimulation.CoursePoint(0, lanes[i]);
                Box(root.transform, "obj_start_pad_" + i, p + Vector3.up * 0.018f - Vector3.right * 2, new Vector3(3.5f, 0.025f, 2.5f), teal);
                WorldLabel(root.transform, "txt_lane_" + i, (i + 1).ToString(), p + new Vector3(-3, 0.055f, 0), 0.7f, Quaternion.Euler(90, 0, 0), Color.white);
            }
            for (int i = 1; i <= 7; i++)
            {
                float distance = Data_RaceSimulation.Length - i * 100;
                var p = Data_RaceSimulation.CoursePoint(distance, -10.7f); var forward = Data_RaceSimulation.CourseForward(distance);
                var post = Box(root.transform, "obj_distance_post_" + i, p + Vector3.up * 1.4f, new Vector3(0.18f, 2.8f, 0.18f), gold);
                var board = Box(root.transform, "obj_distance_board_" + i, p + Vector3.up * 3.0f, new Vector3(2.1f, 1.1f, 0.15f), teal);
                board.transform.rotation = Quaternion.LookRotation(-Vector3.Cross(Vector3.up, forward));
                WorldLabel(board.transform, "txt_distance", (i * 100).ToString(), board.transform.position + board.transform.forward * -0.11f, 0.3f, board.transform.rotation, Color.white);
            }
            // Infield pennants provide nearby parallax without obstructing riders or the camera corridor.
            for (int i = 0; i < 22; i++)
            {
                float distance = i * Data_RaceSimulation.Length / 22;
                var p = Data_RaceSimulation.CoursePoint(distance, -12);
                Box(root.transform, "obj_pennant_post_" + i, p + Vector3.up * 1.5f, new Vector3(0.07f, 3, 0.07f), ivory);
                var flag = Box(root.transform, "obj_pennant_" + i, p + new Vector3(0.6f, 2.7f, 0), new Vector3(1.2f, 0.6f, 0.04f), i % 2 == 0 ? teal : gold);
                flag.transform.rotation = Quaternion.Euler(0, i * 13, 0);
            }
            WorldLabel(root.transform, "txt_course_title", "MONSTER GRAND PRIX", new Vector3(4, 1.8f, -57.5f), 0.42f, Quaternion.Euler(0, 180, 0), Hex("173F41"));
            RepairWorldLabels(root);
        }
        private static void UpdateStartGrid(GameObject root)
        {
            var teal = CourseMaterial("Mat_TrackTeal", "125B5B");
            for (int i = 0; i < mounts.Length; i++)
            {
                Vector3 p = Data_RaceSimulation.CoursePoint(0, lanes[i]);
                var pad = root.transform.Find("obj_start_pad_" + i);
                if (pad == null) pad = Box(root.transform, "obj_start_pad_" + i, p, Vector3.one, teal).transform;
                pad.position = p + Vector3.up * .018f - Vector3.right * 2;
                pad.localScale = new Vector3(3.5f, .025f, 1.6f);
                var label = root.transform.Find("txt_lane_" + i);
                if (label == null) { WorldLabel(root.transform, "txt_lane_" + i, (i + 1).ToString(), p, .7f, Quaternion.Euler(90, 0, 0), Color.white); label = root.transform.Find("txt_lane_" + i); }
                label.position = p + new Vector3(-3, .055f, 0);
            }
        }
        public static void RepairWorldLabels(GameObject root)
        {
            var worldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            worldFont.RequestCharactersInTexture("0123456789MONSTER GRAND PIX", 64);
            string path = art + "/Materials/Mat_WorldText.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("ProjectRun/CharacterSimple")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetTexture("_BaseMap", worldFont.material.mainTexture); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_SurfaceMode", 2); mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0); mat.SetFloat("_VertexColor", 1); mat.renderQueue = 3000;
            foreach (var label in root.GetComponentsInChildren<TextMesh>())
            {
                label.GetComponent<Renderer>().sharedMaterial = mat;
                label.characterSize = label.name.StartsWith("txt_lane") ? 0.13f : label.name == "txt_distance" ? 0.08f : 0.15f;
                var scale = label.transform.parent.lossyScale;
                label.transform.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            }
        }
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false);
            g.transform.position = position; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = material; Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
        }
        private static void WorldLabel(Transform parent, string name, string value, Vector3 position, float size, Quaternion rotation, Color color)
        {
            var label = new GameObject(name).AddComponent<TextMesh>(); label.transform.SetParent(parent, false);
            label.transform.position = position; label.transform.rotation = rotation; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.text = value; label.fontSize = 64; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter; label.color = color;
        }
    }
}
