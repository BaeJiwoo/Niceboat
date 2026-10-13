Shader "NiceBoat/Water/Ripple"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; half2 uv:TEXCOORD0; half3 detail:TEXCOORD1; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; half2 uv:TEXCOORD0; half3 detail:TEXCOORD1; half4 color:COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS);
                o.uv=v.uv*2.0h-1.0h;
                o.color=v.color;
                o.detail=v.detail;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half2 p=i.uv;
                half variation=sin(p.y*21.0h+i.detail.y*29.0h)+sin(p.x*31.0h-p.y*9.0h+i.detail.y*11.0h);
                half broken=smoothstep(-0.25h,0.45h,variation);
                half aa=max(fwidth(p.x),0.012h);
                half wing=abs(abs(p.x)-(0.15h+(1.0h-p.y)*0.34h+variation*0.025h));
                half wake=(1.0h-smoothstep(0.035h,0.035h+aa,wing))*broken;
                half churn=(1.0h-smoothstep(0.08h,0.3h,abs(p.x)))*smoothstep(0.1h,0.8h,variation)*0.32h;
                half ends=smoothstep(-1.0h,-0.55h,p.y)*(1.0h-smoothstep(0.55h,1.0h,p.y));
                wake=(wake+churn)*ends;
                half r=length(p);
                half ring=(1.0h-smoothstep(0.018h,0.018h+aa,abs(r-0.7h)))*broken*0.65h;
                half bow=(1.0h-smoothstep(0.035h,0.035h+aa,abs(r-0.76h)))*smoothstep(-0.2h,0.25h,p.y)*broken;
                half mask=lerp(ring,wake,step(0.5h,i.detail.x));
                mask=lerp(mask,bow,step(1.5h,i.detail.x));
                return half4(i.color.rgb,i.color.a*mask);
            }
            ENDHLSL
        }
    }
}
