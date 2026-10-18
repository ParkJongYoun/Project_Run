Shader "ProjectRun/UI/LobbyCurtain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Curtain", 2D) = "white" {}
        _MotionMask ("Cloth interior mask", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Amplitude ("Displacement in source pixels", Float) = 1.5
        _Phase ("Phase", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 localPosition : TEXCOORD1; };
            sampler2D _MainTex;
            sampler2D _MotionMask;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;
            float _Amplitude;
            float _Phase;
            v2f vert(appdata input)
            {
                v2f output;
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                float mask = tex2D(_MotionMask, input.texcoord).r;
                float2 uv = input.texcoord;
                uv.x += sin(_Phase) * sin(uv.y * 3.14159265) * _Amplitude * _MainTex_TexelSize.x * mask;
                fixed4 color = tex2D(_MainTex, uv) * input.color;
                color.a *= mask;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
