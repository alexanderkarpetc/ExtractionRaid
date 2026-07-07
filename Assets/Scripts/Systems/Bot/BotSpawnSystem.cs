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
            var weaponId   = state.AllocateEId();
            var weaponItem = ItemState.CreateWeapon(weaponId, "Weapon", config.WeaponConfig);
            var registry   = coreDefinitions ?? (App.IsInitialized ? App.Instance.CoreDefinitions : null);
            bot.Weapon     = WeaponSyncSystem.BuildWeaponForItem(weaponItem, registry, events);

            bot.Blackboard.MedkitsRemaining = config.MedkitCount;
            bot.Blackboard.GrenadesRemaining = config.GrenadeCount;
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

            if (config.HelmetDefinitionId != null || config.BodyArmorDefinitionId != null)
            {
                var armorSlots = new ArmorSlotState();
                if (config.HelmetDefinitionId != null)
                {
                    var def = ItemDefinition.Get(config.HelmetDefinitionId);
                    if (def != null)
                        armorSlots.Helmet = ArmorState.Create(def.ArmorPoints, def.MaxDurability);
                }
                if (config.BodyArmorDefinitionId != null)
                {
                    var def = ItemDefinition.Get(config.BodyArmorDefinitionId);
                    if (def != null)
                        armorSlots.BodyArmor = ArmorState.Create(def.ArmorPoints, def.MaxDurability);
                }
                if (armorSlots.Helmet != null || armorSlots.BodyArmor != null)
                    state.ArmorMap[id] = armorSlots;
            }

            events.BotSpawned(id, position, typeId);
        }
    }
}
