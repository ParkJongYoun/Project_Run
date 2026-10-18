Shader "ProjectRun/UI/LobbyEnvironment"
{
    Properties
    {
        [PerRendererData] _MainTex ("Room illustration", 2D) = "white" {}
        _MotionMask ("R curtains / G flowers / B foreground leaves", 2D) = "black" {}
        _MovementPixels ("Movement in source pixels", Vector) = (20,8,14,0)
        _Clock ("Animation time", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
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
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 localPosition:TEXCOORD1; };
            sampler2D _MainTex;
            sampler2D _MotionMask;
            float4 _MainTex_TexelSize;
            float4 _MovementPixels;
            float4 _ClipRect;
            fixed4 _Color;
            float _Clock;
            v2f vert(appdata input)
            {
                v2f output;
                output.localPosition=input.vertex;
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.uv=input.uv;
                output.color=input.color*_Color;
                return output;
            }
            fixed4 frag(v2f input):SV_Target
            {
                float3 mask=tex2D(_MotionMask,input.uv).rgb;
                float t=_Clock;
                float cloth=sin(t*1.35+input.uv.y*2.1)*.75+sin(t*2.03-input.uv.y*3)*.25;
                float flowers=sin(t*1.65+input.uv.x*4);
                float leaves=sin(t*1.17+input.uv.y*3.2);
                float2 offset;
                offset.x=cloth*mask.r*_MovementPixels.x+flowers*mask.g*_MovementPixels.y+leaves*mask.b*_MovementPixels.z;
                offset.y=sin(t*1.35)*mask.r*2.5+cos(t*1.65)*mask.g*2.5+cos(t*1.17)*mask.b*3;
                fixed4 color=tex2D(_MainTex,input.uv+offset*_MainTex_TexelSize.xy)*input.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(input.localPosition.xy,_ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
