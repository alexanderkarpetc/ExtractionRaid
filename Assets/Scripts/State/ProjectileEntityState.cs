using UnityEngine;

namespace State
{
    public class ProjectileEntityState
    {
        public EId Id;
        public Vector3 Position;
        public Vector3 Direction;
        public float Speed;
        public float SpawnTime;
        public float Lifetime;

        public static ProjectileEntityState Create(
            EId id, Vector3 position, Vector3 direction,
            float speed, float spawnTime, float lifetime)
        {
            return new ProjectileEntityState
            {
                Id = id,
                Position = position,
                Direction = direction,
                Speed = speed,
                SpawnTime = spawnTime,
                Lifetime = lifetime,
            };
        }
    }
}
