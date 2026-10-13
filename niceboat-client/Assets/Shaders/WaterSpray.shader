Shader "NiceBoat/Water/Spray"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
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
            struct Attributes { float3 positionOS:POSITION; half2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; half2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(v.positionOS);
                o.uv=v.uv*2.0h-1.0h;
                o.color=v.color;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half alpha=1.0h-smoothstep(0.4h,1.0h,dot(i.uv,i.uv));
                return half4(i.color.rgb,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
