Shader "NiceBoat/Wraith/Ground Shadow"
{
    Properties { _Color ("Shadow", Color) = (0.01,0.02,0.025,0.3) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; half2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half2 uv:TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv*2.0h-1.0h; return o; }
            half4 frag(Varyings i):SV_Target { return half4(_Color.rgb,_Color.a*(1.0h-smoothstep(0.05h,1.0h,dot(i.uv,i.uv)))); }
            ENDHLSL
        }
    }
}
