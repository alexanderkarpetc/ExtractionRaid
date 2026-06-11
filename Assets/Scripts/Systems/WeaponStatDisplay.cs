using System.Collections.Generic;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Maps composed <see cref="WeaponStats"/> → the ordered list of player-facing
    /// rows shown in weapon UIs (tooltip now; Weapon Builder live-preview later).
    /// Single source of truth for the parameter set + display rules agreed for the
    /// Weapon Attachments epic — see docs/ai/weapon-builder/attachments/stats.md.
    ///
    /// Each row carries a <see cref="StatDisplayRow.Value"/> (always) and an optional
    /// 0..1 <see cref="StatDisplayRow.BarRatio01"/> ("goodness" fill, fuller = better):
    ///   • Bar rows (value + bar): Damage, Rate of Fire, Stability, Accuracy, Ergonomics.
    ///   • Value-only rows (sink to bottom): Headshot Mult, Magazine.
    /// All bar rows are higher = better (value up, bar fuller) — so the feel stats are
    /// framed as Stability/Accuracy/Ergonomics scores (0..100), not raw kick/spread.
    ///
    /// OMITTED: Reload (delta-only later), Sight/FOV (hidden), Noise (no mechanic yet),
    /// Bleed/Penetration/ArmorDamage (ammo channel — not a weapon stat).
    ///
    /// "Ergonomics" is an AGGREGATE: P1 folds the existing handling fields (equip/unequip
    /// speed + turn rate); ADS-speed + move-speed fields join in P2 once they exist.
    ///
    /// Pure C# (Unity value types only — Mathf). No Unity object refs, no state.
    /// </summary>
    public static class WeaponStatDisplay
    {
        /// <summary>One player-facing weapon parameter for UI rendering.</summary>
        public readonly struct StatDisplayRow
        {
            public readonly string Label;
            /// <summary>Display text — always set (e.g. "15", "2.5/s", "70", "2×", "8/12").</summary>
            public readonly string Value;
            /// <summary>0..1 "goodness" fill (fuller = better) for bar rows; -1 = value-only.</summary>
            public readonly float BarRatio01;

            public StatDisplayRow(string label, string value, float barRatio01)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                BarRatio01 = barRatio01;
            }

            public bool HasBar => BarRatio01 >= 0f;
        }

        // ── Reference ranges for bar normalization (tunable; promote to ViewCheats
        //    later if runtime tuning is wanted). Chosen to span the current 6 archetypes. ──
        const float DamageRefMax         = 50f;   // Damage upper reference
        const float RofRefMax            = 12f;   // shots/sec upper reference
        const float RecoilKickRefMax     = 6f;    // KickForward + KickSide upper reference
        const float RecoilRecoveryRefMax = 8f;    // RecoilRecoverySpeed upper reference (higher = better)
        const float SpreadRefMax         = 12f;   // SpreadAngle upper reference (degrees)
        const float EquipMin             = 0.1f;  // fastest draw
        const float EquipMax             = 0.6f;  // slowest draw
        const float UnequipMin           = 0.1f;
        const float UnequipMax           = 0.5f;
        const float TurnRateMin          = 100f;  // BodyRotationSpeed deg/s (slow)
        const float TurnRateMax          = 400f;  // (fast)

        public static IReadOnlyList<StatDisplayRow> Build(in WeaponStats stats)
        {
            float rof = stats.FireInterval > 0f ? 1f / stats.FireInterval : 0f;

            var rows = new List<StatDisplayRow>(7)
            {
                // ── Bar rows (value + goodness fill, higher = better) ──
                BarValue("Damage",       stats.Damage.ToString("0.##"),
                         stats.Damage / DamageRefMax),
                BarValue("Rate of Fire", stats.FireInterval > 0f ? $"{rof:0.#}/s" : "—",
                         rof / RofRefMax),
                BarValue("Stability",    Score(StabilityGoodness(stats)),  StabilityGoodness(stats)),
                BarValue("Accuracy",     Score(AccuracyGoodness(stats)),   AccuracyGoodness(stats)),
                BarValue("Ergonomics",   Score(ErgonomicsGoodness(stats)), ErgonomicsGoodness(stats)),

                // ── Value-only rows (no bar — sink to bottom) ──
                ValueOnly("Headshot", $"{stats.HeadshotDamageMultiplier:0.##}×"),
                ValueOnly("Magazine", stats.MagazineSize.ToString()),
            };
            return rows;
        }

        // ── Row builders ──────────────────────────────────────
        static StatDisplayRow BarValue(string label, string value, float goodness01)
            => new(label, value, Mathf.Clamp01(goodness01));
        static StatDisplayRow ValueOnly(string label, string value) => new(label, value, -1f);

        // 0..1 goodness → "0".."100" score for the value column.
        static string Score(float goodness01) => Mathf.RoundToInt(Mathf.Clamp01(goodness01) * 100f).ToString();

        // ── Derived goodness (higher = better) ────────────────

        /// <summary>Low total kick is good; faster recovery nudges it up.</summary>
        static float StabilityGoodness(in WeaponStats s)
        {
            float kickNorm     = Mathf.Clamp01((s.RecoilKickForward + s.RecoilKickSide) / RecoilKickRefMax);
            float recoveryNorm = Mathf.Clamp01(s.RecoilRecoverySpeed / RecoilRecoveryRefMax);
            return (1f - kickNorm) * 0.7f + recoveryNorm * 0.3f;
        }

        /// <summary>Tighter spread (lower SpreadAngle) is more accurate.</summary>
        static float AccuracyGoodness(in WeaponStats s)
            => 1f - Mathf.Clamp01(s.SpreadAngle / SpreadRefMax);

        /// <summary>Aggregate of existing handling fields (P1).</summary>
        static float ErgonomicsGoodness(in WeaponStats s)
        {
            float equipGood   = 1f - InverseLerp01(EquipMin, EquipMax, s.EquipTime);
            float unequipGood = 1f - InverseLerp01(UnequipMin, UnequipMax, s.UnequipTime);
            float turnGood    = InverseLerp01(TurnRateMin, TurnRateMax, s.BodyRotationSpeed);
            return (equipGood + unequipGood + turnGood) / 3f;
        }

        static float InverseLerp01(float a, float b, float v)
            => Mathf.Approximately(a, b) ? 0f : Mathf.Clamp01((v - a) / (b - a));
    }
}
