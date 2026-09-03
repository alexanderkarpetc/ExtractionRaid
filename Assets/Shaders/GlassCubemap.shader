Shader "ExtractionRaid/Glass Cubemap"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (0.7, 0.9, 1.0, 1.0)
        _Opacity("Opacity", Range(0.0, 1.0)) = 0.15
        [NoScaleOffset] _ReflectionCube("Reflection Cubemap", Cube) = "black" {}
        _ReflectionStrength("Reflection Strength", Range(0.0, 2.0)) = 1.0
        _Mask("Mask", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);
            TEXTURECUBE(_ReflectionCube);
            SAMPLER(sampler_ReflectionCube);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                half _ReflectionStrength;
                float4 _Mask_ST;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = input.uv * _Mask_ST.xy + _Mask_ST.zw;
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, input.uv).r;
                half faceSign = IS_FRONT_VFACE(isFrontFace, 1.0h, -1.0h);
                half3 normalWS = normalize(input.normalWS) * faceSign;
                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 reflectionDirectionWS = reflect(-viewDirectionWS, normalWS);

                half3 reflection = SAMPLE_TEXTURECUBE(
                    _ReflectionCube,
                    sampler_ReflectionCube,
                    reflectionDirectionWS).rgb;

                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirectionWS)), 5.0h);
                half reflectionAmount = _ReflectionStrength * lerp(0.35h, 1.0h, fresnel) * mask;
                half alpha = saturate(_Opacity * _Color.a * mask);

                // Premultiplied transparency keeps reflections visible on clear glass.
                half3 color = _Color.rgb * alpha;
                color += reflection * _Color.rgb * reflectionAmount;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
