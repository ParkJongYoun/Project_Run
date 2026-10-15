Shader "Hole_X_Ray/Hole_X_Ray_ToonBasic"
{
	Properties
	{
		_Color("Main Color", Color) = (.5,.5,.5,1)
		_MainTex("Base (RGB)", 2D) = "white" {}
		_ToonShade("ToonShader Cubemap(RGB)", CUBE) = "" { }
	}

	SubShader
	{
		Tags {"RenderType" = "Transparent" "Queue" = "Transparent"}
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			Name "X_Ray_BASE"

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			samplerCUBE _ToonShade;
			float4 _MainTex_ST;
			float4 _Color;

			float3 _Camera_Pos;
			float3 _Target_Pos;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 texcoord : TEXCOORD0;
				float3 cubenormal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				UNITY_FOG_COORDS(3)
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.cubenormal = UnityObjectToViewPos(v.normal); //o.cubenormal = mul(UNITY_MATRIX_MV, float4(v.normal,0));
				o.worldPos = mul(unity_ObjectToWorld, v.vertex);
				UNITY_TRANSFER_FOG(o,o.pos);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed4 col = _Color * tex2D(_MainTex, i.texcoord);

				// 카메라 와 캐릭터 사이에 있는지 판단..
				float3 targetToCamera = _Camera_Pos - _Target_Pos;
				float3 targetToObj = i.worldPos - _Target_Pos;
				float resultDot = dot(normalize(targetToCamera), normalize(targetToObj));

				// 거리 계산..
				float distance = sqrt(dot(targetToObj, targetToObj)) * sin(acos(resultDot));

				// 오브젝트와 카메라 거리에 의한 크기 조정..
				float alpha = lerp(1.8, 0.5, clamp(abs(targetToCamera.z - targetToObj.z) / 12, 0, 1));
				alpha = clamp((distance * alpha), 0, 1);
				col.a = (0 < resultDot) ? alpha : col.a;

				//
				fixed4 cube = texCUBE(_ToonShade, i.cubenormal);
				fixed4 c = fixed4(2.0f * cube.rgb * col.rgb, col.a);
				UNITY_APPLY_FOG(i.fogCoord, c);
				return c;
			}
			ENDCG
		}
	}

	Fallback "VertexLit"
}