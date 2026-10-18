Shader "ProjectRun/Racecourse/WorldRock"
{
    Properties { _BaseColor("Rock tint",Color)=(.7,.6,.4,1) _BaseMap("Rock detail",2D)="white"{} _Cutoff("Cutoff",Float)=0 _Cull("Cull",Float)=2 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;float4 _BaseMap_ST;float _Cutoff;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;};
            struct V {float4 clip:SV_POSITION;float3 p:TEXCOORD0;float3 n:TEXCOORD1;float fog:TEXCOORD2;};
            V Vert(A i){V o;o.p=TransformObjectToWorld(i.p.xyz);o.n=TransformObjectToWorldNormal(i.n);o.clip=TransformWorldToHClip(o.p);o.fog=ComputeFogFactor(o.clip.z);return o;}
            half4 Frag(V i):SV_Target
            {
                float3 n=normalize(i.n);float3 w=pow(abs(n),4);w/=max(dot(w,1),.0001);
                half3 tx=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.p.zy*.035).rgb*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.p.xz*.035).rgb*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.p.xy*.035).rgb*w.z;
                float tone=dot(tx,float3(.3,.59,.11));
                float strata=sin(i.p.y*1.1+sin(i.p.x*.2)*.5+sin(i.p.z*.18)*.5);
                half3 base=_BaseColor.rgb*(.55+tone*1.35)*(1+strata*.045);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.p));
                half3 col=base*(half3(.3,.35,.37)+light.color*saturate(dot(n,light.direction))*.7*lerp(.5,1,light.shadowAttenuation));
                return half4(MixFog(col,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
