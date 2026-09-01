namespace State
{
    /// <summary>
    /// Pattern that a Delivery Core implements when firing.
    /// Used as a dispatch key inside ShootingSystem — each pattern maps
    /// to a static handler method. See docs/ai/weapons.md.
    /// </summary>
    public enum FiringPattern
    {
        /// <summary>One projectile, one shot, full cooldown between shots.</summary>
        Single  = 0,
        /// <summary>Hold to fire, repeats at FireInterval cadence.</summary>
        Auto    = 1,
        /// <summary>One trigger pull, multiple projectiles with spread (shotgun-like).</summary>
        Scatter = 2,
        /// <summary>Requires spin-up before firing; high sustained rate.</summary>
        Rotary  = 3,
        /// <summary>One trigger pull produces a volley — series of shots with internal interval.</summary>
        Swarm   = 4,
    }
}
