// QuestBeam — soft vertical light shaft rising from a quest-giver NPC. Additive,
// unlit, procedural (no textures). Mesh = one vertical quad (built in C# by
// NpcQuestIndicator, billboarded toward the camera).
//
// Soft particles: samples the scene depth and fades the beam as it approaches or
// passes behind geometry, so it never hard-clips through crates/walls — it dissolves
// into them like real volumetric light. Requires the URP depth texture (PC_RPAsset
// has RequireDepthTexture on).
//
// Params:
//   _Color RGBA  — beam tint (yellow = available, green = ready to turn in)
//   _Alpha 0..1  — global intensity (breathes on the indicator's pulse)
//   _SoftFade    — meters over which the beam fades out near geometry
Shader "VFX/QuestBeam"
{
    Properties
    {
        _Color ("Color", Color) = (1.0, 0.82, 0.15, 1)
        _Alpha ("Alpha", Range(0,1)) = 0.7
        _SoftFade ("Soft Fade Distance", Float) = 0.7
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One   // additive — alpha drives contribution intensity

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _Color;
            float  _Alpha;
            float  _SoftFade;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Vertical profile: bright at base, fade upward, soft cut off the ground.
                float vProfile = pow(saturate(1.0 - IN.uv.y), 1.3);
                vProfile *= smoothstep(0.0, 0.06, IN.uv.y);
                // Horizontal profile: bright core, soft side edges.
                float edge = 1.0 - abs(IN.uv.x * 2.0 - 1.0);
                edge = pow(saturate(edge), 1.6);

                // Soft-particle depth fade — dissolve into geometry instead of clipping.
                float2 screenUV = IN.screenPos.xy / max(1e-4, IN.screenPos.w);
                float sceneRaw = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                float fragEye  = IN.screenPos.w;
                float soft = saturate((sceneEye - fragEye) / max(0.01, _SoftFade));

                float a = vProfile * edge * soft * _Alpha * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
