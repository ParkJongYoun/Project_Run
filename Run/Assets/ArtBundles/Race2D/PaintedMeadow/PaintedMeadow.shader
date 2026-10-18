Shader "ProjectRun/Race2D/PaintedMeadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _GrassTex ("Painted grass", 2D) = "white" {}
        _SandTex ("Painted path", 2D) = "white" {}
        _StartGridTex ("Painted starting boxes", 2D) = "black" {}
        _GrassScale ("Grass tile size in course metres", Float) = 18
        _GrassContrast ("Grass detail contrast", Range(0,1)) = 0.68
        _TrackTint ("Track tint", Color) = (0.64,0.78,0.40,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float2 ground:TEXCOORD0; half4 color:COLOR; };
            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_SandTex); SAMPLER(sampler_SandTex);
            TEXTURE2D(_StartGridTex); SAMPLER(sampler_StartGridTex);
            CBUFFER_START(UnityPerMaterial)
            float _GrassScale, _GrassContrast;
            half4 _TrackTint;
            CBUFFER_END
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Varyings o;
                float3 world = TransformObjectToWorld(UnityFlipSprite(v.positionOS.xyz, unity_SpriteProps.xy));
                o.positionCS = TransformWorldToHClip(world);
                // Inverse of Data_Race2DProjection: 38-degree pitch, 35-degree yaw.
                float depth = world.y / 0.615661475;
                o.ground = float2(0.819152044 * world.x + 0.573576436 * depth,
                    -0.573576436 * world.x + 0.819152044 * depth);
                o.color = v.color * unity_SpriteColor;
                return o;
            }
            float Band(float value, float center, float halfWidth, float aa)
            {
                return 1 - smoothstep(halfWidth - aa, halfWidth + aa, abs(value - center));
            }
            float2 MirroredTile(float2 uv)
            {
                // Mirroring removes the baked edge mismatch; an offset second layer breaks the repeat.
                return 1 - abs(frac(uv * .5) * 2 - 1);
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p = i.ground;
                float2 turfUv = p / _GrassScale;
                half3 grassA = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, MirroredTile(turfUv)).rgb;
                half3 grassB = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, MirroredTile(turfUv.yx * float2(-.73,.73) + .43)).rgb;
                half3 grass = lerp(grassA,grassB,.23);
                half3 broad = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, MirroredTile(p / 63 + float2(.31,.17))).rgb;
                half3 sand = SAMPLE_TEXTURE2D(_SandTex, sampler_SandTex, MirroredTile(p / 12)).rgb;
                float aa = max(.045, fwidth(p.x) + fwidth(p.y));
                float r = length(float2(max(0, abs(p.x) - 40), p.y));
                float edgeGrain = (grass.g - .5) * .45;
                float track = smoothstep(18 - aa, 18 + aa, r) * (1 - smoothstep(38 - aa, 38 + aa, r));
                float infield = 1 - smoothstep(17.3, 17.7, r);
                float outerPath = smoothstep(39 + edgeGrain, 39.5 + edgeGrain, r)
                    * (1 - smoothstep(44.7 + edgeGrain, 45.4 + edgeGrain, r));
                float gardenPath = (1 - smoothstep(1.45 + edgeGrain, 1.9 + edgeGrain, abs(p.y)))
                    * (1 - smoothstep(15.6, 16.2, r));
                // Retain blade detail while keeping the six runners visually dominant.
                half3 quietGrass = lerp(half3(.20,.34,.09), grass, _GrassContrast);
                half3 color = quietGrass * half3(.94,1.06,1.07);
                color *= lerp(.94,1.06,saturate(broad.g));
                color = lerp(color, quietGrass * half3(.98,1.06,.98), infield);
                float mowing = .985 + .015 * sin(p.x * .48);
                half3 raceGrass = lerp(_TrackTint.rgb, grass * half3(1.10,1.17,1.04), .54) * mowing;
                color = lerp(color, raceGrass, track);
                color = lerp(color, sand * half3(.98,.97,.94), max(outerPath,gardenPath));
                // Soft stone edging and a fine cream inner highlight follow the exact course.
                float kerb = max(Band(r,17.65,.28,aa),Band(r,38.42,.29,aa));
                float edgeShadow = max(Band(r,17.18,.30,aa),Band(r,38.94,.30,aa));
                color *= 1 - edgeShadow * .13;
                color = lerp(color,sand * half3(1.02,1.01,.97),kerb);
                float rim = max(Band(r,17.85,.07,aa),Band(r,38.23,.07,aa));
                color = lerp(color,half3(.94,.91,.73),rim * .72);
                // Independent transparent floor artwork, projected in course space with no standing gate.
                float2 gridUv = float2((-p.y - 18.5) / 19, (p.x + 29) / 8.0);
                half4 grid = SAMPLE_TEXTURE2D(_StartGridTex,sampler_StartGridTex,saturate(gridUv));
                float gridMask = step(0,gridUv.x)*step(gridUv.x,1)*step(0,gridUv.y)*step(gridUv.y,1)*track;
                color = lerp(color,grid.rgb,grid.a*gridMask*.94);
                // The finish stripe stays at the unchanged simulation start/finish coordinate.
                float finish = Band(p.x,-20,.80,aa) * step(p.y,-18) * track;
                float check = fmod(floor((p.x + 20.8)/.8) + floor(-p.y),2);
                color = lerp(color,lerp(half3(.24,.36,.26),half3(.97,.94,.78),check),finish*.96);
                return half4(color,i.color.a);
            }
            ENDHLSL
        }
    }
}
