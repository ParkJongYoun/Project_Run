// Project Run URP adaptation of the purchased shader. Source preserved in Project OT.
Shader "ProjectRun/RaceSkill/SH_Vefects_BIRP_Refraction_Shockwave_01_2d787569"
{
Properties
	{
		[Space(33)][Header(Cutout)][Space(13)]_CutoutTexture("Cutout Texture", 2D) = "white" {}
		_CutoutMaskSelector("Cutout Mask Selector", Vector) = (0,1,0,0)
		_CutoutErosion("Cutout Erosion", Float) = 0
		_CutoutErosionSmoothness("Cutout Erosion Smoothness", Float) = 1
		[Space(33)][Header(Distortion Noise)][Space(13)]_DistortionNoise("Distortion Noise", 2D) = "white" {}
		_DistortionNoiseSelector("Distortion Noise Selector", Vector) = (0,1,0,0)
		_DistUVS("Dist UV S", Vector) = (1,1,0,0)
		_DistUVP("Dist UV P", Vector) = (0,0,0,0)
		_DistortionLerp("Distortion Lerp", Float) = 1
		[Space(33)][Header(Distortion Dist Noise)][Space(13)]_DistortionDist("Distortion Dist", 2D) = "white" {}
		_DistDistUVS("Dist Dist UV S", Vector) = (1,1,0,0)
		_DistDistUVP("Dist Dist UV P", Vector) = (0,0,0,0)
		_DistortionDistLerp("Distortion Dist Lerp", Float) = 0.1
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 0
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
Pass {
Tags { "LightMode"="SRPDefaultUnlit" }
Cull [_Cull]
ZWrite Off
ZTest LEqual
Blend [_Src] [_Dst]
HLSLPROGRAM
#pragma target 3.5
#pragma vertex RaceVert
#pragma fragment RaceFrag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
float _RunRaceEffectTime;
struct SurfaceOutput { half3 Albedo; half3 Normal; half3 Emission; half Specular; half Gloss; half Alpha; };

		#define ASE_VERSION 19701
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 screenPos;
			float4 uv_texcoord;
			float4 vertexColor : COLOR;
		};

		uniform float _Cull;
		uniform float _Src;
		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;

		uniform sampler2D _DistortionNoise;
		uniform float2 _DistUVP;
		uniform float2 _DistUVS;
		uniform sampler2D _DistortionDist;
		uniform float2 _DistDistUVP;
		uniform float2 _DistDistUVS;
		uniform float _DistortionDistLerp;
		uniform float4 _DistortionNoiseSelector;
		uniform float _CutoutErosion;
		uniform float _CutoutErosionSmoothness;
		uniform sampler2D _CutoutTexture;
		uniform float4 _CutoutTexture_ST;
		uniform float4 _CutoutMaskSelector;
		uniform float _DistortionLerp;
		
		


inline float4 ASE_ComputeGrabScreenPos( float4 pos )
{
	#if UNITY_UV_STARTS_AT_TOP
	float scale = -1.0;
	#else
	float scale = 1.0;
	#endif
	float4 o = pos;
	o.y = pos.w * 0.5f;
	o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
	return o;
}


		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_screenPos );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float2 appendResult18 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
			float2 panner35 = ( 1.0 * _RunRaceEffectTime * _DistUVP + ( i.uv_texcoord.xy * _DistUVS ));
			float2 panner50 = ( 1.0 * _RunRaceEffectTime * _DistDistUVP + ( i.uv_texcoord.xy * _DistDistUVS ));
			float2 lerpResult59 = lerp( float2( 0,0 ) , ( ( (tex2D( _DistortionDist, panner50 ).rgb).xy + -0.5 ) * 2.0 ) , _DistortionDistLerp);
			float4 tex2DNode10 = tex2D( _DistortionNoise, ( panner35 + lerpResult59 ) );
			float dotResult37 = dot( tex2DNode10 , _DistortionNoiseSelector );
			float2 uv_CutoutTexture = i.uv_texcoord * _CutoutTexture_ST.xy + _CutoutTexture_ST.zw;
			float dotResult41 = dot( tex2D( _CutoutTexture, uv_CutoutTexture ) , _CutoutMaskSelector );
			float smoothstepResult69 = smoothstep( _CutoutErosion , ( _CutoutErosion + _CutoutErosionSmoothness ) , saturate( dotResult41 ));
			float2 temp_cast_2 = (saturate( ( saturate( dotResult37 ) * saturate( smoothstepResult69 ) ) )).xx;
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			
			float distanceDepth71 = 1.0;
			float2 lerpResult21 = lerp( float2( 0,0 ) , temp_cast_2 , ( ( _DistortionLerp * i.uv_texcoord.z ) * distanceDepth71 ));
			float4 screenColor12 = float4(SampleSceneColor(appendResult18 + lerpResult21), 1);
			o.Emission = screenColor12.rgb;
			o.Alpha = i.vertexColor.a;
		}

		
struct RaceAttributes {
 float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR;
 float4 uv0:TEXCOORD0; float4 uv1:TEXCOORD1; float4 uv2:TEXCOORD2; float4 uv3:TEXCOORD3;
};
struct RaceVaryings {
 float4 positionCS:SV_POSITION; float4 color:COLOR;
 float4 uv0:TEXCOORD0; float4 uv1:TEXCOORD1; float4 uv2:TEXCOORD2; float4 uv3:TEXCOORD3;
 float4 screenPos:TEXCOORD4; float3 worldPos:TEXCOORD5; float3 worldNormal:TEXCOORD6;
};
RaceVaryings RaceVert(RaceAttributes v) {
 RaceVaryings o=(RaceVaryings)0;
 o.worldPos=TransformObjectToWorld(v.vertex.xyz);
 o.positionCS=TransformWorldToHClip(o.worldPos);
 o.worldNormal=TransformObjectToWorldNormal(v.normal);
 o.screenPos=ComputeScreenPos(o.positionCS);
 o.color=v.color; o.uv0=v.uv0; o.uv1=v.uv1; o.uv2=v.uv2; o.uv3=v.uv3; return o;
}
half4 RaceFrag(RaceVaryings v, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target {
 Input i=(Input)0; i.screenPos = v.screenPos; i.uv_texcoord = v.uv0; i.vertexColor = v.color;
 SurfaceOutput o=(SurfaceOutput)0; surf(i,o); return half4(o.Emission, saturate(o.Alpha));
}
ENDHLSL
}
}
}
