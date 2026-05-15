// CrosshairSDF — single fullscreen SDF reticle for v2 aim cursor.
// Renders 4-line crosshair + center dot + optional ring (reload/charge) procedurally.
// All visual params driven via MaterialPropertyBlock at runtime by CrosshairPresenter.
// Screen-Space Overlay canvas → ZTest disabled, transparent blending.
//
// Param namespace (Stage 1 baseline; later stages extend):
//   _Color       RGBA face tint
//   _Alpha       global opacity (fade-in/out for equip/unequip/dead)
//   _CenterPx    Vector4(centerX, centerY, screenW, screenH) — pixel coords
//   _Gap         pixels — distance from center to inner end of each arm
//   _LineLength  pixels — arm length
//   _LineThickness pixels
//   _DotRadius   pixels — center dot
//   _LinesHidden 0 = show, 1 = hide (reload phase)
//   _RingFill    0..1 — reload arc progress (0 = none, 1 = full circle)
//   _RingRadius  pixels — ring distance from center
//   _RingThickness pixels
//   _ChargeFill  0..1 — laser charge arc progress
//   _ChargeColor RGBA — charge arc tint (cyan)
//   _EdgeSoftness pixels — antialiasing edge width (Stage 1 fixed; Stage 3 will drive blur)

Shader "Crosshair/SDF"
{
    Properties
    {
        // Declared but unused — RawImage's CanvasRenderer probes _MainTex on assignment.
        _MainTex ("Texture (unused)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 1
        _CenterPx ("Center px (x,y,screenW,screenH)", Vector) = (640, 360, 1280, 720)
        _Gap ("Gap px", Float) = 6
        _LineLength ("Line Length px", Float) = 10
        _LineThickness ("Line Thickness px", Float) = 2
        _DotRadius ("Dot Radius px", Float) = 2
        _LinesHidden ("Lines Hidden", Float) = 0
        _RingFill ("Ring Fill (0..1)", Range(0,1)) = 0
        _RingRadius ("Ring Radius px", Float) = 42
        _RingThickness ("Ring Thickness px", Float) = 3
        _ChargeFill ("Charge Fill (0..1)", Range(0,1)) = 0
        _ChargeColorCold ("Charge Cold (inner / just-lit)", Color) = (1, 1, 1, 1)
        _ChargeColorMid ("Charge Mid (yellow)", Color) = (1, 0.85, 0.2, 1)
        _ChargeColorHot ("Charge Hot (outer / overheating)", Color) = (1, 0.3, 0.1, 1)
        _ChargeBarThicknessRatio ("Charge Bar Thickness × LineThickness", Range(0.2, 2)) = 0.7
        _EdgeSoftness ("Edge Softness px", Range(0.1, 4)) = 1
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 0.85)
        _OutlineWidth ("Outline Width px", Range(0, 6)) = 1.5
        _TopArmAlpha ("Top Arm Alpha (1=show, 0=hide for ADS)", Range(0, 1)) = 1
        _HitPulseProgress ("Hit Pulse Progress (0..1, 1=ended)", Range(0,1)) = 1
        _HitPulseColor ("Hit Pulse Color", Color) = (1, 0.25, 0.25, 1)
        _HitPulseInnerStart ("Hit Pulse Inner Start px", Float) = 12
        _HitPulseInnerEnd ("Hit Pulse Inner End px", Float) = 28
        _HitPulseLength ("Hit Pulse Stub Length px", Float) = 14
        _HitPulseThickness ("Hit Pulse Thickness px", Float) = 3
        _HitPulseRotationRad ("Hit Pulse Rotation rad (max)", Range(0, 1.5)) = 0.21
        _HitPulseThicknessTaperEnd ("Thickness end multiplier", Range(0.1, 2)) = 0.55
        _HitPulseThicknessTaperStart ("Thickness start multiplier", Range(0.5, 2)) = 1.05
        _HitPulseBurstPhaseEnd ("Burst phase end (0..1)", Range(0, 0.5)) = 0.12
        _HitPulseHoldPhaseEnd ("Hold phase end (0..1)", Range(0, 0.8)) = 0.30
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

            float4 _Color;
            float _Alpha;
            float4 _CenterPx;       // xy = center px, zw = screen px
            float _Gap;
            float _LineLength;
            float _LineThickness;
            float _DotRadius;
            float _LinesHidden;
            float _RingFill;
            float _RingRadius;
            float _RingThickness;
            float _ChargeFill;
            float4 _ChargeColorCold;
            float4 _ChargeColorMid;
            float4 _ChargeColorHot;
            float _ChargeBarThicknessRatio;
            float _EdgeSoftness;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _TopArmAlpha;
            float _HitPulseProgress;
            float4 _HitPulseColor;
            float _HitPulseInnerStart;
            float _HitPulseInnerEnd;
            float _HitPulseLength;
            float _HitPulseThickness;
            float _HitPulseRotationRad;
            float _HitPulseThicknessTaperEnd;
            float _HitPulseThicknessTaperStart;
            float _HitPulseBurstPhaseEnd;
            float _HitPulseHoldPhaseEnd;

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

            // SDF for axis-aligned line segment (vertical or horizontal arm).
            // segCenter: midpoint of segment, segHalfLen: along axis, segHalfThick: across axis.
            float sdAxisLine(float2 p, float2 segCenter, float halfLen, float halfThick, bool vertical)
            {
                float2 d = p - segCenter;
                float along = vertical ? d.y : d.x;
                float across = vertical ? d.x : d.y;
                float distAlong = max(abs(along) - halfLen, 0);
                float distAcross = abs(across) - halfThick;
                return max(distAcross, distAlong);
            }

            // SDF for filled circle.
            float sdCircle(float2 p, float2 c, float r)
            {
                return length(p - c) - r;
            }

            // SDF for line segment between two points з given thickness.
            float sdSegment(float2 p, float2 a, float2 b, float halfThick)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
                return length(pa - ba * h) - halfThick;
            }

            // SDF for ring arc — clockwise fill from top (12 o'clock) by 'fill' fraction.
            // Returns large value if pixel outside ring or beyond fill angle.
            float sdRingArc(float2 p, float2 c, float radius, float thickness, float fill)
            {
                if (fill <= 0.0001) return 1e6;
                float2 d = p - c;
                float r = length(d);
                float ringDist = abs(r - radius) - thickness * 0.5;

                // Angle from top, clockwise. atan2 returns (-PI..PI) for (y, x).
                // Top is +y in screen coords; we want clockwise progression from there.
                float ang = atan2(d.x, d.y); // 0 = up, PI/2 = right, PI = down, -PI = also down going left
                if (ang < 0) ang += 6.2831853; // normalize to [0..2PI]
                float fillAng = fill * 6.2831853;
                float arcMask = (ang <= fillAng) ? 0 : 1e6;

                return max(ringDist, arcMask);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 px = IN.uv * _CenterPx.zw;
                float2 center = _CenterPx.xy;

                // ── Main shapes (crosshair lines + dot + reload ring) share _Color
                float dMain = 1e6;

                if (_LinesHidden < 0.5)
                {
                    float halfThick = _LineThickness * 0.5;
                    float halfLen = _LineLength * 0.5;
                    float armCenterOffset = _Gap + halfLen;

                    // Top arm — binary on/off via _TopArmAlpha (ADS hides top arm).
                    if (_TopArmAlpha > 0.5)
                    {
                        dMain = min(dMain, sdAxisLine(px, center + float2(0, armCenterOffset), halfLen, halfThick, true));
                    }
                    dMain = min(dMain, sdAxisLine(px, center + float2(0, -armCenterOffset), halfLen, halfThick, true));
                    dMain = min(dMain, sdAxisLine(px, center + float2(armCenterOffset, 0), halfLen, halfThick, false));
                    dMain = min(dMain, sdAxisLine(px, center + float2(-armCenterOffset, 0), halfLen, halfThick, false));
                }
                if (_DotRadius > 0.01)
                    dMain = min(dMain, sdCircle(px, center, _DotRadius));

                float dRing = sdRingArc(px, center, _RingRadius, _RingThickness, _RingFill);
                dMain = min(dMain, dRing);

                // ── Charge fill — overlay on the 4 crosshair arm segments themselves.
                // Bar starts at arm inner edge (_Gap) and grows outward toward _Gap + _LineLength
                // proportional to chargeRatio. Same path as main arms — overlays them з flame color.
                // Top arm respects _TopArmAlpha (ADS hides it consistently).
                float dCharge = 1e6;
                half3 chargeGradient = (half3)_ChargeColorCold.rgb;
                if (_ChargeFill > 0.001)
                {
                    float fillLen = _LineLength * _ChargeFill;
                    float halfThick = _LineThickness * 0.5 * _ChargeBarThicknessRatio;

                    // 4 cardinal bars matching main arms (anchored at _Gap, length scales з chargeRatio)
                    if (_TopArmAlpha > 0.5)
                        dCharge = min(dCharge, sdSegment(px, center + float2(0,  _Gap), center + float2(0,  _Gap + fillLen), halfThick));
                    dCharge = min(dCharge, sdSegment(px, center + float2(0, -_Gap), center + float2(0, -_Gap - fillLen), halfThick));
                    dCharge = min(dCharge, sdSegment(px, center + float2( _Gap, 0), center + float2( _Gap + fillLen, 0), halfThick));
                    dCharge = min(dCharge, sdSegment(px, center + float2(-_Gap, 0), center + float2(-_Gap - fillLen, 0), halfThick));

                    // Along position: 0 at arm inner edge (cold), 1 at arm outer edge (hot)
                    float2 toPx = px - center;
                    float along;
                    if (abs(toPx.y) > abs(toPx.x))
                        along = (abs(toPx.y) - _Gap) / max(0.001, _LineLength);
                    else
                        along = (abs(toPx.x) - _Gap) / max(0.001, _LineLength);
                    along = saturate(along);

                    // Flame gradient: white (inner, near gap) → yellow (mid) → red (outer tip, hot)
                    if (along < 0.5)
                        chargeGradient = lerp((half3)_ChargeColorCold.rgb, (half3)_ChargeColorMid.rgb, along * 2.0);
                    else
                        chargeGradient = lerp((half3)_ChargeColorMid.rgb, (half3)_ChargeColorHot.rgb, (along - 0.5) * 2.0);
                }

                // ── Hit pulse — 4 diagonal stubs з 3-phase animation:
                //   1) Burst (0..BurstPhaseEnd): scale-up from inner anchor, full alpha
                //   2) Hold  (BurstPhaseEnd..HoldPhaseEnd): max alpha, slow drift outward
                //   3) Decay (HoldPhaseEnd..1.0): drift outward (ease-out) + rotation + fade + thickness taper
                float dPulse = 1e6;
                float pulseFade = 0;
                if (_HitPulseProgress < 1.0)
                {
                    float p = saturate(_HitPulseProgress);
                    float burstEnd = max(0.01, _HitPulseBurstPhaseEnd);
                    float holdEnd  = max(burstEnd + 0.01, _HitPulseHoldPhaseEnd);

                    // ── Phase-driven inner radius ──
                    float innerR;
                    if (p < burstEnd)
                    {
                        // Burst: snap out from half-radius to full inner anchor (ease-out cubic)
                        float bt = p / burstEnd;
                        float ease = 1.0 - pow(1.0 - bt, 3.0);
                        innerR = lerp(_HitPulseInnerStart * 0.5, _HitPulseInnerStart, ease);
                    }
                    else
                    {
                        // Hold + Decay: spread outward via EASE-OUT (visible while alpha still high)
                        float dt = (p - burstEnd) / (1.0 - burstEnd);
                        // Ease-out quad: 1 - (1-dt)² — fast at start, slow at end → spread happens EARLY у visible window.
                        float ease = 1.0 - (1.0 - dt) * (1.0 - dt);
                        innerR = lerp(_HitPulseInnerStart, _HitPulseInnerEnd, ease);
                    }

                    // Alpha — full hold then quadratic fade
                    if (p < holdEnd) pulseFade = 1.0;
                    else
                    {
                        float ft = (p - holdEnd) / max(0.001, 1.0 - holdEnd);
                        pulseFade = (1.0 - ft) * (1.0 - ft);
                    }

                    // Thickness taper — start at TaperStart × thickness, end at TaperEnd × thickness
                    float halfThick = _HitPulseThickness * 0.5 * lerp(_HitPulseThicknessTaperEnd, _HitPulseThicknessTaperStart, 1.0 - p);

                    // Rotation drift — stubs rotate by RotationRad over lifetime (clockwise fan).
                    float ang = p * _HitPulseRotationRad;
                    float c = cos(ang), s = sin(ang);
                    const float DIAG = 0.70710678;
                    // Base diagonal vectors rotated by +ang (45°, 135°, 225°, 315° → +ang each)
                    float2 d1 = float2( DIAG * c - DIAG * s,   DIAG * s + DIAG * c);   // 45° + ang
                    float2 d2 = float2(-DIAG * c - DIAG * s,  -DIAG * s + DIAG * c);   // 135° + ang
                    float2 d3 = float2(-DIAG * c + DIAG * s,  -DIAG * s - DIAG * c);   // 225° + ang
                    float2 d4 = float2( DIAG * c + DIAG * s,   DIAG * s - DIAG * c);   // 315° + ang

                    float outerR = innerR + _HitPulseLength;
                    dPulse = min(dPulse, sdSegment(px, center + d1 * innerR, center + d1 * outerR, halfThick));
                    dPulse = min(dPulse, sdSegment(px, center + d2 * innerR, center + d2 * outerR, halfThick));
                    dPulse = min(dPulse, sdSegment(px, center + d3 * innerR, center + d3 * outerR, halfThick));
                    dPulse = min(dPulse, sdSegment(px, center + d4 * innerR, center + d4 * outerR, halfThick));
                }

                // ── Face + outline alphas per group
                float faceMain    = 1.0 - smoothstep(0.0, _EdgeSoftness, dMain);
                float totalMain   = 1.0 - smoothstep(_OutlineWidth, _OutlineWidth + _EdgeSoftness, dMain);
                float outlineMain = max(0.0, totalMain - faceMain);

                float faceCharge    = 1.0 - smoothstep(0.0, _EdgeSoftness, dCharge);
                float totalCharge   = 1.0 - smoothstep(_OutlineWidth, _OutlineWidth + _EdgeSoftness, dCharge);
                float outlineCharge = max(0.0, totalCharge - faceCharge);

                float facePulse    = (1.0 - smoothstep(0.0, _EdgeSoftness, dPulse)) * pulseFade;
                float totalPulse   = (1.0 - smoothstep(_OutlineWidth, _OutlineWidth + _EdgeSoftness, dPulse)) * pulseFade;
                float outlinePulse = max(0.0, totalPulse - facePulse);

                // ── Composite: outline layer (back) → face layer (front)
                half4 col = half4(0, 0, 0, 0);

                // Combined outline (max of all groups' outline contribution)
                float outlineAlpha = max(max(outlineMain, outlineCharge), outlinePulse);
                col.rgb = _OutlineColor.rgb;
                col.a = outlineAlpha * _OutlineColor.a;

                // Main face over outline
                col.rgb = lerp(col.rgb, _Color.rgb, faceMain);
                col.a   = max(col.a, faceMain * _Color.a);

                // Charge face — flame gradient (white inner → yellow → red outer)
                col.rgb = lerp(col.rgb, chargeGradient, faceCharge);
                col.a   = max(col.a, faceCharge);

                // Hit pulse face — last layer, on top of everything.
                col.rgb = lerp(col.rgb, _HitPulseColor.rgb, facePulse);
                col.a   = max(col.a, facePulse * _HitPulseColor.a);

                col.a *= _Alpha;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
