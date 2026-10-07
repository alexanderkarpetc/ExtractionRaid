Shader "ExtractShaders/SplatRGBA"
{
    Properties
    {
        _SplatMap("Splat Map (R Base / G Layer 1 / B Layer 2 / A Layer 3)", 2D) = "red" {}
        _BaseMap("Base Texture (R)", 2D) = "white" {}
        _Layer1("Layer 1 (G)", 2D) = "white" {}
        _Layer2("Layer 2 (B)", 2D) = "white" {}
        _Layer3("Layer 3 (A)", 2D) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.2
        [ToggleUI] _UseOpacityMask("Use Opacity Mask (Transparent)", Float) = 0
        _MaskMap("Opacity Mask (R: White = Visible)", 2D) = "white" {}
        _Opacity("Opacity", Float) = 1
        _DepthOffset("Depth Offset", Range(-5,0)) = 0
        [ToggleUI] _UseRoadDetailUV("Use Road Detail UV (UV3)", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _SplatMap_ST, _BaseMap_ST, _Layer1_ST, _Layer2_ST, _Layer3_ST;
            float4 _MaskMap_ST;
            half4 _Tint;
            half _Metallic, _Smoothness;
            float _UseOpacityMask, _Opacity, _DepthOffset, _SrcBlend, _DstBlend, _ZWrite;
            float _UseRoadDetailUV;
        CBUFFER_END
        TEXTURE2D(_SplatMap); SAMPLER(sampler_SplatMap);
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_Layer1); SAMPLER(sampler_Layer1);
        TEXTURE2D(_Layer2); SAMPLER(sampler_Layer2);
        TEXTURE2D(_Layer3); SAMPLER(sampler_Layer3);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

        half SampleOpacity(float2 uv)
        {
            return saturate(SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, TRANSFORM_TEX(uv, _MaskMap)).r * _Opacity * _Tint.a);
        }

        half3 SampleSplatAlbedo(float2 uv, float2 detailUV)
        {
            float2 layerUV = _UseRoadDetailUV > 0.5 ? detailUV : uv;
            // Import the control map as linear data, with alpha preserved.
            float4 weights = saturate(SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, TRANSFORM_TEX(uv, _SplatMap)));
            float total = dot(weights, float4(1,1,1,1));
            weights = total > 0.0001 ? weights / max(total, 0.0001) : float4(1,0,0,0);
            half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(layerUV, _BaseMap)).rgb * weights.r;
            color += SAMPLE_TEXTURE2D(_Layer1, sampler_Layer1, TRANSFORM_TEX(layerUV, _Layer1)).rgb * weights.g;
            color += SAMPLE_TEXTURE2D(_Layer2, sampler_Layer2, TRANSFORM_TEX(layerUV, _Layer2)).rgb * weights.b;
            color += SAMPLE_TEXTURE2D(_Layer3, sampler_Layer3, TRANSFORM_TEX(layerUV, _Layer3)).rgb * weights.a;
            return color * _Tint.rgb;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Offset [_DepthOffset], [_DepthOffset]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SplatVertex
            #pragma fragment SplatFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                float2 detailUV : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 fogAndVertexLight : TEXCOORD3;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                float2 detailUV : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings SplatVertex(Attributes input)
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
                output.detailUV = input.detailUV;
                output.fogAndVertexLight = half4(ComputeFogFactor(position.positionCS.z), VertexLighting(position.positionWS, output.normalWS));
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }
            half4 SplatFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = NormalizeNormalPerPixel(input.normalWS);
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
                data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                data.fogCoord = input.fogAndVertexLight.x;
                data.vertexLighting = input.fogAndVertexLight.yzw;
                data.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, data.normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = SampleSplatAlbedo(input.uv, input.detailUV);
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = 1;
                surface.alpha = 1;
                #if defined(_SURFACE_TYPE_TRANSPARENT)
                surface.alpha = SampleOpacity(input.uv);
                #endif
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Offset [_DepthOffset], [_DepthOffset]
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Offset [_DepthOffset], [_DepthOffset]
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex SplatMetaVertex
            #pragma fragment SplatMetaFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            struct MetaAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 detailUV : TEXCOORD3;
            };
            struct MetaVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 detailUV : TEXCOORD1; };
            MetaVaryings SplatMetaVertex(MetaAttributes input)
            {
                MetaVaryings output;
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2, unity_LightmapST, unity_DynamicLightmapST);
                output.uv = input.uv;
                output.detailUV = input.detailUV;
                return output;
            }
            half4 SplatMetaFragment(MetaVaryings input) : SV_Target
            {
                // Transparent surfaces do not contribute solid geometry to a bake.
                if (_UseOpacityMask > 0.5) clip(SampleOpacity(input.uv) - 0.001h);
                MetaInput meta = (MetaInput)0;
                half3 albedo = SampleSplatAlbedo(input.uv, input.detailUV);
                BRDFData brdf;
                half alpha = 1;
                InitializeBRDFData(albedo, _Metallic, half3(0,0,0), _Smoothness, alpha, brdf);
                meta.Albedo = brdf.diffuse + brdf.specular * brdf.roughness * 0.5;
                return UnityMetaFragment(meta);
            }
            ENDHLSL
        }
    }
    CustomEditor "ExtractionRaid.Editor.SplatMap.SplatShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
