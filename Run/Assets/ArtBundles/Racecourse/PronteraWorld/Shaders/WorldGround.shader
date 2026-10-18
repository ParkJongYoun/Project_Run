Shader "ProjectRun/Racecourse/WorldGround"
{
    Properties { _GrassMap("Grass",2D)="white"{} _SandMap("Sand",2D)="white"{} _CliffMap("Cliffs",2D)="white"{} }
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
            TEXTURE2D(_GrassMap);SAMPLER(sampler_GrassMap);
            TEXTURE2D(_SandMap);SAMPLER(sampler_SandMap);
            TEXTURE2D(_CliffMap);SAMPLER(sampler_CliffMap);
            struct Input {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Output {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float fog:TEXCOORD2;};
            Output Vert(Input v){Output o;o.world=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;}
            half4 Frag(Output i):SV_Target
            {
                float boundary=80+sin(i.world.z*.024)*13+sin(i.world.z*.071)*4;
                float dry=smoothstep(boundary-24,boundary+30,i.world.x);
                float forest=1-smoothstep(-135,-72,i.world.x);
                float patch=sin(i.world.x*.09+sin(i.world.z*.07))*sin(i.world.z*.11+i.world.x*.024);
                half3 grass=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,i.world.xz*.095).rgb*lerp(half3(.69,1.02,.58),half3(.54,.88,.57),forest)*(1+patch*.09);
                half3 sand=SAMPLE_TEXTURE2D(_SandMap,sampler_SandMap,i.world.xz*.075).rgb*half3(1.12,1.00,.77);
                half3 base=lerp(grass,sand,dry);
                float3 normal=normalize(i.normal);
                float slope=1-smoothstep(.55,.88,normal.y);
                float2 sideUV=float2(i.world.x+i.world.z,i.world.y)*.043;
                half3 cliff=SAMPLE_TEXTURE2D(_CliffMap,sampler_CliffMap,sideUV).rgb*half3(1.4,1.38,1.22);
                base=lerp(base,cliff,slope*(1-dry));
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                half diffuse=saturate(dot(normalize(i.normal),light.direction));
                half3 color=base*(half3(.3,.36,.32)+light.color*diffuse*lerp(.42,1,light.shadowAttenuation)*.65);
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
    }
}
