// Broad lighting planes and per-face vertex tint; no albedo/normal textures.
Shader "NiceBoat/Harbor/Painted Lit"
{
    Properties
    {
        _BaseColor ("Paint Color", Color) = (0.7, 0.6, 0.4, 1)
        _ShadowTint ("Cool Shadow Tint", Color) = (0.57, 0.64, 0.60, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowTint;
            half _Cull;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            half3 normalOS : NORMAL;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tintAndFog : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings PaintedVertex(Attributes v)
        {
            Varyings o = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v,o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.tintAndFog = half4(v.color.rgb, ComputeFogFactor(o.positionCS.z));
            return o;
        }
        ENDHLSL
        Pass
        {
            Name "PaintedForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PaintedVertex
            #pragma fragment PaintedFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            half4 PaintedFragment(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half n = dot(normalize(i.normalWS), sun.direction);
                half band = 0.24h + 0.43h * step(0.12h,n) + 0.33h * step(0.65h,n);
                half illumination = band * lerp(0.23h,1.0h,sun.shadowAttenuation);
                half3 shade = lerp(_ShadowTint.rgb, half3(1.07h,1.03h,0.93h), illumination);
                half3 color = _BaseColor.rgb * i.tintAndFog.rgb * shade;
                return half4(MixFog(color,i.tintAndFog.a),1.0h);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            float3 _LightDirection;
            float4 ShadowVertex(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                half3 n = TransformObjectToWorldNormal(v.normalOS);
                float4 clip = TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z = min(clip.z,UNITY_NEAR_CLIP_VALUE * clip.w);
                #else
                clip.z = max(clip.z,UNITY_NEAR_CLIP_VALUE * clip.w);
                #endif
                return clip;
            }
            half4 DepthFragment() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PaintedVertex
            #pragma fragment NormalFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            half4 NormalFragment(Varyings i) : SV_Target
            {
                half3 normalWS=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct=PackNormalOctQuadEncode(normalWS);
                return half4(PackFloat2To888(saturate(oct*0.5+0.5)),0);
                #else
                return half4(normalWS,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex PaintedVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }
    FallBack Off
}
