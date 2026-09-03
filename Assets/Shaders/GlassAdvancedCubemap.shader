Shader "ExtractionRaid/Glass Advanced Cubemap"
{
    Properties
    {
        [Header(Glass)]
        [HDR] _GlassColor("Glass Color", Color) = (0.65, 0.85, 1.0, 1.0)
        _Opacity("Opacity", Range(0.0, 1.0)) = 0.12

        [Header(Reflection)]
        [NoScaleOffset] _ReflectionCube("Reflection Cubemap", Cube) = "black" {}
        [HDR] _ReflectionColor("Reflection Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _ReflectionStrength("Reflection Strength", Range(0.0, 3.0)) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.9
        _CubemapRotation("Cubemap Rotation", Range(0.0, 360.0)) = 0.0

        [Header(Fresnel)]
        _ReflectionAtNormal("Reflection Facing Camera", Range(0.0, 1.0)) = 0.3
        _FresnelPower("Fresnel Power", Range(0.5, 10.0)) = 5.0

        [Header(Surface)]
        [Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0.0, 2.0)) = 0.25
        [NoScaleOffset] _MaskMap("Mask (R Opacity, G Reflection, B Smoothness)", 2D) = "white" {}
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
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                float2 uv : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            TEXTURECUBE(_ReflectionCube);
            SAMPLER(sampler_ReflectionCube);

            CBUFFER_START(UnityPerMaterial)
                half4 _GlassColor;
                half4 _ReflectionColor;
                half _Opacity;
                half _ReflectionStrength;
                half _Smoothness;
                half _CubemapRotation;
                half _ReflectionAtNormal;
                half _FresnelPower;
                half _NormalStrength;
                float4 _NormalMap_ST;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half3 RotateAroundY(half3 direction, half degrees)
            {
                half angle = radians(degrees);
                half sine;
                half cosine;
                sincos(angle, sine, cosine);
                half2 rotatedXZ = half2(
                    direction.x * cosine - direction.z * sine,
                    direction.x * sine + direction.z * cosine);
                return half3(rotatedXZ.x, direction.y, rotatedXZ.y);
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                float2 normalUV = input.uv * _NormalMap_ST.xy + _NormalMap_ST.zw;
                half3 normalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV),
                    _NormalStrength);

                half3x3 tangentToWorld = half3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS));
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));
                half faceSign = IS_FRONT_VFACE(isFrontFace, 1.0h, -1.0h);
                normalWS *= faceSign;

                half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 reflectionDirectionWS = reflect(-viewDirectionWS, normalWS);
                reflectionDirectionWS = RotateAroundY(reflectionDirectionWS, _CubemapRotation);

                half smoothness = saturate(_Smoothness * mask.b);
                half reflectionMip = (1.0h - smoothness) * 6.0h;
                half3 reflection = SAMPLE_TEXTURECUBE_LOD(
                    _ReflectionCube,
                    sampler_ReflectionCube,
                    reflectionDirectionWS,
                    reflectionMip).rgb;

                half viewDotNormal = saturate(dot(normalWS, viewDirectionWS));
                half fresnel = pow(1.0h - viewDotNormal, _FresnelPower);
                half reflectionShape = lerp(_ReflectionAtNormal, 1.0h, fresnel);
                half reflectionAmount = _ReflectionStrength * reflectionShape * mask.g;

                half alpha = saturate(_Opacity * _GlassColor.a * mask.r);
                half3 color = _GlassColor.rgb * alpha;
                color += reflection * _ReflectionColor.rgb * reflectionAmount;

                half fogIntensity = ComputeFogIntensity(input.fogFactor);
                color = lerp(unity_FogColor.rgb * alpha, color, fogIntensity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
