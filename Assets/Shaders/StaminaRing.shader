// StaminaRing — procedural SDF radial gauge for the worldspace stamina ring under/beside
// the player. Annular ring (donut) with a gray "track" + a filled arc that grows clockwise
// from 12 o'clock. Fill color lerps across a 3-stop gradient (green → orange → red) by
// fill ratio, boosted to HDR for a juicy pop through URP tonemap. Dark outline around the
// donut. Exhaustion blink + global fade driven from C# (WorldStaminaRing).
//
// Renders ON TOP of world geometry (ZTest Always) so the ring never sinks into the ground /
// walls. All values pushed per-frame via the per-instance material (Image.material instances).

Shader "BattleHud/StaminaRing"
{
    Properties
    {
        _MainTex ("Texture (unused — Image requires it)", 2D) = "white" {}
        _FillRatio ("Fill ratio (0..1)", Range(0,1)) = 1
        _Radius ("Ring radius (UV units)", Range(0.1, 0.5)) = 0.36
        _Thickness ("Ring thickness (UV units)", Range(0.01, 0.3)) = 0.11
        _EdgeSoftness ("Edge softness", Range(0.001, 0.05)) = 0.012
        _TrackColor ("Track (empty) color", Color) = (0.18, 0.18, 0.20, 0.7)
        _ColorHigh ("Fill color — high", Color) = (0.25, 1.0, 0.40, 1)
        _ColorMid  ("Fill color — mid",  Color) = (1.0, 0.62, 0.08, 1)
        _ColorLow  ("Fill color — low",  Color) = (1.0, 0.18, 0.14, 1)
        _FillIntensity ("Fill HDR intensity", Range(1, 3)) = 1.5
        _OutlineColor ("Outline color", Color) = (0.02, 0.02, 0.03, 0.95)
        _OutlineWidth ("Outline width (UV units)", Range(0, 0.1)) = 0.028
        _Blink ("Exhaust blink (0..1)", Range(0,1)) = 0
        _BlinkMinAlpha ("Blink min alpha (dim point)", Range(0, 1)) = 0.25
        _GlobalAlpha ("Global alpha (fade)", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest Always          // draw over world geometry — never sinks into ground/walls
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _FillRatio;
            float _Radius;
            float _Thickness;
            float _EdgeSoftness;
            float4 _TrackColor;
            float4 _ColorHigh;
            float4 _ColorMid;
            float4 _ColorLow;
            float _FillIntensity;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _Blink;
            float _BlinkMinAlpha;
            float _GlobalAlpha;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            // 3-stop fill color: red (0) → orange (0.5) → green (1).
            float3 FillColor(float r)
            {
                float3 lo = lerp(_ColorLow.rgb, _ColorMid.rgb, saturate(r * 2.0));
                float3 hi = lerp(_ColorMid.rgb, _ColorHigh.rgb, saturate((r - 0.5) * 2.0));
                return r < 0.5 ? lo : hi;
            }

            #define PI 3.14159265

            half4 Frag(Varyings IN) : SV_Target
            {
                // Centered coords, y up.
                float2 p = IN.uv - 0.5;
                float dist = length(p);

                // Annular ring SDF: distance to the ring centerline minus half-thickness.
                // ringSdf < 0 = inside the donut body.
                float ringSdf = abs(dist - _Radius) - _Thickness * 0.5;
                float ringFace = 1.0 - smoothstep(0.0, _EdgeSoftness, ringSdf);

                // Outline = a band hugging the donut's outer + inner edges (the donut grown by
                // _OutlineWidth, minus the donut face itself).
                float grownFace   = 1.0 - smoothstep(0.0, _EdgeSoftness, ringSdf - _OutlineWidth);
                float outlineFace = saturate(grownFace - ringFace);

                if ((ringFace + outlineFace) * _GlobalAlpha < 0.002) discard;

                // Angle: 0 at top (12 o'clock), increasing clockwise, normalized to [0,1).
                float ang = atan2(p.x, p.y);     // top = 0, right = +pi/2 (clockwise)
                float frac = ang / (2.0 * PI);
                if (frac < 0.0) frac += 1.0;

                // Soft boundary between filled arc and empty track.
                float fillEdge = 0.012;
                float filled = 1.0 - smoothstep(_FillRatio, _FillRatio + fillEdge, frac);
                if (_FillRatio >= 0.999) filled = 1.0; // full → whole ring filled

                // Donut content: track (gray) → HDR-boosted fill color by ratio.
                float3 fillCol  = FillColor(_FillRatio) * _FillIntensity;
                float3 donutCol = lerp(_TrackColor.rgb, fillCol, filled);
                float  donutA   = lerp(_TrackColor.a,   1.0,     filled);

                // Compose: outline behind, donut content on top (overwrites where present).
                float3 col = _OutlineColor.rgb;
                float  a   = outlineFace * _OutlineColor.a;
                col = lerp(col, donutCol, ringFace);
                a   = max(a, donutA * ringFace);

                // Exhaustion blink — gentle opacity pulse (up/down) over the whole ring,
                // instead of an aggressive red flash. _Blink is a 0..1 sine from C#
                // (0 when not exhausted → multiplier stays 1, no change).
                a *= lerp(1.0, _BlinkMinAlpha, _Blink);

                return half4(col, a * _GlobalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
