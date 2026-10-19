Shader "ProjectRun/Ranch/PaintedMeadow"
{
    Properties
    {
        _GrassMap("Painted grass", 2D) = "white" {}
        _PathMap("Painted earth", 2D) = "white" {}
        _GrassTint("Grass tint", Color) = (1,1,1,1)
        _PathTint("Path tint", Color) = (1,1,1,1)
        _ShadeColor("Soft shadow color", Color) = (.70,.77,.72,1)
        _GrassScale("Grass repeats per metre", Float) = .15
        _GrassDetail("Painted texture contrast", Range(0,1)) = .72
        _PathScale("Earth repeats per metre", Float) = .28
        _EdgeWidth("Soft broken edge width", Range(.01,.5)) = .16
        _EdgeVariation("Edge irregularity", Range(0,.5)) = .20
        _ShadowStrength("Shadow strength", Range(0,1)) = .62
        _MainPath("Main path center XZ and half size", Vector) = (0,-2.3,14.5,.825)
        _EntryPath("Entry path center XZ and half size", Vector) = (.5,-6.1,.825,4.8)
        _SouthPath("House lane", Vector) = (-2,-7,8,.55)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
        TEXTURE2D(_PathMap); SAMPLER(sampler_PathMap);
        CBUFFER_START(UnityPerMaterial)
        half4 _GrassTint, _PathTint, _ShadeColor;
        float4 _MainPath, _EntryPath, _SouthPath;
        float _GrassScale, _GrassDetail, _PathScale, _EdgeWidth, _EdgeVariation, _ShadowStrength;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS=p.positionCS; output.positionWS=p.positionWS;
            output.normalWS=TransformObjectToWorldNormal(input.normalOS);
            output.fog=ComputeFogFactor(p.positionCS.z);
            return output;
        }
        float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float Noise(float2 p)
        {
            float2 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
        }
        float RoundedPath(float2 p,float4 rect)
        {
            float radius=.38;
            float2 q=abs(p-rect.xy)-rect.zw+radius;
            return length(max(q,0))+min(max(q.x,q.y),0)-radius;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.positionWS.xz;
                float broad=Noise(p*.18);
                // Two offset samples suppress repeating texture edges without a second mesh.
                half3 grass=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,p*_GrassScale).rgb;
                half3 grassB=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,p.yx*_GrassScale*.73+float2(.37,.61)).rgb;
                grass=lerp(grass,grassB,.12);
                grass=lerp(half3(.29,.40,.145),grass,_GrassDetail)*_GrassTint.rgb*lerp(.94,1.06,broad);
                // A quieter central lawn and dry, broken colour on the outer meadow.
                float outer=smoothstep(11.6,15.5,max(abs(p.x),abs(p.y)*1.22));
                grass=lerp(grass,grass*half3(1.12,.94,.78),outer*Noise(p*.65)*.65);
                half3 earth=SAMPLE_TEXTURE2D(_PathMap,sampler_PathMap,p*_PathScale).rgb*_PathTint.rgb;
                float wildSoil=smoothstep(.28,.72,Noise(p*.43)+Noise(p*1.3)*.18)*outer;
                half3 wildEarth=earth*half3(.73,.78,.58);
                grass=lerp(grass,wildEarth,wildSoil*.78);
                float distance=min(min(RoundedPath(p,_MainPath),RoundedPath(p,_EntryPath)),RoundedPath(p,_SouthPath));
                distance=min(distance,RoundedPath(p,float4(-9.45,3.9,.58,1.05)));
                distance=min(distance,RoundedPath(p,float4(-2.7,3.9,.58,1.05)));
                distance+=(Noise(p*2.6)-.5)*_EdgeVariation*2+(Noise(p*11)-.5)*.055;
                float path=1-smoothstep(-_EdgeWidth,_EdgeWidth,distance);
                float verge=(1-smoothstep(.01,.32,abs(distance)))*(1-path);
                grass=lerp(grass,grass*half3(.75,.85,.68),verge*.48);
                // Worn center stays light, while a thin earth lip gives the edge definition.
                earth*=lerp(.78,1.02,smoothstep(0,.42,-distance));
                float2 brushCell=floor(p*5.5);
                float2 brush=frac(p*5.5)-.5;
                float blade=(1-smoothstep(.045,.10,abs(brush.x+brush.y*.28)))*(1-smoothstep(.16,.38,abs(brush.y)));
                float fleck=blade*step(.78,Hash(brushCell))*lerp(.25,1,saturate(verge+outer));
                grass=lerp(grass,grass*half3(1.19,1.12,.90),fleck*.45);
                half3 base=lerp(grass,earth,path);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord=ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord=TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light light=GetMainLight(shadowCoord);
                half diffuse=saturate(dot(normalize(input.normalWS),light.direction));
                half occlusion=(1-light.shadowAttenuation)*_ShadowStrength;
                half3 illumination=lerp(half3(1,1,1),_ShadeColor.rgb,occlusion);
                illumination*=lerp(half3(.97,1,1),min(light.color,half3(1.13,1.13,1.13)),.30)*lerp(.96,1.03,diffuse);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    illumination*=lerp(.48h,1.h,ao.indirectAmbientOcclusion);
                #endif
                return half4(MixFog(base*illumination,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half DepthFrag(Varyings input):SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input):SV_Target
            {
                float3 n=normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}
