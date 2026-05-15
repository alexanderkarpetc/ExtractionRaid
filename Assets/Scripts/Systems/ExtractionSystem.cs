using Constants;
using Session;
using State;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Ticks extraction progress on <see cref="PlayerEntityState"/> based on
    /// whether the player is currently standing inside any
    /// <see cref="ExtractionPointState"/>'s radius (XZ plane — height ignored).
    /// Leaving every zone resets progress to 0 and clears the active point id.
    /// Reaching 1.0 stops there — the HUD presenter is the one that observes the
    /// completion and calls <c>App.RequestExtraction()</c>, since systems are
    /// forbidden from touching <c>App</c> (CLAUDE.md rule 4).
    /// </summary>
    public static class ExtractionSystem
    {
        public static void Tick(RaidState state, in RaidContext context)
        {
            var player = state.PlayerEntity;
            if (player == null) return;
            if (state.ExtractionPoints == null || state.ExtractionPoints.Count == 0) return;
            // Already extracted — leave the 1.0 reading visible until the HUD acts on it.
            if (player.ExtractionProgress01 >= 1f) return;

            var inside = FindContainingZone(state, player.Position);
            if (inside == null)
            {
                if (player.ActiveExtractionPointId != EId.None)
                {
                    player.ActiveExtractionPointId = EId.None;
                    player.ExtractionProgress01 = 0f;
                }
                return;
            }

            player.ActiveExtractionPointId = inside.Id;
            float duration = Mathf.Max(0.0001f, ExtractionConstants.ExtractDurationSeconds);
            float step = context.DeltaTime / duration;
            player.ExtractionProgress01 = Mathf.Clamp01(player.ExtractionProgress01 + step);
        }

        static ExtractionPointState FindContainingZone(RaidState state, Vector3 playerPos)
        {
            // Multiple zones in a single level are allowed; first match wins. Distance
            // check is XZ-only — extraction is a floor footprint, not a sphere.
            for (int i = 0; i < state.ExtractionPoints.Count; i++)
            {
                var ep = state.ExtractionPoints[i];
                float dx = playerPos.x - ep.Position.x;
                float dz = playerPos.z - ep.Position.z;
                if (dx * dx + dz * dz <= ep.Radius * ep.Radius)
                    return ep;
            }
            return null;
        }
    }
}
