Shader "ExtractionRaid/Road Marking Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Texture (RGB), Opacity (A)", 2D) = "white" {}
        [MainColor] _BaseColor("Paint Color", Color) = (1, 1, 1, 1)
        _Brightness("Brightness", Range(0, 10)) = 1
        _Opacity("Opacity", Range(0, 1)) = 1
        _MaskMap("Mask (White = Visible)", 2D) = "white" {}
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

        Pass
        {
            Name "RoadMarkingUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }
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
                float _Opacity;
                float _MaskChannel;
                float _BlendMode;
                float _AlphaClip;
                float _Cutoff;
                float _DepthOffset;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 baseUV : TEXCOORD0;
                float2 maskUV : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half3 vertexColor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.baseUV = TRANSFORM_TEX(input.uv, _BaseMap);
                output.maskUV = TRANSFORM_TEX(input.uv, _MaskMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.vertexColor = input.color.rgb;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
                half4 maskSample = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.maskUV);
                half mask = _MaskChannel < 0.5 ? maskSample.r
                    : _MaskChannel < 1.5 ? maskSample.g
                    : _MaskChannel < 2.5 ? maskSample.b : maskSample.a;
                half alpha = saturate(baseSample.a * _BaseColor.a * _Opacity * mask);
                if (_AlphaClip > 0.5)
                    clip(alpha - _Cutoff);

                half3 color = baseSample.rgb * _BaseColor.rgb * input.vertexColor * _Brightness;
                // Additive marks fade towards black so fog does not add extra light.
                color = MixFogColor(color, _BlendMode > 1.5 ? half3(0, 0, 0) : unity_FogColor.rgb, input.fogFactor);
                // Textures use straight alpha; premultiplication happens exactly once here.
                if (_BlendMode > 0.5 && _BlendMode < 1.5)
                    color *= alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    CustomEditor "ExtractionRaid.Editor.RoadMarkingShaderGUI"
    FallBack Off
}
