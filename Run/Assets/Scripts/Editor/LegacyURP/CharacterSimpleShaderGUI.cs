using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRun.Editor
{
    public sealed class CharacterSimpleShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox("조명 없이 표시됩니다. Surface와 Visible faces를 재질에 맞게 설정하세요. Self shading은 선택 사항입니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            base.OnGUI(materialEditor, properties);
            if (EditorGUI.EndChangeCheck())
                foreach (var target in materialEditor.targets) Configure((Material)target);
        }

        public override void ValidateMaterial(Material material) => Configure(material);

        public static void Configure(Material material)
        {
            int mode = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_SurfaceMode")), 0, 3);
            bool transparent = mode >= 2;
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : mode == 1 ? "TransparentCutout" : "Opaque");
            material.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat("_DstBlend", (float)(mode == 3 ? BlendMode.One : transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat("_ZWrite", transparent ? 0 : 1);
            material.renderQueue = (transparent ? 3000 : mode == 1 ? 2450 : 2000) + Mathf.RoundToInt(material.GetFloat("_QueueOffset"));
            SetKeyword(material, "_ALPHATEST_ON", mode == 1);
            SetKeyword(material, "_SELF_SHADE", material.GetFloat("_SelfShade") > 0.5f);
            SetKeyword(material, "_MASK_MAP", material.GetFloat("_UseMask") > 0.5f);
            material.doubleSidedGI = material.GetFloat("_Cull") == 0;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled) material.EnableKeyword(keyword);
            else material.DisableKeyword(keyword);
        }
    }
}
