// Project Run URP adaptation of the purchased shader. Source preserved in Project OT.
Shader "ProjectRun/RaceSkill/SH_Vefects_BIRP_Light_Rays_Add_01_d4414f95"
{
Properties
	{
		[Space(33)][Header(Main Texture)][Space(13)]_MainTexture("Main Texture", 2D) = "white" {}
		_Ray01UVScale("Ray 01 UV Scale", Float) = 0.5
		_Ray01UVSpeed("Ray 01 UV Speed", Vector) = (-0.12,0,0,0)
		_Ray02UVScale("Ray 02 UV Scale", Float) = 0.25
		_Ray02UVSpeed("Ray 02 UV Speed", Vector) = (0.07,0,0,0)
		_Emission("Emission", Float) = 1
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 2
		_Src("Src", Float) = 1
		_Dst("Dst", Float) = 1
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
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 screenPos;
		};

		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		uniform float _Src;
		uniform float _Dst;
		uniform sampler2D _MainTexture;
		uniform float2 _Ray01UVSpeed;
		uniform float _Ray01UVScale;
		uniform float2 _Ray02UVSpeed;
		uniform float _Ray02UVScale;
		
		
		uniform float _Emission;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 panner54 = ( 1.0 * _RunRaceEffectTime * _Ray01UVSpeed + ( i.uv_texcoord.xy * _Ray01UVScale ));
			float random64 = i.uv2_texcoord2.x;
			float2 panner58 = ( 1.0 * _RunRaceEffectTime * _Ray02UVSpeed + ( i.uv_texcoord.xy * _Ray02UVScale ));
			float4 tex2DNode31 = tex2D( _MainTexture, i.uv_texcoord.xy );
			float temp_output_70_0 = saturate( tex2DNode31.b );
			float saferPower44 = abs( ( temp_output_70_0 * 0.5 ) );
			float saferPower32 = abs( tex2DNode31.g );
			float4 ase_screenPos = float4( i.screenPos.xyz , i.screenPos.w + 0.00000000001 );
			float4 ase_screenPosNorm = ase_screenPos / ase_screenPos.w;
			ase_screenPosNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_screenPosNorm.z : ase_screenPosNorm.z * 0.5 + 0.5;
			
			float distanceDepth26 = 1.0;
			float temp_output_18_0 = saturate( ( saturate( ( saturate( ( saturate( ( saturate( ( saturate( ( tex2D( _MainTexture, ( panner54 + random64 ) ).r * tex2D( _MainTexture, ( panner58 + random64 ) ).r ) ) * temp_output_70_0 ) ) + pow( saferPower44 , 2.0 ) ) ) * pow( saferPower32 , 2.0 ) ) ) * i.vertexColor.a ) ) * distanceDepth26 ) );
			o.Emission = ( ( i.vertexColor * temp_output_18_0 ) * ( _Emission * i.uv_texcoord.z ) ).rgb;
			o.Alpha = temp_output_18_0;
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
 Input i=(Input)0; i.vertexColor = v.color; i.uv_texcoord = v.uv0; i.uv2_texcoord2 = v.uv1; i.screenPos = v.screenPos;
 SurfaceOutput o=(SurfaceOutput)0; surf(i,o); return half4(o.Emission, saturate(o.Alpha));
}
ENDHLSL
}
}
}
