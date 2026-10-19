Shader "ProjectRun/Ranch/WaterAccent"
{
    Properties
    {
        _BaseColor("Water tint", Color) = (.55,.94,.97,.7)
        _FoamMode("Foam rings", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _FoamMode;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fog:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.uv=input.uv; o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.uv;
                float t=_Time.y;
                half alpha, light;
                if (_FoamMode>.5h)
                {
                    float r=length(uv*2-1);
                    float ring=frac(r*2.7-t*.6);
                    light=smoothstep(.66,.86,ring)*(1-smoothstep(.9,1.,ring));
                    alpha=(light*.55+.08)*(1-smoothstep(.65,1.,r))*smoothstep(0.,.13,r);
                }
                else
                {
                    // V runs from the top lip to the pool: streaks travel downward.
                    float streak=sin(uv.x*47+sin(uv.x*13)*2);
                    float falling=sin(uv.y*33-t*11+uv.x*18);
                    light=smoothstep(.25,.92,streak)*(.5+.5*smoothstep(-.5,.7,falling));
                    alpha=(.3+light*.5)*smoothstep(0.,.12,uv.x)*(1-smoothstep(.88,1.,uv.x));
                    alpha*=smoothstep(0.,.06,uv.y)*(1-smoothstep(.86,1.,uv.y));
                }
                half3 color=lerp(_BaseColor.rgb,half3(.95,1,1),light*.75);
                return half4(MixFog(color,input.fog),alpha*_BaseColor.a);
            }
            ENDHLSL
        }
    }
}
