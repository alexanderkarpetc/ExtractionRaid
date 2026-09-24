// Place on a horizontal Quad/Plane above the area to conceal. Requires UV0 in 0..1.
// Unlit, double-sided, texture-free; works with perspective and orthographic cameras.
// Edge Softness is relative to mesh UVs. Noise Tiling XY compensates for long meshes.
// Depth fade requires URP/camera Depth Texture; only geometry in that texture contributes.
// Distance is in world units along the view axis. Set to zero to disable depth sampling.
// Color alpha is ignored. Opacity = 1 fully conceals the interior, even with noise enabled.
Shader "ExtractionRaid/VFX/BunkerFog"
{
    Properties
    {
        _BaseColor ("Fog Color (RGB Only)", Color) = (0.28, 0.32, 0.34, 1)
        _HighlightColor ("Cloud Highlight Color (RGB Only)", Color) = (0.48, 0.53, 0.55, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _NoiseOpacity ("Noise Opacity Variation", Range(0, 1)) = 0.25
        _EdgeSoftness ("Edge Softness", Range(0.001, 1)) = 0.3
        _DepthFadeDistance ("Depth Fade Distance", Range(0, 5)) = 0.5
        [Enum(Rectangle, 0, Ellipse, 1)] _Shape ("Shape", Float) = 0
        _NoiseTiling ("Noise Tiling (XY)", Vector) = (4, 4, 0, 0)
        _Drift ("Drift Speed (XY)", Vector) = (0.035, 0.018, 0, 0)
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.35
        _Contrast ("Noise Contrast", Range(0.1, 4)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BunkerFog"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _HighlightColor;
                float4 _NoiseTiling;
                float4 _Drift;
                float _Opacity;
                float _NoiseOpacity;
                float _EdgeSoftness;
                float _DepthFadeDistance;
                float _Shape;
                float _DetailStrength;
                float _Contrast;
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
                float2 uv : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), f.x),
                    f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.eyeDepth = -positions.positionVS.z;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = abs(input.uv * 2.0 - 1.0);
                float distanceToEdge = lerp(
                    1.0 - max(centered.x, centered.y),
                    1.0 - length(centered), saturate(_Shape));
                float edge = smoothstep(0.0, max(_EdgeSoftness, 0.001), distanceToEdge);

                float depthFade = 1.0;
                if (_DepthFadeDistance > 0.0)
                {
                    float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    float rawDepth = SampleSceneDepth(screenUV);
                    float sceneEyeDepth = (unity_OrthoParams.w > 0.5)
                        ? LinearDepthToEyeDepth(rawDepth)
                        : LinearEyeDepth(rawDepth, _ZBufferParams);
                    // View-space depth works for both camera projections (clip W does not).
                    depthFade = smoothstep(0.0, max(_DepthFadeDistance, 0.0001),
                        sceneEyeDepth - input.eyeDepth);
                }

                float2 p = input.uv * _NoiseTiling.xy;
                float2 drift = _Time.y * _Drift.xy;
                float largeClouds = Noise(p - drift);
                // Different direction and scale keep the layers from moving as one flat sheet.
                float detail = Noise(p * 2.13 + drift.yx * 0.73 + float2(17.2, 9.4));
                float clouds = (largeClouds + detail * _DetailStrength) / (1.0 + _DetailStrength);
                clouds = saturate((clouds - 0.5) * _Contrast + 0.5);

                half3 color = lerp(_BaseColor.rgb, _HighlightColor.rgb, clouds);
                // Noise can thin partial opacity, but cannot punch holes at full opacity.
                // RGB and color-picker alpha never affect concealment.
                float opacity = saturate(_Opacity);
                float noiseVariation = saturate(_NoiseOpacity) * (1.0 - opacity);
                float alpha = edge * depthFade * opacity * lerp(1.0, clouds, noiseVariation);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
