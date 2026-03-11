using UnityEngine;

namespace State
{
    public class GrenadeEntityState
    {
        public EId Id;
        public EId OwnerId;
        public Vector3 ThrowVelocity;
        public float SpawnTime;
        public float FuseTime;
        public float Damage;
        public float ExplosionRadius;

        public static GrenadeEntityState Create(
            EId id, EId ownerId, Vector3 throwVelocity,
            float spawnTime, float fuseTime, float damage, float explosionRadius)
        {
            return new GrenadeEntityState
            {
                Id = id,
                OwnerId = ownerId,
                ThrowVelocity = throwVelocity,
                SpawnTime = spawnTime,
                FuseTime = fuseTime,
                Damage = damage,
                ExplosionRadius = explosionRadius,
            };
        }
    }
}
