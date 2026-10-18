using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    /// <summary>Creates a usable library without modifying the imported archive or its model importers.</summary>
    public static class LegacyUrpImporter
    {
        public const string SourceRoot = "Assets/OldAsset/";
        public const string OutputRoot = "Assets/ArtBundles/LegacyURP/";
        public const string ShaderPath = "Assets/ArtBundles/Shaders/Character/CharacterSimple.shader";
        private static readonly string[] categories = { "Mounts", "Pets", "Characters", "Monsters", "Equipment" };
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static readonly HashSet<string> folders = new HashSet<string>();
        private static Dictionary<string, List<string>> materialNames;
        private static Data_ConversionState state;
        private static Shader shader;
        private static bool processing;
        private static Queue<Data_ConversionItem> repairs;

        [Serializable]
        public sealed class Data_ConversionItem
        {
            public string Source;
            public string Kind;
            public string Destination;
            public string Result;
            public string Notes;
        }

        [Serializable]
        public sealed class Data_ConversionState
        {
            public string Status;
            public int Index;
            public int Materials;
            public int Prefabs;
            public int Models;
            public int Skipped;
            public int Errors;
            public List<Data_ConversionItem> Items = new List<Data_ConversionItem>();
            public List<string> Warnings = new List<string>();
        }

        public static string ReportDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../DataBundles/기존에셋_URP변환"));
        public static string StatePath => Path.Combine(ReportDirectory, "ConversionState.json");

        [MenuItem("Tools/Project Run/Legacy URP/Build missing converted assets")]
        public static void BuildAllMenu() => Start();

        public static string Start()
        {
            if (processing) return Status();
            Prepare();
            state = new Data_ConversionState { Status = "running" };
            foreach (var category in categories)
            {
                string directory = SourceRoot + category;
                foreach (var path in Directory.GetFiles(directory, "*.mat", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                    state.Items.Add(new Data_ConversionItem { Source = Normalize(path), Kind = "material" });
                foreach (var path in Directory.GetFiles(directory, "*.prefab", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                    state.Items.Add(new Data_ConversionItem { Source = Normalize(path), Kind = "prefab" });
                foreach (var path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                    .Where(p => Path.GetExtension(p).Equals(".fbx", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(p).Contains("@"))
                    .OrderBy(p => p, StringComparer.Ordinal))
                    state.Items.Add(new Data_ConversionItem { Source = Normalize(path), Kind = "model" });
            }
            Persist();
            processing = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return Status();
        }

        [MenuItem("Tools/Project Run/Legacy URP/Resume interrupted conversion")]
        public static void Resume()
        {
            if (processing || !File.Exists(StatePath)) return;
            Prepare();
            state = JsonUtility.FromJson<Data_ConversionState>(File.ReadAllText(StatePath));
            state.Status = "running";
            processing = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Project Run/Legacy URP/Stop conversion after current asset")]
        public static void Stop()
        {
            processing = false;
            EditorApplication.update -= Tick;
            EditorApplication.update -= TickRepairs;
            if (state == null) return;
            state.Status = "stopped";
            SaveConvertedMaterials();
            Persist();
        }

        public static string Status()
        {
            if (state == null) return "No conversion running";
            return $"{state.Status}: {state.Index}/{state.Items.Count}; materials={state.Materials}, prefabs={state.Prefabs}, models={state.Models}, skipped={state.Skipped}, errors={state.Errors}";
        }

        private static void Prepare()
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("CharacterSimple shader is missing or has compiler errors.");
            materials.Clear();
            folders.Clear();
            materialNames = Directory.GetFiles(SourceRoot, "*.mat", SearchOption.AllDirectories)
                .Select(Normalize).GroupBy(p => Path.GetFileNameWithoutExtension(p), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(ReportDirectory);
        }

        private static void Tick()
        {
            if (!processing || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int batchCount = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                while (state.Index < state.Items.Count && batchCount < 20 && timer.ElapsedMilliseconds < 300)
                {
                    var item = state.Items[state.Index];
                    try
                    {
                        if (item.Kind == "material")
                        {
                            var source = AssetDatabase.LoadAssetAtPath<Material>(item.Source);
                            if (source == null) throw new InvalidOperationException("Source material could not be loaded.");
                            var target = ConvertMaterial(source, item.Source);
                            item.Destination = AssetDatabase.GetAssetPath(target);
                            item.Result = "ready";
                            state.Materials++;
                        }
                        else
                        {
                            item.Destination = ConvertModelPrefab(item.Source, item.Kind == "model");
                            item.Result = string.IsNullOrEmpty(item.Destination) ? "no_renderer" : "ready";
                            if (item.Result == "no_renderer") state.Skipped++;
                            else if (item.Kind == "model") state.Models++;
                            else state.Prefabs++;
                        }
                    }
                    catch (Exception exception)
                    {
                        item.Result = "error";
                        item.Notes = exception.ToString();
                        state.Errors++;
                    }
                    state.Index++;
                    batchCount++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (state.Index >= state.Items.Count)
            {
                processing = false;
                EditorApplication.update -= Tick;
                state.Status = state.Errors == 0 ? "complete" : "complete_with_errors";
                SaveConvertedMaterials();
                Debug.Log("Legacy URP: " + Status());
            }
            Persist();
        }

        private static void Persist()
        {
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllText(StatePath, JsonUtility.ToJson(state, true));
        }

        public static string RepairMissingSourceExceptions()
        {
            if (processing) throw new InvalidOperationException("Conversion is still running.");
            Prepare();
            state = JsonUtility.FromJson<Data_ConversionState>(File.ReadAllText(StatePath));
            var sources = new HashSet<string>(state.Warnings.Where(w => w.Contains("source material slot missing;"))
                .Select(w => w.Split(new[] { " / " }, StringSplitOptions.None)[0]));
            repairs = new Queue<Data_ConversionItem>(state.Items.Where(i => i.Result == "ready" && i.Kind != "material" && sources.Contains(i.Source)));
            state.Status = "repairing_source_exceptions";
            processing = true;
            EditorApplication.update -= TickRepairs;
            EditorApplication.update += TickRepairs;
            return "Reviewing " + repairs.Count + " generated prefabs with missing source materials.";
        }

        private static void TickRepairs()
        {
            if (!processing || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < 8 && repairs.Count > 0; i++)
                {
                    var item = repairs.Dequeue();
                    try
                    {
                        string destination = ConvertModelPrefab(item.Source, item.Kind == "model", true);
                        if (string.IsNullOrEmpty(destination))
                        {
                            if (!item.Destination.StartsWith(OutputRoot, StringComparison.Ordinal)) throw new InvalidOperationException("Output path guard failed.");
                            AssetDatabase.DeleteAsset(item.Destination);
                            item.Destination = "";
                            item.Result = "source_geometry_missing";
                            item.Notes = "No source mesh or usable particle material. Excluded from the usable library.";
                            if (item.Kind == "model") state.Models--; else state.Prefabs--;
                            state.Skipped++;
                        }
                    }
                    catch (Exception exception) { state.Errors++; item.Result = "error"; item.Notes = exception.ToString(); }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            if (repairs.Count == 0)
            {
                processing = false;
                EditorApplication.update -= TickRepairs;
                state.Status = state.Errors == 0 ? "complete" : "complete_with_errors";
                SaveConvertedMaterials();
            }
            Persist();
        }

        public static string ConvertSingle(string path, bool rebuild = false)
        {
            if (processing) throw new InvalidOperationException("Wait for the current conversion to finish.");
            Prepare();
            state = state ?? new Data_ConversionState { Status = "sample" };
            string result = ConvertModelPrefab(path, path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase), rebuild);
            SaveConvertedMaterials();
            return result;
        }

        public static string ConvertedPrefabPath(string sourcePath, bool model)
        {
            string relative = sourcePath.Substring(SourceRoot.Length);
            int slash = relative.IndexOf('/');
            string category = relative.Substring(0, slash);
            string remainder = relative.Substring(slash + 1);
            if (remainder.StartsWith("prefabs/", StringComparison.OrdinalIgnoreCase)) remainder = remainder.Substring(8);
            string directory = Normalize(Path.GetDirectoryName(remainder) ?? "");
            return OutputRoot + category + (model ? "/Models/" : "/Prefabs/") +
                (directory.Length == 0 ? "" : directory + "/") + "M_" + Path.GetFileNameWithoutExtension(sourcePath) + ".prefab";
        }

        private static string ConvertModelPrefab(string path, bool model, bool rebuild = false)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidOperationException("Source model/prefab could not be loaded.");
            if (!HasDrawableGeometry(source)) return null;
            string destination = ConvertedPrefabPath(path, model);
            if (File.Exists(destination) && !rebuild) return destination;
            EnsureFolder(Normalize(Path.GetDirectoryName(destination)));
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var wrapper = new GameObject(Path.GetFileNameWithoutExtension(destination));
                SceneManager.MoveGameObjectToScene(wrapper, scene);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.transform.SetParent(wrapper.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.SetActive(true);
                if (Path.GetFileNameWithoutExtension(path) == "character_m_pawn" || Path.GetFileNameWithoutExtension(path) == "character_w_pawn")
                    AssembleDefaultPlayer(instance, path, scene);
                RepairUnboundMeshes(instance, path);
                foreach (var transform in instance.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
                foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;
                foreach (var audio in instance.GetComponentsInChildren<AudioSource>(true)) audio.playOnAwake = false;
                foreach (var listener in instance.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    bool missingSourceEffect = renderer is ParticleSystemRenderer && slots.Any(m => m == null);
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var material = ResolveEmbeddedMaterial(slots[i], path, renderer.name);
                        slots[i] = material == null ? FallbackMaterial(path + " / " + renderer.name) : ConvertMaterial(material, path);
                    }
                    renderer.sharedMaterials = slots;
                    if (missingSourceEffect)
                    {
                        renderer.enabled = false;
                        Warn(path + " / " + renderer.name + ": missing source particle material; effect renderer disabled.");
                    }
                    renderer.receiveShadows = false;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                var visible = instance.GetComponentsInChildren<Renderer>(true)
                    .Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer) && r.enabled && r.gameObject.activeInHierarchy).ToArray();
                if (visible.Length > 0)
                {
                    Bounds bounds = visible[0].bounds;
                    foreach (var renderer in visible.Skip(1)) bounds.Encapsulate(renderer.bounds);
                    if (Finite(bounds.center) && Finite(bounds.size) && bounds.size.magnitude < 10000)
                        instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                }
                bool success;
                PrefabUtility.SaveAsPrefabAsset(wrapper, destination, out success);
                if (!success) throw new InvalidOperationException("SaveAsPrefabAsset failed: " + destination);
                return destination;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static bool HasDrawableGeometry(GameObject source)
        {
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                var skin = renderer as SkinnedMeshRenderer;
                if (skin != null && skin.sharedMesh != null && skin.sharedMesh.vertexCount > 0) return true;
                var filter = renderer.GetComponent<MeshFilter>();
                if (renderer is MeshRenderer && filter != null && filter.sharedMesh != null && filter.sharedMesh.vertexCount > 0) return true;
                if ((renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) && renderer.sharedMaterials.Any(m => m != null)) return true;
            }
            return false;
        }

        private static void AssembleDefaultPlayer(GameObject instance, string path, Scene scene)
        {
            string sex = Path.GetFileNameWithoutExtension(path).Contains("_w_") ? "w" : "m";
            var reference = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.bones.Length > 0 && r.bones.All(b => b != null));
            if (reference == null) throw new InvalidOperationException("Player rig has no valid bone source: " + path);
            string playerRoot = SourceRoot + "Characters/Players/";
            string[] parts = { playerRoot + "body/novice_" + sex + "_mesh.prefab", playerRoot + "face/face_" + sex + "_mesh.prefab", playerRoot + "hair/hair_01_1_" + sex + "_mesh.prefab" };
            foreach (var partPath in parts)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(partPath);
                if (asset == null) throw new InvalidOperationException("Default player part missing: " + partPath);
                var part = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                PrefabUtility.UnpackPrefabInstance(part, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                part.transform.SetParent(instance.transform, false);
                part.transform.localPosition = Vector3.zero;
                part.transform.localRotation = Quaternion.identity;
                foreach (var renderer in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (renderer.sharedMesh == null || renderer.sharedMesh.bindposes.Length != reference.bones.Length)
                        throw new InvalidOperationException("Default player bind pose count differs: " + partPath);
                    renderer.bones = reference.bones;
                    renderer.rootBone = reference.rootBone;
                }
            }
            reference.enabled = false;
        }

        private static void RepairUnboundMeshes(GameObject instance, string path)
        {
            Transform[] playerBones = null;
            Transform playerRoot = null;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null || mesh.bindposes.Length == 0) continue;
                if (renderer.bones.Length == mesh.bindposes.Length && renderer.bones.All(b => b != null)) continue;
                if ((mesh.bindposes.Length == 55 || mesh.bindposes.Length == 65) && (path.Contains("/Players/") || path.Contains("/Equipment/")))
                {
                    if (playerBones == null || playerBones.Length != mesh.bindposes.Length)
                    {
                        bool female = mesh.bindposes.Length == 65;
                        var rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourceRoot + "Characters/Players/prefabs/character_" + (female ? "w" : "m") + "_pawn.prefab");
                        var reference = rigAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.bones.Length == mesh.bindposes.Length);
                        playerRoot = Object.Instantiate(reference.rootBone.gameObject, instance.transform, false).transform;
                        playerRoot.name = reference.rootBone.name;
                        var byName = playerRoot.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name, t => t);
                        playerBones = reference.bones.Select(b => byName[b.name]).ToArray();
                    }
                    renderer.bones = playerBones;
                    renderer.rootBone = playerRoot;
                }
                else
                {
                    // A detached equipment mesh can have no exported skeleton. Reconstruct its
                    // rest pose for immediate inspection, without inventing an animation hierarchy.
                    var bones = new Transform[mesh.bindposes.Length];
                    for (int i = 0; i < bones.Length; i++)
                    {
                        var bone = new GameObject("PreviewBone_" + i).transform;
                        bone.SetParent(renderer.transform, false);
                        Matrix4x4 pose = mesh.bindposes[i].inverse;
                        bone.localPosition = pose.GetColumn(3);
                        bone.localRotation = pose.rotation;
                        bone.localScale = pose.lossyScale;
                        bones[i] = bone;
                    }
                    renderer.bones = bones;
                    renderer.rootBone = renderer.transform;
                    Warn(path + " / " + renderer.name + ": reconstructed rest-pose bones for detached part; animation requires its owning rig.");
                }
            }
        }

        private static Material ResolveEmbeddedMaterial(Material material, string context, string rendererName)
        {
            if (material != null && AssetDatabase.GetAssetPath(material).EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) return material;
            string name = material == null ? rendererName : material.name;
            List<string> candidates;
            if (!materialNames.TryGetValue(name, out candidates)) return material;
            string best = candidates.OrderByDescending(p => SharedPrefix(p, context)).ThenBy(p => p, StringComparer.Ordinal).First();
            return AssetDatabase.LoadAssetAtPath<Material>(best) ?? material;
        }

        private static int SharedPrefix(string left, string right)
        {
            int length = Math.Min(left.Length, right.Length), i = 0;
            while (i < length && char.ToLowerInvariant(left[i]) == char.ToLowerInvariant(right[i])) i++;
            return i;
        }

        private static Material ConvertMaterial(Material source, string context)
        {
            string guid;
            long localId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out guid, out localId);
            string key = guid + ":" + localId;
            Material existing;
            if (materials.TryGetValue(key, out existing)) return existing;
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string relative = sourcePath.StartsWith(SourceRoot, StringComparison.Ordinal) ? sourcePath.Substring(SourceRoot.Length) : "Shared/Embedded/" + SafeName(source.name) + "_" + guid + "_" + localId + ".mat";
            if (!relative.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                relative = Normalize(Path.ChangeExtension(relative, null)) + "/" + SafeName(source.name) + "_" + localId + ".mat";
            int slash = relative.IndexOf('/');
            string destination = OutputRoot + relative.Substring(0, slash) + "/Materials/" + relative.Substring(slash + 1);
            existing = AssetDatabase.LoadAssetAtPath<Material>(destination);
            if (existing != null) { materials[key] = existing; return existing; }
            var target = new Material(shader) { name = source.name, enableInstancing = true };
            string name = source.shader == null ? "" : source.shader.name;
            bool toon = name == "project-rs/unitychan_dswf_mobile";
            bool urp = name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
            bool particle = name.IndexOf("Particle", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("FX/Eff/");
            string textureProperty = urp ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : source.HasProperty("_Texture") ? "_Texture" : "_TextureSample0";
            Texture texture = Texture(source, textureProperty);
            if (texture == null && (source.name == "face_m" || source.name == "face_w"))
            {
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OldAsset/Characters/Players/face/fbx/Materials/face_01_" + (source.name == "face_m" ? "m" : "w") + ".png");
                Warn(sourcePath + ": assigned default face 01 for drag-and-drop display.");
            }
            if (texture == null && source.HasProperty(textureProperty))
            {
                string directory = Normalize(Path.GetDirectoryName(sourcePath));
                texture = new[] { ".png", ".tga", ".jpg" }.Select(ext => AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/" + source.name + ext)).FirstOrDefault(t => t != null);
                if (texture == null) Warn(sourcePath + ": base texture absent; using plain material color.");
            }
            target.SetTexture("_BaseMap", texture);
            if (source.HasProperty(textureProperty))
            {
                target.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                target.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
            }
            Color tint = Color.white;
            if ((toon || urp) && source.HasProperty("_BaseColor")) tint = source.GetColor("_BaseColor");
            else if (source.HasProperty("_TintColor")) tint = source.GetColor("_TintColor");
            else if (source.HasProperty("_Color")) tint = source.GetColor("_Color");
            target.SetColor("_BaseColor", tint);
            float cull = toon ? Float(source, "_CullMode", 0) : Float(source, "_Cull", particle ? 0 : 2);
            target.SetFloat("_Cull", cull);
            target.SetFloat("_VertexColor", particle || name == "Mobile/Vertex Colored" || name.StartsWith("Environment/Scroll") ? 1 : 0);
            string renderType = source.GetTag("RenderType", false);
            int mode = renderType == "TransparentCutout" || name.Contains("Cutout") ? 1 : renderType == "Transparent" ? 2 : 0;
            if (name.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0 || name.StartsWith("FX/Eff/eff_mask_add")) mode = 3;
            if (name == "Particles/Standard Unlit" && Float(source,"_Mode",0) == 4) mode = 3;
            target.SetFloat("_SurfaceMode", mode);
            target.SetFloat("_Cutoff", Float(source, "_Cutoff", 0.5f));
            if (source.rawRenderQueue >= 0)
                target.SetFloat("_QueueOffset", Mathf.Clamp(source.renderQueue - (mode >= 2 ? 3000 : mode == 1 ? 2450 : 2000), -50, 50));
            if (source.HasProperty("_TintColor") && (name.StartsWith("Legacy Shaders/Particles/") || name.StartsWith("FX/Eff/")))
            {
                target.SetFloat("_ColorMultiplier", 2);
                target.SetFloat("_AlphaMultiplier", 2);
            }
            string maskProperty = name.StartsWith("FX/Eff/") ? "_Mask" : name.StartsWith("Environment/Scroll") ? "_DetailTex" : "";
            if (maskProperty.Length > 0 && Texture(source, maskProperty) != null)
            {
                target.SetFloat("_UseMask", 1);
                target.SetTexture("_MaskMap", Texture(source, maskProperty));
                target.SetTextureScale("_MaskMap", source.GetTextureScale(maskProperty));
                target.SetTextureOffset("_MaskMap", source.GetTextureOffset(maskProperty));
            }
            if (name.StartsWith("Environment/Scroll"))
            {
                target.SetVector("_BaseScroll", new Vector4(Float(source,"_ScrollX",0),Float(source,"_ScrollY",0),0,0) / 20f);
                target.SetVector("_MaskScroll", new Vector4(Float(source,"_Scroll2X",0),Float(source,"_Scroll2Y",0),0,0) / 20f);
                target.SetFloat("_ColorMultiplier", Float(source,"_MMultiplier",1));
                target.SetFloat("_AlphaMultiplier", Float(source,"_MMultiplier",1));
                Warn(sourcePath + ": simplified scrolling effect; environment-specific motion omitted.");
            }
            if (name.Contains("Force Field") || name.Contains("Distortion") || name.Contains("play_plant"))
                Warn(sourcePath + ": simplified effect to visible URP texture/color; refraction, distortion and environment interaction omitted.");
            CharacterSimpleShaderGUI.Configure(target);
            EnsureFolder(Normalize(Path.GetDirectoryName(destination)));
            AssetDatabase.CreateAsset(target, destination);
            materials[key] = target;
            return target;
        }

        private static Material FallbackMaterial(string context)
        {
            Warn(context + ": source material slot missing; neutral URP fallback assigned.");
            const string path = OutputRoot + "Shared/Materials/Mat_MissingSource.mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            if (materials.TryGetValue("fallback", out result)) return result;
            result = new Material(shader) { name = "Mat_MissingSource" };
            result.SetColor("_BaseColor", new Color(0.65f,0.65f,0.65f,1));
            CharacterSimpleShaderGUI.Configure(result);
            EnsureFolder(Normalize(Path.GetDirectoryName(path)));
            AssetDatabase.CreateAsset(result, path);
            materials["fallback"] = result;
            return result;
        }

        private static Texture Texture(Material material, string property) => material.HasProperty(property) ? material.GetTexture(property) : null;
        private static void SaveConvertedMaterials()
        {
            // Do not flush unrelated imported archive assets or project settings that Unity upgraded in memory.
            foreach (var material in materials.Values.Distinct())
                if (material != null && AssetDatabase.GetAssetPath(material).StartsWith(OutputRoot, StringComparison.Ordinal))
                    AssetDatabase.SaveAssetIfDirty(material);
        }
        private static float Float(Material material, string property, float fallback) => material.HasProperty(property) ? material.GetFloat(property) : fallback;
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x + value.y + value.z) && !float.IsInfinity(value.x + value.y + value.z);
        private static string Normalize(string path) => path.Replace('\\', '/');
        private static string SafeName(string name) => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        private static void Warn(string message) { if (state != null && !state.Warnings.Contains(message)) state.Warnings.Add(message); }

        private static void EnsureFolder(string path)
        {
            if (folders.Contains(path)) return;
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Normalize(Path.GetDirectoryName(path));
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
            folders.Add(path);
        }
    }
}
