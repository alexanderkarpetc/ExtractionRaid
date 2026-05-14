Shader "Hidden/ExtractionRaid/InteractableOutlineComposite"
{
    Properties
    {
        _MainTex ("Scene Color", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0.2, 0.95, 1, 1)
        _Thickness ("Thickness", Range(1, 8)) = 3
        _Opacity ("Opacity", Range(0, 1)) = 0.9
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    TEXTURE2D(_OutlineMask);
    SAMPLER(sampler_OutlineMask);

    float4 _OutlineColor;
    float _Thickness;
    float _Opacity;

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

    half4 Frag(Varyings input) : SV_Target
    {
        float2 texel = _Thickness / _ScreenParams.xy;
        float center = SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv).r;

        float edge = 0;
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv + float2(texel.x, 0)).r);
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv + float2(-texel.x, 0)).r);
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv + float2(0, texel.y)).r);
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv + float2(0, -texel.y)).r);
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv + texel).r);
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv - texel).r);
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv + float2(texel.x, -texel.y)).r);
        edge = max(edge, SAMPLE_TEXTURE2D(_OutlineMask, sampler_OutlineMask, input.uv + float2(-texel.x, texel.y)).r);

        float outline = saturate(edge - center) * _Opacity;
        float3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
        float3 color = lerp(scene, _OutlineColor.rgb, outline * _OutlineColor.a);
        return half4(color, 1);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "InteractableOutlineComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
