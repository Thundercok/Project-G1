Shader "Hidden/G1/CinematicPresentation"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Tint ("Scene Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(-2, 2)) = 0
        _Contrast ("Contrast", Range(0.5, 1.5)) = 1.08
        _Saturation ("Saturation", Range(0, 1.5)) = 0.95
        _Vignette ("Vignette", Range(0, 1)) = 0.28
        _Grain ("Film Grain", Range(0, 0.12)) = 0.018
        _BloomIntensity ("Bloom Intensity", Range(0, 2)) = 0.45
        _BloomThreshold ("Bloom Threshold", Range(0, 2)) = 0.78
        _BloomRadius ("Bloom Radius", Range(0.5, 3)) = 1.35
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.01)) = 0.0012
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Tint;
            float _Exposure;
            float _Contrast;
            float _Saturation;
            float _Vignette;
            float _Grain;
            float _BloomIntensity;
            float _BloomThreshold;
            float _BloomRadius;
            float _ChromaticAberration;

            float Luminance(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            float3 AcesToneMap(float3 c)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c1 = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return saturate((c * (a * c + b)) / (c * (c1 * c + d) + e));
            }

            float3 SampleBloom(float2 uv)
            {
                float2 offset = _MainTex_TexelSize.xy * _BloomRadius;
                float3 sum = tex2D(_MainTex, uv).rgb * 0.16;
                sum += tex2D(_MainTex, uv + float2( offset.x, 0)).rgb * 0.10;
                sum += tex2D(_MainTex, uv + float2(-offset.x, 0)).rgb * 0.10;
                sum += tex2D(_MainTex, uv + float2(0,  offset.y)).rgb * 0.10;
                sum += tex2D(_MainTex, uv + float2(0, -offset.y)).rgb * 0.10;
                sum += tex2D(_MainTex, uv + float2( offset.x,  offset.y)).rgb * 0.11;
                sum += tex2D(_MainTex, uv + float2(-offset.x,  offset.y)).rgb * 0.11;
                sum += tex2D(_MainTex, uv + float2( offset.x, -offset.y)).rgb * 0.11;
                sum += tex2D(_MainTex, uv + float2(-offset.x, -offset.y)).rgb * 0.11;
                return max(sum - _BloomThreshold, 0.0);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;
                float radial = dot(centered, centered);
                float2 aberration = centered * (_ChromaticAberration * (0.35 + radial * 2.4));

                float3 color;
                color.r = tex2D(_MainTex, uv + aberration).r;
                color.g = tex2D(_MainTex, uv).g;
                color.b = tex2D(_MainTex, uv - aberration).b;

                color += SampleBloom(uv) * _BloomIntensity;
                color *= exp2(_Exposure);
                color = AcesToneMap(max(color, 0.0));

                float luma = Luminance(color);
                color = lerp(luma.xxx, color, _Saturation);
                color = (color - 0.5) * _Contrast + 0.5;
                color *= _Tint.rgb;

                float vignette = smoothstep(0.78, 0.15, radial);
                color *= lerp(1.0 - _Vignette, 1.0, vignette);

                float noise = frac(sin(dot(uv * _ScreenParams.xy + _Time.y, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                color += noise * _Grain;
                return float4(saturate(color), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
