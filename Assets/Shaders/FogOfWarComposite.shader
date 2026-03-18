Shader "FogOfWar/Composite"
{
    Properties
    {
        _MainTex ("Scene Color", 2D) = "white" {}
        // _FoWBlurred is set globally via cmd.SetGlobalTexture — NOT per-material.
        // Keeping it out of Properties ensures the global value is always used.
        _FogColor ("Fog Color", Color) = (0.02, 0.02, 0.05, 1)
        _FogIntensity ("Fog Intensity", Range(0, 1)) = 0.85
        _DesaturationAmount ("Desaturation", Range(0, 1)) = 0.7
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    TEXTURE2D(_FoWBlurred);
    SAMPLER(sampler_FoWBlurred);

    float4 _FogColor;
    float _FogIntensity;
    float _DesaturationAmount;

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
        float3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
        float visibility = SAMPLE_TEXTURE2D(_FoWBlurred, sampler_FoWBlurred, input.uv).r;

        // 1. Desaturate proportionally to fog coverage
        float fogFactor = (1.0 - visibility) * _FogIntensity;
        float luminance = dot(scene, float3(0.299, 0.587, 0.114));
        float3 gray = float3(luminance, luminance, luminance);
        float3 desaturated = lerp(scene, gray, _DesaturationAmount * fogFactor);

        // 2. Darken toward fog color
        float3 fogged = lerp(desaturated, _FogColor.rgb, fogFactor);

        return float4(fogged, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "FoWComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
