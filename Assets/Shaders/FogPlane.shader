// Depth-based plane fog, following the principle of Cyanilux's accurate example:
// https://www.cyanilux.com/tutorials/fog-plane-shader-breakdown/
// Assign to a flat Plane/Quad; point its mesh normals toward the camera.
// Fog fills the half-space behind the plane, within the mesh's screen footprint.
// Requires URP Depth Texture (including the camera override). Only objects in
// that texture affect fog; ordinary transparent objects do not contribute depth.
// Density is inverse world units: 0.5 reaches full opacity 2 units behind the plane.
// Supports perspective/orthographic cameras and non-uniform positive mesh scale.
// The camera must remain on the front side. This is not a traversable fog volume.
// Mask/noise use UV0 and the red channel: white preserves fog, black removes it.
// Import as linear data (sRGB off); use Clamp for the mask, Repeat for tileable noise.
// Each texture has independent tiling/offset; noise speeds are UV units per second.
Shader "ExtractionRaid/VFX/FogPlane"
{
    Properties
    {
        [MainColor] _BaseColor ("Fog Color", Color) = (0.65, 0.7, 0.75, 1)
        _Density ("Density (Per World Unit)", Range(0, 10)) = 0.5
        [Header(Mask)]
        _MaskTex ("Fog Mask (R)", 2D) = "white" {}
        [Header(Animated Noise)]
        _NoiseTexA ("Noise A (R)", 2D) = "white" {}
        _NoiseSpeedA ("Noise A Speed (XY)", Vector) = (0.025, 0.01, 0, 0)
        _NoiseTexB ("Noise B (R)", 2D) = "white" {}
        _NoiseSpeedB ("Noise B Speed (XY)", Vector) = (-0.015, 0.02, 0, 0)
        _NoiseMix ("Noise Mix (0 = A, 1 = B)", Range(0, 1)) = 0.5
        _NoiseStrength ("Noise Opacity Strength", Range(0, 1)) = 0.5
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
            Name "FogPlane"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_NoiseTexA);
            SAMPLER(sampler_NoiseTexA);
            TEXTURE2D(_NoiseTexB);
            SAMPLER(sampler_NoiseTexB);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MaskTex_ST;
                float4 _NoiseTexA_ST;
                float4 _NoiseTexB_ST;
                float4 _NoiseSpeedA;
                float4 _NoiseSpeedB;
                float _Density;
                float _NoiseMix;
                float _NoiseStrength;
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
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);

                // Reconstruction expects device NDC depth: 0..1 on D3D/Metal,
                // -1..1 on OpenGL. Inverse VP also handles orthographic cameras.
                #if UNITY_REVERSED_Z
                    float deviceDepth = rawDepth;
                #else
                    float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 sceneWS = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
                // Perpendicular distance to the actual fragment's plane, in world
                // units: independent of mesh pivot, mesh scale and viewing angle.
                float distanceBehindPlane = max(0.0,
                    dot(input.positionWS - sceneWS, normalize(input.normalWS)));
                half alpha = saturate(distanceBehindPlane * max(_Density, 0.0))
                    * saturate(_BaseColor.a);

                float2 maskUV = TRANSFORM_TEX(input.uv, _MaskTex);
                float2 noiseUVA = TRANSFORM_TEX(input.uv, _NoiseTexA) + _Time.y * _NoiseSpeedA.xy;
                float2 noiseUVB = TRANSFORM_TEX(input.uv, _NoiseTexB) + _Time.y * _NoiseSpeedB.xy;
                half mask = saturate(SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r);
                half noiseA = saturate(SAMPLE_TEXTURE2D(_NoiseTexA, sampler_NoiseTexA, noiseUVA).r);
                half noiseB = saturate(SAMPLE_TEXTURE2D(_NoiseTexB, sampler_NoiseTexB, noiseUVB).r);
                half mixedNoise = lerp(noiseA, noiseB, saturate(_NoiseMix));
                // Modulate after the depth ramp so motion remains visible even
                // where the depth-based fog has already reached full density.
                alpha *= mask * lerp(1.0h, mixedNoise, saturate(_NoiseStrength));
                return half4(_BaseColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
