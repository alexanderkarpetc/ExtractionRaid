namespace State
{
    public class GrenadeEntityState
    {
        public EId Id;
        public EId OwnerId;
        public float SpawnTime;
        public float FuseTime;
        public float Damage;
        public float ExplosionRadius;

        public static GrenadeEntityState Create(
            EId id, EId ownerId,
            float spawnTime, float fuseTime, float damage, float explosionRadius)
        {
            return new GrenadeEntityState
            {
                Id = id,
                OwnerId = ownerId,
                SpawnTime = spawnTime,
                FuseTime = fuseTime,
                Damage = damage,
                ExplosionRadius = explosionRadius,
            };
        }
    }
}
