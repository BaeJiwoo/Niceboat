// URP 17.3 / Unity 6.3. Assign SummerOcean.mat to a subdivided horizontal XZ mesh.
// Enable Depth Texture on the active URP asset/camera. Opaque objects supply depth.
// Expand mesh/renderer bounds vertically by at least 1.5 * Wave Amplitude.
// Keep mesh UV0 in 0..1 and use moderate tiling to retain mobile half-UV precision.
// Depth and foam widths measure view-space thickness, not vertical seabed depth.
// Two surface/depth reads, plus an optional main-light shadow map.
// No refraction or additional lights; waves and scroll stay in the vertex stage.
Shader "NiceBoat/Water/Summer Ocean"
{
    Properties
    {
        [Header(Depth Colors)]
        _ShallowColor ("Shallow Cyan", Color) = (0.10, 0.85, 0.83, 1)
        _DeepColor ("Deep Ocean Blue", Color) = (0.025, 0.32, 0.65, 1)
        _DepthDistance ("Deep Color Distance (Eye Space Meters)", Range(0.1, 30)) = 6
        _ShallowOpacity ("Shallow Opacity", Range(0, 1)) = 0.8
        _DeepOpacity ("Deep Opacity", Range(0, 1)) = 1

        [Header(Cel Lighting)]
        _ShadowColor ("Teal Shadow Color", Color) = (0.025, 0.24, 0.34, 1)
        _ShadowThreshold ("Shadow Band Threshold", Range(0, 1)) = 0.45
        _LightThreshold ("Sunlit Band Threshold", Range(0, 1)) = 0.8
        _MidtoneBrightness ("Midtone Brightness", Range(0, 1)) = 0.78
        _SunColorInfluence ("Sun Color Influence", Range(0, 1)) = 0.35
        [Toggle(_WATER_SHADOWS)] _ReceiveWaterShadows ("Receive Main Light Shadows", Float) = 0

        [Header(Packed Surface Detail)]
        // Linear RGBA: RG = signed XZ normal offsets encoded as 0..1,
        // B = bright Voronoi edges, A = smooth noise for foam and sun breakup.
        [NoScaleOffset] _PackedSurface ("Surface (RG Slopes B Ripples A Noise)", 2D) = "gray" {}
        _NoiseScale ("Surface Tiling (Mesh UV0)", Range(0.1, 8)) = 3
        _WorldUVScale ("World Tiling (0 Uses Mesh UV)", Range(0, 1)) = 0
        _SurfaceScroll ("Surface Scroll Speed (UV Per Second XY)", Vector) = (0.025, 0.018, 0, 0)
        _RippleNormalStrength ("Ripple Normal Strength", Range(0, 0.5)) = 0.18
        _RippleColor ("Ripple Color", Color) = (0.52, 0.95, 0.94, 1)
        _RippleThreshold ("Ripple Line Threshold", Range(0.05, 0.99)) = 0.72
        _RippleStrength ("Ripple Line Strength", Range(0, 1)) = 0.24

        [Header(Intersection Foam)]
        _FoamColor ("Foam Color", Color) = (0.96, 1, 0.98, 1)
        _FoamWidth ("Foam Width (Eye Space Meters)", Range(0.01, 2)) = 0.4
        _FoamBreakup ("Foam Edge Breakup", Range(0, 1)) = 0.55

        [Header(Gentle Swells)]
        _WaveAmplitude ("Wave Amplitude (World Meters)", Range(0, 1)) = 0.18
        _WaveLength ("Primary Wavelength (World Meters)", Range(0.5, 40)) = 10
        _WaveSpeed ("Wave Speed (Radians Per Second)", Range(0, 5)) = 1.1

        [Header(Sun Reflection)]
        [HDR] _SpecularColor ("Sun Reflection Color", Color) = (1, 0.98, 0.85, 1)
        _SpecularThreshold ("Sun Reflection Threshold", Range(0.8, 0.999)) = 0.995
        _SpecularIntensity ("Sun Reflection Intensity", Range(0, 4)) = 1.2
        _SpecularBreakup ("Sun Reflection Breakup", Range(0, 1)) = 0.85
        _SpecularSurfaceMask ("Limit Sun To Surface Strokes", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "SummerOceanForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _WATER_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_PackedSurface);
            SAMPLER(sampler_PackedSurface);
            // A scene's OceanSurface supplies the same clock used by buoyancy.
            float _NiceBoatOceanTime;
            float _NiceBoatUseOceanTime;

            // A single material buffer preserves SRP Batcher compatibility.
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _ShadowColor;
                half4 _RippleColor;
                half4 _FoamColor;
                half4 _SpecularColor;
                float4 _SurfaceScroll;
                float _DepthDistance;
                float _FoamWidth;
                float _WaveAmplitude;
                float _WaveLength;
                float _WaveSpeed;
                half _ShallowOpacity;
                half _DeepOpacity;
                half _ShadowThreshold;
                half _LightThreshold;
                half _MidtoneBrightness;
                half _SunColorInfluence;
                half _ReceiveWaterShadows;
                half _NoiseScale;
                half _WorldUVScale;
                half _RippleNormalStrength;
                half _RippleThreshold;
                half _RippleStrength;
                half _FoamBreakup;
                half _SpecularThreshold;
                half _SpecularIntensity;
                half _SpecularBreakup;
                half _SpecularSurfaceMask;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                // XYZ = unnormalized camera vector; W = displaced eye depth.
                // float prevents overflow on distant ocean vertices.
                float4 viewAndDepth : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half2 surfaceUV : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(Attributes input)
            {
                v2f output = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Use an up-facing, horizontal XZ mesh with UV0 in 0..1.
                // Two world-space waves keep swell motion consistent across tiles.
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                const float2 directionA = float2(0.8, 0.6);
                const float2 directionB = float2(-0.6, 0.8);
                float waveNumber = TWO_PI / max(_WaveLength, 0.001);
                float2 frequencies = waveNumber * float2(1.0, 1.61);
                float2 phase = float2(dot(positionWS.xz, directionA),
                                      dot(positionWS.xz, directionB)) * frequencies;
                float oceanTime = lerp(_Time.y, _NiceBoatOceanTime, _NiceBoatUseOceanTime);
                phase += oceanTime * _WaveSpeed * float2(1.0, 1.23);

                float2 sine;
                float2 cosine;
                sincos(phase, sine, cosine);
                float2 amplitudes = _WaveAmplitude * float2(1.0, 0.5);
                positionWS.y += dot(sine, amplitudes);

                // Analytic height derivatives: no extra texture or neighbor reads.
                float2 slopes = cosine * amplitudes * frequencies;
                float2 gradient = slopes.x * directionA + slopes.y * directionB;
                output.normalWS = (half3)normalize(float3(-gradient.x, 1.0, -gradient.y));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;

                // Orthographic cameras have parallel view rays.
                output.viewAndDepth.xyz = lerp(GetCameraPositionWS() - positionWS,
                                              -GetViewForwardDir(), unity_OrthoParams.w);
                output.viewAndDepth.w = -TransformWorldToView(positionWS).z;

                // Wrap only the uniform time offset. Wrapping per-vertex UVs
                // would introduce interpolation seams across triangles.
                float2 scroll = frac(oceanTime * _SurfaceScroll.xy);
                float2 surfaceUV = lerp(input.uv * _NoiseScale, positionWS.xz * _WorldUVScale,
                                       step(0.0001h, _WorldUVScale));
                output.surfaceUV = (half2)(surfaceUV + scroll);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Two texture reads; the optional shadow variant adds a shadow-map read.
                // Screen/depth coordinates require float for stable intersections.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = lerp(LinearEyeDepth(rawDepth, _ZBufferParams),
                                          LinearDepthToEyeDepth(rawDepth), unity_OrthoParams.w);
                float thickness = max(sceneEyeDepth - input.viewAndDepth.w, 0.0);
                half hasScene = (half)step(0.000001, abs(rawDepth - UNITY_RAW_FAR_CLIP_VALUE));
                half depthBlend = (half)saturate(thickness / max(_DepthDistance, 0.001));
                depthBlend = lerp(1.0h, depthBlend, hasScene);

                half4 surface = SAMPLE_TEXTURE2D(_PackedSurface, sampler_PackedSurface, input.surfaceUV);
                half2 rippleSlope = (surface.rg * 2.0h - 1.0h) * _RippleNormalStrength;
                half3 normalWS = normalize(input.normalWS + half3(rippleSlope.x, 0.0h, rippleSlope.y));
                half3 viewWS = (half3)normalize(input.viewAndDepth.xyz);
                Light sun = GetMainLight();
                #if defined(_WATER_SHADOWS)
                sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #endif
                half NdotL = saturate(dot(normalWS, sun.direction));

                // Three crisp bands; the unlit band has its own teal color.
                // Main directional light only: no additional-light loops or shadow-map reads.
                half3 baseColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthBlend);
                half middleBand = step(_ShadowThreshold, NdotL);
                half litBand = step(max(_LightThreshold, _ShadowThreshold), NdotL);
                half3 color = lerp(_ShadowColor.rgb, baseColor * _MidtoneBrightness, middleBand);
                color = lerp(color, baseColor, litBand);
                color *= lerp(half3(1.0h, 1.0h, 1.0h), sun.color, _SunColorInfluence);
                color *= lerp(half3(0.69h,0.77h,0.72h),half3(1.0h,1.0h,1.0h),sun.shadowAttenuation);

                // One-pixel edge antialiasing retains a graphic, cut-paper look.
                half rippleAA = max(fwidth(surface.b), 0.002h);
                half ripple = smoothstep(_RippleThreshold - rippleAA,
                                         _RippleThreshold + rippleAA, surface.b);
                ripple *= smoothstep(0.38h, 0.62h, surface.a);
                color = lerp(color, _RippleColor.rgb, ripple * _RippleStrength);

                half foamDepth = (half)saturate(thickness / max(_FoamWidth, 0.001));
                half foamEdge = lerp(0.95h, 0.25h + surface.a * 0.7h, _FoamBreakup);
                half foamSignal = foamEdge - foamDepth;
                half foamAA = max(fwidth(foamSignal), 0.002h);
                half foam = smoothstep(-foamAA, foamAA, foamSignal) * hasScene;
                foam *= lerp(1.0h, smoothstep(0.32h, 0.62h, surface.a), _FoamBreakup);

                // Thresholding N.H replaces an expensive high-exponent pow().
                half3 halfDirection = SafeNormalize(sun.direction + viewWS);
                half NdotH = saturate(dot(normalWS, halfDirection));
                half specularAA = max(fwidth(NdotH), 0.0005h);
                half specular = smoothstep(_SpecularThreshold - specularAA,
                                          _SpecularThreshold + specularAA, NdotH);
                specular *= step(0.001h, NdotL);
                specular *= lerp(1.0h, step(0.55h, surface.a), _SpecularBreakup);
                specular *= lerp(1.0h, ripple, _SpecularSurfaceMask) * sun.shadowAttenuation;
                color += _SpecularColor.rgb * sun.color * (_SpecularIntensity * specular);
                color = lerp(color, _FoamColor.rgb, foam);

                half alpha = lerp(_ShallowOpacity, _DeepOpacity, depthBlend);
                alpha = lerp(alpha, 1.0h, max(foam, specular * saturate(_SpecularIntensity)));
                return half4(MixFog(color, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
