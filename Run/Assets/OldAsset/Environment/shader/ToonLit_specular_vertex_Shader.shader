Shader "Toon/Lit Specular Vertex" {
    Properties{
       
        [Header(Main)]
        _Color("Main Color", Color) = (1,1,1,1)
        _MainTex("Base (RGB)", 2D) = "white" {}
        _Ramp("Toon Ramp (RGB)", 2D) = "gray" {}
        [Space]
        [Header(Specular)]
        _SColor("Specular Color", Color) = (1,1,1,1)
        _RampS("Specular Ramp (RGB)", 2D) = "gray" {} // specular ramp, cutoff point
        _SpecSize("Specular Size", Range(0.65,0.999)) = 0.9 // specular size
        _SpecOffset("Specular Offset", Range(0,1)) = 0.5 // specular offset of the spec Ramp
        [Space]
         [Header(Rim Light)]
        _RimColor("Fresnel Rim Color", Color) = (0.49,0.94,0.64,1)
        _RimPower("Rim Power", Range(0,20)) = 3.2
    }
 
        SubShader{
        Tags{ "RenderType" = "Opaque" }
        LOD 200
 
        CGPROGRAM
#pragma surface surf ToonRamp vertex:vert
        sampler2D _Ramp;
 
    // custom lighting function that uses a texture ramp based
    // on angle between light direction and normal
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
        c.a = 0;
        return c;
    }
 
    sampler2D _MainTex;
    float4 _Color, _SColor, _RimColor;
    sampler2D _RampS; // specular ramp
    float _SpecSize; // specular size
    float _SpecOffset; // offset specular ramp
    float _RimPower; // gradient bottom offset
 
    struct Input {
        float2 uv_MainTex : TEXCOORD0;
        float3 lightDir;
        float3 viewDir;
        float4 vertexColor: COLOR; // vertex color
    };
 
    void vert(inout appdata_full v, out Input o)
    {
        UNITY_INITIALIZE_OUTPUT(Input, o);
        o.lightDir = WorldSpaceLightDir(v.vertex); // get the worldspace lighting direction
    }
 
    void surf(Input IN, inout SurfaceOutput o) {
     
        half4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color; // main texture
        half d = dot(o.Normal, IN.lightDir + IN.viewDir) + _SpecOffset; // basing on normal and light direction
        half3 rampS = tex2D(_RampS, float2(d, d)).rgb; // specular ramp
        float rim = 1 - saturate(dot(IN.viewDir, o.Normal)); // calculate fresnel rim
        o.Emission = _RimColor.rgb * pow(rim, _RimPower) * IN.vertexColor.r; // fresnel rim
        float3 spec = ((step(_SpecSize, rampS.r)) * rampS * d* _SColor) * IN.vertexColor.r; // specular
        o.Albedo = c.rgb + spec; // multiply color by gradient lerp
        o.Alpha = c.a;
    }
    ENDCG
 
    }
 
        Fallback "Diffuse"
}