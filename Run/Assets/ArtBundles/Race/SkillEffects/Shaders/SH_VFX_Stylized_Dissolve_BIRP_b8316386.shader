// Project Run URP adaptation of the purchased shader. Source preserved in Project OT.
Shader "ProjectRun/RaceSkill/SH_VFX_Stylized_Dissolve_BIRP_b8316386"
{
Properties
	{
		[Space(33)][Header(Main Texture)][Space(13)]_Texture("Texture", 2D) = "white" {}
		_TextureChannel("Texture Channel", Vector) = (0,1,0,0)
		_TextureRotation("Texture Rotation", Float) = 0
		_TexturePanSpeed("Texture Pan Speed", Vector) = (0,0,0,0)
		[Space(33)][Header(Color Texture)][Space(13)]_ColorTexture("Color Texture", 2D) = "white" {}
		_ColorRotation("Color Rotation", Float) = 0
		[Space(33)][Header(Gradient Shape)][Space(13)]_GradientShape("Gradient Shape", 2D) = "white" {}
		_GradientShapeChannel("Gradient Shape Channel", Vector) = (0,1,0,0)
		_GradientShapeRotation("Gradient Shape Rotation", Float) = 0
		[Space(33)][Header(Gradient Map)][Space(13)]_GradientMap("Gradient Map", 2D) = "white" {}
		_GradientMapDisplacement("Gradient Map Displacement", Float) = 0.1
		_InvertGradient("Invert Gradient", Float) = 0
		[Space(33)][Header(Distortion)][Space(13)]_DistortionMask("Distortion Mask", 2D) = "white" {}
		_DistortionMaskChannel("Distortion Mask Channel", Vector) = (0,1,0,0)
		_DistortionMaskRotation("Distortion Mask Rotation", Float) = 0
		_DistortionMaskPanSpeed("Distortion Mask Pan Speed", Vector) = (0,0,0,0)
		_DistortionIntensity("Distortion Intensity", Float) = 0
		[Space(33)][Header(Dissolve)][Space(13)]_DissolveMask("Dissolve Mask", 2D) = "white" {}
		_DissolveMaskChannel("Dissolve Mask Channel", Vector) = (0,1,0,0)
		_DissolveMaskRotation("Dissolve Mask Rotation", Float) = 0
		_DissolveMaskPanSpeed("Dissolve Mask Pan Speed", Vector) = (0,0,0,0)
		_DissolveMaskInvert("Dissolve Mask Invert", Range( 0 , 1)) = 0
		_DissolveOffset("Dissolve Offset", Float) = 0
		[Space(33)][Header(Properties)][Space(13)]_EmissionIntensity("Emission Intensity", Float) = 1
		_CoreColor("Core Color", Color) = (1,1,1,0)
		_DifferentCoreColor("Different Core Color", Float) = 0
		_CorePower("Core Power", Float) = 1
		_CoreIntensity("Core Intensity", Float) = 0
		_GlowIntensity("Glow Intensity", Float) = 1
		_AlphaBoldness("Alpha Boldness", Float) = 1
		[Toggle(_CUSTOMPANSWITCH_ON)] _CustomPanSwitch("CustomPanSwitch", Float) = 0
		[Toggle(_MESHVERTEXCOLOR_ON)] _MeshVertexColor("MeshVertexColor", Float) = 0
		[Toggle(_STEP_ON)] _Step("Step", Float) = 0
		_ValueStep("Value Step", Float) = 0
		_ValueStepAdd("Value Step Add", Float) = 0.1
		[Space(33)][Header(Depth Fade)][Space(13)][Toggle(_USEDEPTHFADE_ON)] _UseDepthFade("Use Depth Fade", Float) = 0
		_DepthFadeIntensity("Depth Fade Intensity", Float) = 0
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord4( "", 2D ) = "white" {}
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

		#pragma shader_feature_local _USEDEPTHFADE_ON
		#pragma shader_feature_local _CUSTOMPANSWITCH_ON
		#pragma shader_feature_local _STEP_ON
		#pragma shader_feature_local _MESHVERTEXCOLOR_ON
		#define ASE_VERSION 19701

		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 uv_texcoord;
			float2 uv2_texcoord2;
			float2 uv4_texcoord4;
			float4 vertexColor : COLOR;
			float4 screenPos;
		};

		uniform float _Cull;
		uniform float _Src;
		uniform float _Dst;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform sampler2D _ColorTexture;
		uniform float4 _ColorTexture_ST;
		uniform float _ColorRotation;
		uniform sampler2D _DistortionMask;
		uniform float4 _DistortionMask_ST;
		uniform float _DistortionMaskRotation;
		uniform float2 _DistortionMaskPanSpeed;
		uniform float4 _DistortionMaskChannel;
		uniform float _DistortionIntensity;
		uniform sampler2D _GradientMap;
		uniform sampler2D _GradientShape;
		uniform float4 _GradientShape_ST;
		uniform float _GradientShapeRotation;
		uniform float4 _GradientShapeChannel;
		uniform sampler2D _DissolveMask;
		uniform float4 _DissolveMask_ST;
		uniform float _DissolveMaskRotation;
		uniform float2 _DissolveMaskPanSpeed;
		uniform float4 _DissolveMaskChannel;
		uniform float _DissolveMaskInvert;
		uniform float _DissolveOffset;
		uniform float _InvertGradient;
		uniform float _GradientMapDisplacement;
		uniform float4 _CoreColor;
		uniform sampler2D _Texture;
		uniform float4 _Texture_ST;
		uniform float _TextureRotation;
		uniform float2 _TexturePanSpeed;
		uniform float4 _TextureChannel;
		uniform float _CorePower;
		uniform float _CoreIntensity;
		uniform float _DifferentCoreColor;
		uniform float _EmissionIntensity;
		uniform float _GlowIntensity;
		uniform float _AlphaBoldness;
		uniform float _ValueStep;
		uniform float _ValueStepAdd;
		
		
		uniform float _DepthFadeIntensity;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float4 uvs_ColorTexture = i.uv_texcoord;
			uvs_ColorTexture.xy = i.uv_texcoord.xy * _ColorTexture_ST.xy + _ColorTexture_ST.zw;
			float cos190 = cos( radians( _ColorRotation ) );
			float sin190 = sin( radians( _ColorRotation ) );
			float2 rotator190 = mul( uvs_ColorTexture.xy - float2( 0.5,0.5 ) , float2x2( cos190 , -sin190 , sin190 , cos190 )) + float2( 0.5,0.5 );
			float2 temp_cast_0 = (0.0).xx;
			#ifdef _CUSTOMPANSWITCH_ON
				float2 staticSwitch85 = i.uv2_texcoord2;
			#else
				float2 staticSwitch85 = temp_cast_0;
			#endif
			float2 CustomUV89 = staticSwitch85;
			float4 uvs_DistortionMask = i.uv_texcoord;
			uvs_DistortionMask.xy = i.uv_texcoord.xy * _DistortionMask_ST.xy + _DistortionMask_ST.zw;
			float cos95 = cos( radians( _DistortionMaskRotation ) );
			float sin95 = sin( radians( _DistortionMaskRotation ) );
			float2 rotator95 = mul( uvs_DistortionMask.xy - float2( 0.5,0.5 ) , float2x2( cos95 , -sin95 , sin95 , cos95 )) + float2( 0.5,0.5 );
			float dotResult100 = dot( tex2D( _DistortionMask, ( rotator95 + uvs_DistortionMask.w + CustomUV89 + ( _RunRaceEffectTime * _DistortionMaskPanSpeed ) ) ) , _DistortionMaskChannel );
			float Disto107 = ( saturate( dotResult100 ) * _DistortionIntensity );
			float4 uvs_GradientShape = i.uv_texcoord;
			uvs_GradientShape.xy = i.uv_texcoord.xy * _GradientShape_ST.xy + _GradientShape_ST.zw;
			float cos218 = cos( radians( _GradientShapeRotation ) );
			float sin218 = sin( radians( _GradientShapeRotation ) );
			float2 rotator218 = mul( uvs_GradientShape.xy - float2( 0.5,0.5 ) , float2x2( cos218 , -sin218 , sin218 , cos218 )) + float2( 0.5,0.5 );
			float dotResult232 = dot( tex2D( _GradientShape, ( rotator218 + CustomUV89 + Disto107 ) ) , _GradientShapeChannel );
			float4 uvs_DissolveMask = i.uv_texcoord;
			uvs_DissolveMask.xy = i.uv_texcoord.xy * _DissolveMask_ST.xy + _DissolveMask_ST.zw;
			float cos112 = cos( radians( _DissolveMaskRotation ) );
			float sin112 = sin( radians( _DissolveMaskRotation ) );
			float2 rotator112 = mul( uvs_DissolveMask.xy - float2( 0.5,0.5 ) , float2x2( cos112 , -sin112 , sin112 , cos112 )) + float2( 0.5,0.5 );
			float dotResult122 = dot( tex2D( _DissolveMask, ( rotator112 + uvs_DissolveMask.w + CustomUV89 + ( _RunRaceEffectTime * _DissolveMaskPanSpeed ) + Disto107 ) ) , _DissolveMaskChannel );
			float temp_output_126_0 = saturate( dotResult122 );
			float lerpResult138 = lerp( temp_output_126_0 , saturate( ( 1.0 - temp_output_126_0 ) ) , _DissolveMaskInvert);
			float temp_output_145_0 = ( saturate( lerpResult138 ) + i.uv_texcoord.z + _DissolveOffset );
			float temp_output_225_0 = saturate( ( saturate( dotResult232 ) * temp_output_145_0 ) );
			float lerpResult196 = lerp( saturate( ( 1.0 - temp_output_225_0 ) ) , temp_output_225_0 , _InvertGradient);
			float2 temp_cast_4 = (( lerpResult196 + _GradientMapDisplacement )).xx;
			float3 temp_output_157_0 = (i.vertexColor).rgb;
			float3 temp_output_198_0 = ( saturate( ( (tex2D( _ColorTexture, ( rotator190 + CustomUV89 + Disto107 ) )).rgb + i.uv4_texcoord4.x ) ) * (tex2D( _GradientMap, temp_cast_4 )).rgb * temp_output_157_0 );
			float4 uvs_Texture = i.uv_texcoord;
			uvs_Texture.xy = i.uv_texcoord.xy * _Texture_ST.xy + _Texture_ST.zw;
			float cos129 = cos( radians( _TextureRotation ) );
			float sin129 = sin( radians( _TextureRotation ) );
			float2 rotator129 = mul( uvs_Texture.xy - float2( 0.5,0.5 ) , float2x2( cos129 , -sin129 , sin129 , cos129 )) + float2( 0.5,0.5 );
			float dotResult140 = dot( tex2D( _Texture, ( rotator129 + ( _RunRaceEffectTime * _TexturePanSpeed ) + CustomUV89 + Disto107 ) ) , _TextureChannel );
			float temp_output_147_0 = ( temp_output_145_0 * saturate( dotResult140 ) );
			float temp_output_153_0 = ( pow( temp_output_147_0 , _CorePower ) * _CoreIntensity );
			float4 lerpResult188 = lerp( float4( temp_output_198_0 , 0.0 ) , _CoreColor , saturate( temp_output_153_0 ));
			float4 lerpResult217 = lerp( float4( temp_output_198_0 , 0.0 ) , saturate( lerpResult188 ) , _DifferentCoreColor);
			float3 temp_cast_8 = (1.0).xxx;
			#ifdef _MESHVERTEXCOLOR_ON
				float3 staticSwitch159 = temp_output_157_0;
			#else
				float3 staticSwitch159 = temp_cast_8;
			#endif
			float3 temp_output_167_0 = saturate( ( ( i.vertexColor.a * saturate( ( temp_output_153_0 + ( temp_output_147_0 * _GlowIntensity ) ) ) * staticSwitch159 ) * _AlphaBoldness ) );
			float3 temp_cast_9 = (_ValueStep).xxx;
			float3 temp_cast_10 = (( _ValueStep + _ValueStepAdd )).xxx;
			float3 smoothstepResult170 = smoothstep( temp_cast_9 , temp_cast_10 , temp_output_167_0);
			#ifdef _STEP_ON
				float3 staticSwitch183 = saturate( smoothstepResult170 );
			#else
				float3 staticSwitch183 = temp_output_167_0;
			#endif
			float4 temp_output_234_0 = ( ( saturate( lerpResult217 ) * _EmissionIntensity ) * float4( staticSwitch183 , 0.0 ) );
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			
			float distanceDepth213 = 1.0;
			float temp_output_236_0 = saturate( distanceDepth213 );
			#ifdef _USEDEPTHFADE_ON
				float4 staticSwitch238 = ( temp_output_234_0 * temp_output_236_0 );
			#else
				float4 staticSwitch238 = temp_output_234_0;
			#endif
			o.Emission = staticSwitch238.rgb;
			#ifdef _USEDEPTHFADE_ON
				float3 staticSwitch239 = ( staticSwitch183 * temp_output_236_0 );
			#else
				float3 staticSwitch239 = staticSwitch183;
			#endif
			o.Alpha = staticSwitch239.x;
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
 Input i=(Input)0; i.uv_texcoord = v.uv0; i.uv2_texcoord2 = v.uv1.xy; i.uv4_texcoord4 = v.uv3.xy; i.vertexColor = v.color; i.screenPos = v.screenPos;
 SurfaceOutput o=(SurfaceOutput)0; surf(i,o); return half4(o.Emission, saturate(o.Alpha));
}
ENDHLSL
}
}
}
