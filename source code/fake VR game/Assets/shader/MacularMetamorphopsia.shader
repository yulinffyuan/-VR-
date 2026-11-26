Shader "Hidden/Custom/MacularMetamorphopsia"
{
    Properties
    {
        _Intensity ("Overall Intensity", Range(0, 1)) = 1.0
        _Aspect ("Quad Aspect Ratio", Float) = 1.5
        _DistortRadius ("Macular Region Radius", Range(0.05, 0.5)) = 0.22
        _DistortStrength ("Metamorphopsia Strength", Range(0, 0.1)) = 0.025
        _WaveFrequency ("Grid Wave Frequency", Float) = 28.0
        _WaveSpeed ("Subtle Fluctuation Speed", Float) = 1.2
        _BlurAmount ("Central Blur Radius", Range(0, 0.015)) = 0.004
        _ScotomaRadius ("Central Scotoma Radius", Range(0, 0.3)) = 0.08
        _ScotomaColor ("Scotoma Tint & Opacity", Color) = (0.12, 0.12, 0.13, 0.65)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest Always // 確保靠近牆壁時局部扭曲遮罩不會被牆面吃掉

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D_X(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            float _Intensity;
            float _Aspect;
            float _DistortRadius;
            float _DistortStrength;
            float _WaveFrequency;
            float _WaveSpeed;
            float _BlurAmount;
            float _ScotomaRadius;
            half4 _ScotomaColor;

            // 簡易二維雜訊，打破生硬的正圓邊界
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                if (_Intensity <= 0.001) discard;

                // 以 Quad 中心為黃斑部中央凹 (0, 0)，並校正長寬比為正圓
                float2 p = i.uv - 0.5;
                p.x *= _Aspect;

                float rawDist = length(p);
                float organicOffset = (valueNoise(p * 12.0 + 3.1) - 0.5) * 0.035;
                float dist = rawDist + organicOffset;

                // 超出黃斑部半徑的區域完全不處理，保持周邊視野 100% 筆直清晰
                if (dist > _DistortRadius * 1.15) discard;

                // 1. 計算局部權重（中心最強，邊緣平滑衰減為 0）
                float regionMask = 1.0 - smoothstep(_DistortRadius * 0.25, _DistortRadius, dist);
                float activeWeight = regionMask * _Intensity;

                // 2. 模擬視物變形 (Metamorphopsia)：局部波浪彎曲 + 中心向內拉扯凹陷
                float2 wave;
                wave.x = sin(p.y * _WaveFrequency + _Time.y * _WaveSpeed) * cos(p.x * _WaveFrequency * 0.65);
                wave.y = cos(p.x * _WaveFrequency - _Time.y * _WaveSpeed * 0.8) * sin(p.y * _WaveFrequency * 0.65);

                float2 dirToCenter = -p / max(rawDist, 0.001);
                float2 radialPinch = dirToCenter * sin(saturate(rawDist / _DistortRadius) * 3.14159) * 0.6;

                float2 totalOffset = (wave * 0.6 + radialPinch) * _DistortStrength * activeWeight;

                // 取得螢幕空間 UV 並加上局部扭曲偏移
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float2 warpedUV = clamp(screenUV + totalOffset, 0.001, 0.999);

                // 3. 局部中心敏銳度下降（9 點採樣微模糊，僅在中央區塊生效）
                float blurRadius = _BlurAmount * activeWeight;
                half3 col = SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, warpedUV).rgb;

                if (blurRadius > 0.0001)
                {
                    half3 blurSum = col * 2.0;
                    float2 offsets[8] = {
                        float2(-1, -1), float2(0, -1), float2(1, -1),
                        float2(-1,  0),                float2(1,  0),
                        float2(-1,  1), float2(0,  1), float2(1,  1)
                    };
                    for (int k = 0; k < 8; k++)
                    {
                        float2 sampleUV = clamp(warpedUV + offsets[k] * blurRadius, 0.001, 0.999);
                        blurSum += SAMPLE_TEXTURE2D_X(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, sampleUV).rgb;
                    }
                    col = blurSum / 10.0;
                }

                // 4. 局部中心暗點/對比下降 (Central Scotoma，可透過 _ScotomaColor.a 調整深淺)
                float scotomaMask = 1.0 - smoothstep(_ScotomaRadius * 0.35, max(_ScotomaRadius, 0.001), dist);
                float scotomaAlpha = scotomaMask * _Intensity * _ScotomaColor.a;
                col = lerp(col, _ScotomaColor.rgb, scotomaAlpha);

                // 邊緣完全透明無縫銜接周邊正常畫面
                float edgeAlpha = 1.0 - smoothstep(_DistortRadius * 0.85, _DistortRadius * 1.1, dist);
                return half4(col, edgeAlpha);
            }
            ENDHLSL
        }
    }
}