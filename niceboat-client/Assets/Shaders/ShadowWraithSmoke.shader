Shader "NiceBoat/Wraith/Smoke"
{
    Properties
    {
        _Noise ("Packed cloud noise", 2D) = "white" {}
        _Color ("Smoke tint", Color) = (0.018,0.024,0.029,1)
        _Opacity ("Density", Range(0,1)) = 0.8
        _FlowSpeed ("Noise flow", Float) = 0.12
        _SoftDistance ("Intersection softness", Range(0.01,2)) = 0.45
        [Toggle(_SOFTPARTICLES_ON)] _SoftParticles ("Soft particles", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _SOFTPARTICLES_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity, _FlowSpeed, _SoftDistance, _SoftParticles;
            CBUFFER_END
            // UV.xy + AgePercent in UV.z, configured explicitly on the particle renderer.
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float3 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; half2 uv:TEXCOORD0; half3 noiseAge:TEXCOORD1; float eyeDepth:TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.eyeDepth = -TransformWorldToView(world).z;
                o.uv = v.uv.xy * 2.0h - 1.0h;
                o.noiseAge = half3(v.uv.xy * 0.8h + half2(0.13h,-0.21h) * (_Time.y * _FlowSpeed),v.uv.z);
                o.color = v.color;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half2 noise = SAMPLE_TEXTURE2D(_Noise,sampler_Noise,i.noiseAge.xy).rg;
                half outline = (1.0h-dot(i.uv,i.uv)) * 1.35h + (noise.r-0.5h) * 1.8h;
                half dissolve = smoothstep(i.noiseAge.z * 0.52h - 0.08h,i.noiseAge.z * 0.52h + 0.2h,noise.g);
                half alpha = smoothstep(0.0h,0.65h,outline) * dissolve * i.color.a * _Opacity;
                #if defined(_SOFTPARTICLES_ON)
                    float depth = SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                    float perspective = LinearEyeDepth(depth,_ZBufferParams);
                    #if UNITY_REVERSED_Z
                        depth = 1.0-depth;
                    #endif
                    float orthographic = lerp(_ProjectionParams.y,_ProjectionParams.z,depth);
                    float sceneEye = lerp(perspective,orthographic,unity_OrthoParams.w);
                    alpha *= saturate((sceneEye-i.eyeDepth) / max(_SoftDistance,0.01h));
                #endif
                return half4(_Color.rgb * i.color.rgb * lerp(0.65h,1.7h,noise.r),alpha);
            }
            ENDHLSL
        }
    }
}
