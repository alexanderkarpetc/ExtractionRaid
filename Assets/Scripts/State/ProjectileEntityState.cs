using UnityEngine;

namespace State
{
    public class ProjectileEntityState
    {
        public EId Id;
        public EId OwnerId;
        public Vector3 Position;
        public Vector3 Direction;
        public float Speed;
        public float SpawnTime;
        public float Lifetime;
        public float Damage;
        public float HeadshotDamageMultiplier;
        public float Penetration;
        public float ArmorDamage;
        public float BleedChance;
        public EId TargetedEntityId; // EId of the character the player aimed at (convergence), default = not targeted

        public static ProjectileEntityState Create(
            EId id, EId ownerId, Vector3 position, Vector3 direction,
            float speed, float spawnTime, float lifetime,
            float damage, float headshotDamageMultiplier = 1f, EId targetedEntityId = default,
            float penetration = 0f, float armorDamage = 0f, float bleedChance = 0f)
        {
            return new ProjectileEntityState
            {
                Id = id,
                OwnerId = ownerId,
                Position = position,
                Direction = direction,
                Speed = speed,
                SpawnTime = spawnTime,
                Lifetime = lifetime,
                Damage = damage,
                HeadshotDamageMultiplier = headshotDamageMultiplier,
                Penetration = penetration,
                ArmorDamage = armorDamage,
                BleedChance = bleedChance,
                TargetedEntityId = targetedEntityId,
            };
        }
    }
}
