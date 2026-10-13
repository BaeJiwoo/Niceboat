Shader "NiceBoat/Wraith/Eye Glow"
{
    Properties
    {
        [HDR] _Color ("Eye color", Color) = (1,0.012,0.003,1)
        _Intensity ("Emission", Range(0,12)) = 3
        _SoftEdge ("Halo falloff", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+40" }
        Blend One One
        ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity, _SoftEdge;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; half2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half2 uv:TEXCOORD0; };
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv*2.0h-1.0h; return o; }
            half4 frag(Varyings i):SV_Target
            {
                half halo = saturate(1.0h-dot(i.uv,i.uv)); halo *= halo * halo;
                return half4(_Color.rgb * _Intensity * lerp(1.0h,halo,_SoftEdge),0);
            }
            ENDHLSL
        }
    }
}
