using Adapters;
using ApplicationCore;
using Constants;
using State;
using UnityEngine;

namespace Systems.Bot
{
    public static class BotSpawnSystem
    {
        public static void SpawnBot(RaidState state, string typeId, Vector3 position,
            Vector3[] patrolWaypoints, IRaidEvents events,
            ICoreDefinitionRegistry coreDefinitions = null)
        {
            var config = BotConstants.GetConfig(typeId);
            var id = state.AllocateEId();
            var bot = BotEntityState.Create(id, typeId, position, patrolWaypoints);

            // Tier 4a — bot weapon assembled через same Builder pipeline as player.
            // Build a transient ItemState wrapping the bot's WeaponConfiguration → run it
            // through WeaponSyncSystem.BuildWeaponForItem → composed Stats з Penetration,
            // ArmorDamage, BleedChance, HeadshotMultiplier, etc. (No more hardcoded per-bot
            // stats у BotConstants.)
            // Fallback на App.Instance.CoreDefinitions if registry not passed — match'ить
            // pattern PlayerSpawnSystem.cs which also resolves через App. Tests can override
            // by passing explicit registry.
            // Weapon: roll from the weighted equipment pool if this type provides one,
            // otherwise use the fixed WeaponConfig.
            // A pool entry carries an authored rarity — respect it. The fixed WeaponConfig is
            // Common/Common by construction, so roll each module there instead of shipping every
            // bot with the same grey gun.
            var weaponConfig = TryPickWeighted(config.WeaponPool, w => w.Weight, out var rolledWeapon)
                ? rolledWeapon.Config
                : RollModuleRarities(config.WeaponConfig);
            var weaponId   = state.AllocateEId();
            var weaponItem = ItemState.CreateWeapon(weaponId, "Weapon", weaponConfig);
            var registry   = coreDefinitions ?? (App.IsInitialized ? App.Instance.CoreDefinitions : null);
            bot.Weapon     = WeaponSyncSystem.BuildWeaponForItem(weaponItem, registry, events);

            bot.Blackboard.MedkitsRemaining = config.MedkitCount;
            // Grenade count: loot-config range (rolled per spawn) takes precedence over the
            // built-in/legacy fixed GrenadeCount. Drives both throwing behaviour and leftover drop.
            bot.Blackboard.GrenadesRemaining = config.GrenadeMaxCount > 0
                ? Random.Range(config.GrenadeMinCount, config.GrenadeMaxCount + 1)
                : config.GrenadeCount;
            // Every enemy carries 0-4 bandages so they're easy to scavenge.
            bot.Blackboard.BandagesRemaining = Random.Range(0, 5);

            // Personality: one dice-roll per spawn so bots of the same type aren't clones.
            // Slow-twitch vs jumpy (reaction), sharp vs sloppy (accuracy), and aggression
            // (burst length/pause, strafe energy) all vary per individual.
            bot.Blackboard.ReactionTimeMult = Random.Range(BotConstants.ReactionTimeMultMin, BotConstants.ReactionTimeMultMax);
            bot.Blackboard.AccuracyMult     = Random.Range(BotConstants.AccuracyMultMin,     BotConstants.AccuracyMultMax);
            bot.Blackboard.Aggression       = Random.Range(BotConstants.AggressionMin,       BotConstants.AggressionMax);

            state.Bots.Add(bot);
            state.HealthMap[id] = HealthState.Create(config.MaxHp);

            // Armor: pools (when supplied) override the fixed ids and are rolled per spawn;
            // a rolled entry with a null id means "no item" (bare head / no vest).
            string helmetId = TryPickWeighted(config.HelmetPool, h => h.Weight, out var rolledHelmet)
                ? rolledHelmet.Id
                : config.HelmetDefinitionId;
            string bodyArmorId = TryPickWeighted(config.BodyArmorPool, a => a.Weight, out var rolledArmor)
                ? rolledArmor.Id
                : config.BodyArmorDefinitionId;

            if (helmetId != null || bodyArmorId != null)
            {
                var armorSlots = new ArmorSlotState();
                if (helmetId != null)
                {
                    var def = ItemDefinition.Get(helmetId);
                    if (def != null)
                    {
                        armorSlots.Helmet = ArmorState.Create(def.ArmorPoints, def.MaxDurability);
                        armorSlots.Helmet.CurrentDurability =
                            RollWear(config.HelmetDurabilityMin, config.HelmetDurabilityMax) * def.MaxDurability;
                        armorSlots.HelmetDefinitionId = helmetId;
                    }
                }
                if (bodyArmorId != null)
                {
                    var def = ItemDefinition.Get(bodyArmorId);
                    if (def != null)
                    {
                        armorSlots.BodyArmor = ArmorState.Create(def.ArmorPoints, def.MaxDurability);
                        armorSlots.BodyArmor.CurrentDurability =
                            RollWear(config.BodyArmorDurabilityMin, config.BodyArmorDurabilityMax) * def.MaxDurability;
                        armorSlots.BodyArmorDefinitionId = bodyArmorId;
                    }
                }
                if (armorSlots.Helmet != null || armorSlots.BodyArmor != null)
                    state.ArmorMap[id] = armorSlots;
            }

            events.BotSpawned(id, position, typeId);
        }

        // Rolls a durability fraction in [min, max] (clamped to 0..1). A pristine (1,1)
        // range returns 1 — no behaviour change for armor that hasn't been tuned.
        static float RollWear(float min, float max)
        {
            float lo = Mathf.Clamp01(Mathf.Min(min, max));
            float hi = Mathf.Clamp01(Mathf.Max(min, max));
            return Random.Range(lo, hi);
        }

        /// <summary>
        /// Re-rolls payload and delivery rarity independently (see the weights in
        /// <see cref="BotConstants"/>). The exotic slot is left exactly as authored — bots carry
        /// none today, and an exotic is a designed drop, not a dice roll.
        /// </summary>
        static WeaponConfiguration RollModuleRarities(in WeaponConfiguration config)
        {
            var payload  = new PayloadCoreInstance(config.Payload.DefinitionId, PickRarity(Random.value));
            var delivery = new DeliveryCoreInstance(config.Delivery.DefinitionId, PickRarity(Random.value));

            // AmmoInMagazine is carried over untouched. Rarity only ever goes up from the authored
            // Common, so a bigger composed magazine just means the bot starts it partially loaded.
            return new WeaponConfiguration(payload, delivery, config.Exotic, config.AmmoInMagazine);
        }

        /// <summary>
        /// Maps a 0..1 roll onto the bot weapon rarity distribution. Pure on purpose — the
        /// distribution is the thing worth testing, and it can be checked without touching RNG.
        /// </summary>
        public static RarityTier PickRarity(float roll01)
        {
            float total = BotConstants.BotWeaponCommonWeight
                          + BotConstants.BotWeaponUncommonWeight
                          + BotConstants.BotWeaponRareWeight;
            if (total <= 0f) return RarityTier.Common;

            float r = Mathf.Clamp01(roll01) * total;
            if (r < BotConstants.BotWeaponCommonWeight) return RarityTier.Common;
            if (r < BotConstants.BotWeaponCommonWeight + BotConstants.BotWeaponUncommonWeight)
                return RarityTier.Uncommon;
            return RarityTier.Rare;
        }

        // Weighted random pick over a pool. Returns false (picked = default) when the pool
        // is empty or all weights are non-positive, so callers fall back to fixed config.
        static bool TryPickWeighted<T>(T[] pool, System.Func<T, float> weightOf, out T picked)
        {
            picked = default;
            if (pool == null || pool.Length == 0) return false;

            float total = 0f;
            for (int i = 0; i < pool.Length; i++)
                total += Mathf.Max(0f, weightOf(pool[i]));
            if (total <= 0f) return false;

            float r = Random.value * total;
            for (int i = 0; i < pool.Length; i++)
            {
                r -= Mathf.Max(0f, weightOf(pool[i]));
                if (r <= 0f) { picked = pool[i]; return true; }
            }
            picked = pool[pool.Length - 1]; // float drift guard
            return true;
        }
    }
}
