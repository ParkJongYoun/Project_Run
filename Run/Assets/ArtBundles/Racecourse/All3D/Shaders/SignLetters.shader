Shader "ProjectRun/Racecourse/SignLetters"
{
    Properties { [MainTexture] _BaseMap("Letters",2D)="white"{} [MainColor] _BaseColor("Ink",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back ZWrite On
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float fog:TEXCOORD1;};
            Varyings Vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 Frag(Varyings i):SV_Target {half alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a;clip(alpha-0.4);return half4(MixFog(_BaseColor.rgb,i.fog),1);}
            ENDHLSL
        }
    }
}
