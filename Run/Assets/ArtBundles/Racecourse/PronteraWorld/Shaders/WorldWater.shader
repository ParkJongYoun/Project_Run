Shader "ProjectRun/Racecourse/WorldWater"
{
    Properties { _BaseColor("Deep water",Color)=(.04,.37,.42,1) _ShallowColor("Shallow water",Color)=(.32,.8,.7,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_ShallowColor;
            CBUFFER_END
            struct V {float4 positionCS:SV_POSITION;float3 p:TEXCOORD0;float fog:TEXCOORD1;};
            V Vert(float4 vertex:POSITION){V o;o.p=TransformObjectToWorld(vertex.xyz);o.positionCS=TransformWorldToHClip(o.p);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),f.x),lerp(Hash(a+float2(0,1)),Hash(a+1),f.x),f.y);}
            half4 Frag(V i):SV_Target
            {
                float2 p=i.p.xz;float time=_Time.y*.4;
                float wave=Noise(p*.2+float2(time*.15,0))*2-1;
                float detail=Noise(p*.9+float2(0,time*.2));
                float lake=length((p-float2(-12,-67))/float2(64,41));
                float oasis=length((p-float2(157,-4))/float2(23,22));
                float shore=i.p.y<0?lake:i.p.y<40&&i.p.x>90?oasis:0;
                float edge=smoothstep(.62,1,shore);
                float3 n=normalize(float3(cos(p.x*.8+p.y*.3+time)*.12,1,sin(p.y*.9-p.x*.35-time)*.12));
                float3 v=normalize(_WorldSpaceCameraPos-i.p);
                float fresnel=pow(1-saturate(dot(n,v)),3);
                half3 col=lerp(_BaseColor.rgb,_ShallowColor.rgb,saturate(.12+edge*.5+fresnel*.3+wave*.08));
                col+=pow(saturate(detail),12)*(.09+edge*.12);
                float foam=smoothstep(.96,.99,shore)*(1-smoothstep(.99,1.02,shore))*saturate(wave*.2+.6);
                col=lerp(col,half3(.78,.94,.82),foam*.7);
                float glint=pow(saturate(dot(reflect(-normalize(float3(-.4,.85,-.3)),n),v)),90);
                return half4(MixFog(col+glint*.4,i.fog),1);
            }
            ENDHLSL
        }
    }
}
