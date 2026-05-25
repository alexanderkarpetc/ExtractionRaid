// StatusEffectIcon — procedural SDF icon for status effect tiles. One shader, multiple
// shape branches via _IconShape int. Used by worldspace WorldStatusIcons cells; could be
// reused by HUD tiles too if we drop emoji glyphs later.
//
// Shapes:
//   0 = blood drop (bleed L1, L2 — color encodes severity)
//   1 = X cross    (fracture, future)
//   2 = spiral     (pain, future)

Shader "BattleHud/StatusEffectIcon"
{
    Properties
    {
        _MainTex ("Texture (unused — Image requires it)", 2D) = "white" {}
        _BgColor   ("Background color", Color) = (0.59, 0.12, 0.12, 0.85)
        _FgColor   ("Foreground (glyph) color", Color) = (1, 0.3, 0.3, 1)
        _OutlineColor ("Outline color", Color) = (0, 0, 0, 0.85)
        _OutlineWidth ("Outline width (UV units)", Range(0, 0.15)) = 0.04
        _EdgeSoftness ("Edge softness", Range(0.001, 0.05)) = 0.01
        _CornerRadius ("Tile corner radius (UV units)", Range(0, 0.5)) = 0.12
        _IconShape ("Icon shape (0=drop, 1=cross, 2=spiral)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _BgColor;
            float4 _FgColor;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _EdgeSoftness;
            float _CornerRadius;
            float _IconShape;

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

            // SDF rounded box (centered) — used for tile background shape.
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // SDF circle — primitive used by drop composition.
            float sdCircle(float2 p, float r) { return length(p) - r; }

            // SDF blood-drop — circle bottom + triangular taper top.
            // p in [-0.5, +0.5] UV-centered. Returns negative inside.
            float sdDrop(float2 p)
            {
                // Bottom blob — circle centered slightly below mid.
                float bottom = sdCircle(p - float2(0, -0.1), 0.28);
                // Top taper — triangle apex at top, base merges with circle.
                // Use linear distance to a slanted line on each side of x=0.
                float topY = p.y - 0.05;
                float halfW = lerp(0.28, 0.0, saturate(topY / 0.35));
                float dx = abs(p.x) - halfW;
                float top = max(dx, -topY); // inside if topY > 0 AND |x| < halfW
                // Only count top when above center.
                if (p.y < -0.05) top = 1e6;
                return min(bottom, top);
            }

            // SDF cross (X) — for fracture.
            float sdCross(float2 p, float thickness)
            {
                float a = abs(p.x + p.y);
                float b = abs(p.x - p.y);
                return max(a, b) - thickness * 1.414;
            }

            // Pick shape SDF by index.
            float sampleShape(float2 p)
            {
                int shape = (int)(_IconShape + 0.5);
                if (shape == 1) return sdCross(p, 0.06);
                // Default: drop (also used by shape 2 spiral until implemented).
                return sdDrop(p);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // p in [-0.5, +0.5] centered, y up.
                float2 p = IN.uv - 0.5;

                // Tile background — rounded box covering the cell minus outline allowance.
                float bgSdf = sdRoundedBox(p, float2(0.5, 0.5) - _OutlineWidth, _CornerRadius);
                float bgFace = 1.0 - smoothstep(0.0, _EdgeSoftness, bgSdf);
                float bgOutline = 1.0 - smoothstep(_OutlineWidth, _OutlineWidth + _EdgeSoftness, bgSdf);
                float bgOutlineAlpha = max(0.0, bgOutline - bgFace);

                // Foreground shape (glyph).
                float fgSdf = sampleShape(p);
                float fgFace = 1.0 - smoothstep(0.0, _EdgeSoftness, fgSdf);

                if (bgFace + bgOutlineAlpha + fgFace < 0.001) discard;

                half4 col = half4(0, 0, 0, 0);
                // Outline behind.
                col.rgb = _OutlineColor.rgb;
                col.a = bgOutlineAlpha * _OutlineColor.a;
                // Bg face.
                col.rgb = lerp(col.rgb, _BgColor.rgb, bgFace);
                col.a = max(col.a, bgFace * _BgColor.a);
                // Glyph on top (fg color), only inside bg.
                float glyphMask = fgFace * bgFace;
                col.rgb = lerp(col.rgb, _FgColor.rgb, glyphMask);
                col.a = max(col.a, glyphMask * _FgColor.a);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
