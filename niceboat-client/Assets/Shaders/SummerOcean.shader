// URP 17.3 / Unity 6.3. Assign SummerOcean.mat to a subdivided horizontal XZ mesh.
// Enable Depth Texture on the active URP asset/camera. Opaque objects supply depth.
// Expand mesh/renderer bounds vertically by at least 1.5 * Wave Amplitude.
// Keep mesh UV0 in 0..1 and use moderate tiling to retain mobile half-UV precision.
// Contact foam uses eye depth; the optional world map supplies seabed/shore data.
// Five reads (two surface, depth, world mask, interaction), plus optional shadows.
// No refraction or additional lights; waves and scroll stay in the vertex stage.
Shader "NiceBoat/Water/Summer Ocean"
{
    Properties
    {
        [Header(Depth Colors)]
        _ShallowColor ("Shallow Cyan", Color) = (0.10, 0.85, 0.83, 1)
        _DeepColor ("Deep Ocean Blue", Color) = (0.025, 0.32, 0.65, 1)
        _DepthDistance ("Deep Color Distance (Meters)", Range(0.1, 30)) = 6
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
        // B = sparse painted strokes, A = smooth noise for foam and sun breakup.
        [NoScaleOffset] _PackedSurface ("Surface (RG Slopes B Ripples A Noise)", 2D) = "gray" {}
        _NoiseScale ("Surface Tiling (Mesh UV0)", Range(0.1, 8)) = 3
        _WorldUVScale ("World Tiling (0 Uses Mesh UV)", Range(0, 1)) = 0
        _SurfaceScroll ("Surface Scroll Speed (UV Per Second XY)", Vector) = (0.025, 0.018, 0, 0)
        _DetailScale ("Second Layer Scale", Range(0.5, 5)) = 2.37
        _DetailScroll ("Second Layer Scroll XY", Vector) = (-0.009, 0.006, 0, 0)
        _DetailNormalStrength ("Second Layer Normal Strength", Range(0, 0.5)) = 0.045
        _RippleNormalStrength ("Ripple Normal Strength", Range(0, 0.5)) = 0.18
        _RippleColor ("Ripple Color", Color) = (0.52, 0.95, 0.94, 1)
        _RippleThreshold ("Ripple Line Threshold", Range(0.05, 0.99)) = 0.72
        _RippleStrength ("Ripple Line Strength", Range(0, 1)) = 0.24

        [Header(Intersection Foam)]
        _FoamColor ("Foam Color", Color) = (0.96, 1, 0.98, 1)
        _FoamWidth ("Foam Width (Eye Space Meters)", Range(0.01, 2)) = 0.4
        _FoamBreakup ("Foam Edge Breakup", Range(0, 1)) = 0.55

        [Header(Harbor World Map)]
        [NoScaleOffset] _WorldWaterMask ("R Depth G Shore Distance B Exposure A Foam", 2D) = "white" {}
        _WorldWaterBounds ("World Map Min XZ And Inverse Size ZW", Vector) = (-120, -100, 0.004166667, 0.004166667)
        _WorldMaskStrength ("Use Baked Water Map", Range(0, 1)) = 0
        _WorldDepthRange ("Map Depth Range (Meters)", Float) = 12
        _WorldShoreRange ("Map Shore Distance Range (Meters)", Float) = 16
        _ShoreFoamWidth ("Shore Foam Reach (Meters)", Range(0.5, 8)) = 4.5
        _ShoreWaveLength ("Shore Wave Spacing (Meters)", Range(1, 12)) = 5.5
        _ShoreWaveSpeed ("Shore Wave Cycles Per Second", Range(0, 1)) = 0.14
        _ShoreFoamStrength ("Shore Foam Strength", Range(0, 1)) = 0.65

        [Header(Boat Interaction)]
        _WakeFoamStrength ("Wake Foam Strength", Range(0, 1)) = 0.75
        _WakeNormalStrength ("Wake Normal Strength", Range(0, 1)) = 0.18

        [Header(Sky Reflection Tint)]
        _SkyReflectionColor ("Sky Reflection Color", Color) = (0.60, 0.67, 0.59, 1)
        _SkyReflectionStrength ("Sky Reflection Strength", Range(0, 1)) = 0.2

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
            TEXTURE2D(_WorldWaterMask);
            SAMPLER(sampler_WorldWaterMask);
            // Scene-owned field; disabled and bound to black when not running.
            TEXTURE2D(_NiceBoatInteractionMap);
            SAMPLER(sampler_NiceBoatInteractionMap);
            float4 _NiceBoatInteractionBounds;
            half _NiceBoatInteractionEnabled;
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
                half4 _SkyReflectionColor;
                float4 _SurfaceScroll;
                float4 _DetailScroll;
                float4 _WorldWaterBounds;
                float _DepthDistance;
                float _FoamWidth;
                float _WaveAmplitude;
                float _WaveLength;
                float _WaveSpeed;
                float _WorldDepthRange;
                float _WorldShoreRange;
                float _ShoreFoamWidth;
                float _ShoreWaveLength;
                float _ShoreWaveSpeed;
                half _WorldMaskStrength;
                half _ShoreFoamStrength;
                half _DetailScale;
                half _DetailNormalStrength;
                half _WakeFoamStrength;
                half _WakeNormalStrength;
                half _SkyReflectionStrength;
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
                half4 surfaceUV : TEXCOORD2;
                half2 fogAndShorePhase : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                half4 mapUV : TEXCOORD5;
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
                // Rotate the second layer as well as changing scale/speed.
                float2 detailUV = float2(surfaceUV.x * 0.8 - surfaceUV.y * 0.6,
                                        surfaceUV.x * 0.6 + surfaceUV.y * 0.8) * _DetailScale;
                output.surfaceUV = half4((half2)(surfaceUV + scroll),
                                        (half2)(detailUV + frac(oceanTime * _DetailScroll.xy)));
                output.mapUV.xy = (half2)((positionWS.xz - _WorldWaterBounds.xy) * _WorldWaterBounds.zw);
                output.mapUV.zw = (half2)((positionWS.xz - _NiceBoatInteractionBounds.xy) * _NiceBoatInteractionBounds.zw);
                output.fogAndShorePhase = half2(ComputeFogFactor(output.positionCS.z), frac(oceanTime * _ShoreWaveSpeed));
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Screen/depth coordinates require float for stable intersections.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = lerp(LinearEyeDepth(rawDepth, _ZBufferParams),
                                          LinearDepthToEyeDepth(rawDepth), unity_OrthoParams.w);
                float thickness = max(sceneEyeDepth - input.viewAndDepth.w, 0.0);
                half hasScene = (half)step(0.000001, abs(rawDepth - UNITY_RAW_FAR_CLIP_VALUE));
                half depthBlend = (half)saturate(thickness / max(_DepthDistance, 0.001));
                depthBlend = lerp(1.0h, depthBlend, hasScene);
                half opticalBlend = depthBlend;

                half4 region = SAMPLE_TEXTURE2D(_WorldWaterMask, sampler_WorldWaterMask, input.mapUV.xy);
                half mapEdge = min(min(input.mapUV.x, input.mapUV.y), min(1.0h - input.mapUV.x, 1.0h - input.mapUV.y));
                half mapped = smoothstep(0.0h, 0.025h, mapEdge) * _WorldMaskStrength;
                half mappedDepth = (half)saturate(region.r * _WorldDepthRange / max(_DepthDistance, 0.001));
                depthBlend = lerp(depthBlend, smoothstep(0.0h, 1.0h, mappedDepth), mapped);
                half exposure = lerp(1.0h, region.b, mapped);

                half4 surface = SAMPLE_TEXTURE2D(_PackedSurface, sampler_PackedSurface, input.surfaceUV.xy);
                half4 detail = SAMPLE_TEXTURE2D(_PackedSurface, sampler_PackedSurface, input.surfaceUV.zw);
                half2 smallSlope = detail.rg * 2.0h - 1.0h;
                smallSlope = half2(smallSlope.x * 0.8h + smallSlope.y * 0.6h,
                                 -smallSlope.x * 0.6h + smallSlope.y * 0.8h);
                half2 rippleSlope = ((surface.rg * 2.0h - 1.0h) * _RippleNormalStrength
                                    + smallSlope * _DetailNormalStrength) * lerp(0.3h, 1.0h, exposure);
                half4 interaction = SAMPLE_TEXTURE2D(_NiceBoatInteractionMap, sampler_NiceBoatInteractionMap, input.mapUV.zw);
                half wakeEdge = min(min(input.mapUV.z, input.mapUV.w), min(1.0h - input.mapUV.z, 1.0h - input.mapUV.w));
                half hasWake = smoothstep(0.0h, 0.04h, wakeEdge) * _NiceBoatInteractionEnabled;
                rippleSlope += (interaction.ba * 2.0h - 1.0h) * (_WakeNormalStrength * hasWake);
                half3 normalWS = normalize(input.normalWS + half3(rippleSlope.x, 0.0h, rippleSlope.y));
                half3 viewWS = (half3)normalize(input.viewAndDepth.xyz);
                Light sun = GetMainLight();
                #if defined(_WATER_SHADOWS)
                sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                #endif
                half NdotL = saturate(dot(normalWS, sun.direction));

                // Three crisp bands; the unlit band has its own teal color.
                // Main directional light only; no additional-light loops.
                half3 baseColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthBlend);
                half middleBand = step(_ShadowThreshold, NdotL);
                half litBand = step(max(_LightThreshold, _ShadowThreshold), NdotL);
                half3 color = lerp(_ShadowColor.rgb, baseColor * _MidtoneBrightness, middleBand);
                color = lerp(color, baseColor, litBand);
                color *= lerp(half3(1.0h, 1.0h, 1.0h), sun.color, _SunColorInfluence);
                color *= lerp(half3(0.69h,0.77h,0.72h),half3(1.0h,1.0h,1.0h),sun.shadowAttenuation);

                // An art-directed sky tint, without another camera or scene-color copy.
                half facing = 1.0h - saturate(dot(normalWS, viewWS));
                half facing2 = facing * facing;
                half fresnel = 0.04h + 0.96h * facing2;
                color = lerp(color, _SkyReflectionColor.rgb, saturate(fresnel * _SkyReflectionStrength));

                // One-pixel edge antialiasing retains a graphic, cut-paper look.
                half rippleAA = max(fwidth(surface.b), 0.002h);
                half ripple = smoothstep(_RippleThreshold - rippleAA,
                                         _RippleThreshold + rippleAA, surface.b);
                ripple *= smoothstep(0.38h, 0.62h, surface.a);
                color = lerp(color, _RippleColor.rgb, ripple * _RippleStrength * exposure);

                half foamDepth = (half)saturate(thickness / max(_FoamWidth, 0.001));
                half foamEdge = lerp(0.95h, 0.25h + surface.a * 0.7h, _FoamBreakup);
                half foamSignal = foamEdge - foamDepth;
                half foamAA = max(fwidth(foamSignal), 0.002h);
                half foam = smoothstep(-foamAA, foamAA, foamSignal) * hasScene;
                foam *= lerp(1.0h, smoothstep(0.32h, 0.62h, surface.a), _FoamBreakup);

                // Baked distance makes incoming foam follow the actual shoreline.
                half shoreDistance = region.g * (half)_WorldShoreRange;
                half shorePhase = frac(shoreDistance / (half)max(_ShoreWaveLength, 0.01)
                                       + input.fogAndShorePhase.y + (detail.a - 0.5h) * 0.18h);
                half shoreSignal = 0.1h - abs(shorePhase - 0.5h);
                half shoreAA = max(fwidth(shoreSignal), 0.004h);
                half shoreBand = smoothstep(-shoreAA, shoreAA, shoreSignal);
                half shoreReach = 1.0h - smoothstep(0.0h, (half)_ShoreFoamWidth, shoreDistance);
                half foamNoise = surface.a * 0.45h + detail.a * 0.55h;
                half grainAA = max(fwidth(detail.a), 0.005h);
                half foamGrain = smoothstep(0.5h - grainAA, 0.5h + grainAA, detail.a);
                half shoreFoam = shoreBand * shoreReach * foamGrain
                                 * region.a * mapped * _ShoreFoamStrength;
                half wakeSignal = interaction.r - lerp(0.025h, 0.125h, detail.a);
                half wakeAA = max(fwidth(wakeSignal), 0.004h);
                half wakeGrain = smoothstep(0.46h - grainAA, 0.46h + grainAA, detail.a);
                half wakeFoam = smoothstep(-wakeAA, wakeAA, wakeSignal) * saturate(interaction.r * 5.0h) * wakeGrain
                                * hasWake * _WakeFoamStrength;
                foam = max(foam, max(shoreFoam, wakeFoam));

                // Thresholding N.H replaces an expensive high-exponent pow().
                half3 halfDirection = SafeNormalize(sun.direction + viewWS);
                half NdotH = saturate(dot(normalWS, halfDirection));
                half specularAA = max(fwidth(NdotH), 0.0005h);
                half specular = smoothstep(_SpecularThreshold - specularAA,
                                          _SpecularThreshold + specularAA, NdotH);
                specular *= step(0.001h, NdotL);
                specular *= lerp(1.0h, smoothstep(0.48h, 0.65h, foamNoise), _SpecularBreakup);
                specular *= lerp(1.0h, ripple, _SpecularSurfaceMask) * sun.shadowAttenuation;
                color += _SpecularColor.rgb * sun.color * (_SpecularIntensity * specular);
                color = lerp(color, _FoamColor.rgb, foam);

                half alpha = lerp(_ShallowOpacity, _DeepOpacity, opticalBlend);
                alpha = lerp(alpha, 1.0h, max(foam, specular * saturate(_SpecularIntensity)));
                return half4(MixFog(color, input.fogAndShorePhase.x), alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
