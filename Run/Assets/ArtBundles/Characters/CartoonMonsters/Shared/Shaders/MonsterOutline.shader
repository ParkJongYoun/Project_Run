Shader "ProjectRun/CartoonMonsters/MonsterOutline"
{
    Properties
    {
        _OutlineColor("Outline", Color) = (.09,.08,.15,1)
        _OutlineWidth("Width in pixels", Range(0,3)) = 1.1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite Off ZTest LEqual
            Stencil { Ref 8 ReadMask 8 WriteMask 0 Comp NotEqual Pass Keep }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor; float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half fog : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS));
                float2 direction = normalVS.xy / max(length(normalVS.xy), .001);
                // Constant screen thickness avoids a thick shell in close-ups.
                output.positionCS.xy += direction * _OutlineWidth * 2 / _ScaledScreenParams.xy * output.positionCS.w;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return half4(MixFog(_OutlineColor.rgb,input.fog),1); }
            ENDHLSL
        }
    }
}
