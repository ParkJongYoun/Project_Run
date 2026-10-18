Shader "ProjectRun/Race2D/GroundShadow"
{
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.uv=a.uv;return v;}
            half4 Frag(V v):SV_Target { float r=length((v.uv-.5)*2);return half4(.12,.22,.17,(1-smoothstep(.5,1,r))*.28); }
            ENDHLSL
        }
    }
}
