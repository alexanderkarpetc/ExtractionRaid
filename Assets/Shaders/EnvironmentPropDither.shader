Shader "ExtractShaders/EnvironmentPropDither"
{
    Properties
    {
        [MainTexture] _Texture("Texture", 2D) = "white" {}
        [MainColor] _Color2("Color", Color) = (1, 1, 1, 1)
        _ColorStreng("Color Strength", Float) = 0
        _Saturate("Saturate", Float) = 0

        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.35
        _Occlusion("Occlusion", Range(0, 1)) = 1

        [HDR] _ColorEmission("Emission Color", Color) = (1, 1, 1, 0)
        _Emission("Emission", Float) = 0

        Dither("Dither", Range(0, 1)) = 0
        _Dither("Dither Fallback", Range(0, 1)) = 0
        _DitherPatternScale("Dither Pattern Scale", Range(0, 0.03)) = 0.003

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        LOD 250
        Cull [_Cull]
        ZWrite On
        ZTest LEqual
        Blend One Zero
        AlphaToMask Off

        HLSLINCLUDE
        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        TEXTURE2D(_Texture);
        SAMPLER(sampler_Texture);

        CBUFFER_START(UnityPerMaterial)
            float4 _Texture_ST;
            half4 _Color2;
            half4 _ColorEmission;
            half _ColorStreng;
            half _Saturate;
            half _Metallic;
            half _Smoothness;
            half _Occlusion;
            half _Emission;
            half Dither;
            half _Dither;
            float _DitherPatternScale;
        CBUFFER_END

        float3 _LightDirection;
        float3 _LightPosition;
        float4 _PlayerFoliageDitherPosition;
        float4 _PlayerFoliageDitherParams;
        float4 _CursorFoliageDitherPosition;
        float4 _CursorFoliageDitherParams;

        half ScreenDither4x4(float2 positionSS)
        {
            float screenScale = max(min(_ScreenParams.x, _ScreenParams.y), 1.0);
            float cellSize = max(_DitherPatternScale * screenScale, 1.0);
            uint2 pixel = (uint2)floor(positionSS / cellSize) & 3;
            uint lowBits = (((pixel.x & 1u) ^ (pixel.y & 1u)) << 1) | (pixel.y & 1u);
            uint highBits = ((((pixel.x >> 1) & 1u) ^ ((pixel.y >> 1) & 1u)) << 1) | ((pixel.y >> 1) & 1u);
            return (half)(lowBits * 4u + highBits) * 0.0625h + 0.03125h;
        }

        half MaterialDither()
        {
            return saturate(max(Dither, _Dither));
        }

        half PlayerZoneDither(float4 positionCS)
        {
            half radius = max((half)_PlayerFoliageDitherParams.x, 0.0h);
            half softness = max((half)_PlayerFoliageDitherParams.y, 0.01h);
            half amount = saturate((half)_PlayerFoliageDitherParams.z);
            float4 playerCS = TransformWorldToHClip(_PlayerFoliageDitherPosition.xyz);
            float4 playerScreen = ComputeScreenPos(playerCS);
            float2 playerPixel = playerScreen.xy / max(playerScreen.w, 0.0001) * _ScreenParams.xy;
            half dist = (half)distance(positionCS.xy, playerPixel);

            return (1.0h - smoothstep(radius, radius + softness, dist)) * amount;
        }

        half CursorZoneDither(float4 positionCS)
        {
            half radius = max((half)_CursorFoliageDitherParams.x, 0.0h);
            half softness = max((half)_CursorFoliageDitherParams.y, 0.01h);
            half amount = saturate((half)_CursorFoliageDitherParams.z);
            half dist = (half)distance(positionCS.xy, _CursorFoliageDitherPosition.xy);

            return (1.0h - smoothstep(radius, radius + softness, dist)) * amount;
        }

        void ClipDither(float4 positionCS, half dither)
        {
            if (dither <= 0.001h)
                return;

            clip(ScreenDither4x4(positionCS.xy) - dither);
        }

        half3 ApplyColorControls(half3 albedo)
        {
            half luminance = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
            half saturation = max(_Saturate + 1.0h, 0.0h);
            half3 saturated = lerp(luminance.xxx, albedo, saturation);
            return lerp(saturated, saturated * _Color2.rgb, _ColorStreng);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            half3 normalOS : NORMAL;
            half4 tangentOS : TANGENT;
            float2 texcoord : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            float4 shadowCoord : TEXCOORD4;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings LitVertex(Attributes input)
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
            output.uv = TRANSFORM_TEX(input.texcoord, _Texture);
            output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = GetShadowCoord(positionInputs);
            #endif
            return output;
        }

        half4 LitFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half dither = saturate(max(max(MaterialDither(), PlayerZoneDither(input.positionCS)), CursorZoneDither(input.positionCS)));
            ClipDither(input.positionCS, dither);

            half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            half3 albedo = ApplyColorControls(SAMPLE_TEXTURE2D(_Texture, sampler_Texture, input.uv).rgb);

            SurfaceData surfaceData = (SurfaceData)0;
            surfaceData.albedo = albedo;
            surfaceData.alpha = 1.0h;
            surfaceData.metallic = _Metallic;
            surfaceData.specular = 0;
            surfaceData.smoothness = _Smoothness;
            surfaceData.normalTS = half3(0, 0, 1);
            surfaceData.occlusion = _Occlusion;
            surfaceData.emission = _ColorEmission.rgb * _Emission;
            surfaceData.clearCoatMask = 0;
            surfaceData.clearCoatSmoothness = 0;

            InputData inputData = (InputData)0;
            inputData.positionWS = input.positionWS;
            inputData.positionCS = input.positionCS;
            inputData.normalWS = normalWS;
            inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            inputData.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
            inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
            inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
            inputData.fogCoord = input.fogFactor;
            inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
            inputData.bakedGI = SampleSH(normalWS);
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            inputData.shadowMask = half4(1, 1, 1, 1);

            half4 color = UniversalFragmentPBR(inputData, surfaceData);
            color.rgb = MixFog(color.rgb, inputData.fogCoord);
            color.a = 1.0h;
            return color;
        }

        struct DepthVaryings
        {
            float4 positionCS : SV_POSITION;
            half3 normalWS : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        DepthVaryings DepthVertex(Attributes input)
        {
            DepthVaryings output = (DepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = positionInputs.positionCS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }

        DepthVaryings ShadowVertex(Attributes input)
        {
            DepthVaryings output = (DepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
            float3 lightDirectionWS = _LightDirection;
            #endif

            output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            output.positionCS = ApplyShadowClamping(output.positionCS);
            output.normalWS = normalWS;
            return output;
        }

        half4 DepthFragment(DepthVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            ClipDither(input.positionCS, MaterialDither());
            return 0;
        }

        half4 DepthNormalsFragment(DepthVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            ClipDither(input.positionCS, MaterialDither());

            float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                return half4(packedNormalWS, 0.0h);
            #else
                return half4(normalWS, 0.0h);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            ColorMask RGBA
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex LitVertex
            #pragma fragment LitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            ZTest LEqual
            ColorMask RGBA
            AlphaToMask Off

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
