Shader "Hole_X_Ray/Hole_X_Ray_ToonLit"
{
	Properties
	{
		_Color("Main Color", Color) = (0.5,0.5,0.5,1)
		_MainTex("Base (RGB)", 2D) = "white" {}
		_Ramp("Toon Ramp (RGB)", 2D) = "gray" {}
	}

	SubShader
	{
		Tags {"RenderType" = "Transparent" "Queue" = "Transparent"}
		Blend SrcAlpha OneMinusSrcAlpha

		LOD 200

		CGPROGRAM
		#pragma surface surf ToonRamp keepalpha

		float4 _Color;
		sampler2D _MainTex;
		sampler2D _Ramp;

		float3 _Camera_Pos;
		float3 _Target_Pos;

		#pragma lighting ToonRamp exclude_path:prepass
		inline half4 LightingToonRamp(SurfaceOutput s, half3 lightDir, half atten)
		{
			#ifndef USING_DIRECTIONAL_LIGHT
			lightDir = normalize(lightDir);
			#endif

			half d = dot(s.Normal, lightDir)*0.5 + 0.5;
			half3 ramp = tex2D(_Ramp, float2(d,d)).rgb;

			half4 c;
			c.rgb = s.Albedo * _LightColor0.rgb * ramp * (atten * 2);
			c.a = s.Alpha;
			return c;
		}

		struct Input
		{
			float2 uv_MainTex : TEXCOORD0;
			float3 worldPos;
		};

		void surf(Input IN, inout SurfaceOutput o)
		{
			//
			half4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;

			// 카메라 와 캐릭터 사이에 있는지 판단..
			float3 targetToCamera = _Camera_Pos - _Target_Pos;
			float3 targetToObj = IN.worldPos.xyz - _Target_Pos;
			float resultDot = dot(normalize(targetToCamera), normalize(targetToObj));

			// 거리 계산..
			float distance = sqrt(dot(targetToObj, targetToObj)) * sin(acos(resultDot));

			// 오브젝트와 카메라 거리에 의한 크기 조정..
			float alpha = lerp(1.8, 0.5, clamp(abs(targetToCamera.z - targetToObj.z) / 12, 0, 1));
			alpha = clamp((distance * alpha), 0, 1);

			//
			o.Alpha = (0 < resultDot) ? alpha : c.a;
		}
		ENDCG
	}

	Fallback "Diffuse"
}
