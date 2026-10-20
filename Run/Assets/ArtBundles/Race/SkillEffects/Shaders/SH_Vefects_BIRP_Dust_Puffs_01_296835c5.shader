// Project Run URP adaptation of the purchased shader. Source preserved in Project OT.
Shader "ProjectRun/RaceSkill/SH_Vefects_BIRP_Dust_Puffs_01_296835c5"
{
Properties
	{
		[Space(33)][Header(Main Texture)][Space(13)]_MainTexture("Main Texture", 2D) = "white" {}
		_Emission("Emission", Float) = 1
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		_ParticleColorLUT("Particle Color / LUT", Float) = 0
		[Space(33)][Header(Distortion)][Space(13)]_DistortionNoise("Distortion Noise", 2D) = "white" {}
		_DistortionNoiseTextureSelector("Distortion Noise Texture Selector", Vector) = (0,1,0,0)
		_DistortionNoiseUVScale("Distortion Noise UV Scale", Vector) = (1,1,0,0)
		_DistortionNoiseUVPanSpeed("Distortion Noise UV Pan Speed", Vector) = (0.05,-0.2,0,0)
		_DistortionIntensity("Distortion Intensity", Float) = 0.03
		[Space(33)][Header(LUT)][Space(13)]_LUT("LUT", 2D) = "white" {}
		_LUTAmplitude("LUT Amplitude", Float) = 1
		_LUTOffset("LUT Offset", Float) = 0
		_LUTPanSpeed("LUT Pan Speed", Float) = 0
		_LUTErosionSmoothness("LUT Erosion Smoothness", Float) = 1
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
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

float _RunRaceEffectTime;
struct SurfaceOutput { half3 Albedo; half3 Normal; half3 Emission; half Specular; half Gloss; half Alpha; };

		#define ASE_VERSION 19701
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 vertexColor : COLOR;
			float4 uv2_texcoord2;
			float4 uv_texcoord;
			float4 screenPos;
		};

		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform float _Src;
		uniform sampler2D _LUT;
		uniform float _LUTPanSpeed;
		uniform float _LUTErosionSmoothness;
		uniform sampler2D _MainTexture;
		uniform sampler2D _DistortionNoise;
		uniform float2 _DistortionNoiseUVPanSpeed;
		uniform float2 _DistortionNoiseUVScale;
		uniform float4 _DistortionNoiseTextureSelector;
		uniform float _DistortionIntensity;
		uniform float _LUTAmplitude;
		uniform float _LUTOffset;
		uniform float _ParticleColorLUT;
		uniform float _Emission;
		uniform float _ErosionSmoothness;
		
		

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float2 panner38 = ( 1.0 * _RunRaceEffectTime * _DistortionNoiseUVPanSpeed + ( i.uv_texcoord.xy * _DistortionNoiseUVScale ));
			float dotResult41 = dot( tex2D( _DistortionNoise, panner38 ) , _DistortionNoiseTextureSelector );
			float UVDist47 = ( ( saturate( dotResult41 ) + -0.5 ) * 2.0 );
			float4 tex2DNode16 = tex2D( _MainTexture, ( i.uv_texcoord.xy + ( UVDist47 * _DistortionIntensity ) ) );
			float smoothstepResult68 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _LUTErosionSmoothness ) , tex2DNode16.g);
			float2 temp_cast_2 = (( ( saturate( smoothstepResult68 ) * _LUTAmplitude ) + _LUTOffset )).xx;
			float2 panner58 = ( 1.0 * _RunRaceEffectTime * temp_cast_0 + temp_cast_2);
			float4 lerpResult62 = lerp( i.vertexColor , ( i.vertexColor * float4( tex2D( _LUT, panner58 ).rgb , 0.0 ) ) , _ParticleColorLUT);
			o.Emission = ( lerpResult62 * ( _Emission * i.uv_texcoord.z ) ).rgb;
			float smoothstepResult29 = smoothstep( i.uv2_texcoord2.x , ( i.uv2_texcoord2.x + _ErosionSmoothness ) , tex2DNode16.g);
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			
			float distanceDepth26 = 1.0;
			o.Alpha = saturate( ( saturate( ( saturate( smoothstepResult29 ) * i.vertexColor.a ) ) * distanceDepth26 ) );
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
 Input i=(Input)0; i.vertexColor = v.color; i.uv2_texcoord2 = v.uv1; i.uv_texcoord = v.uv0; i.screenPos = v.screenPos;
 SurfaceOutput o=(SurfaceOutput)0; surf(i,o); return half4(o.Emission, saturate(o.Alpha));
}
ENDHLSL
}
}
}
