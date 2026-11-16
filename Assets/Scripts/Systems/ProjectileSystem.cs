using Session;
using State;

namespace Systems
{
    public static class ProjectileSystem
    {
        public static void ResolveCollision(RaidState state, in CollisionSignal collision, in RaidContext context)
        {
            for (int i = state.Projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = state.Projectiles[i];
                if (projectile.Id != collision.ProjectileId) continue;

                float magnitude = projectile.ImpactImpulse;
                var impulse = magnitude > 0f && !float.IsInfinity(magnitude)
                    ? projectile.Direction.normalized * magnitude
                    : UnityEngine.Vector3.zero;
                context.Events.ProjectileHit(projectile.Id, collision.Position, collision.Normal,
                    collision.SurfaceType ?? "surface", projectile.Archetype, impulse);
                context.Events.ProjectileDespawned(projectile.Id);
                state.Projectiles.RemoveAt(i);
                return;
            }
        }

        public static void Tick(RaidState state, in RaidContext context)
        {
            for (int i = state.Projectiles.Count - 1; i >= 0; i--)
            {
                var proj = state.Projectiles[i];

                proj.Position += proj.Direction * (proj.Speed * context.DeltaTime);

                if (state.ElapsedTime - proj.SpawnTime >= proj.Lifetime)
                {
                    context.Events.ProjectileDespawned(proj.Id);
                    state.Projectiles.RemoveAt(i);
                }
            }
        }
    }
}
