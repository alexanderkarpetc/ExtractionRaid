using UnityEngine;

namespace State
{
    /// <summary>
    /// Exotic Mod definition (ScriptableObject) — an optional single modifier that adds
    /// a distinctive twist on top of a Payload + Delivery composition.
    /// No rarity (hard rule from design.md §3).
    ///
    /// This is a minimal shell for Tier 0 — the full stat-modifier and behaviour-hook
    /// shape is defined in Tier 5. See docs/ai/weapon-builder/plan/roadmap.md.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewExoticMod",
        menuName = "Weapon Builder/Exotic Mod")]
    public class ExoticModDefinition : ScriptableObject
    {
        [SerializeField] string _id;
        [SerializeField] string _archetype;

        public string Id        => _id;
        public string Archetype => _archetype;

        // NOTE: stat modifiers and behaviour hooks are intentionally absent here.
        // They are defined as part of Tier 5 (Exotic Mods implementation).
    }
}
