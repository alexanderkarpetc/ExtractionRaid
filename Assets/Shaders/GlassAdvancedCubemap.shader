Shader "ExtractionRaid/Glass Advanced Cubemap"
{
    Properties
    {
        [Header(Glass)]
        [MainTexture] _BaseMap("Glass Texture (RGB)", 2D) = "white" {}
        [MainColor] _GlassColor("Glass Color", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 0.5
        _MaskMap("Opacity Mask (White Visible - Black Transparent)", 2D) = "white" {}

        [Header(Reflection)]
        [NoScaleOffset] _ReflectionCube("Reflection Cubemap", Cube) = "black" {}
        [HDR] _ReflectionColor("Reflection Color", Color) = (1, 1, 1, 1)
        _ReflectionStrength("Reflection Strength", Range(0, 3)) = 0.5
        _CubemapRotation("Cubemap Rotation", Range(0, 360)) = 0

        [Header(Fresnel)]
        _ReflectionAtNormal("Reflection Facing Camera", Range(0, 1)) = 0.1
        _FresnelPower("Fresnel Power", Range(0.5, 10)) = 5

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Culling", Float) = 0
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
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            TEXTURECUBE(_ReflectionCube);
            SAMPLER(sampler_ReflectionCube);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MaskMap_ST;
                half4 _GlassColor;
                half4 _ReflectionColor;
                half _Opacity;
                half _ReflectionStrength;
                float _CubemapRotation;
                half _ReflectionAtNormal;
                half _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
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
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Base texture alpha is ignored; the grayscale mask controls coverage.
                half3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap,
                    TRANSFORM_TEX(input.uv, _BaseMap)).rgb;
                half mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap,
                    TRANSFORM_TEX(input.uv, _MaskMap)).r;
                half alpha = saturate(_Opacity * _GlassColor.a * mask);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                // Absolute dot gives matching Fresnel on both sides of a glass pane.
                half facing = saturate(abs(dot(normalWS, viewWS)));
                half fresnel = pow(1.0h - facing, max(_FresnelPower, 0.5h));
                half reflectionWeight = saturate(_ReflectionStrength
                    * lerp(saturate(_ReflectionAtNormal), 1.0h, fresnel));

                float3 reflectionDirection = reflect(-viewWS, normalWS);
                float sine, cosine;
                sincos(radians(_CubemapRotation), sine, cosine);
                reflectionDirection.xz = float2(
                    reflectionDirection.x * cosine - reflectionDirection.z * sine,
                    reflectionDirection.x * sine + reflectionDirection.z * cosine);
                half3 reflection = SAMPLE_TEXTURECUBE(_ReflectionCube,
                    sampler_ReflectionCube, reflectionDirection).rgb * _ReflectionColor.rgb;

                // Keep the base texture at every angle and apply glass tint to the whole surface.
                half3 color = (baseColor + reflection * reflectionWeight) * _GlassColor.rgb;
                half fogFactor = InitializeInputDataFog(float4(input.positionWS, 1.0), 0.0h);
                // MixFog preserves color when fog is disabled. Premultiply exactly once afterwards.
                color = MixFog(color, fogFactor);
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
