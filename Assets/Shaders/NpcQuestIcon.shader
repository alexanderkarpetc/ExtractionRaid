// NpcQuestIcon — procedural SDF badge для floating quest indicator above NPCs.
// One full-quad UI shader: rounded triangle frame (face + outline) з "!" mark inside,
// plus a soft outer glow that breathes на _PulseT. No textures, no sprite atlas.
//
// Mesh: any Image/RawImage RectTransform — UVs 0..1 mapped to the badge area.
// Aspect ratio is preserved internally (we read _Aspect = width/height and remap UVs
// to a centered unit space so the triangle stays equilateral-ish regardless of rect).
//
// Param namespace:
//   _BorderColor RGBA — triangle outline / stroke
//   _FillColor   RGBA — dark interior фоn behind the "!"
//   _MarkColor   RGBA — color of "!" body + dot
//   _GlowColor   RGBA — outer breathing glow
//   _Alpha       global opacity
//   _Aspect      width / height of the host RectTransform (sent from C#)
//   _BorderWidth UV-units stroke thickness
//   _CornerRadius UV-units rounding on triangle vertices
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
        _MarkColor    ("Mark Color",   Color) = (1.0, 0.95, 0.55, 1)
        _GlowColor    ("Glow Color",   Color) = (1.0, 0.82, 0.15, 1)
        _Alpha        ("Alpha", Range(0,1)) = 1
        _Aspect       ("Aspect (w/h)", Float) = 1.1
        _BorderWidth  ("Border Width", Range(0, 0.2)) = 0.045
        _CornerRadius ("Corner Radius", Range(0, 0.2)) = 0.06
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
            float4 _GlowColor;
            float  _Alpha;
            float  _Aspect;
            float  _BorderWidth;
            float  _CornerRadius;
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

            // Inigo Quilez — isoceles triangle SDF. Apex at origin, base centered below.
            // q = (halfBase, -height). Returns signed distance (negative inside).
            float sdIsoTriangle(float2 p, float2 q)
            {
                p.x = abs(p.x);
                float2 a = p - q * clamp(dot(p, q) / max(dot(q, q), 1e-6), 0.0, 1.0);
                float2 b = p - q * float2(clamp(p.x / max(q.x, 1e-6), 0.0, 1.0), 1.0);
                float k = sign(q.y);
                float d = min(dot(a, a), dot(b, b));
                float s = max(k * (p.x * q.y - p.y * q.x), k * (p.y - q.y));
                return sqrt(d) * sign(s);
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
                // This keeps the triangle's geometry independent of the rect's pixel aspect.
                float2 uv = IN.uv - 0.5;
                uv.x *= _Aspect;

                // Triangle: apex high (positive Y), base low (negative Y).
                // Tip at (0, +0.42), half-base = 0.46, height = 0.84 — leaves margin for glow.
                const float2 APEX = float2(0.0, 0.42);
                const float HALF_BASE = 0.46;
                const float HEIGHT    = 0.84;

                float2 pTri = uv - APEX;
                float dTri = sdIsoTriangle(pTri, float2(HALF_BASE, -HEIGHT));

                // Inflate corners by subtracting CornerRadius — same trick as rounded box.
                // This rounds the triangle's tips without warping the edges.
                float dTriRounded = dTri - _CornerRadius;

                // Frame border = annulus of width _BorderWidth around the rounded triangle edge.
                float dBorder = abs(dTriRounded) - _BorderWidth * 0.5;

                // Inner fill = inside the triangle, минус the border ring.
                float dFill = dTriRounded + _BorderWidth * 0.5;

                // ── "!" mark, positioned near the triangle's visual centroid (about 1/3 up from base).
                // Centroid of triangle relative to apex/base: y = APEX.y - HEIGHT/3 = 0.42 - 0.28 = 0.14
                // Shift the mark slightly above that so it reads visually centered (eye favors top).
                const float MARK_CY = 0.05;
                float2 pMark = float2(uv.x, uv.y - MARK_CY);

                // Bar — vertical rounded rect.
                float barHalfW = 0.045;
                float barHalfH = 0.135;
                float barRound = 0.035;
                float2 pBar = float2(pMark.x, pMark.y - 0.065); // bar sits above mid
                float dBar = sdRoundedBox(pBar, float2(barHalfW, barHalfH), barRound);

                // Dot — circle below the bar.
                float dotR = 0.05;
                float2 pDot = float2(pMark.x, pMark.y + 0.13);
                float dDot = sdCircle(pDot, dotR);

                float dMark = min(dBar, dDot);

                // Clip the mark to inside the triangle (don't render bits that poke through edges).
                float dMarkClipped = max(dMark, dTriRounded + _BorderWidth);

                // ── Coverage masks (smoothstep AA).
                float aaFill   = 1.0 - smoothstep(0.0, _EdgeSoftness, dFill);
                float aaBorder = 1.0 - smoothstep(0.0, _EdgeSoftness, dBorder);
                float aaMark   = 1.0 - smoothstep(0.0, _EdgeSoftness, dMarkClipped);

                // ── Outer glow — soft falloff outside the triangle edge.
                // Visible only where outside the shape (dTriRounded > 0).
                float glowReach = max(_GlowRadius, 1e-4);
                float glow = 1.0 - smoothstep(0.0, glowReach, dTriRounded);
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

                // Mark on top of fill
                col.rgb = lerp(col.rgb, _MarkColor.rgb, aaMark);
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
