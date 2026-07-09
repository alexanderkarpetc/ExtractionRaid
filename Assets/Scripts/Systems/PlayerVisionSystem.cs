using Session;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Resolves the player's sniper-scope reveal each tick from the equipped weapon's
    /// <see cref="WeaponStats.SightRangeBonus"/> and the current ADS blend. Writes
    /// ScopeReveal/ScopeRadius/ScopeCenter onto <see cref="PlayerEntityState"/>; those are
    /// consumed by <see cref="PlayerFOVSystem"/> (spotting bots through the scope) and by the
    /// camera + fog-of-war view (pan-to-cursor + circular reveal). Pure — no side effects
    /// beyond the player state fields. Runs right before PlayerFOVSystem in the tick order.
    /// </summary>
    public static class PlayerVisionSystem
    {
        public static void Tick(RaidState state, in RaidContext ctx)
        {
            var player = state.PlayerEntity;
            if (player == null) return;

            float bonus = player.EquippedWeapon != null ? player.EquippedWeapon.Stats.SightRangeBonus : 0f;
            bool hasScope = bonus > 0f;

            // Scope only "opens" while aiming (ZERO Sievert model) AND blends by how far the aim
            // point is from the player: cursor near the character → normal crosshair (reveal ~0);
            // pushed far out → full sniper scope (reveal ~1). AdsBlend eases the whole thing in.
            float reveal = 0f;
            if (hasScope && player.IsADS)
            {
                var toAim = player.RawAimPoint - player.Position;
                toAim.y = 0f;
                float distBlend = Mathf.InverseLerp(ctx.AimConfig.ScopeNearDistance, ctx.AimConfig.ScopeFarDistance, toAim.magnitude);
                reveal = Mathf.Clamp01(player.AdsBlend) * distBlend;
            }
            player.ScopeReveal = reveal;
            player.ScopeRadius = hasScope ? bonus : 0f;
            player.ScopeCenter = player.RawAimPoint; // unclamped cursor point — the circle follows the mouse
        }
    }
}
