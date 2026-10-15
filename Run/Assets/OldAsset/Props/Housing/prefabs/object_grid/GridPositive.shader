Shader "Custom/GridPositive"
{
	Properties
	{
		_IlluminCol("Self-Illumination color (RGB)", Color) = (1,1,1,1)
		_Color("Main Color", Color) = (1,1,1,0)
		_MainTex("Base (RGB)", 2D) = "white" {}
	}

		SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off
			//Blend SrcAlpha OneMinusSrcAlpha// Additive
			Blend One One
			// Set up basic vertex lighting
			Material
			{
				Diffuse[_Color]
				Ambient[_Color]
				Shininess[_Shininess]
				Specular[_SpecColor]
				Emission[_Emission]
			}
			Lighting On

			// Use texture alpha to blend up to white (= full illumination)
			SetTexture[_MainTex]
			{
				constantColor[_IlluminCol]
				combine constant lerp(texture) previous
			}
			// Multiply in texture
			SetTexture[_MainTex]
			{
				combine previous * texture
			}
		}
	}
}