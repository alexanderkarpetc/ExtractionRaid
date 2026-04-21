using Adapters;
using Constants;
using State;
using UnityEngine;

namespace Systems.Bot
{
    public static class BotSpawnSystem
    {
        public static void SpawnBot(RaidState state, string typeId, Vector3 position,
            Vector3[] patrolWaypoints, IRaidEvents events)
        {
            var config = BotConstants.GetConfig(typeId);
            var id = state.AllocateEId();
            var bot = BotEntityState.Create(id, typeId, position, patrolWaypoints);

            var weaponId = state.AllocateEId();
            bot.Weapon = new WeaponEntityState
            {
                Id           = weaponId,
                PrefabId     = config.WeaponPrefabId,
                LastFireTime = -999f,
                Stats = new WeaponStats
                {
                    Damage             = config.ProjectileDamage,
                    ProjectileSpeed    = config.ProjectileSpeed,
                    ProjectileLifetime = config.ProjectileLifetime,
                    ProjectilesPerShot = config.ProjectilesPerShot,
                    SpreadAngle        = config.SpreadAngle,
                    FireInterval       = config.FireInterval,
                    ConeHalfAngle      = 45f,
                    BodyRotationSpeed  = 270f,
                },
            };

            bot.Blackboard.MedkitsRemaining = config.MedkitCount;
            bot.Blackboard.GrenadesRemaining = config.GrenadeCount;

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
