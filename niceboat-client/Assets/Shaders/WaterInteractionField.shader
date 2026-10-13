// RG = foam / disturbance. BA = encoded world XZ disturbance slopes.
// A local, fixed-resolution visual field; buoyancy still uses OceanSurface waves.
Shader "NiceBoat/Water/Interaction Field"
{
    Properties
    {
        [HideInInspector] _MainTex ("Previous Interaction History", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;
        CBUFFER_START(UnityPerMaterial)
            float4 _FieldBounds;
            float4 _PreviousOffset;
            half4 _Evolution; // foam decay, disturbance decay, diffusion, slope scale
        CBUFFER_END
        half Inside(float2 uv)
        {
            return (half)(step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0));
        }
        half2 ReadField(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rg * Inside(uv);
        }
        ENDHLSL

        Pass
        {
            Name "AdvectAndDecay"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv + _PreviousOffset.xy;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 dx = float2(_MainTex_TexelSize.x, 0.0);
                float2 dy = float2(0.0, _MainTex_TexelSize.y);
                half2 center = ReadField(input.uv);
                half2 west = ReadField(input.uv - dx), east = ReadField(input.uv + dx);
                half2 south = ReadField(input.uv - dy), north = ReadField(input.uv + dy);
                half2 spread = (west + east + south + north) * 0.25h;
                // Linear tail removal prevents immortal low values in the 8-bit history.
                half2 value = max(0.0h, lerp(center, spread, _Evolution.z) * _Evolution.xy - (half2)_PreviousOffset.zw);
                half2 slope = half2(east.g - west.g, north.g - south.g) * _Evolution.w;
                return half4(saturate(value), saturate(0.5h + slope * 0.5h));
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepositTrail"
            Blend One One
            ColorMask RG
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes
            {
                float3 positionOS : POSITION; // World XZ positions, independent of camera.
                half2 uv : TEXCOORD0; // Across/along distance in meters.
                half2 capsule : TEXCOORD1; // Half segment length / inverse radius.
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half2 uv : TEXCOORD0;
                half2 capsule : TEXCOORD1;
                half4 color : COLOR;
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                float2 uv = (input.positionOS.xz - _FieldBounds.xy) * _FieldBounds.zw;
                // Direct clip coordinates: consistent texture orientation on D3D/GLES/Vulkan.
                float2 clip = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    clip.y = -clip.y;
                #endif
                output.positionCS = float4(clip, 0.0, 1.0);
                output.uv = input.uv;
                output.capsule = input.capsule;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half2 delta = half2(input.uv.x, max(abs(input.uv.y) - input.capsule.x, 0.0h));
                half distance = length(delta) * input.capsule.y;
                half body = 1.0h - smoothstep(0.1h, 1.0h, distance);
                return half4(input.color.rg * body, 0.0h, 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
