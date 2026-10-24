Shader "ExtractionRaid/Road Marking Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture (RGB), Opacity (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Paint Color", Color) = (1, 1, 1, 1)
        _Brightness("Brightness", Range(0, 10)) = 1
        _LightingInfluence("Lighting Influence", Range(0, 1)) = 1
        _ShadowStrength("Main Light Shadow Strength", Range(0, 1)) = 0.75
        _Opacity("Opacity", Range(0, 1)) = 1
        _MaskMap("Mask (White = Visible)", 2D) = "white" {}
        [ToggleUI] _UseRoadMaskUV("Use UV2 for Painted Mask", Float) = 0
        [Enum(R,0,G,1,B,2,A,3)] _MaskChannel("Mask Channel", Float) = 0
        [Enum(Alpha,0,Premultiply,1,Additive,2)] _BlendMode("Blending", Float) = 0
        [Toggle] _AlphaClip("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _DepthOffset("Depth Offset", Range(-5, 0)) = -1
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap);
        SAMPLER(sampler_MaskMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _MaskMap_ST;
            half4 _BaseColor;
            float _Brightness;
            float _LightingInfluence;
            float _ShadowStrength;
            float _Opacity;
            float _MaskChannel;
            float _UseRoadMaskUV;
            float _BlendMode;
            float _AlphaClip;
            float _Cutoff;
            float _DepthOffset;
            float _SrcBlend;
            float _DstBlend;
        CBUFFER_END

        half RoadMask(half4 maskSample)
        {
            return _MaskChannel < 0.5 ? maskSample.r
                : _MaskChannel < 1.5 ? maskSample.g
                : _MaskChannel < 2.5 ? maskSample.b : maskSample.a;
        }
        ENDHLSL

        Pass
        {
            Name "RoadMarkingUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset [_DepthOffset], [_DepthOffset]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Transparent geometry samples the shadow atlas, not opaque screen-space depth.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 paintUV : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 baseUV : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half4 vertexColor : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                half3 normalWS : TEXCOORD5;
                half3 vertexLighting : TEXCOORD6;
                #if defined(LIGHTMAP_ON)
                    float2 lightmapUV : TEXCOORD7;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.vertexLighting = VertexLighting(output.positionWS, output.normalWS);
                output.baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                float2 maskUV = _UseRoadMaskUV > 0.5 ? input.paintUV : input.uv;
                output.maskUV = TRANSFORM_TEX(maskUV, _MaskMap);
                // UV2 is shared with painting; only the lightmap gets the atlas transform.
                #if defined(LIGHTMAP_ON)
                    OUTPUT_LIGHTMAP_UV(input.paintUV, unity_LightmapST, output.lightmapUV);
                #endif
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.vertexColor = input.color;
                return output;
            }

            half3 RoadDiffuse(Light light, half3 normalWS)
            {
                return light.color * light.distanceAttenuation * light.shadowAttenuation
                    * saturate(dot(normalWS, light.direction));
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                half4 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.maskUV);
                half mask = RoadMask(maskSample);
                half alpha = saturate(baseSample.a * _BaseColor.a * input.vertexColor.a * _Opacity * mask);
                if (_AlphaClip > 0.5)
                    clip(alpha - _Cutoff);

                half3 color = baseSample.rgb * _BaseColor.rgb * input.vertexColor.rgb * _Brightness;
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS)
                    * IS_FRONT_VFACE(face, 1.0h, -1.0h);
                // Compute shadow coordinates per pixel so large road meshes cross cascades correctly.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half4 shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                half mainShadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                mainLight.shadowAttenuation = mainShadow;
                #if defined(LIGHTMAP_ON)
                    half3 bakedGI = SampleLightmap(input.lightmapUV, float2(0, 0), normalWS);
                #else
                    half3 bakedGI = SampleSH(normalWS);
                #endif
                MixRealtimeAndBakedGI(mainLight, normalWS, bakedGI);
                half3 lighting = bakedGI + RoadDiffuse(mainLight, normalWS);

                #if defined(_ADDITIONAL_LIGHTS)
                    // URP's clustered loop reads these fields from inputData.
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    #if USE_CLUSTER_LIGHT_LOOP
                        UNITY_LOOP for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                            lighting += RoadDiffuse(light, normalWS);
                        }
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        lighting += RoadDiffuse(light, normalWS);
                    LIGHT_LOOP_END
                #endif
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    lighting += input.vertexLighting;
                #endif
                color *= lerp(half3(mainShadow, mainShadow, mainShadow), lighting, _LightingInfluence);
                // Additive marks fade towards black so fog does not add extra light.
                color = MixFogColor(color, _BlendMode > 1.5 ? half3(0, 0, 0) : unity_FogColor.rgb, input.fogFactor);
                // Textures use straight alpha; premultiplication happens exactly once here.
                if (_BlendMode > 0.5 && _BlendMode < 1.5)
                    color *= alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct MetaAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float2 uv3 : TEXCOORD2;
                half4 color : COLOR;
            };

            struct MetaVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 baseUV : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                half4 color : TEXCOORD2;
            };

            MetaVaryings MetaVert(MetaAttributes input)
            {
                MetaVaryings output;
                output.positionCS = MetaVertexPosition(input.positionOS, input.uv2, input.uv3,
                    unity_LightmapST, unity_DynamicLightmapST);
                output.baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                float2 maskUV = _UseRoadMaskUV > 0.5 ? input.uv2 : input.uv;
                output.maskUV = TRANSFORM_TEX(maskUV, _MaskMap);
                output.color = input.color;
                return output;
            }

            half4 MetaFrag(MetaVaryings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                half mask = RoadMask(SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.maskUV));
                half alpha = saturate(baseSample.a * _BaseColor.a * input.color.a * _Opacity * mask);
                if (_AlphaClip > 0.5)
                    clip(alpha - _Cutoff);
                MetaInput meta = (MetaInput)0;
                // Approximate transparent coverage for bounced diffuse light; brightness is not emission.
                meta.Albedo = baseSample.rgb * _BaseColor.rgb * input.color.rgb * _Brightness * alpha;
                return MetaFragment(meta);
            }
            ENDHLSL
        }
    }
    CustomEditor "ExtractionRaid.Editor.RoadMarkingShaderGUI"
    FallBack Off
}
