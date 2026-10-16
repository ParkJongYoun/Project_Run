Shader "Hidden/ProjectRun/FoxCaptureDepth"
{
    Properties { _BaseMap("Alpha",2D)="white"{} _BaseColor("Tint",Color)=(1,1,1,1) _Cutoff("Cutoff",Float)=0.5 _AlphaClip("Clip",Float)=0 _Cull("Cull",Float)=2 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull] ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST, _BaseColor, _CaptureCenter, _CaptureDirection;
            float _Cutoff, _AlphaClip, _Radius;
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            V Vert(A a) { V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.uv=TRANSFORM_TEX(a.uv,_BaseMap); return o; }
            float4 Frag(V i):SV_Target
            {
                if(_AlphaClip>0.5) clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*_BaseColor.a-_Cutoff);
                float depth=saturate(dot(i.positionWS-_CaptureCenter.xyz,_CaptureDirection.xyz)/(_Radius*2)+0.5);
                float q=round(depth*65535);
                return float4(floor(q/256)/255,fmod(q,256)/255,0,1);
            }
            ENDHLSL
        }
    }
}
