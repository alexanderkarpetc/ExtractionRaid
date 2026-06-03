// NpcQuestIcon — procedural SDF badge для floating quest indicator above NPCs.
// One full-quad UI shader: circular frame (face + outline) з a chunky "!" mark inside,
// plus a soft outer glow that breathes на _PulseT. No textures, no sprite atlas.
//
// Mesh: any Image/RawImage RectTransform — UVs 0..1 mapped to the badge area.
// Aspect ratio is preserved internally (we read _Aspect = width/height and remap UVs
// to a centered unit space so the disc stays circular regardless of rect shape).
//
// Param namespace:
//   _BorderColor RGBA — disc outline / stroke
//   _FillColor   RGBA — dark interior фоn behind the "!"
//   _MarkColor   RGBA — color of "!" body + dot
//   _GlowColor   RGBA — outer breathing glow
//   _Alpha       global opacity
//   _Aspect      width / height of the host RectTransform (sent from C#)
//   _BorderWidth UV-units stroke thickness
//   _DiscRadius  UV-units radius of the frame
//   _EdgeSoftness UV-units anti-aliasing band
//   _PulseT      0..1 — drives glow breathe (typical: 0.5 + 0.5*sin(Time*PI))
//   _GlowStrength 0..1 — peak glow alpha at pulse=1
//   _GlowRadius  UV-units — how far the glow extends past the border

Shader "UI/NpcQuestIcon"
{
    Properties
    {
        _MainTex      ("Texture (unused)", 2D) = "white" {}
        _BorderColor  ("Border Color", Color) = (1.0, 0.82, 0.15, 1)
        _FillColor    ("Fill Color",   Color) = (0.08, 0.08, 0.10, 0.95)
        _MarkColor    ("Mark Color (mid)",   Color) = (1.0, 0.85, 0.10, 1)
        _MarkTopColor ("Mark Color (top hi)", Color) = (1.0, 1.0, 0.55, 1)
        _MarkBotColor ("Mark Color (bottom shadow)", Color) = (0.55, 0.35, 0.05, 1)
        _MarkOutlineColor ("Mark Outline", Color) = (0.05, 0.04, 0.0, 1)
        _MarkOutlineWidth ("Mark Outline Width", Range(0, 0.05)) = 0.012
        _GlowColor    ("Glow Color",   Color) = (1.0, 0.82, 0.15, 1)
        _Alpha        ("Alpha", Range(0,1)) = 1
        _Aspect       ("Aspect (w/h)", Float) = 1.1
        _BorderWidth  ("Border Width", Range(0, 0.2)) = 0.055
        _DiscRadius   ("Disc Radius", Range(0.1, 0.5)) = 0.44
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.04)) = 0.006
        _PulseT       ("Pulse T (0..1)", Range(0,1)) = 0
        _GlowStrength ("Glow Strength", Range(0,1)) = 0.55
        _GlowRadius   ("Glow Radius", Range(0, 0.4)) = 0.18

        // Stencil props — required so the shader can be used by UI.Image under a Mask.
        _StencilComp  ("Stencil Comparison", Float) = 8
        _Stencil      ("Stencil ID", Float) = 0
        _StencilOp    ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask    ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BorderColor;
            float4 _FillColor;
            float4 _MarkColor;
            float4 _MarkTopColor;
            float4 _MarkBotColor;
            float4 _MarkOutlineColor;
            float  _MarkOutlineWidth;
            float4 _GlowColor;
            float  _Alpha;
            float  _Aspect;
            float  _BorderWidth;
            float  _DiscRadius;
            float  _EdgeSoftness;
            float  _PulseT;
            float  _GlowStrength;
            float  _GlowRadius;

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

            // Rounded box SDF — p is local point, b is half-extents, r is corner radius.
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 d = abs(p) - b + r;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            float sdCircle(float2 p, float r)
            {
                return length(p) - r;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Remap UV to a centered space where Y ∈ [-0.5, 0.5] and X scaled by aspect.
                // This keeps the disc geometrically circular regardless of rect aspect.
                float2 uv = IN.uv - 0.5;
                uv.x *= _Aspect;

                // Disc — centered, radius _DiscRadius. Leaves outer margin for glow.
                float dDisc = sdCircle(uv, _DiscRadius);

                // Frame border = annulus of width _BorderWidth around the disc edge.
                float dBorder = abs(dDisc) - _BorderWidth * 0.5;

                // Inner fill = inside the disc минус the border ring.
                float dFill = dDisc + _BorderWidth * 0.5;

                // ── "!" mark — chunky stylized exclamation, centered in the disc.
                const float MARK_CY = 0.0;
                float2 pMark = float2(uv.x, uv.y - MARK_CY);

                // Bar — rounded box, slightly wider than tall stripe. Big and bold так
                // it reads from distance like the reference screenshot.
                const float BAR_HALF_W = 0.07;
                const float BAR_HALF_H = 0.18;
                const float BAR_ROUND  = 0.055;
                float2 pBar = float2(pMark.x, pMark.y - 0.09); // bar above mid
                float dBar = sdRoundedBox(pBar, float2(BAR_HALF_W, BAR_HALF_H), BAR_ROUND);

                // Dot — bold circle below the bar з a clear gap.
                const float DOT_R = 0.085;
                float2 pDot = float2(pMark.x, pMark.y + 0.20);
                float dDot = sdCircle(pDot, DOT_R);

                float dMark = min(dBar, dDot);

                // Clip the mark to inside the disc (don't render bits poking out).
                float dMarkClipped    = max(dMark, dDisc + _BorderWidth);
                // Outline ring around the mark — slightly larger silhouette.
                float dMarkOutline    = max(dMark + _MarkOutlineWidth, dDisc + _BorderWidth);

                // Vertical gradient across the mark's bounding span so it reads as 3D:
                // bright highlight at the top of the bar, mid-tone through middle,
                // shadow toward the bottom of the dot. t in [0..1] from top→bottom.
                // UV.y increases upward, so markTop has the larger Y value.
                float markTopY    =  0.30 + MARK_CY;   // approx Y of bar top
                float markBotY    = -0.32 + MARK_CY;   // approx Y of dot bottom
                float t = saturate((markTopY - uv.y) / max(markTopY - markBotY, 1e-4));
                // Two-stop ramp: top→mid in first half, mid→bottom in second half.
                half3 markCol = t < 0.5
                    ? lerp((half3)_MarkTopColor.rgb, (half3)_MarkColor.rgb, t * 2.0)
                    : lerp((half3)_MarkColor.rgb, (half3)_MarkBotColor.rgb, (t - 0.5) * 2.0);

                // Specular sliver — bright highlight on the bar's upper-left edge.
                // Cheap fake: distance from a vertical line on the left side, gated by Y.
                float specMask = saturate(1.0 - smoothstep(0.0, 0.025, abs(pBar.x + BAR_HALF_W * 0.55)))
                               * saturate(smoothstep(-0.05, 0.10, pBar.y))
                               * step(dBar, 0.0); // only inside the bar, upper portion
                markCol = lerp(markCol, half3(1.0, 1.0, 0.92), specMask * 0.6);

                // ── Coverage masks (smoothstep AA).
                float aaFill        = 1.0 - smoothstep(0.0, _EdgeSoftness, dFill);
                float aaBorder      = 1.0 - smoothstep(0.0, _EdgeSoftness, dBorder);
                float aaMark        = 1.0 - smoothstep(0.0, _EdgeSoftness, dMarkClipped);
                float aaMarkOutline = 1.0 - smoothstep(0.0, _EdgeSoftness, dMarkOutline);

                // ── Outer glow — soft falloff outside the disc edge.
                // Visible only where outside the shape (dDisc > 0).
                float glowReach = max(_GlowRadius, 1e-4);
                float glow = 1.0 - smoothstep(0.0, glowReach, dDisc);
                glow = pow(saturate(glow), 1.8);                     // gentle falloff
                glow *= saturate(1.0 - aaBorder - aaFill);           // don't double-shade the body
                glow *= _GlowStrength * saturate(_PulseT);

                // ── Composite: glow (back) → fill → mark → border (front, sharpest).
                half4 col = half4(0, 0, 0, 0);

                // Glow layer
                col.rgb = _GlowColor.rgb;
                col.a   = glow * _GlowColor.a;

                // Fill behind everything
                col.rgb = lerp(col.rgb, _FillColor.rgb, aaFill);
                col.a   = max(col.a, aaFill * _FillColor.a);

                // Mark outline (dark halo) — sits between fill and mark face.
                col.rgb = lerp(col.rgb, _MarkOutlineColor.rgb, aaMarkOutline);
                col.a   = max(col.a, aaMarkOutline * _MarkOutlineColor.a);

                // Mark face — gradient yellow з spec sliver.
                col.rgb = lerp(col.rgb, markCol, aaMark);
                col.a   = max(col.a, aaMark * _MarkColor.a);

                // Border last — it reads as the strongest silhouette.
                col.rgb = lerp(col.rgb, _BorderColor.rgb, aaBorder);
                col.a   = max(col.a, aaBorder * _BorderColor.a);

                col.a *= _Alpha * IN.color.a;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
