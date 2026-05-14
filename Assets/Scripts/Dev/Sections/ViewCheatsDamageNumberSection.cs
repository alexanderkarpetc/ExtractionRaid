using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Trajectory profile для damage popup. Per-tier selectable у <see cref="ViewCheatsDamageNumberSection"/>.
    /// </summary>
    public enum DamageNumberTrajectory : byte
    {
        /// <summary>Straight world-Y up. Default for normal hits — clean, tactical.</summary>
        FloatUp,
        /// <summary>Float up + slight horizontal drift у bullet direction. Telegraph для ricochet / direction-sensitive feedback.</summary>
        FloatUpDrift,
        /// <summary>Number flies away from shooter along bullet direction. "Hit pushed the number" cue. Punchy для shotgun hits.</summary>
        Knockback,
        /// <summary>Ballistic arc — initial velocity along bullet direction + gravity. Cinematic punctuation для kill / big hits.</summary>
        ArcGravity,
    }

    /// <summary>
    /// Floating damage numbers (uGUI + TMP, World Space Canvas, pool). Per-tier sizes,
    /// lifetimes, drift, consolidation window. Runtime-tunable. v1 IMGUI overlay removed
    /// 2026-05-14 after design lock.
    /// </summary>
    public class ViewCheatsDamageNumberSection : ScriptableObject
    {
        public bool Enabled = true;

        [Header("Pool")]
        [Range(10, 200)] public int PoolSize = 50;

        [Tooltip("World-Space Canvas localScale. UI units × WorldScale = world units. Top-down camera ~5m above ground → 0.015 reads naturally, 0.03 = oversize, 0.01 = sparkle artifacts.")]
        [Range(0.005f, 0.05f)] public float WorldScale = 0.015f;

        [Header("Per-tier size multipliers")]
        [Range(0.3f, 3f)] public float NormalSize   = 1.0f;
        [Range(0.3f, 3f)] public float HeadshotSize = 1.3f;
        [Range(0.3f, 3f)] public float KillSize     = 1.6f;
        [Range(0.3f, 3f)] public float BleedSize    = 0.8f;
        [Range(0.3f, 3f)] public float AbsorbedSize = 0.85f;
        [Range(0.3f, 3f)] public float RicochetSize = 1.0f;

        [Header("Animation timings (ms)")]
        [Tooltip("Ease-out scale-up at spawn.")]
        [Range(0f, 500f)] public float SpawnMs = 70f;
        [Tooltip("Peak hold (1.0 scale, full alpha).")]
        [Range(0f, 1000f)] public float HoldMs = 150f;
        [Range(0f, 1000f)] public float HoldMsKill = 250f;
        [Tooltip("Decay phase: fade alpha + scale → endScale + drift up.")]
        [Range(100f, 2000f)] public float DecayMs = 580f;
        [Tooltip("Bleed total lifetime override (spawn+hold+decay combined). Slower fade — DoT character.")]
        [Range(500f, 3000f)] public float BleedTotalMs = 1200f;
        [Tooltip("Ricochet total lifetime (no merge ever — short flash).")]
        [Range(300f, 1500f)] public float RicochetTotalMs = 500f;

        [Header("Drift (world Y units over total lifetime)")]
        [Range(0f, 2f)] public float DriftUpWorld = 0.5f;
        [Tooltip("Vertical offset above hit point — spawn number above character head to avoid body occlusion. 1.5m = above typical 2m char head when hit was on chest.")]
        [Range(0f, 3f)] public float SpawnYOffset = 1.5f;

        [Header("Trajectory mode per tier")]
        public DamageNumberTrajectory NormalTrajectory   = DamageNumberTrajectory.FloatUp;
        public DamageNumberTrajectory HeadshotTrajectory = DamageNumberTrajectory.FloatUp;
        public DamageNumberTrajectory KillTrajectory     = DamageNumberTrajectory.ArcGravity;
        public DamageNumberTrajectory BleedTrajectory    = DamageNumberTrajectory.FloatUp;
        public DamageNumberTrajectory AbsorbedTrajectory = DamageNumberTrajectory.FloatUp;
        public DamageNumberTrajectory RicochetTrajectory = DamageNumberTrajectory.FloatUpDrift;

        [Header("Trajectory physics (shared across modes)")]
        [Tooltip("Horizontal drift bias (FloatUpDrift mode) — small bullet-dir push paired з FloatUp vertical. 0.2-0.4 = telegraph без overpowering.")]
        [Range(0f, 1f)] public float DriftBiasFactor = 0.3f;

        [Tooltip("Knockback mode horizontal distance (world units) over total lifetime.")]
        [Range(0f, 3f)] public float KnockbackDistance = 1.0f;

        [Tooltip("Knockback mode vertical component (% of FloatUp DriftUpWorld). 0 = pure horizontal, 0.5 = slight rise.")]
        [Range(0f, 1f)] public float KnockbackUpRatio = 0.3f;

        [Tooltip("ArcGravity mode initial bullet-dir horizontal velocity (world units/s).")]
        [Range(0f, 5f)] public float ArcInitialHorizontal = 1.5f;

        [Tooltip("ArcGravity mode initial upward velocity (world units/s).")]
        [Range(0f, 5f)] public float ArcInitialUp = 2.0f;

        [Tooltip("ArcGravity gravity (world units/s²).")]
        [Range(0f, 15f)] public float ArcGravity = 6.0f;
        [Tooltip("End scale at decay completion. 1.0 = no shrink, 0.85 = soft shrink.")]
        [Range(0.3f, 1f)] public float EndScale = 0.85f;

        [Header("Consolidation (same-target merge)")]
        [Tooltip("Same-target hit within window → merge add to existing popup. 0 = disabled.")]
        [Range(0f, 1000f)] public float ConsolidationWindowMs = 200f;

        [Header("Base font + label text")]
        [Range(8, 100)] public int FontSize = 36;
        [Tooltip("Sub-label rendered below number for headshot tier (50% size, lifted below digits).")]
        public string HeadshotLabel = "HEAD";
        [Tooltip("Sub-label rendered below number for kill tier.")]
        public string KillLabel = "KILL";
        [Tooltip("Word shown on ricochet (single-line, replaces number entirely).")]
        public string RicochetLabel = "RICOCHET";

        [Header("Sub-label layout (Headshot/Kill)")]
        [Tooltip("Line-height for sub-label у % — lower = tighter gap between digit and label.")]
        [Range(20, 100)] public int SubLabelLineHeight = 60;
        [Tooltip("Sub-label font size % — relative to main digit size.")]
        [Range(30, 90)] public int SubLabelSizePct = 55;
    }
}
