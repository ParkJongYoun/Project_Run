Shader "ProjectRun/CharacterSimple"
{
    Properties
    {
        [Enum(Opaque,0,Cutout,1,Transparent,2,Additive,3)] _SurfaceMode("Surface", Float) = 0
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Visible faces (Off = both sides)", Float) = 0
        [Toggle] _VertexColor("Use mesh / particle vertex colors", Float) = 0
        _ColorMultiplier("Texture brightness", Range(0,8)) = 1
        _AlphaMultiplier("Alpha multiplier", Range(0,8)) = 1
        [Toggle(_SELF_SHADE)] _SelfShade("Optional self shading (no scene lights)", Float) = 0
        _ShadeColor("Self shade color", Color) = (0.8,0.8,0.85,1)
        _ShadeStrength("Self shade strength", Range(0,1)) = 0.25
        _LightDirection("Local light direction", Vector) = (-0.5,0.9,-1,0)
        [Toggle(_MASK_MAP)] _UseMask("Multiply a second texture", Float) = 0
        _MaskMap("Mask / second texture", 2D) = "white" {}
        _BaseScroll("Texture scroll XY (per second)", Vector) = (0,0,0,0)
        _MaskScroll("Mask scroll XY (per second)", Vector) = (0,0,0,0)
        _QueueOffset("Sorting offset", Range(-50,50)) = 0
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _SrcBlendAlpha("Source alpha blend", Float) = 1
        [HideInInspector] _DstBlendAlpha("Destination alpha blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "UniversalMaterialType"="Unlit" "IgnoreProjector"="True" }
        Pass
        {
            Name "CharacterUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local _SELF_SHADE
            #pragma shader_feature_local_fragment _MASK_MAP
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Unity texture/color property identifiers are retained for material tooling.
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MaskMap_ST;
                half4 _BaseColor;
                half4 _ShadeColor;
                float4 _LightDirection;
                float4 _BaseScroll;
                float4 _MaskScroll;
                half _Cutoff;
                half _VertexColor;
                half _ColorMultiplier;
                half _AlphaMultiplier;
                half _ShadeStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                #if defined(_SELF_SHADE)
                    half shade : TEXCOORD1;
                #endif
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = lerp(half4(1,1,1,1), input.color, _VertexColor);
                #if defined(_SELF_SHADE)
                    float3 normal = SafeNormalize(input.normalOS);
                    float3 direction = SafeNormalize(_LightDirection.xyz);
                    output.shade = saturate(dot(normal, direction) * 0.5 + 0.5);
                #endif
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _BaseScroll.xy * _Time.y;
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor * input.color;
                #if defined(_MASK_MAP)
                    float2 maskUV = input.uv * _MaskMap_ST.xy + _MaskMap_ST.zw + _MaskScroll.xy * _Time.y;
                    color *= SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, maskUV);
                #endif
                color.rgb *= _ColorMultiplier;
                color.a = saturate(color.a * _AlphaMultiplier);
                #if defined(_ALPHATEST_ON)
                    clip(color.a - _Cutoff);
                #endif
                #if defined(_SELF_SHADE)
                    color.rgb *= lerp(half3(1,1,1), lerp(_ShadeColor.rgb, half3(1,1,1), smoothstep(0.2,0.75,input.shade)), _ShadeStrength);
                #endif
                return color;
            }
            ENDHLSL
        }
    }
    CustomEditor "ProjectRun.Editor.CharacterSimpleShaderGUI"
    Fallback Off
}
