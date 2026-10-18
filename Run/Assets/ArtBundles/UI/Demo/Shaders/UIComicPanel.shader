Shader "ProjectRun/UI/ComicPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Saturation ("Saturation", Range(0,1)) = 1
        _TearSide ("Torn paper side", Float) = 0
        _TearAxis ("Torn paper direction", Vector) = (1,0,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float _Saturation;
            float _TearSide;
            float4 _TearAxis;
            float4 _ClipRect;
            v2f vert(appdata v)
            {
                v2f o; o.local = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float gray = dot(c.rgb, float3(.299, .587, .114));
                c.rgb = lerp(gray.xxx, c.rgb, _Saturation);
                c *= i.color;
                if (abs(_TearSide) > .5)
                {
                    // Shared jagged seam: both halves meet exactly before they fly apart.
                    float row = dot(i.uv, float2(-_TearAxis.y, _TearAxis.x)) * 31;
                    float a = frac(sin(floor(row) * 127.1) * 43758.5453);
                    float b = frac(sin((floor(row) + 1) * 127.1) * 43758.5453);
                    float seam = (lerp(a, b, frac(row)) - .5) * .035;
                    float distance = (dot(i.uv - .5, _TearAxis.xy) - seam) * _TearSide;
                    clip(distance);
                    c.rgb = lerp(float3(1, .94, .75), c.rgb, smoothstep(.002, .006, distance));
                }
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - .001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
