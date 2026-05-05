// Hand-written URP character shader replacing Amplify-generated CharacterMainBase
// for player + bot characters. Three feature blocks:
//
//   1. Base styling — texture + saturate + secondary tint + alpha mask. Same
//      visual output as the legacy ExtractShaders/MainBase shader (matte: no
//      smoothness, no metallic, hardcoded specular = 0.5).
//
//   2. Static emission — _ColorEmission * _BrightnessEmission for authored
//      glow on character (LEDs / glowing parts via material properties).
//
//   3. Hit feedback (TWO mechanisms, driven via MaterialPropertyBlock per
//      renderer — see View/BotView.cs):
//
//      a) Rim flash on hit — fresnel-based edge glow (1 - dot(N, V))^power
//         tinted by _HitFlashColor, scaled by _HitFlashIntensity. C# fades
//         intensity to 0 over flash duration. Replaces the old "whole body
//         glows red via _BrightnessEmission" technique.
//
//      b) Bullet impact decals — array of world-space positions. Each entry
//         paints a red circle on the body at that position with smooth
//         falloff. C# pushes new hits and fades their intensity over time.
//         Up to VIBE_HIT_DECAL_MAX simultaneously.
//
// SRP batcher caveat: MPB-driven properties (_HitFlashIntensity, _HitDecals,
// _HitDecalCount) sit OUTSIDE the UnityPerMaterial CBUFFER, so per-renderer
// overrides break batching for character draws only. Acceptable trade-off
// for ~10 characters on screen.

Shader "ExtractShaders/VibeCharacterShader"
{
    Properties
    {
        // Legacy names retained so 3 character materials authored against the
        // old ExtractShaders/MainBase shader keep their values when the shader
        // reference is swapped to this one — no per-material re-authoring.
        [MainTexture] _Texture("Texture", 2D) = "white" {}
        _Saturate("Saturate", Float) = 1.5

        _Color2("Tint Color", Color) = (1, 1, 1, 0)
        _ColorStreng("Tint Strength", Float) = 0
        [Toggle(_USEALPHA_ON)] _UseAlpha("Use Alpha As Mask", Float) = 0

        _ColorEmission("Color Emission", Color) = (1, 1, 1, 0)
        _BrightnessEmission("Brightness Emission", Float) = 0

        [Header(Hit Rim Flash)]
        _HitFlashColor("Hit Flash Color", Color) = (1, 0.15, 0.15, 1)
        _HitFlashIntensity("Hit Flash Intensity", Range(0, 5)) = 0
        _HitFlashRimPower("Hit Flash Rim Power", Range(0.5, 8)) = 2.5
        _HitFlashRimWidth("Hit Flash Rim Width", Range(0, 1)) = 0.6

        [Header(Hit Decals)]
        _HitDecalColor("Hit Decal Color", Color) = (0.85, 0.05, 0.05, 1)
        _HitDecalRadius("Hit Decal Radius", Float) = 0.35
        _HitDecalSoftness("Hit Decal Softness", Range(0.05, 1)) = 0.55

        [HideInInspector] _Cull("__cull", Float) = 2.0
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
        LOD 200

        // Per-material constants packed in UnityPerMaterial cbuffer (SRP-batchable).
        // Per-renderer MPB overrides (HitFlash + decal array) live outside the
        // cbuffer in the Pass HLSL block below.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Texture_ST;
            float  _Saturate;
            float4 _Color2;
            float  _ColorStreng;
            float4 _ColorEmission;
            float  _BrightnessEmission;
            float4 _HitFlashColor;
            float  _HitFlashRimPower;
            float  _HitFlashRimWidth;
            float4 _HitDecalColor;
            float  _HitDecalRadius;
            float  _HitDecalSoftness;
            float  _Cull;
        CBUFFER_END

        TEXTURE2D(_Texture);
        SAMPLER(sampler_Texture);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #pragma shader_feature_local _USEALPHA_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Per-renderer MPB overrides — must live outside UnityPerMaterial cbuffer
            // so MaterialPropertyBlock can write per-draw values without breaking
            // the SRP batcher cbuffer layout for other draws. Count is float (not int)
            // because some MPB binding paths convert int → float anyway, and float
            // is universally safe.
            #define VIBE_HIT_DECAL_MAX 8
            float  _HitFlashIntensity;
            float  _HitDecalCount;
            float4 _HitDecals[VIBE_HIT_DECAL_MAX]; // xyz = world pos, w = intensity (0..1)

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float  fogCoord    : TEXCOORD3;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS);
                VertexNormalInputs   vn = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = vp.positionCS;
                OUT.positionWS  = vp.positionWS;
                OUT.normalWS    = vn.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _Texture);
                OUT.fogCoord    = ComputeFogFactor(vp.positionCS.z);
                return OUT;
            }

            // Match legacy MainBase surface logic exactly:
            //   luminance = dot(rgb, Rec709)
            //   saturated = lerp(luminance, rgb, _Saturate)        // 0=BW, 1=normal, >1=oversat
            //   mask      = useAlpha ? (1 - tex.a) : 1
            //   base      = lerp(saturated * mask,
            //                    saturated * tint * mask * tintStrength, 0.5)
            half3 ComputeStylizedBase(half4 tex)
            {
                half lum = dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half3 saturated = lerp(half3(lum, lum, lum), tex.rgb, _Saturate);

                #ifdef _USEALPHA_ON
                    half mask = 1.0h - tex.a;
                #else
                    half mask = 1.0h;
                #endif

                half3 plain  = saturated * mask;
                half3 tinted = saturated * _Color2.rgb * mask * _ColorStreng;
                return lerp(plain, tinted, 0.5h);
            }

            // Fresnel-style edge glow. Sharper at edges, fades over time
            // when C# decays _HitFlashIntensity to 0.
            half3 ComputeRimFlash(half3 N, half3 V)
            {
                if (_HitFlashIntensity <= 0.0001h) return half3(0, 0, 0);

                half facing = saturate(dot(N, V));
                half rimRaw = 1.0h - facing;
                // _HitFlashRimWidth shifts where rim starts; values < 1 push rim closer
                // to the silhouette edge for a tighter outline.
                half rim = saturate((rimRaw - (1.0h - _HitFlashRimWidth)) / max(_HitFlashRimWidth, 0.001h));
                rim = pow(rim, _HitFlashRimPower);
                return _HitFlashColor.rgb * rim * _HitFlashIntensity;
            }

            // Returns coverage [0..1] для current fragment — combined opacity з
            // ALL active decals (max blend, не sum so overlapping не over-saturates).
            // Coverage не залежить від decal color, тому темний колір тінтить
            // albedo з тією самою силою як яскравий.
            half ComputeHitDecalCoverage(float3 positionWS)
            {
                if (_HitDecalCount < 0.5) return 0.0h;

                half coverage = 0.0h;
                half innerR = _HitDecalRadius * (1.0h - saturate(_HitDecalSoftness));
                half outerR = _HitDecalRadius;

                [unroll(VIBE_HIT_DECAL_MAX)]
                for (int i = 0; i < VIBE_HIT_DECAL_MAX; ++i)
                {
                    float4 entry = _HitDecals[i];
                    half   life  = (half)entry.w;
                    if (life > 0.0h)
                    {
                        half d    = (half)distance(positionWS, entry.xyz);
                        half mask = 1.0h - smoothstep(innerR, outerR, d);
                        coverage = max(coverage, mask * life);
                    }
                }
                return saturate(coverage);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, IN.uv);
                half3 stylized = ComputeStylizedBase(tex);

                // Decal stain — pure albedo replacement at coverage [0..1]. Decal
                // does NOT glow (no emission contribution); behaves як кров на шкірі.
                // Coverage independent of color, тому dark colors тінтять так само
                // visible як bright.
                half  decalCoverage = ComputeHitDecalCoverage(IN.positionWS);
                half3 albedo        = lerp(stylized, _HitDecalColor.rgb, decalCoverage);

                half3 N = normalize(IN.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Match legacy Amplify shader output: it ran through URP's
                // UniversalFragmentPBR з Metallic=0 + Smoothness=0, which collapses
                // to plain Lambert + SH ambient (specular term zeros out from BRDF).
                // No half-Lambert wrap, no constant ambient floor — dark sides will
                // be dark unless scene has light probes baked.
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half NdotL = saturate(dot(N, mainLight.direction));
                half3 directDiffuse = albedo * mainLight.color
                                    * NdotL * mainLight.shadowAttenuation;

                half3 ambient = albedo * SampleSH(N);

                half3 emission = (_ColorEmission.rgb * _BrightnessEmission)
                               + ComputeRimFlash(N, V);

                half3 final = directDiffuse + ambient + emission;
                final = MixFog(final, IN.fogCoord);
                return half4(final, 1.0h);
            }
            ENDHLSL
        }

        // Shadow caster — boilerplate position-only pass.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float3 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionHCS : SV_POSITION; };

            float4 ApplyBias(float3 positionWS, float3 normalWS)
            {
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 pos = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return pos;
            }

            V ShadowVert(A IN)
            {
                V OUT;
                float3 ws = TransformObjectToWorld(IN.positionOS);
                float3 nw = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = ApplyBias(ws, nw);
                return OUT;
            }

            half4 ShadowFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth-only — for SSAO + post-FX depth.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DOVert
            #pragma fragment DOFrag

            struct A { float3 positionOS : POSITION; };
            struct V { float4 positionHCS : SV_POSITION; };

            V DOVert(A IN)
            {
                V OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                return OUT;
            }

            half4 DOFrag(V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth+Normals for SSAO with normal reconstruction.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DNVert
            #pragma fragment DNFrag

            struct A { float3 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            V DNVert(A IN)
            {
                V OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 DNFrag(V IN) : SV_Target
            {
                half3 n = normalize(IN.normalWS);
                return half4(n * 0.5h + 0.5h, 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
