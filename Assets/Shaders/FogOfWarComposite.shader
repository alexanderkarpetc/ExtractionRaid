Shader "FogOfWar/Composite"
{
    Properties
    {
        _MainTex ("Scene Color", 2D) = "white" {}
        // _FoWBlurred is set globally via cmd.SetGlobalTexture — NOT per-material.
        // Keeping it out of Properties ensures the global value is always used.
        _FogColor ("Fog Color", Color) = (0.02, 0.02, 0.05, 1)
        _FogIntensity ("Fog Intensity", Range(0, 1)) = 0.85
        _DesaturationAmount ("Desaturation", Range(0, 1)) = 0.7
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    TEXTURE2D(_FoWBlurred);
    SAMPLER(sampler_FoWBlurred);

    float4 _FogColor;
    float _FogIntensity;
    float _DesaturationAmount;

    // Sniper scope — a crisp, screen-space reveal circle centered on the cursor. Drawn here in
    // the composite (procedural SDF) rather than in the blurred visibility mask, so it reads as
    // a sharp scope ring, not a soft floodlight. All set globally each frame by FogOfWarController;
    // default 0 → no effect when unset.
    float _FoWScopeBlackout; // 0..1 "scoped in" amount (eases with ADS)
    float4 _ScopeCenter;     // screen UV center (.xy)
    float _ScopeRadius;      // circle radius as a fraction of screen height
    float _ScopeRing;        // ring thickness (UV)
    float _ScopeDark;        // how dark outside the circle gets (0..1)
    float _ScopeRingBright;  // ring edge highlight strength
    float _ScreenAspect;     // width/height, to keep the circle round

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.uv;
        return output;
    }

    float4 Frag(Varyings input) : SV_Target
    {
        float3 scene = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
        float visibility = SAMPLE_TEXTURE2D(_FoWBlurred, sampler_FoWBlurred, input.uv).r;

        // Base fog coverage from the FoW visibility mask.
        float fogFactor = (1.0 - visibility) * _FogIntensity;

        // --- Sniper scope: crisp screen-space reveal circle around the cursor ---
        float scoped = saturate(_FoWScopeBlackout);
        float ringAdd = 0.0;
        if (scoped > 0.0001)
        {
            float2 d = input.uv - _ScopeCenter.xy;
            d.x *= _ScreenAspect;                 // keep the circle round, not oval
            float dist = length(d);
            float feather = max(_ScopeRadius * 0.04, 0.001); // mostly crisp, tiny AA edge
            float outside = smoothstep(_ScopeRadius - feather, _ScopeRadius, dist); // 0 inside → 1 outside

            // A thin bright ring right at the circle boundary (the scope rim).
            float innerR = _ScopeRadius - _ScopeRing;
            float ring = smoothstep(innerR - feather, innerR, dist)
                       * (1.0 - smoothstep(_ScopeRadius, _ScopeRadius + feather, dist));

            // Sniper crosshair: thin vertical + horizontal lines through the centre with a gap.
            // No centre dot here — the crosshair presenter owns the one constant centre dot.
            float lineHalf = 0.0013;
            float gap = _ScopeRadius * 0.12;
            float reach = _ScopeRadius * 0.92;
            float vert  = (1.0 - smoothstep(lineHalf, lineHalf + feather, abs(d.x)))
                        * step(gap, abs(d.y)) * (1.0 - step(reach, abs(d.y)));
            float horiz = (1.0 - smoothstep(lineHalf, lineHalf + feather, abs(d.y)))
                        * step(gap, abs(d.x)) * (1.0 - step(reach, abs(d.x)));
            float reticle = max(vert, horiz);

            ringAdd = max(ring, reticle) * _ScopeRingBright * scoped;

            // Scoped fog = darken everything outside the circle; inside stays clear.
            float scopeFog = outside * _ScopeDark;
            fogFactor = lerp(fogFactor, scopeFog, scoped);
        }

        // Desaturate + darken toward fog color, proportional to fog coverage.
        float luminance = dot(scene, float3(0.299, 0.587, 0.114));
        float3 gray = float3(luminance, luminance, luminance);
        float3 desaturated = lerp(scene, gray, _DesaturationAmount * fogFactor);
        float3 fogged = lerp(desaturated, _FogColor.rgb, fogFactor);

        // Add the scope rim highlight on top.
        fogged += ringAdd;

        return float4(fogged, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "FoWComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
