// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
//
Shader "project-rs/dissolve_burn"
{
	Properties
	{
		_MaskClipValue("Mask Clip Value", Float) = 0.5
		[HideInInspector] __dirty("", Int) = 1
		//
		_MainTex("BaseMap", 2D) = "white" {}
		_DissolveGuide("Dissolve Guide", 2D) = "white" {}
		_BurnRamp("Burn Ramp", 2D) = "white" {}
		//
		_MixProgress("Dissolve Progress", Range(0, 1)) = 0
	}

	SubShader
	{
		Tags{ "RenderType" = "TransparentCutout"  "Queue" = "AlphaTest+0" }
		Cull Off
		ZTest LEqual

		CGPROGRAM
		#pragma target 3.0
		#pragma surface surf Standard addshadow fullforwardshadows

		struct Input
		{
			float2 uv_MainTex;
			float2 uv_DissolveGuide;
		};

		uniform sampler2D _MainTex;
		uniform sampler2D _DissolveGuide;
		uniform sampler2D _BurnRamp;

		uniform float _MixProgress;
		uniform float _MaskClipValue = 0.5;

		void surf (Input input, inout SurfaceOutputStandard output)
		{
			output.Albedo = tex2D(_MainTex, input.uv_MainTex).xyz;
			float temp_output_73_0 = ( (-0.6 + (( 1.0 - _MixProgress) - 0.0) * (0.6 - -0.6) / (1.0 - 0.0)) + tex2D(_DissolveGuide, input.uv_DissolveGuide).r );
			float temp_output_130_0 = ( 1.0 - clamp( (-4.0 + (temp_output_73_0 - 0.0) * (4.0 - -4.0) / (1.0 - 0.0)) , 0.0 , 1.0 ) );
			output.Emission = ( temp_output_130_0 * tex2D(_BurnRamp, float2(temp_output_130_0, 0)) ).xyz;
			clip( temp_output_73_0 - _MaskClipValue );
		}

		ENDCG
	}
	Fallback "Diffuse"
}