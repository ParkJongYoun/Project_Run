Shader "ProjectRun/Ranch/GroundFoliage"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 vertex:POSITION;float3 normal:NORMAL;half4 color:COLOR; };
            struct V { float4 vertex:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half4 color:COLOR; };
            V Vert(A i) { V o;o.world=TransformObjectToWorld(i.vertex.xyz);o.vertex=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(i.normal);o.color=i.color;return o; }
            half4 Frag(V i):SV_Target
            {
                float4 sc=TransformWorldToShadowCoord(i.world);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    sc=ComputeScreenPos(TransformWorldToHClip(i.world));
                #endif
                Light l=GetMainLight(sc);
                half ndl=saturate(dot(normalize(i.normal),l.direction)*.5+.5);
                half3 shade=lerp(half3(.48,.58,.47),half3(1.06,1.04,.96),ndl*l.shadowAttenuation);
                return half4(i.color.rgb*shade,1);
            }
            ENDHLSL
        }
    }
}
