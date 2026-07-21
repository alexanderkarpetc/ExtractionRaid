// QuestGroundGlow — a soft additive pool of light on the floor beneath a quest-giver.
// Paired with QuestBeam so the marker reads as a light SOURCE (light spilling onto the
// ground) rather than a flat mesh. Procedural, no textures. Mesh = one horizontal quad
// (XZ plane) built in C# by NpcQuestIndicator; UV 0..1.
//
// Soft particles: fades where the quad meets standing geometry (crate bases, walls) so
// the pool hugs the floor instead of drawing flat over props. Requires the URP depth
// texture (PC_RPAsset has RequireDepthTexture on).
//
// Params:
//   _Color RGBA — pool tint (matches the quest state color)
//   _Alpha 0..1 — global intensity (breathes on the indicator's pulse)
//   _SoftFade   — meters over which the pool fades out near geometry
Shader "VFX/QuestGroundGlow"
{
    Properties
    {
        _Color ("Color", Color) = (1.0, 0.82, 0.15, 1)
        _Alpha ("Alpha", Range(0,1)) = 0.6
        _SoftFade ("Soft Fade Distance", Float) = 0.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One   // additive — reads as emitted light on the ground

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
                // Radial falloff from the quad center. d: 0 center → 1 at the inscribed edge.
                float d = length(IN.uv - 0.5) * 2.0;
                float core = pow(saturate(1.0 - d), 2.2);
                float halo = pow(saturate(1.0 - d), 5.0) * 0.6;
                float radial = saturate(core + halo);

                // Soft-particle depth fade — hug the floor, don't paint over props.
                float2 screenUV = IN.screenPos.xy / max(1e-4, IN.screenPos.w);
                float sceneRaw = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(sceneRaw, _ZBufferParams);
                float fragEye  = IN.screenPos.w;
                float soft = saturate((sceneEye - fragEye) / max(0.01, _SoftFade));

                float a = radial * soft * _Alpha * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
