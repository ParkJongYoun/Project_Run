// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "furn_self_ill"
{
	Properties
	{
		_MainTex("Texture 0", 2D) = "white" {}
		_1st("1st", Range( -1 , 2)) = 0.6
		_2nd("2nd", Range( -1 , 5)) = 3.3
		_3rd("3rd", Range( -1 , 3)) = 2.02
		_texcoord( "", 2D ) = "white" {}
		__dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
		};

		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float _1st;
		uniform float _2nd;
		uniform float _3rd;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uvTexture0 = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 temp_cast_0 = (_1st).xxxx;
			float4 temp_output_11_0 = pow( tex2D(_MainTex, uvTexture0 ) , temp_cast_0 );
			float luminance17 = Luminance(temp_output_11_0.rgb);
			float temp_output_1_0_g1 = luminance17;
			o.Emission = ( temp_output_11_0 * ( temp_output_11_0 + ( ( 0.0 - temp_output_1_0_g1 ) / ( _2nd - temp_output_1_0_g1 ) ) ) * _3rd ).rgb;
			o.Alpha = 1;
		}

		ENDCG
	}
	Fallback "Diffuse"

}
/*ASEBEGIN
Version=18703
3557;16;1738;977;1019.66;127.689;1;True;True
Node;AmplifyShaderEditor.TexturePropertyNode;10;-1517.254,-89.05696;Inherit;True;Property;Texture0;Texture 0;0;0;Create;False;0;0;False;0;False;None;c8587ced9b83548419bbf0f3c66dab6d;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SamplerNode;1;-1152.127,-87.77297;Inherit;True;Property;_TextureSample0;Texture Sample 0;2;0;Create;True;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;16;-674.0908,-1.022507;Inherit;False;Property;_1st;1st;1;0;Create;True;0;0;False;0;False;0.97;0.6;-1;2;0;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;11;-302.254,-91.0128;Inherit;False;False;2;0;COLOR;0,0,0,0;False;1;FLOAT;1;False;1;COLOR;0
Node;AmplifyShaderEditor.LuminanceNode;17;-132.5897,218.9126;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;30;-261.874,492.375;Inherit;False;Property;_2nd;2nd;2;0;Create;True;0;0;False;0;False;0.97;3.3;-1;5;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;25;59.30899,218.0937;Inherit;False;Inverse Lerp;-1;;1;09cbe79402f023141a4dc1fddd4c9511;0;3;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;3;389.2311,73.02672;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;34;405.6062,247.568;Inherit;False;Property;_3rd;3rd;3;0;Create;True;0;0;False;0;False;0.97;2.02;-1;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldSpaceLightDirHlpNode;7;-2778.811,-339.285;Inherit;False;False;1;0;FLOAT;0;False;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;23;691.3083,-103.5069;Inherit;False;3;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;21;1154.3,-153.5;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;furn_self_ill;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;False;-1;0;False;-1;False;0;False;-1;0;False;-1;False;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;14;all;True;True;True;True;0;False;-1;False;0;False;-1;255;False;-1;255;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;-1;False;2;15;10;25;False;0.5;True;0;0;False;-1;0;False;-1;0;0;False;-1;0;False;-1;0;False;-1;0;False;-1;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;-1;-1;0;False;-1;0;0;0;False;0.1;False;-1;0;False;-1;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1;0;10;0
WireConnection;11;0;1;0
WireConnection;11;1;16;0
WireConnection;17;0;11;0
WireConnection;25;1;17;0
WireConnection;25;2;30;0
WireConnection;3;0;11;0
WireConnection;3;1;25;0
WireConnection;23;0;11;0
WireConnection;23;1;3;0
WireConnection;23;2;34;0
WireConnection;21;2;23;0
ASEEND*/
//CHKSM=F47B89CA9C7A3D9A96345C4E335F23C797B8A549