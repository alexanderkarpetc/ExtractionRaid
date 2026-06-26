Shader "ExtractShaders/BushWindCutout"
{
    Properties
    {
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        _WindStrength("Wind Strength", Range(0, 1)) = 0.08
        _WindSpeed("Wind Speed", Range(0, 10)) = 1.8
        _WindScale("Wind Scale", Range(0.01, 5)) = 0.7
        _WindDirection("Wind Direction XZ", Vector) = (1, 0, 0.35, 0)
        _WindPivotY("Wind Pivot Y", Float) = 0
        _WindHeightFade("Wind Height Fade", Range(0.01, 10)) = 1.5
        _WindLeafFlutter("Leaf Flutter", Range(0, 1)) = 0.25
        _WindVertexColorMask("Vertex Color Mask", Range(0, 1)) = 0

        [Toggle] _TwoSidedLighting("Two Sided Lighting", Float) = 1
        _BackFaceLight("Back Face Light", Range(0, 1)) = 0.45
        _AmbientBoost("Ambient Boost", Range(0, 1)) = 0.18

        _Smoothness("Smoothness", Range(0, 1)) = 0.25
        _Specular("Specular", Range(0, 1)) = 0.1
        Dither("Dither", Range(0, 1)) = 0
        _Dither("Dither Fallback", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "UniversalMaterialType" = "Lit"
        }

        LOD 300
        Cull [_Cull]
        ZWrite On
        AlphaToMask On

        HLSLINCLUDE
        #pragma target 3.5

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _WindStrength;
            half _WindSpeed;
            half _WindScale;
            float4 _WindDirection;
            half _WindPivotY;
            half _WindHeightFade;
            half _WindLeafFlutter;
            half _WindVertexColorMask;
            half _TwoSidedLighting;
            half _BackFaceLight;
            half _AmbientBoost;
            half _Smoothness;
            half _Specular;
            half Dither;
            half _Dither;
        CBUFFER_END

        float3 _LightDirection;
        float3 _LightPosition;
        float4 _PlayerFoliageDitherPosition;
        float4 _PlayerFoliageDitherParams;
        float4 _CursorFoliageDitherPosition;
        float4 _CursorFoliageDitherParams;

        half ScreenDither4x4(float2 positionSS)
        {
            uint2 pixel = (uint2)positionSS & 3;
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
            clip(ScreenDither4x4(positionCS.xy) - dither);
        }

        struct Attributes
        {
            float4 positionOS : POSITION;
            half3 normalOS : NORMAL;
            half4 tangentOS : TANGENT;
            float2 texcoord : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fogFactor : TEXCOORD4;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            float4 shadowCoord : TEXCOORD5;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 ApplyBushWind(float3 positionOS, half4 vertexColor)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float2 windDir = normalize(_WindDirection.xz + float2(0.0001, 0.0001));
            float heightMask = saturate((positionOS.y - _WindPivotY) * _WindHeightFade);
            float vertexMask = lerp(1.0, vertexColor.r, _WindVertexColorMask);

            float phase = dot(positionWS.xz, windDir) * _WindScale + _Time.y * _WindSpeed;
            float sway = sin(phase) * 0.7 + sin(phase * 2.17 + positionWS.y) * 0.3;
            float flutter = sin(phase * 5.0 + positionOS.x * 3.0 + positionOS.z * 2.0) * _WindLeafFlutter;
            float amount = (sway + flutter) * _WindStrength * heightMask * vertexMask;

            float3 offsetWS = float3(windDir.x, 0.0, windDir.y) * amount;
            return positionOS + mul((float3x3)GetWorldToObjectMatrix(), offsetWS);
        }

        Varyings WindLitVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 windPositionOS = ApplyBushWind(input.positionOS.xyz, input.color);
            VertexPositionInputs positionInputs = GetVertexPositionInputs(windPositionOS);
            VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

            output.positionCS = positionInputs.positionCS;
            output.positionWS = positionInputs.positionWS;
            output.normalWS = normalInputs.normalWS;
            output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
            output.shadowCoord = GetShadowCoord(positionInputs);
            #endif
            return output;
        }

        half4 WindLitFragment(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            clip(tex.a - _Cutoff);
            ClipDither(
                input.positionCS,
                saturate(max(max(MaterialDither(), PlayerZoneDither(input.positionCS)), CursorZoneDither(input.positionCS))));

            half facingSign = IS_FRONT_VFACE(facing, 1.0h, -1.0h);
            half normalSign = lerp(1.0h, facingSign, _TwoSidedLighting);
            half3 normalWS = NormalizeNormalPerPixel(input.normalWS * normalSign);

            SurfaceData surfaceData = (SurfaceData)0;
            surfaceData.albedo = tex.rgb;
            surfaceData.alpha = tex.a;
            surfaceData.metallic = 0;
            surfaceData.specular = _Specular.xxx;
            surfaceData.smoothness = _Smoothness;
            surfaceData.occlusion = 1;
            surfaceData.emission = 0;

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
            inputData.vertexLighting = VertexLighting(input.positionWS, inputData.normalWS);
            inputData.bakedGI = SampleSH(inputData.normalWS) + tex.rgb * _AmbientBoost;
            inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            inputData.shadowMask = half4(1, 1, 1, 1);

            half4 color = UniversalFragmentPBR(inputData, surfaceData);
            half backFaceMask = (1.0h - saturate(facingSign)) * _TwoSidedLighting;
            color.rgb += tex.rgb * _BackFaceLight * backFaceMask;
            color.rgb = MixFog(color.rgb, inputData.fogCoord);
            return color;
        }

        struct DepthVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD1;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        DepthVaryings WindDepthVertex(Attributes input)
        {
            DepthVaryings output = (DepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 windPositionOS = ApplyBushWind(input.positionOS.xyz, input.color);
            output.positionWS = TransformObjectToWorld(windPositionOS);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            return output;
        }

        DepthVaryings WindShadowVertex(Attributes input)
        {
            DepthVaryings output = (DepthVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 windPositionOS = ApplyBushWind(input.positionOS.xyz, input.color);
            float3 positionWS = TransformObjectToWorld(windPositionOS);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
            float3 lightDirectionWS = _LightDirection;
            #endif

            output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            output.positionCS = ApplyShadowClamping(output.positionCS);
            output.positionWS = positionWS;
            output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
            return output;
        }

        half4 WindDepthFragment(DepthVaryings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
            clip(alpha - _Cutoff);
            ClipDither(input.positionCS, MaterialDither());
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex WindLitVertex
            #pragma fragment WindLitFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
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

            HLSLPROGRAM
            #pragma vertex WindShadowVertex
            #pragma fragment WindDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex WindDepthVertex
            #pragma fragment WindDepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
