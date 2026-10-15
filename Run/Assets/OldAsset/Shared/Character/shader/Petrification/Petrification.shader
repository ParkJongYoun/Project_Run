//
// http://blog.naver.com/PostView.nhn?blogId=plasticbag0&logNo=221753685229&redirect=Dlog&widgetTypeCall=true&directAccess=false
//
Shader "project-rs/petrification"
{
    Properties
    {
        _MainTex ("BaseMap", 2D) = "white" {}
		_StoneTex ("Stone Texture", 2D) = "white" {}
		_MaskTex ("Mask Texture", 2D) = "white" {}
		//
		_MixProgress("Stone Progress", Range(0, 1)) = 0
		_MixValue("Stone Rate", Range(0, 1)) = 0.5
		_Thickness("Stone thickness", float) = 0.01
    }

    SubShader
    {
		Tags { "RenderType"="Opaque" }
        LOD 100

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
		sampler2D _MaskTex;
		sampler2D _StoneTex;

		float _MixProgress;
		float _MixValue;
		float _Thickness;

        struct Input
        {
            float2 uv_MainTex;
			float2 uv_StoneTex;
			float2 uv_MaskTex;
        };

		void vert(inout appdata_full v)
		{
			float m = tex2Dlod(_MaskTex, v.texcoord).r;
			v.vertex.xyz += v.normal * _Thickness *((m <= (_MixProgress)) ? 1 : 0);
		}


        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float4 c = tex2D (_MainTex, IN.uv_MainTex);
			float3 stone = tex2D(_StoneTex, IN.uv_StoneTex);
			float3 mixStone = lerp((c.r + c.g + c.b)*0.3, stone, _MixValue);
			float mask = tex2D(_MaskTex, IN.uv_MaskTex).r;
            o.Albedo = lerp(c, mixStone, (mask <= (_MixProgress)) ? 1 : 0);
            o.Alpha = c.a;
        }
        ENDCG
    }
}
