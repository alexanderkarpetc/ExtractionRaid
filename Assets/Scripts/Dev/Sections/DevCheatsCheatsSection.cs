using UnityEngine;

namespace Dev
{
    public class DevCheatsCheatsSection : ScriptableObject
    {
        public bool GodMode;
        public bool InfiniteAmmo;
        // Hard-off for bleed on player victim — blocks both apply AND tick damage.
        // Orthogonal to GodMode: GodMode lets bleed apply (so HUD/worldspace icons
        // show on player) but zeroes tick damage; this flag is the explicit kill switch.
        public bool IgnoreBleed;
    }
}
