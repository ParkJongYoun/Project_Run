Shader "ProjectRun/Ranch/FlowingWater"
{
    Properties
    {
        [MainTexture] _BaseMap("Color", 2D) = "white" {}
        [Normal] _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = .8
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _AlbedoPower("Painted shadow lift", Range(.75,1.1)) = .92
        _ShadeColor("Shadow tint", Color) = (.66,.71,.85,1)
        _Threshold("Light boundary", Range(-1,1)) = .22
        _Softness("Boundary softness", Range(.01,.3)) = .09
        _Specular("Highlight strength", Range(0,1)) = .12
        _Gloss("Highlight exponent", Range(8,128)) = 36
        _RimStrength("Rim strength", Range(0,1)) = 0
        _RimColor("Rim tint", Color) = (.65,.8,1,1)
        _AmbientStrength("Ambient fill", Range(0,1)) = .2
        _FillStrength("Additional light response", Range(0,1)) = .5
        _RimPower("Rim tightness", Range(1,8)) = 3
        _Cutoff("Alpha cutoff", Range(0,1)) = .01
        [Toggle] _AlphaClip("Alpha clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _ShadeColor, _RimColor;
        half _Threshold, _Softness, _Specular, _Gloss, _RimStrength, _Cutoff, _AlphaClip, _Cull;
        half _AmbientStrength, _FillStrength, _RimPower, _AlbedoPower, _BumpScale;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 tangentOS:TANGENT; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; half4 tangentWS:TEXCOORD4; };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = p.positionCS; output.positionWS = p.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.tangentWS=half4(TransformObjectToWorldDir(input.tangentOS.xyz),input.tangentOS.w*GetOddNegativeScale());
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(p.positionCS.z);
            return output;
        }
        half4 Albedo(float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            color.rgb = pow(max(color.rgb, .0001h), _AlbedoPower);
            if (_AlphaClip > .5h) clip(color.a - _Cutoff); return color;
        }
        half3 SurfaceNormal(Varyings input)
        {
            half3 n=normalize(input.normalWS);
            if(dot(input.tangentWS.xyz,input.tangentWS.xyz)<.01h || _BumpScale<=0) return n;
            half3 t=normalize(input.tangentWS.xyz-n*dot(n,input.tangentWS.xyz));
            half3 b=cross(n,t)*input.tangentWS.w;
            half3 mapped=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv),_BumpScale);
            return normalize(t*mapped.x+b*mapped.y+n*mapped.z);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            // Ranch surfaces use soft shading without an outline stencil.

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            half3 FillLight(Light light, half3 n, half3 v, half3 albedo)
            {
                half ndl = saturate(dot(n, light.direction));
                half diffuse = smoothstep(.05h, .75h, ndl);
                half3 h = SafeNormalize(light.direction + v);
                half highlight = smoothstep(.35h, .8h, pow(saturate(dot(n,h)), _Gloss));
                return (albedo * diffuse + highlight * _Specular * .35h)
                    * light.color * light.distanceAttenuation * light.shadowAttenuation * _FillStrength;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 base = Albedo(input.uv);
                half3 n = SurfaceNormal(input);
                half3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                half ndl = dot(n, light.direction);
                half band = smoothstep(_Threshold - _Softness, _Threshold + _Softness, ndl);
                half shadow = lerp(.12h, 1.h, light.shadowAttenuation);
                half lit = band * shadow;
                half3 illumination = lerp(_ShadeColor.rgb, half3(1,1,1), lit);
                half3 key = light.color * light.distanceAttenuation;
                half3 ambient = max(SampleSH(n), 0) * _AmbientStrength;
                // Keep continuous relief inside the broad cartoon light band.
                half relief = lerp(.86h,1.04h,saturate(ndl));
                half3 color = base.rgb * (illumination * key * relief + ambient);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    color*=lerp(.65h,1.h,ao.indirectAmbientOcclusion);
                #endif
                half3 h = SafeNormalize(light.direction + v);
                half spec = smoothstep(.32h, .7h, pow(saturate(dot(n,h)), _Gloss));
                color += spec * _Specular * lit * key;

                // Match URP 17's Forward+ directional and clustered punctual light loops.
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_ADDITIONAL_LIGHTS)
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        Light fill = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        color += FillLight(fill, n, v, base.rgb);
                    }
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light fill = GetAdditionalLight(lightIndex, input.positionWS, half4(1,1,1,1));
                        color += FillLight(fill, n, v, base.rgb);
                    LIGHT_LOOP_END
                #endif

                // A restrained view rim, gated by key direction, leaves the silhouette readable.
                half rim = pow(1-saturate(dot(n,v)), _RimPower);
                rim = smoothstep(.25h,.85h,rim) * _RimStrength;
                color += rim * _RimColor.rgb * lerp(.2h,1.h,saturate(dot(n,light.direction)));
                // Cyan pixels isolate the supplied water albedo; rocks and wood keep their original shading.
                half water = step(base.r*1.18h,base.g)*step(base.r*1.18h,base.b)*smoothstep(.15h,.5h,base.g);
                half horizontal=smoothstep(.4h,.85h,abs(input.normalWS.y));
                float ripple=sin(input.positionWS.x*9+_Time.y*1.7+sin(input.positionWS.z*6-_Time.y));
                ripple*=sin(input.positionWS.z*11-_Time.y*1.3);
                float fall=sin(input.positionWS.y*24+_Time.y*9+input.positionWS.x*8);
                half sheen=lerp(smoothstep(.35,.95,fall),smoothstep(.45,.9,ripple),horizontal);
                color=lerp(color,color*1.06h+half3(.12,.2,.19)*sheen,water*.65h);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                float3 direction = _LightDirection;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    direction = normalize(_LightPosition-output.positionWS);
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_Target { Albedo(input.uv); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half DepthFrag(Varyings input) : SV_Target { Albedo(input.uv); return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input) : SV_Target
            {
                Albedo(input.uv);
                float3 n = SurfaceNormal(input);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
                #else
                    return half4(n,0);
                #endif
            }
            ENDHLSL
        }
    }
}

