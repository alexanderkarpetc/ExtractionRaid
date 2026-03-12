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
                Id = weaponId,
                PrefabId = config.WeaponPrefabId,
                FireInterval = config.FireInterval,
                ProjectileSpeed = config.ProjectileSpeed,
                ProjectileDamage = config.ProjectileDamage,
                ProjectileLifetime = config.ProjectileLifetime,
                ProjectilesPerShot = config.ProjectilesPerShot,
                SpreadAngle = config.SpreadAngle,
                ConeHalfAngle = 45f,
                BodyRotationSpeed = 270f,
                LastFireTime = -999f,
            };

            bot.Blackboard.GrenadesRemaining = config.GrenadeCount;

            state.Bots.Add(bot);
            state.HealthMap[id] = HealthState.Create(config.MaxHp);
            events.BotSpawned(id, position, typeId);
        }
    }
}
