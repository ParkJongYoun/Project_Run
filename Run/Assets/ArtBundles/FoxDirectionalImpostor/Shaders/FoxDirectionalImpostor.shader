Shader "ProjectRun/FoxDirectionalImpostor"
{
    Properties { _ColorAtlas("Color",2D)="white"{} _DepthAtlas("Depth RG16",2D)="black"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ColorAtlas); SAMPLER(sampler_ColorAtlas);
            TEXTURE2D(_DepthAtlas); SAMPLER(sampler_DepthAtlas);
            float4 _ViewRight[4],_ViewUp[4],_ViewDirection[4],_Tiles[4],_Weights,_CaptureCenter;
            float4x4 _WorldToCapture,_CaptureToWorld;
            float _Radius,_TileSize,_UseDepth;
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float2 uv:TEXCOORD1; };
            struct O { float4 color:SV_Target; float depth:SV_Depth; };
            V Vert(A a) { V o; o.positionWS=TransformObjectToWorld(a.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.uv=a.uv; return o; }
            float2 Project(float3 p,int k) { return float2(dot(p,_ViewRight[k].xyz),dot(p,_ViewUp[k].xyz))/(_Radius*2)+0.5; }
            float2 Atlas(float2 uv,int k) { return (_Tiles[k].xy+clamp(uv,0.5/_TileSize,1-0.5/_TileSize))*_Tiles[k].zw; }
            float4 ReadDepth(float2 uv,int k) { return SAMPLE_TEXTURE2D_LOD(_DepthAtlas,sampler_DepthAtlas,Atlas(uv,k),0); }
            float Decode(float4 d) { return ((d.r*65280+d.g*255)/65535-0.5)*2*_Radius; }
            float Surface(float3 p,int k,out float valid)
            {
                float2 uv=Project(p,k); float4 d=ReadDepth(uv,k);
                valid=d.a*step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                return dot(p,_ViewDirection[k].xyz)-Decode(d);
            }
            float4 Trace(float3 origin,float3 ray,float start,float end,int k,out float hit)
            {
                hit=end+_Radius*4;
                float stepSize=(end-start)/32;
                float previous=start;
                [loop] for(int j=0;j<=32;j++)
                {
                    float t=start+j*stepSize,valid;
                    float f=Surface(origin+ray*t,k,valid);
                    if(valid>0.85 && f<=0)
                    {
                        float lo=previous,hi=t;
                        [unroll] for(int b=0;b<5;b++)
                        {
                            float mid=(lo+hi)*0.5,v;
                            float e=Surface(origin+ray*mid,k,v);
                            if(v>0.85 && e<=0) hi=mid; else lo=mid;
                        }
                        float3 p=origin+ray*hi; float v;
                        float residual=abs(Surface(p,k,v));
                        // Reject the extruded sides of a depth silhouette: another view supplies that surface.
                        if(residual<_Radius*0.018 && v>0.85)
                        {
                            float4 c=SAMPLE_TEXTURE2D_LOD(_ColorAtlas,sampler_ColorAtlas,Atlas(Project(p,k),k),0);
                            if(c.a>0.2) { hit=hi; return c; }
                        }
                    }
                    previous=t;
                }
                return 0;
            }
            O Frag(V i)
            {
                O o;
                float3 origin=mul(_WorldToCapture,float4(_WorldSpaceCameraPos,1)).xyz-_CaptureCenter.xyz;
                float3 plane=mul(_WorldToCapture,float4(i.positionWS,1)).xyz-_CaptureCenter.xyz;
                float3 ray=normalize(plane-origin);
                if(_UseDepth<0.5)
                {
                    int selected=0; [unroll] for(int n=1;n<4;n++) if(_Weights[n]>_Weights[selected]) selected=n;
                    o.color=SAMPLE_TEXTURE2D_LOD(_ColorAtlas,sampler_ColorAtlas,Atlas(i.uv,selected),0);
                    clip(o.color.a-0.3); o.color.a=1; o.depth=i.positionCS.z; return o;
                }
                float along=dot(origin,ray), discriminant=along*along-dot(origin,origin)+_Radius*_Radius;
                clip(discriminant);
                float extent=sqrt(max(0,discriminant));
                float start=max(0,-along-extent),end=-along+extent;
                float4 colors[4]; float hits[4]; float nearest=end+_Radius*4;
                [unroll] for(int k=0;k<4;k++)
                {
                    colors[k]=0; hits[k]=end+_Radius*4;
                    if(_Weights[k]>0.0001) colors[k]=Trace(origin,ray,start,end,k,hits[k]);
                    if(colors[k].a>0.2) nearest=min(nearest,hits[k]);
                }
                float4 color=0; float total=0,positionT=0;
                [unroll] for(int blend=0;blend<4;blend++)
                {
                    float w=_Weights[blend]*step(0.2,colors[blend].a)*saturate(1-abs(hits[blend]-nearest)/(_Radius*0.075));
                    color+=colors[blend]*w; positionT+=hits[blend]*w; total+=w;
                }
                clip(total-0.00001);
                float3 capturePosition=origin+ray*(positionT/total)+_CaptureCenter.xyz;
                float3 worldPosition=mul(_CaptureToWorld,float4(capturePosition,1)).xyz;
                float4 h=TransformWorldToHClip(worldPosition);
                o.depth=h.z/h.w;
                #if !UNITY_REVERSED_Z
                    o.depth=(o.depth-UNITY_NEAR_CLIP_VALUE)/(1-UNITY_NEAR_CLIP_VALUE);
                #endif
                o.color=float4(color.rgb/total,1); return o;
            }
            ENDHLSL
        }
    }
}
