Shader "ProjectRun/Ranch/GroundPatch"
{
    Properties
    {
        _BaseMap("Earth",2D)="white"{}
        _BaseColor("Earth tint",Color)=(.97,.93,.83,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A { float4 vertex:POSITION;float2 uv:TEXCOORD0; };
            struct V { float4 vertex:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1; };
            V Vert(A i) { V o;o.world=TransformObjectToWorld(i.vertex.xyz);o.vertex=TransformWorldToHClip(o.world);o.uv=i.uv;return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            half4 Frag(V i):SV_Target
            {
                float2 q=abs(i.uv*2-1);
                float edge=max(q.x,q.y)+(Noise(i.world.xz*7)-.5)*.12;
                float alpha=1-smoothstep(.83,1,edge);
                clip(alpha-.005);
                half3 earth=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.world.xz*.22).rgb*_BaseColor.rgb;
                earth*=lerp(.66,1,smoothstep(.30,.79,edge));
                float4 sc=TransformWorldToShadowCoord(i.world);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    sc=ComputeScreenPos(TransformWorldToHClip(i.world));
                #endif
                Light l=GetMainLight(sc);
                earth*=lerp(half3(.37,.45,.43),half3(1,1,1),l.shadowAttenuation);
                return half4(earth,alpha*.88);
            }
            ENDHLSL
        }
    }
}
