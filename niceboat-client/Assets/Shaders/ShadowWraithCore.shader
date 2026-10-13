Shader "NiceBoat/Wraith/Core"
{
    Properties
    {
        _BaseColor ("Charcoal", Color) = (0.035,0.045,0.055,1)
        _Noise ("Packed cloud noise", 2D) = "white" {}
        _Dissolve ("Dissolve", Range(0,1.1)) = 0
        _MotionAmount ("Surface motion", Range(0,0.3)) = 0.11
        _MotionSpeed ("Surface speed", Float) = 0.7
        _Phase ("Phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Dissolve, _MotionAmount, _MotionSpeed, _Phase;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION; half3 normalOS:NORMAL; half4 color:COLOR; };
        struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; half2 noiseUV:TEXCOORD1; half4 color:COLOR; };
        Varyings vert(Attributes v)
        {
            Varyings o;
            float t = _Time.y * _MotionSpeed + _Phase;
            float3 p = v.positionOS.xyz;
            // Position-based displacement keeps duplicated flat-shaded vertices welded.
            p += sin(p.yzx * 3.1 + p.zxy * 1.7 + t * float3(0.7,1.1,0.9)) * _MotionAmount;
            o.positionCS = TransformObjectToHClip(p);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.noiseUV = v.positionOS.xz * 0.43h + v.positionOS.y * half2(0.17h,0.23h) + _Phase + t * half2(0.017h,-0.024h);
            o.color = v.color;
            return o;
        }
        half cloud(Varyings i)
        {
            half n = SAMPLE_TEXTURE2D(_Noise,sampler_Noise,i.noiseUV).r;
            clip(n - max(_Dissolve,1.0h-i.color.a));
            return n;
        }
        ENDHLSL
        Pass
        {
            Name "WraithCore"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            half4 frag(Varyings i):SV_Target
            {
                half n = cloud(i);
                Light sun = GetMainLight();
                half lightBand = 0.62h + 0.22h * step(0.1h,dot(normalize(i.normalWS),sun.direction));
                half shade = lightBand + n * 0.32h;
                return half4(_BaseColor.rgb * i.color.rgb * shade,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment depth
            half4 depth(Varyings i):SV_Target { cloud(i); return 0; }
            ENDHLSL
        }
    }
}
