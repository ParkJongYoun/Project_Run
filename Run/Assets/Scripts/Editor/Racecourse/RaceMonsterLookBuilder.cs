using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using ProjectRun.Race;
using Object = UnityEngine.Object;

namespace ProjectRun.Editor
{
    public static class RaceMonsterLookBuilder
    {
        private const string art = "Assets/ArtBundles/RaceShowcase/MonsterLook";

        [MenuItem("Tools/Project Run/Race/Install monster-only rendering comparison")]
        public static void Install()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Scene_All3D")
                throw new InvalidOperationException("Open Scene_All3D in Edit mode.");
            var presentation = Object.FindFirstObjectByType<Sys_RacePresentation>();
            for (int i = 0; i < presentation.Race.Entrants.Length; i++)
            {
                string path = AssetDatabase.GetAssetPath(presentation.Race.Entrants[i].Prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<RaceRunnerView>();
                    RemoveRider(view);
                    ConfigureRunner(view);
                    view.IdleClip.SampleAnimation(view.MountRoot.gameObject, .2f);
                    view.MountRoot.localPosition = Vector3.zero;
                    view.MountRoot.localRotation = Quaternion.identity;
                    view.FaceTarget.position = view.MonsterFaceTarget.position;
                    var look = root.GetComponent<Sys_MonsterLook>();
                    look.Apply(E_MonsterRenderStyle.Toon);
                    RaceShowcaseBuilder.CapturePortrait(root, i);
                    RaceShowcaseBuilder.CapturePortrait(root, i, true);
                    look.Apply(E_MonsterRenderStyle.Outlined);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                var entry = presentation.Race.Entrants[i];
                entry.Name = entry.Mount;
                entry.Portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ArtBundles/RaceShowcase/UI/Portrait_" + i + ".png");
                entry.FacePortrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ArtBundles/RaceShowcase/UI/FacePortrait_" + i + ".png");
            }
            foreach (var view in presentation.Views)
            {
                RemoveRider(view);
                // Illustrated frames were captured with a rider; keep those sources but use the live monster for comparison.
                var sprites = view.GetComponent<RaceFoxSpriteView>();
                if (sprites != null)
                {
                    sprites.UseSprites = false; sprites.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sprites);
                }
                EditorUtility.SetDirty(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            }
            ConfigureScene();
            EditorUtility.SetDirty(presentation.Race);
            PrefabUtility.RecordPrefabInstancePropertyModifications(presentation.Race);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(presentation.gameObject.scene);
            EditorSceneManager.SaveScene(presentation.gameObject.scene);
            Debug.Log("Six monsters installed. Rendering buttons: original / toon / toon + outline. Default: outlined.");
        }

        private static void RemoveRider(RaceRunnerView view)
        {
            if (view.RiderRoot != null) Object.DestroyImmediate(view.RiderRoot.gameObject);
            view.RiderRoot = null; view.RiderAnimator = null; view.RiderHead = null; view.RiderPelvis = null;
            view.RidingClip = null; view.SeatBone = null; view.UseSocketAttachment = false; view.UseRiderFacingCloseup = false;
            view.FaceTarget.position = view.MonsterFaceTarget.position;
        }

        public static void ConfigureRunner(RaceRunnerView view)
        {
            Directory.CreateDirectory(art + "/Materials");
            Directory.CreateDirectory(art + "/Meshes");
            var toonShader = Shader.Find("ProjectRun/MonsterToon");
            var outlineShader = Shader.Find("ProjectRun/MonsterOutline");
            if (toonShader == null || outlineShader == null) throw new InvalidOperationException("Monster shaders are not imported.");
            var look = view.GetComponent<Sys_MonsterLook>();
            if (look == null) look = view.gameObject.AddComponent<Sys_MonsterLook>();
            if (look.Surfaces != null) look.Apply(E_MonsterRenderStyle.Original);
            foreach (var old in view.MountRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("obj_outline")).ToArray())
                Object.DestroyImmediate(old.gameObject);
            var skins = view.MountRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            look.Surfaces = new Sys_MonsterLook.Surface[skins.Length];
            look.Outlines = new Renderer[skins.Length];
            var outlineMaterial = MaterialAt(art + "/Materials/Mat_MonsterOutline.mat", outlineShader);
            outlineMaterial.SetColor("_OutlineColor", new Color(.075f,.065f,.12f));
            outlineMaterial.SetFloat("_OutlineWidth", 1.05f);
            EditorUtility.SetDirty(outlineMaterial);
            for (int i = 0; i < skins.Length; i++)
            {
                var skin = skins[i]; var originals = skin.sharedMaterials;
                var toon = new Material[originals.Length]; var outlined = new Material[originals.Length];
                for (int m = 0; m < originals.Length; m++)
                {
                    toon[m] = CreateToon(originals[m], view.RunnerId, false, toonShader);
                    outlined[m] = CreateToon(originals[m], view.RunnerId, true, toonShader);
                }
                look.Surfaces[i] = new Sys_MonsterLook.Surface { Renderer = skin, Original = originals, Toon = toon, Outlined = outlined };
                var child = new GameObject("obj_outline_" + i);
                child.transform.SetParent(skin.transform, false); child.layer = skin.gameObject.layer;
                var shell = child.AddComponent<SkinnedMeshRenderer>();
                shell.sharedMesh = OutlineMesh(skin.sharedMesh); shell.bones = skin.bones; shell.rootBone = skin.rootBone;
                shell.quality = skin.quality;
                shell.sharedMaterials = Enumerable.Repeat(outlineMaterial, originals.Length).ToArray();
                shell.localBounds = skin.localBounds; shell.updateWhenOffscreen = skin.updateWhenOffscreen;
                shell.shadowCastingMode = ShadowCastingMode.Off; shell.receiveShadows = false;
                shell.lightProbeUsage = LightProbeUsage.Off; shell.reflectionProbeUsage = ReflectionProbeUsage.Off;
                shell.renderingLayerMask = skin.renderingLayerMask;
                look.Outlines[i] = shell;
            }
            look.Apply(E_MonsterRenderStyle.Outlined);
            EditorUtility.SetDirty(look);
        }

        private static Mesh OutlineMesh(Mesh source)
        {
            string path = art + "/Meshes/" + source.name + "_Outline.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = Object.Instantiate(source); mesh.name = source.name + "_Outline";
            var vertices = mesh.vertices; var normals = mesh.normals;
            var sums = new System.Collections.Generic.Dictionary<Vector3Int,Vector3>();
            var keys = new Vector3Int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i] * 10000;
                keys[i] = new Vector3Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),Mathf.RoundToInt(p.z));
                Vector3 sum; sums.TryGetValue(keys[i],out sum); sums[keys[i]] = sum + normals[i];
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = sums[keys[i]].normalized;
            // Only the outline copy has welded normals; the character keeps its original authored shading.
            mesh.normals = normals; AssetDatabase.CreateAsset(mesh,path); return mesh;
        }

        private static Material MaterialAt(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            else material.shader = shader;
            return material;
        }

        private static Material CreateToon(Material source, int runner, bool rim, Shader shader)
        {
            string path = art + "/Materials/" + source.name + (rim ? "_Outlined" : "_Toon") + ".mat";
            var material = MaterialAt(path, shader);
            material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
            material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            material.SetColor("_ShadeColor", new Color(.64f,.69f,.82f));
            material.SetFloat("_Threshold", .2f); material.SetFloat("_Softness", .085f);
            material.SetFloat("_Specular", runner == 1 ? .32f : runner == 3 ? .16f : runner == 5 ? .24f : .065f);
            material.SetFloat("_Gloss", runner == 1 ? 52 : runner == 5 ? 44 : 28);
            material.SetFloat("_RimStrength", rim ? .19f : 0);
            material.SetFloat("_AlphaClip", source.HasProperty("_AlphaClip") ? source.GetFloat("_AlphaClip") : 0);
            material.SetFloat("_Cull", source.HasProperty("_Cull") ? source.GetFloat("_Cull") : 2);
            material.SetFloat("_Cutoff", source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > .5f ? source.GetFloat("_Cutoff") : .001f);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void ConfigureScene()
        {
            Directory.CreateDirectory(art + "/Materials"); Directory.CreateDirectory(art + "/Textures");
            var presentation = Object.FindFirstObjectByType<Sys_RacePresentation>();
            var styles = Object.FindFirstObjectByType<Sys_RaceRenderStyle>();
            if (styles == null)
            {
                var child = new GameObject("Sys_RaceRenderStyle"); child.transform.SetParent(presentation.transform.parent, false);
                styles = child.AddComponent<Sys_RaceRenderStyle>();
            }
            styles.Presentation = presentation;
            styles.InitialStyle = E_MonsterRenderStyle.Outlined;
            styles.ColorVolume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).First(v => v.name == "60_LightAndColor");
            if (styles.OriginalProfile == null) styles.OriginalProfile = styles.ColorVolume.sharedProfile;
            string profilePath = art + "/MonsterColor.asset";
            styles.RefinedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (styles.RefinedProfile == null)
            {
                styles.RefinedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(styles.RefinedProfile, profilePath);
                foreach (var component in styles.OriginalProfile.components)
                {
                    var copy = Object.Instantiate(component); copy.name = component.name;
                    AssetDatabase.AddObjectToAsset(copy, styles.RefinedProfile); styles.RefinedProfile.components.Add(copy);
                }
            }
            ColorAdjustments grade;
            if (styles.RefinedProfile.TryGet(out grade))
            {
                grade.contrast.Override(6); grade.saturation.Override(0); grade.postExposure.Override(.1f);
                EditorUtility.SetDirty(grade);
            }
            EditorUtility.SetDirty(styles.RefinedProfile);
            // Retain original resources so the first two buttons reproduce the original environment exactly.
            if (styles.TurfRenderers == null || styles.TurfRenderers.Length == 0)
            {
                styles.TurfRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Where(r => r.name == "TurfDark" || r.name == "TurfLight").Cast<Renderer>().ToArray();
                styles.OriginalTurf = styles.TurfRenderers.Select(r => r.sharedMaterial).ToArray();
            }
            styles.RefinedTurf = styles.OriginalTurf.Select(CreateRefinedTurf).ToArray();
            InstallControls(styles);
            styles.SetStyle((int)styles.InitialStyle);
            EditorUtility.SetDirty(styles); EditorUtility.SetDirty(styles.ColorVolume);
            foreach (var renderer in styles.TurfRenderers)
            {
                EditorUtility.SetDirty(renderer); PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            foreach (var view in presentation.Views)
            {
                var look = view.GetComponent<Sys_MonsterLook>();
                if (look == null) continue;
                foreach (var surface in look.Surfaces) PrefabUtility.RecordPrefabInstancePropertyModifications(surface.Renderer);
                foreach (var shell in look.Outlines) PrefabUtility.RecordPrefabInstancePropertyModifications(shell);
            }
        }

        private static Material CreateRefinedTurf(Material source)
        {
            string path = art + "/Materials/" + source.name + "_Calm.mat";
            var material = MaterialAt(path, source.shader); material.CopyPropertiesFromMaterial(source);
            var sourceTexture = source.GetTexture("_BaseMap");
            string texturePath = art + "/Textures/" + sourceTexture.name + "_Calm.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                int size = Mathf.Min(512, sourceTexture.width);
                var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                Graphics.Blit(sourceTexture, rt); RenderTexture.active = rt;
                var copy = new Texture2D(size,size,TextureFormat.RGB24,false);
                copy.ReadPixels(new Rect(0,0,size,size),0,0); copy.Apply();
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
                var pixels = copy.GetPixels(); Color average = Color.black;
                foreach (var pixel in pixels) average += pixel / pixels.Length;
                for (int i = 0; i < pixels.Length; i++)
                {
                    var c = Color.Lerp(pixels[i],average,.24f);
                    pixels[i] = Color.Lerp(new Color(c.grayscale,c.grayscale,c.grayscale),c,.7f);
                }
                copy.SetPixels(pixels); copy.Apply(); File.WriteAllBytes(texturePath,copy.EncodeToPNG()); Object.DestroyImmediate(copy);
                AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.mipmapEnabled=true; importer.wrapMode=TextureWrapMode.Repeat; importer.SaveAndReimport();
                texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }
            material.SetTexture("_BaseMap",texture); EditorUtility.SetDirty(material); return material;
        }

        private static void InstallControls(Sys_RaceRenderStyle styles)
        {
            const string hudPath="Assets/Resources/Prefabs/UI/Panel/UIPanel_RaceHUD.prefab";
            var root=PrefabUtility.LoadPrefabContents(hudPath);
            try { CreateControls(root.GetComponent<UIPanel_RaceHUD>()); PrefabUtility.SaveAsPrefabAsset(root,hudPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var hud=Object.FindFirstObjectByType<UIPanel_RaceHUD>();
            var item=hud.GetComponentInChildren<UIItem_RenderStyle>(true);
            if (item==null) item=CreateControls(hud);
            item.Styles=styles; EditorUtility.SetDirty(item); PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        }

        private static UIItem_RenderStyle CreateControls(UIPanel_RaceHUD hud)
        {
            var existing=hud.GetComponentInChildren<UIItem_RenderStyle>(true);
            if(existing!=null) { ((RectTransform)existing.transform).anchoredPosition=new Vector2(0,-304); return existing; }
            var group=new GameObject("group_render_style",typeof(RectTransform));
            var rect=(RectTransform)group.transform; rect.SetParent(hud.SafeArea,false);
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,1); rect.sizeDelta=new Vector2(650,48); rect.anchoredPosition=new Vector2(0,-304);
            // Before the cutscene and popup siblings, keeping comparison controls below modal UI.
            rect.SetSiblingIndex(4);
            var item=group.AddComponent<UIItem_RenderStyle>();
            item.Buttons=new Button[3]; item.Backgrounds=new Image[3]; item.Labels=new Text[3];
            string[] labels={"1  기존", "2  툰", "3  툰 + 외곽선"};
            for(int i=0;i<3;i++)
            {
                var button=new GameObject("btn_style_"+i,typeof(RectTransform),typeof(Image),typeof(Button));
                var br=(RectTransform)button.transform; br.SetParent(rect,false); br.sizeDelta=new Vector2(208,46); br.anchoredPosition=new Vector2((i-1)*218,0);
                item.Buttons[i]=button.GetComponent<Button>(); item.Backgrounds[i]=button.GetComponent<Image>();
                item.Buttons[i].targetGraphic=item.Backgrounds[i]; item.Backgrounds[i].color=new Color(.06f,.15f,.18f,.94f);
                var text=new GameObject("txt_style",typeof(RectTransform),typeof(Text));
                var tr=(RectTransform)text.transform; tr.SetParent(br,false); tr.anchorMin=Vector2.zero; tr.anchorMax=Vector2.one; tr.offsetMin=tr.offsetMax=Vector2.zero;
                var label=text.GetComponent<Text>(); label.font=hud.PhaseLabel.font; label.fontSize=21; label.alignment=TextAnchor.MiddleCenter; label.text=labels[i]; label.raycastTarget=false;
                item.Labels[i]=label;
            }
            return item;
        }
    }
}
