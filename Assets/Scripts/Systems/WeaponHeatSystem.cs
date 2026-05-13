using Session;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Ballistic Rifle signature mechanic (B1) — barrel heat decay.
    /// Each frame: decreases <see cref="WeaponEntityState.HeatLevel"/> by
    /// <see cref="BarrelHeatConfig.DecayPerSecond"/> × dt. ShootingSystem owns increment
    /// path (only Ballistic+Auto), this system owns the cool-down path для всіх weapons
    /// (no-op коли HeatLevel already 0).
    ///
    /// Persistent: heat decays through reload, weapon swap, charge phases, etc. — no hard
    /// reset, no gating. Behavior is purely time-based cool-down.
    /// </summary>
    public static class WeaponHeatSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            if (!context.BarrelHeatConfig.Enabled) return;

            float decay = context.BarrelHeatConfig.DecayPerSecond * context.DeltaTime;
            if (decay <= 0f) return;

            // Player weapon
            var weapon = state.PlayerEntity?.EquippedWeapon;
            if (weapon != null && weapon.HeatLevel > 0f)
                weapon.HeatLevel = Mathf.Max(0f, weapon.HeatLevel - decay);

            // Bot weapons — same cool path. Currently bots don't increment heat (only player
            // path у ShootingSystem). If bots ever use Ballistic+Auto + need heat signature,
            // increment is the side that needs the change, not this decay loop.
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var bw = state.Bots[i].Weapon;
                if (bw != null && bw.HeatLevel > 0f)
                    bw.HeatLevel = Mathf.Max(0f, bw.HeatLevel - decay);
            }
        }
    }
}
