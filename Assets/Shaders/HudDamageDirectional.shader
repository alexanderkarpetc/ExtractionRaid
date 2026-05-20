// HudDamageDirectional — fullscreen vignette pulse + low-HP edge glow.
// Two compositing layers:
//   1. Directional pulse — up to 4 concurrent hit slots, each (angle, intensity). Renders
//      a sector arc on the screen edge centered on the hit direction.
//   2. Low-HP glow      — non-directional all-edges layer, intensity modulated by heartbeat
//                         (sine) computed externally and pushed as _LowHpGlow.
//
// Single fullscreen RawImage on Screen-Space Overlay canvas. ZTest Always, transparent blend.
//
// Driven entirely by HudDamagePresenter via Image.material accessor (auto-instanced).

Shader "HudDamage/DirectionalVignette"
{
    Properties
    {
        // Required for RawImage CanvasRenderer probe even though unused.
        _MainTex ("Texture (unused)", 2D) = "white" {}
        _BaseColor ("Base color (HDR red)", Color) = (1, 0.15, 0.15, 0.85)
        _InnerRadius ("Inner radius (0..1)", Range(0, 0.9)) = 0.42
        _EdgeSoftness ("Edge softness", Range(0.01, 0.5)) = 0.12
        _SectorHalfWidthRad ("Sector half-width (rad)", Float) = 0.6981317
        _AspectRatio ("Aspect ratio (w/h)", Float) = 1.7777778
        _LowHpGlow ("Low-HP glow intensity", Range(0, 1)) = 0
        _HitSlot0 ("Hit slot 0 (angleRad, intensity, _, _)", Vector) = (0, 0, 0, 0)
        _HitSlot1 ("Hit slot 1", Vector) = (0, 0, 0, 0)
        _HitSlot2 ("Hit slot 2", Vector) = (0, 0, 0, 0)
        _HitSlot3 ("Hit slot 3", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BaseColor;
            float _InnerRadius;
            float _EdgeSoftness;
            float _SectorHalfWidthRad;
            float _AspectRatio;
            float _LowHpGlow;
            float4 _HitSlot0;
            float4 _HitSlot1;
            float4 _HitSlot2;
            float4 _HitSlot3;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // Wrapped shortest angular distance (rad) between two angles in [0, 2PI].
            float angularDelta(float a, float b)
            {
                float d = abs(a - b);
                return min(d, 6.2831853 - d);
            }

            // Per-slot contribution: angular gate × radial gate × intensity.
            // slot.x = angleRad, slot.y = intensity (already fade-multiplied).
            float slotContribution(float4 slot, float pixelAngle, float radialGate)
            {
                if (slot.y < 0.001) return 0;
                float angDelta = angularDelta(pixelAngle, slot.x);
                float halfW = max(0.05, _SectorHalfWidthRad);
                float soft = max(0.001, _EdgeSoftness * 3.0); // multiply since softness in radians scale
                float angularGate = 1.0 - smoothstep(halfW - soft, halfW, angDelta);
                return slot.y * angularGate * radialGate;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Normalized -1..+1 screen coords. Two views of `p`:
                //   - rawP        — UV-space, NOT aspect-corrected. Used для Chebyshev radial gate.
                //                    max(|x|, |y|) gives a square gate aligned з screen edges, so
                //                    top/bottom/left/right are equally "fully on" at the rim.
                //   - aspectP     — aspect-corrected, only used для angle calc, so a sector arc
                //                    points to a real-world bearing instead of an aspect-skewed one.
                float2 rawP = (IN.uv - 0.5) * 2.0;
                float2 aspectP = float2(rawP.x * _AspectRatio, rawP.y);

                float dist = max(abs(rawP.x), abs(rawP.y));
                float radialGate = smoothstep(_InnerRadius, 1.0, dist);
                if (radialGate < 0.001) discard;

                // Pixel angle in screen space [-PI..PI] then normalize to [0..2PI].
                float pixelAngle = atan2(aspectP.y, aspectP.x);
                if (pixelAngle < 0) pixelAngle += 6.2831853;

                // ── Directional layer — composite up to 4 slots via max() blend.
                float directional = 0;
                directional = max(directional, slotContribution(_HitSlot0, pixelAngle, radialGate));
                directional = max(directional, slotContribution(_HitSlot1, pixelAngle, radialGate));
                directional = max(directional, slotContribution(_HitSlot2, pixelAngle, radialGate));
                directional = max(directional, slotContribution(_HitSlot3, pixelAngle, radialGate));

                // ── Low-HP layer — all-edges, no angular gate. Intensity driven externally.
                float lowHp = _LowHpGlow * radialGate;

                // Combine: max blend so layers coexist (low-HP doesn't drown the directional kick).
                float finalAlpha = max(directional, lowHp) * _BaseColor.a;

                if (finalAlpha < 0.001) discard;
                return half4(_BaseColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
