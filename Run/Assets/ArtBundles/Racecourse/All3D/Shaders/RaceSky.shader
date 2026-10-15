Shader "ProjectRun/Racecourse/SummerSky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.12,0.43,0.85,1)
        _Horizon("Horizon", Color) = (0.68,0.86,0.99,1)
        _CloudColor("Clouds", Color) = (1,0.99,0.96,1)
        _SunDirection("Sun direction", Vector) = (-0.4,0.75,0.3,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Zenith, _Horizon, _CloudColor;
            float4 _SunDirection;
            CBUFFER_END
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(float4 vertex:POSITION)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(vertex.xyz); o.direction=vertex.xyz; return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.direction);
                half3 sky=lerp(_Horizon.rgb,_Zenith.rgb,pow(saturate(d.y),0.55));
                float2 p=d.xz/(max(d.y,0.06)+0.22)*2.5+float2(12.2,8.7);
                float n=Noise(p)*0.52+Noise(p*2.03)*0.27+Noise(p*4.01)*0.14+Noise(p*8.1)*0.07;
                float clouds=smoothstep(0.51,0.69,n)*smoothstep(-0.01,0.12,d.y);
                half3 cloudShade=lerp(_Horizon.rgb*0.9,_CloudColor.rgb,smoothstep(0.49,0.73,n));
                sky=lerp(sky,cloudShade,clouds);
                float sun=pow(saturate(dot(d,normalize(_SunDirection.xyz))),240);
                return half4(sky+sun*0.42,1);
            }
            ENDHLSL
        }
    }
}
