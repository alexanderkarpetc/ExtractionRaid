Shader "UI/HealthBarFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Fill ("Fill", Range(0, 1)) = 1
        _TrailFill ("Trail Fill", Range(0, 1)) = 1
        _FlashT ("Flash Phase", Range(0, 1)) = 1
        _SegmentCount ("Segment Count", Float) = 10
        _BarColor ("Bar Color", Color) = (0.2, 0.85, 0.2, 1)
        _TrailColor ("Trail Color", Color) = (0.8, 0.15, 0.1, 1)
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _BgColor ("Background Color", Color) = (0.12, 0.12, 0.12, 0.85)
        _SegmentLineColor ("Segment Line Color", Color) = (0, 0, 0, 0.4)
        _SegmentLineWidth ("Segment Line Width", Float) = 0.012
        _PaddingX ("Padding X (UV fraction)", Float) = 0.02
        _PaddingY ("Padding Y (UV fraction)", Float) = 0.25
        _FlashExpandX ("Flash Expand X", Float) = 0.015
        _FlashExpandY ("Flash Expand Y", Float) = 0.2
        _FlashPower ("Flash Power", Float) = 2.0
        _BorderSize ("Border Size (UV fraction)", Float) = 0.04
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "HealthBar"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float _Fill;
            float _TrailFill;
            float _FlashT;
            float _SegmentCount;
            float4 _BarColor;
            float4 _TrailColor;
            float4 _FlashColor;
            float4 _BgColor;
            float4 _SegmentLineColor;
            float _SegmentLineWidth;
            float _PaddingX;
            float _PaddingY;
            float _FlashExpandX;
            float _FlashExpandY;
            float _FlashPower;
            float _BorderSize;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // Bar bounds in UV space (center region, padding around it)
                float barMinX = _PaddingX;
                float barMaxX = 1.0 - _PaddingX;
                float barMinY = _PaddingY;
                float barMaxY = 1.0 - _PaddingY;

                // Flash intensity (configurable power curve)
                float flash = pow(saturate(1.0 - _FlashT), _FlashPower);

                // Expand bounds during flash (trail zone only)
                float expandX = _FlashExpandX * flash;
                float expandY = _FlashExpandY * flash;

                // Remap uv.x to bar-local 0..1
                float barW = barMaxX - barMinX;
                float barX = (uv.x - barMinX) / barW; // 0..1 within bar

                // Check if pixel is in the bar region (Y axis)
                bool inBarY = uv.y >= barMinY && uv.y <= barMaxY;

                // Check if pixel is in the expanded flash region (Y axis)
                bool inExpandedY = uv.y >= (barMinY - expandY) && uv.y <= (barMaxY + expandY);

                // Check X bounds: bar region vs expanded
                bool inBarX = uv.x >= barMinX && uv.x <= barMaxX;
                bool inExpandedX = uv.x >= (barMinX - expandX) && uv.x <= (barMaxX + expandX);

                // Border bounds — bg color extends beyond bar by _BorderSize
                float borderMinX = barMinX - _BorderSize;
                float borderMaxX = barMaxX + _BorderSize;
                float borderMinY = barMinY - _BorderSize;
                float borderMaxY = barMaxY + _BorderSize;
                bool inBorder = uv.x >= borderMinX && uv.x <= borderMaxX
                             && uv.y >= borderMinY && uv.y <= borderMaxY;

                // Fully outside border AND expanded flash bounds → transparent
                if (!inBorder && (!inExpandedY || !inExpandedX))
                    return half4(0, 0, 0, 0);

                // Flash expansion zone (outside bar, inside expanded) — glow above flash
                bool inExpansionZone = !inBarY || !inBarX;

                if (inExpansionZone)
                {
                    // Try flash glow first (renders on top of border)
                    float nearBarX = clamp(barX, 0.0, 1.0);
                    if (nearBarX >= _Fill && nearBarX <= _TrailFill && flash > 0.01
                        && inExpandedY && inExpandedX)
                    {
                        float distY = 0;
                        if (uv.y < barMinY) distY = (barMinY - uv.y) / max(expandY, 0.001);
                        if (uv.y > barMaxY) distY = (uv.y - barMaxY) / max(expandY, 0.001);

                        float distX = 0;
                        if (uv.x < barMinX) distX = (barMinX - uv.x) / max(expandX, 0.001);
                        if (uv.x > barMaxX) distX = (uv.x - barMaxX) / max(expandX, 0.001);

                        float edgeFade = saturate(1.0 - max(distY, distX));
                        edgeFade = edgeFade * edgeFade;

                        half4 flashCol = _FlashColor;
                        flashCol.a *= edgeFade;
                        return flashCol * i.color;
                    }

                    // Border (bg color around the bar)
                    if (inBorder)
                        return _BgColor * i.color;

                    return half4(0, 0, 0, 0);
                }

                // ── Inside bar bounds ──────────────────────────
                half4 col;

                // Zone 1: Background (empty area beyond trail)
                if (barX > _TrailFill)
                {
                    col = _BgColor;
                }
                // Zone 2: Damage trail (between current fill and trail)
                // Flash covers a shrinking portion of the trail zone (no alpha fade)
                else if (barX > _Fill)
                {
                    float trailWidth = _TrailFill - _Fill;
                    float flashEdge = _Fill + trailWidth * flash; // shrinks from trail end toward fill
                    col = barX < flashEdge ? _FlashColor : _TrailColor;
                }
                // Zone 3: Health fill
                else
                {
                    col = _BarColor;

                    // Segment lines (skip when too many segments — they'd be sub-pixel noise)
                    if (_SegmentCount > 1.0 && _SegmentCount < 40.0)
                    {
                        float segUV = barX * _SegmentCount;
                        float segFrac = frac(segUV);
                        float halfLine = min(_SegmentLineWidth * _SegmentCount * 0.5, 0.4);

                        if ((segFrac < halfLine || segFrac > (1.0 - halfLine)) && segUV > halfLine)
                        {
                            col = lerp(col, _SegmentLineColor, _SegmentLineColor.a);
                        }
                    }
                }

                col *= i.color;
                return col;
            }
            ENDHLSL
        }
    }
}
