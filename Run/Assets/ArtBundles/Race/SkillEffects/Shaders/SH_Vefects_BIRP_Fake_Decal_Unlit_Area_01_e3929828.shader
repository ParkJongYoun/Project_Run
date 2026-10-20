// Project Run URP adaptation of the purchased shader. Source preserved in Project OT.
Shader "ProjectRun/RaceSkill/SH_Vefects_BIRP_Fake_Decal_Unlit_Area_01_e3929828"
{
Properties
	{
		_Emissive("Emissive", Float) = 1
		[Space(33)][Header(Decal)][Space(13)]_DecalTexture("Decal Texture", 2D) = "white" {}
		_DecalTextureSelector("Decal Texture Selector", Vector) = (0,1,0,0)
		_DecalUVScale("Decal UV Scale", Vector) = (1,1,0,0)
		_DecalUVPanSpeed("Decal UV Pan Speed", Vector) = (0,0.3,0,0)
		_DecalRotation("Decal Rotation", Float) = 0
		_DecalScaleFromCenter("Decal Scale From Center", Float) = 1
		_DecalScaleFromCenterNonUniform("Decal Scale From Center Non Uniform", Vector) = (1,1,0,0)
		[Space(33)][Header(Decal Op)][Space(13)]_DecalOpTexture("Decal Op Texture", 2D) = "white" {}
		_DecalOpTextureSelector("Decal Op Texture Selector", Vector) = (0,1,0,0)
		_DecalOpUVScale("Decal Op UV Scale", Vector) = (1,1,0,0)
		_DecalOpUVPanSpeed("Decal Op UV Pan Speed", Vector) = (0,0,0,0)
		[Space(33)][Header(Fake Decal)][Space(13)]_FakeDecalDepthFade("Fake Decal Depth Fade", Float) = 1
		_FakeDecalDepthFadeErosion("Fake Decal Depth Fade Erosion", Float) = 0
		_FakeDecalDepthFadeErosionSmoothness("Fake Decal Depth Fade Erosion Smoothness", Float) = 0.1
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		_OpacityBoost("Opacity Boost", Float) = 1
		_NormalizedErosion("Normalized Erosion", Float) = 0
		_NormalizedErosionSmoothness("Normalized Erosion Smoothness", Float) = 1
		[Space(33)][Header(Radial)][Space(13)]_RadialUVDistortNoise("Radial UV Distort Noise", 2D) = "white" {}
		_RadialUVTile("Radial UV Tile", Vector) = (1,1,0,0)
		_RadialUVPanSpeed("Radial UV Pan Speed", Vector) = (0,0,0,0)
		_RadialUVDistortScale("Radial UV Distort Scale", Vector) = (1,1,0,0)
		_RadialUVDistortSpeed("Radial UV Distort Speed", Vector) = (0.1,0.01,0,0)
		_RadialUVDistortIntensity("Radial UV Distort Intensity", Float) = 0.1
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
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 vertexColor : COLOR;
			float4 screenPos;
		};

		uniform float _Src;
		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform sampler2D _LUT;
		uniform float _LUTPanSpeed;
		uniform float _LUTErosionSmoothness;
		uniform sampler2D _DecalTexture;
		uniform float2 _DecalUVPanSpeed;
		uniform sampler2D _RadialUVDistortNoise;
		uniform float2 _RadialUVDistortScale;
		uniform float2 _DecalScaleFromCenterNonUniform;
		uniform float _DecalScaleFromCenter;
		uniform float _DecalRotation;
		uniform float2 _RadialUVDistortSpeed;
		uniform float _RadialUVDistortIntensity;
		uniform float2 _RadialUVTile;
		uniform float2 _RadialUVPanSpeed;
		uniform float2 _DecalUVScale;
		uniform float4 _DecalTextureSelector;
		uniform float _NormalizedErosion;
		uniform float _NormalizedErosionSmoothness;
		uniform float _LUTAmplitude;
		uniform float _LUTOffset;
		uniform float _Emissive;
		uniform float _ErosionSmoothness;
		uniform sampler2D _DecalOpTexture;
		uniform float2 _DecalOpUVPanSpeed;
		uniform float2 _DecalOpUVScale;
		uniform float4 _DecalOpTextureSelector;
		uniform float _OpacityBoost;
		uniform float _FakeDecalDepthFadeErosion;
		uniform float _FakeDecalDepthFadeErosionSmoothness;
		
		
		uniform float _FakeDecalDepthFade;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 temp_cast_0 = (_LUTPanSpeed).xx;
			float eros79 = i.uv_texcoord.w;
			float2 _Vector1 = float2(0.5,0.5);
			float randomRotate57 = i.uv2_texcoord2.x;
			float cos52 = cos( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * PI ) ) / 360.0 ) );
			float sin52 = sin( ( ( ( _DecalRotation + randomRotate57 ) * ( 2.0 * PI ) ) / 360.0 ) );
			float2 rotator52 = mul( ( ( ( i.uv_texcoord.xy - _Vector1 ) / ( _DecalScaleFromCenterNonUniform * _DecalScaleFromCenter ) ) + _Vector1 ) - float2( 0.5,0.5 ) , float2x2( cos52 , -sin52 , sin52 , cos52 )) + float2( 0.5,0.5 );
			float2 decalUV87 = rotator52;
			float2 break234 = decalUV87;
			float2 appendResult183 = (float2(( (_RadialUVDistortScale).x * break234.x ) , ( break234.y * (_RadialUVDistortScale).y )));
			float2 panner165 = ( ( (_RadialUVDistortSpeed).x * _RunRaceEffectTime ) * float2( 1,0 ) + decalUV87);
			float2 panner166 = ( ( _RunRaceEffectTime * (_RadialUVDistortSpeed).y ) * float2( 0,1 ) + decalUV87);
			float2 appendResult182 = (float2((panner165).x , (panner166).y));
			float2 uvs_TexCoord145 = i.uv_texcoord;
			uvs_TexCoord145.xy = i.uv_texcoord.xy * float2( 2,2 );
			float2 temp_output_147_0 = ( uvs_TexCoord145.xy - float2( 1,1 ) );
			float2 appendResult179 = (float2(frac( ( atan2( (temp_output_147_0).x , (temp_output_147_0).y ) / 6.28318548202515 ) ) , length( temp_output_147_0 )));
			float2 panner185 = ( ( (_RadialUVPanSpeed).x * _RunRaceEffectTime ) * float2( 1,0 ) + appendResult179);
			float2 panner184 = ( ( _RunRaceEffectTime * (_RadialUVPanSpeed).y ) * float2( 0,1 ) + appendResult179);
			float2 appendResult190 = (float2((panner185).x , (panner184).y));
			float2 radialUVs215 = ( ( (tex2D( _RadialUVDistortNoise, ( appendResult183 + appendResult182 ) )).rg * _RadialUVDistortIntensity ) + ( _RadialUVTile * appendResult190 ) );
			float2 panner247 = ( 1.0 * _RunRaceEffectTime * _DecalUVPanSpeed + ( radialUVs215 * _DecalUVScale ));
			float dotResult104 = dot( tex2D( _DecalTexture, panner247 ) , _DecalTextureSelector );
			float smoothstepResult93 = smoothstep( eros79 , ( eros79 + _LUTErosionSmoothness ) , saturate( dotResult104 ));
			float temp_output_222_0 = saturate( (appendResult179).y );
			float saferPower122 = abs( temp_output_222_0 );
			float smoothstepResult239 = smoothstep( _NormalizedErosion , ( _NormalizedErosion + _NormalizedErosionSmoothness ) , temp_output_222_0);
			float LUTOffset109 = i.uv2_texcoord2.y;
			float2 temp_cast_2 = (( ( saturate( ( saturate( ( saturate( ( saturate( smoothstepResult93 ) - saturate( pow( saferPower122 , 2.0 ) ) ) ) * 0.25 ) ) + smoothstepResult239 ) ) * _LUTAmplitude ) + ( _LUTOffset + LUTOffset109 ) )).xx;
			float2 panner98 = ( 1.0 * _RunRaceEffectTime * temp_cast_0 + temp_cast_2);
			float em78 = i.uv_texcoord.z;
			o.Emission = ( ( tex2D( _LUT, panner98 ).rgb * (i.vertexColor).rgb ) * ( _Emissive * em78 ) );
			float2 panner251 = ( 1.0 * _RunRaceEffectTime * _DecalOpUVPanSpeed + ( radialUVs215 * _DecalOpUVScale ));
			float dotResult107 = dot( tex2D( _DecalOpTexture, panner251 ) , _DecalOpTextureSelector );
			float smoothstepResult82 = smoothstep( eros79 , ( eros79 + _ErosionSmoothness ) , saturate( dotResult107 ));
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			
			float distanceDepth270 = 1.0;
			float smoothstepResult273 = smoothstep( _FakeDecalDepthFadeErosion , ( _FakeDecalDepthFadeErosion + _FakeDecalDepthFadeErosionSmoothness ) , distanceDepth270);
			o.Alpha = saturate( ( saturate( ( saturate( ( saturate( saturate( smoothstepResult82 ) ) * _OpacityBoost ) ) * i.vertexColor.a ) ) * ( 1.0 - saturate( smoothstepResult273 ) ) ) );
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
 Input i=(Input)0; i.uv_texcoord = v.uv0; i.uv2_texcoord2 = v.uv1; i.vertexColor = v.color; i.screenPos = v.screenPos;
 SurfaceOutput o=(SurfaceOutput)0; surf(i,o); return half4(o.Emission, saturate(o.Alpha));
}
ENDHLSL
}
}
}
