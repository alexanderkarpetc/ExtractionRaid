using Adapters;
using Progression;
using State;
using UnityEngine;

namespace Systems.Meta
{
    /// <summary>
    /// The COST and RISK half of the region raid sim (<see cref="RegionLootSimulator"/> is the
    /// reward half). Looting a region is no longer free: you burn real reserve ammo killing the
    /// enemies that live there, and you roll to survive.
    ///
    /// Ammo:      rounds = ceil(ΣenemyHp / bulletDamage × <see cref="AccuracyCoef"/>) — the coef
    ///            is "misses happen", so 2 = you land half your shots.
    /// Survival:  a base chance from your GEAR (worst kit → <see cref="BaseSurviveWorstGear"/>,
    ///            best kit → <see cref="BaseSurviveBestGear"/>) raised to the region's difficulty
    ///            multiplier (so diff 1 is exactly the 70%..99% band the design asks for), plus a
    ///            flat skill-tree bonus up to <see cref="MaxSkillBonus"/>, times penalties for
    ///            running short on rounds or fighting with no gun at all.
    ///
    /// Nothing here ever refuses the run — a gunless, ammo-less raid is a legal (bad) play, it
    /// just tanks the odds. The caller decides what dying costs. Both "can't shoot" penalties
    /// bottom out at the same <see cref="ImprovisedPenalty"/> floor, so gearing up is always
    /// monotonically better than showing up empty.
    ///
    /// Stateless, Unity value-types only (CLAUDE.md §3), no <c>App</c> / no editor deps.
    /// </summary>
    public static class RaidCombatSimulator
    {
        // ───────────────────────────────────────── Balance knobs ──

        /// <summary>Rounds-per-kill inflation for missed shots. 2 = ~50% of shots land.</summary>
        public const float AccuracyCoef = 2f;

        /// <summary>Survive chance at difficulty 1 with the worst possible kit.</summary>
        public const float BaseSurviveWorstGear = 0.70f;
        /// <summary>Survive chance at difficulty 1 with a maxed-out kit.</summary>
        public const float BaseSurviveBestGear = 0.99f;
        /// <summary>Flat bonus granted by a fully allocated progression tree.</summary>
        public const float MaxSkillBonus = 0.30f;

        /// <summary>
        /// Worst-case odds multiplier for a fight you can't shoot your way through — no gun, or
        /// a gun with an empty reserve. It's a FLOOR, not a special case: the ammo penalty lerps
        /// from here up to 1 as your reserve covers the bill, so arming yourself can never come
        /// out worse than going in bare-handed.
        /// </summary>
        public const float ImprovisedPenalty = 0.5f;

        public const float SurviveFloor = 0.02f;
        public const float SurviveCeil = 0.995f;

        /// <summary>Sustained DPS that scores the weapon half of the gear score at 1.0.</summary>
        public const float DpsFullScore = 150f;
        /// <summary>Combined helmet + body-armor points that score the armor half at 1.0.</summary>
        public const float ArmorPointsFullScore = 120f;
        /// <summary>Weapon share of the gear score; armor takes the remainder.</summary>
        public const float WeaponScoreWeight = 0.7f;

        // ───────────────────────────────────────────── Data ──

        /// <summary>Everything the player needs to see BEFORE committing to a region.</summary>
        public struct Plan
        {
            public int EnemyCount;
            public float TotalEnemyHp;
            public float Difficulty;

            // Gun the sim fights with (best-DPS equipped weapon).
            public bool HasWeapon;
            public string WeaponName;
            public float BulletDamage;
            public float Dps;
            public string AmmoType;

            public int RoundsNeeded;
            public int RoundsAvailable;
            public int Shortfall;            // RoundsNeeded - RoundsAvailable, floored at 0

            public float GearScore;          // 0..1
            public float SkillFraction;      // 0..1 of the tree allocated
            public float SkillBonus;         // flat, ≤ MaxSkillBonus
            public float AmmoPenalty;        // 0..1 multiplier — ran out of rounds mid-fight
            public float WeaponPenalty;      // 0..1 multiplier — fighting without a gun
            public float SurviveChance;      // final, clamped

            public float TotalPenalty => Mathf.Clamp01(AmmoPenalty) * Mathf.Clamp01(WeaponPenalty);
        }

        public struct Outcome
        {
            public Plan Plan;
            public int RoundsSpent;
            public bool Survived;
            public float Roll;
        }

        // ────────────────────────────────────────── Planning ──

        /// <summary>
        /// Prices a region fight against the player's current kit. Pure — reads the inventory
        /// and progression state, mutates nothing. Call before <see cref="Resolve"/> so the UI
        /// can show the bill (rounds) and the odds.
        /// </summary>
        public static Plan BuildPlan(
            int enemyCount, float totalEnemyHp, float difficulty,
            InventoryState inv, ICoreDefinitionRegistry registry,
            ProgressionTreeConfig tree, PlayerProgressionState progress)
        {
            var plan = new Plan
            {
                EnemyCount = enemyCount,
                TotalEnemyHp = Mathf.Max(0f, totalEnemyHp),
                Difficulty = Mathf.Max(0.1f, difficulty),
                AmmoPenalty = 1f,
                WeaponPenalty = 1f,
            };

            // Best gun we can actually field decides both the ammo bill and half the gear score.
            if (TryPickBestWeapon(inv, registry, out var stats, out var ammoType, out var weaponName))
            {
                plan.HasWeapon = true;
                plan.WeaponName = weaponName;
                plan.AmmoType = ammoType;
                plan.BulletDamage = Mathf.Max(1f, stats.Damage) * Mathf.Max(1, stats.ProjectilesPerShot);
                plan.Dps = plan.BulletDamage / Mathf.Max(0.01f, stats.FireInterval);
            }

            plan.GearScore = GearScore(inv, plan.Dps);
            plan.SkillFraction = SkillFraction(tree, progress);
            plan.SkillBonus = plan.SkillFraction * MaxSkillBonus;

            // Nothing alive here — walk in, no bill, no roll.
            if (plan.TotalEnemyHp <= 0f)
            {
                plan.SurviveChance = SurviveCeil;
                return plan;
            }

            // Bare-handed is a legitimate run (you always have SOMETHING on entry) — no ammo
            // bill, but you're improvising, so the odds get halved rather than blocked.
            if (!plan.HasWeapon || string.IsNullOrEmpty(plan.AmmoType))
            {
                plan.WeaponPenalty = ImprovisedPenalty;
                plan.SurviveChance = SurviveChance(
                    plan.GearScore, plan.Difficulty, plan.SkillBonus, plan.TotalPenalty);
                return plan;
            }

            plan.RoundsNeeded = Mathf.CeilToInt(plan.TotalEnemyHp / plan.BulletDamage * AccuracyCoef);
            plan.RoundsAvailable = AmmoSystem.CountReserve(inv, plan.AmmoType);
            plan.Shortfall = Mathf.Max(0, plan.RoundsNeeded - plan.RoundsAvailable);

            // Running dry mid-fight scales the risk, but only down to ImprovisedPenalty: an empty
            // gun is exactly as bad as no gun, never worse. Full reserve = no penalty at all.
            float covered = plan.RoundsNeeded <= 0
                ? 1f
                : Mathf.Clamp01((float)Mathf.Min(plan.RoundsAvailable, plan.RoundsNeeded) / plan.RoundsNeeded);
            plan.AmmoPenalty = Mathf.Lerp(ImprovisedPenalty, 1f, covered);

            plan.SurviveChance = SurviveChance(
                plan.GearScore, plan.Difficulty, plan.SkillBonus, plan.TotalPenalty);
            return plan;
        }

        /// <summary>
        /// Base gear odds raised to the difficulty multiplier, times <paramref name="penalty"/>
        /// (ammo shortfall × no-gun), plus the flat skill bonus. Difficulty 1 with no penalty
        /// lands exactly on the 70%..99% gear band.
        /// </summary>
        public static float SurviveChance(float gearScore, float difficulty, float skillBonus, float penalty)
        {
            float baseChance = Mathf.Lerp(BaseSurviveWorstGear, BaseSurviveBestGear, Mathf.Clamp01(gearScore));
            float scaled = Mathf.Pow(baseChance, Mathf.Max(0.1f, difficulty));
            return Mathf.Clamp(scaled * Mathf.Clamp01(penalty) + skillBonus, SurviveFloor, SurviveCeil);
        }

        // ────────────────────────────────────────── Resolution ──

        /// <summary>
        /// Spends the ammo (capped at what's actually in the pack) and rolls survival.
        /// Mutates the inventory's ammo stacks — the caller decides what happens to the loot
        /// and, on death, to the rest of the gear.
        /// </summary>
        public static Outcome Resolve(Plan plan, InventoryState inv)
        {
            var outcome = new Outcome { Plan = plan };
            outcome.RoundsSpent = AmmoSystem.ConsumeAmmo(inv, plan.AmmoType, plan.RoundsNeeded);
            outcome.Roll = Random.value;
            outcome.Survived = outcome.Roll < plan.SurviveChance;
            return outcome;
        }

        // ──────────────────────────────────────────── Scoring ──

        /// <summary>Weighted blend of gun DPS and worn armor, both normalised to 0..1.</summary>
        public static float GearScore(InventoryState inv, float dps)
        {
            float weapon = Mathf.Clamp01(dps / DpsFullScore);
            float armor = Mathf.Clamp01(WornArmorPoints(inv) / ArmorPointsFullScore);
            return Mathf.Clamp01(weapon * WeaponScoreWeight + armor * (1f - WeaponScoreWeight));
        }

        /// <summary>Helmet + body-armor points, each discounted by remaining durability.</summary>
        public static float WornArmorPoints(InventoryState inv)
        {
            if (inv == null) return 0f;
            return ArmorPointsOf(inv.HelmetSlot) + ArmorPointsOf(inv.BodyArmorSlot);
        }

        static float ArmorPointsOf(ItemState item)
        {
            var def = item?.Definition;
            if (def == null || def.ArmorPoints <= 0f) return 0f;
            // Only a looted / damaged item carries custom durability; a pristine drop is full.
            float wear = item.HasCustomDurability && item.MaxDurability > 0f
                ? Mathf.Clamp01(item.CurrentDurability / item.MaxDurability)
                : 1f;
            return def.ArmorPoints * wear;
        }

        /// <summary>Fraction of the progression tree allocated — drives the flat survival bonus.</summary>
        public static float SkillFraction(ProgressionTreeConfig tree, PlayerProgressionState progress)
        {
            if (tree == null || progress?.AllocatedNodeIds == null) return 0f;
            int total = tree.NodeCount;
            if (total <= 0) return 0f;
            return Mathf.Clamp01((float)progress.AllocatedNodeIds.Count / total);
        }

        // ───────────────────────────────────────── Weapon pick ──

        /// <summary>Highest-DPS assembled weapon across the equipped weapon slots.</summary>
        static bool TryPickBestWeapon(
            InventoryState inv, ICoreDefinitionRegistry registry,
            out WeaponStats bestStats, out string ammoType, out string displayName)
        {
            bestStats = default; ammoType = null; displayName = null;
            if (inv?.WeaponSlots == null || registry == null) return false;

            float bestDps = -1f;
            for (int i = 0; i < inv.WeaponSlots.Length; i++)
            {
                var item = inv.WeaponSlots[i];
                if (item == null || !item.HasWeaponConfiguration) continue;
                if (!WeaponAssemblySystem.TryAssemble(item.WeaponConfiguration, registry, out var result, out _))
                    continue;

                var s = result.Stats;
                float dps = Mathf.Max(1f, s.Damage) * Mathf.Max(1, s.ProjectilesPerShot)
                            / Mathf.Max(0.01f, s.FireInterval);
                if (dps <= bestDps) continue;

                bestDps = dps;
                bestStats = s;
                ammoType = result.PayloadDefinition?.AmmoType;
                displayName = WeaponDisplayName.For(item, registry);
            }
            return bestDps > 0f;
        }
    }
}
