Shader "Hidden/Custom/ReversePhi"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        Cull Off ZWrite Off ZTest Always
        
        Pass
        {
            Name "ReversePhi"
            
            HLSLPROGRAM
            // 【關鍵修復】：直接使用 Unity 內建的全螢幕頂點著色器 Vert
            #pragma vertex Vert
            #pragma fragment frag
            
            // 引入 Unity 6 後處理必備的函式庫
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Blit.hlsl 已經幫我們宣告好 _BlitTexture 跟 sampler_LinearClamp，所以不需重新宣告
            TEXTURE2D(_HistoryTex);
            SAMPLER(sampler_HistoryTex);
            
            float4 _OffsetVector;
            float _Blend;

            // 輸入參數改為 Blit.hlsl 內建的 Varyings
            half4 frag (Varyings input) : SV_Target 
            {
                // 1. 讀取當前畫面 (使用 input.texcoord)
                half4 current = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // 2. 讀取歷史畫面 (加入位移)
                half2 historyUV = input.texcoord + _OffsetVector.xy;
                half4 history = SAMPLE_TEXTURE2D(_HistoryTex, sampler_HistoryTex, historyUV);
                
                // 3. 核心錯覺：反相處理 (負片)
                history.rgb = 1.0 - history.rgb;
                
                // 4. 疊加混合輸出
                return lerp(current, history, _Blend);
            }
            ENDHLSL
        }
    }
}