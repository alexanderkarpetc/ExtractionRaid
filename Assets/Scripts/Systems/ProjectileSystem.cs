using Session;
using State;

namespace Systems
{
    public static class ProjectileSystem
    {
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
