Shader "FogOfWar/TemporalBlend"
{
    Properties
    {
        _MainTex ("Current", 2D) = "white" {}
        // _PrevTex is set via Material.SetTexture (real RT, not TextureHandle)
        _BlendFactor ("Blend Factor", Range(0.05, 1)) = 0.3
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    TEXTURE2D(_PrevTex);
    SAMPLER(sampler_PrevTex);

    float _BlendFactor;

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

    float4 Frag(Varyings input) : SV_Target
    {
        float current = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;
        float prev = SAMPLE_TEXTURE2D(_PrevTex, sampler_PrevTex, input.uv).r;

        // BlendFactor = how much of the current frame to use
        // 1.0 = no temporal (only current), 0.05 = heavy smoothing (mostly prev)
        float result = lerp(prev, current, _BlendFactor);

        return float4(result, result, result, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "FoWTemporalBlend"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
