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

        public static ProjectileEntityState Create(
            EId id, EId ownerId, Vector3 position, Vector3 direction,
            float speed, float spawnTime, float lifetime,
            float damage)
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
            };
        }
    }
}
