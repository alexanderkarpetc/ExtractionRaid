Shader "FogOfWar/Blur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 1.0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    float4 _MainTex_TexelSize;
    float _BlurSize;

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.uv;
        return output;
    }

    // 9-tap Gaussian weights (sigma ~1.5, normalized)
    static const float weights[5] = { 0.2270270, 0.1945946, 0.1216216, 0.0540541, 0.0162162 };

    float4 BlurSample(float2 uv, float2 direction)
    {
        float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * weights[0];

        for (int i = 1; i < 5; i++)
        {
            float2 offset = direction * _BlurSize * i;
            color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset) * weights[i];
            color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - offset) * weights[i];
        }

        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        // Pass 0: Horizontal blur
        Pass
        {
            Name "BlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragH

            float4 FragH(Varyings input) : SV_Target
            {
                float2 dir = float2(_MainTex_TexelSize.x, 0.0);
                return BlurSample(input.uv, dir);
            }
            ENDHLSL
        }

        // Pass 1: Vertical blur
        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragV

            float4 FragV(Varyings input) : SV_Target
            {
                float2 dir = float2(0.0, _MainTex_TexelSize.y);
                return BlurSample(input.uv, dir);
            }
            ENDHLSL
        }
    }
}
