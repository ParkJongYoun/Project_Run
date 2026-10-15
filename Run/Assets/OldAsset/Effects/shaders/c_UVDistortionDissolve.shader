// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "project-rs/c_UVDistortionDissolve"
{
	Properties
	{
		_Texture("Texture", 2D) = "white" {}
		_Mask02("Mask02", 2D) = "white" {}
		_TextureDistortion("TextureDistortion", 2D) = "white" {}
		_Opacity("Opacity", Float) = 1
		_Emission("Emission", Range( 0 , 5)) = 1
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("CullMode", Float) = 0
		_DistortionPanner_U("DistortionPanner_U", Float) = 0
		_DistortionPanner_V("DistortionPanner_V", Float) = 0
		[HideInInspector] _tex4coord2( "", 2D ) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull [_CullMode]
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma surface surf Unlit keepalpha noshadow 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 vertexColor : COLOR;
			float2 uv_texcoord;
			float4 uv2_tex4coord2;
		};

		uniform float _CullMode;
		uniform float _Emission;
		uniform sampler2D _Mask02;
		SamplerState sampler_Mask02;
		uniform float4 _Mask02_ST;
		uniform sampler2D _Texture;
		SamplerState sampler_Texture;
		uniform sampler2D _TextureDistortion;
		uniform float _DistortionPanner_U;
		uniform float _DistortionPanner_V;
		uniform float _Opacity;

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			o.Emission = ( _Emission * i.vertexColor ).rgb;
			float2 uv_Mask02 = i.uv_texcoord * _Mask02_ST.xy + _Mask02_ST.zw;
			float4 appendResult190 = (float4(i.uv2_tex4coord2.x , i.uv2_tex4coord2.y , 0.0 , 0.0));
			float4 appendResult182 = (float4(_DistortionPanner_U , _DistortionPanner_V , 0.0 , 0.0));
			float clampResult162 = clamp( ( ( tex2D( _Mask02, uv_Mask02 ).r * tex2D( _Texture, ( appendResult190 + ( float4( i.uv_texcoord, 0.0 , 0.0 ) + ( tex2D( _TextureDistortion, ( ( _Time.y * appendResult182 ) + float4( i.uv_texcoord, 0.0 , 0.0 ) ).xy ) * i.uv2_tex4coord2.z ) ) ).xy ).r ) - i.uv2_tex4coord2.w ) , 0.0 , 1.0 );
			o.Alpha = ( i.vertexColor.a * clampResult162 * _Opacity );
		}

		ENDCG
	}
}
/*ASEBEGIN
Version=18703
78.28572;176.5714;2023.429;840.7143;4147.487;1391.078;1.9;True;False
Node;AmplifyShaderEditor.RangedFloatNode;180;-3271.081,-201.6206;Inherit;False;Property;_DistortionPanner_V;DistortionPanner_V;8;0;Create;True;0;0;False;0;False;0;-0.43;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;179;-3272.743,-281.6113;Inherit;False;Property;_DistortionPanner_U;DistortionPanner_U;7;0;Create;True;0;0;False;0;False;0;-0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;182;-3023.557,-250.5887;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleTimeNode;181;-3048.384,-55.34974;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;184;-2868.088,-268.0222;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;183;-3283.168,-117.8216;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleAddOpNode;185;-2706.554,-249.1185;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;FLOAT2;0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;187;-2445.859,-473.4931;Inherit;False;1;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;188;-2556.302,-278.89;Inherit;True;Property;_TextureDistortion;TextureDistortion;3;0;Create;True;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;189;-2228.665,-254.5035;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;191;-2048.213,-124.3739;Inherit;False;2;2;0;FLOAT2;0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DynamicAppendNode;190;-2135.104,-458.9993;Inherit;False;FLOAT4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleAddOpNode;192;-1916.394,-149.2061;Inherit;False;2;2;0;FLOAT4;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode;208;-1782.947,-563.269;Inherit;True;Property;_Mask02;Mask02;2;0;Create;True;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;194;-1781.62,-180.129;Inherit;True;Property;_Texture;Texture;1;0;Create;True;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;176;-1469.143,-237.5255;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;177;-1224.703,-357.322;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;56;-1000.883,-532.1535;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ClampOpNode;162;-974.8826,-355.4704;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;125;-1121.566,-626.3839;Inherit;False;Property;_Emission;Emission;5;0;Create;True;0;0;False;0;False;1;1;0;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;126;-974.1182,-218.5169;Inherit;False;Property;_Opacity;Opacity;4;0;Create;True;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;124;-682.564,-554.5644;Inherit;False;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;146;-494.1428,-181.703;Inherit;False;Property;_CullMode;CullMode;6;1;[Enum];Create;True;0;1;UnityEngine.Rendering.CullMode;True;0;False;0;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;57;-678.8375,-374.9858;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;49;-493.4759,-580.3741;Float;False;True;-1;2;;0;0;Unlit;project-rs/c_UVDistortionDissolve;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Back;2;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Custom;0.5;True;False;0;True;Transparent;;Transparent;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;False;2;5;False;-1;10;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;0;-1;-1;-1;0;False;0;0;True;146;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;15;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;182;0;179;0
WireConnection;182;1;180;0
WireConnection;184;0;181;0
WireConnection;184;1;182;0
WireConnection;185;0;184;0
WireConnection;185;1;183;0
WireConnection;188;1;185;0
WireConnection;189;0;188;0
WireConnection;189;1;187;3
WireConnection;191;0;183;0
WireConnection;191;1;189;0
WireConnection;190;0;187;1
WireConnection;190;1;187;2
WireConnection;192;0;190;0
WireConnection;192;1;191;0
WireConnection;194;1;192;0
WireConnection;176;0;208;1
WireConnection;176;1;194;1
WireConnection;177;0;176;0
WireConnection;177;1;187;4
WireConnection;162;0;177;0
WireConnection;124;0;125;0
WireConnection;124;1;56;0
WireConnection;57;0;56;4
WireConnection;57;1;162;0
WireConnection;57;2;126;0
WireConnection;49;2;124;0
WireConnection;49;9;57;0
ASEEND*/
//CHKSM=67D47E70BCD497C358C745E7920682B0CB25A0CD