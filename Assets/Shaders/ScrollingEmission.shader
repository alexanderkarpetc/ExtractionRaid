Shader "ExtractionRaid/Scrolling Emission"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("Base Texture (RGB) Alpha (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Texture Tint", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 1

        [Header(Emission)]
        _EmissionMap("Emission Map (RGB)", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (1, 1, 1, 1)
        _EmissionIntensity("Emission Intensity", Range(0, 20)) = 1
        _EmissionScrollSpeed("Emission Scroll Speed (X Y)", Vector) = (0, 0, 0, 0)
        _EmissionBlend("Emission Blend", Range(0, 1)) = 1

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Culling", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _EmissionMap_ST;
                float4 _EmissionScrollSpeed;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionIntensity;
                half _EmissionBlend;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                // Independent emission tiling/offset plus UV units per second.
                float2 emissionUV = TRANSFORM_TEX(input.uv, _EmissionMap)
                    + _EmissionScrollSpeed.xy * _Time.y;
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
                half3 baseColor = baseSample.rgb * _BaseColor.rgb;
                half alpha = saturate(baseSample.a * _BaseColor.a * _Opacity);
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, emissionUV).rgb
                    * _EmissionColor.rgb * _EmissionIntensity;
                // Additive emission preserves the base texture when the emission map is black.
                half3 color = baseColor + emission * saturate(_EmissionBlend);
                half fogFactor = InitializeInputDataFog(float4(input.positionWS, 1), 0);
                // Fade both base and emission; zero alpha leaves no residual glow.
                return half4(MixFog(color, fogFactor) * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
