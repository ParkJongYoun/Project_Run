Shader "ProjectRun/Racecourse/GardenWater"
{
    Properties { _BaseColor("Water", Color)=(0.08,0.47,0.5,1) _ShallowColor("Reflection",Color)=(0.4,0.8,0.82,1) }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _ShallowColor;
            CBUFFER_END
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float fog:TEXCOORD1; };
            Varyings Vert(float4 vertex:POSITION)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(vertex.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.positionWS.xz;
                float t=_Time.y*0.28;
                float wave=sin(p.x*1.7+p.y*0.62+t)*sin(p.y*2.1-p.x*0.3-t);
                float3 n=normalize(float3(cos(p.x*1.7+t)*0.06,1,sin(p.y*2.1-t)*0.06));
                float3 v=normalize(_WorldSpaceCameraPos-i.positionWS);
                float fresnel=pow(1-saturate(dot(v,n)),3);
                half3 col=lerp(_BaseColor.rgb,_ShallowColor.rgb,0.35+wave*0.035+fresnel*0.4);
                float sparkle=pow(saturate(dot(reflect(-normalize(float3(-0.4,0.85,-0.3)),n),v)),140);
                col+=sparkle*0.45;
                col=MixFog(col,i.fog);return half4(col,1);
            }
            ENDHLSL
        }
    }
}
