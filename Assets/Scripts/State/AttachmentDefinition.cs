using System;
using System.Collections.Generic;
using UnityEngine;

namespace State
{
    /// <summary>
    /// A weapon attachment (mod) — a sidegrade that tunes player-facing stats without
    /// changing the weapon's logic (logic = Payload + Delivery). Flat: no rarity tiers
    /// (catalog Q28). Compatibility is by <see cref="Slot"/> + optional
    /// <see cref="CompatibleArchetype"/>; enforced at install time (P2.2), not here.
    ///
    /// Stat changes are a list of <see cref="StatDelta"/> on player-facing axes
    /// (option A) — WeaponStatComposer.ApplyAttachments maps each axis to raw
    /// <see cref="WeaponStats"/> fields. See docs/ai/weapon-builder/attachments/.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewAttachment",
        menuName = "Weapon Builder/Attachment")]
    public class AttachmentDefinition : ScriptableObject
    {
        [SerializeField] string _id;
        [SerializeField] string _displayName;
        [SerializeField] AttachmentSlot _slot;

        [Tooltip("Empty = universal. Otherwise restricts to a payload archetype (e.g. \"Laser\") " +
                 "or delivery pattern/form (e.g. \"Scatter\", \"Auto\") — matched at install time (P2.2).")]
        [SerializeField] string _compatibleArchetype;

        [SerializeField] StatDelta[] _modifiers = Array.Empty<StatDelta>();

        public string Id => _id;
        public string DisplayName => _displayName;
        public AttachmentSlot Slot => _slot;
        /// <summary>Empty = universal; else an archetype/pattern token restricting where it fits.</summary>
        public string CompatibleArchetype => _compatibleArchetype;
        public IReadOnlyList<StatDelta> Modifiers => _modifiers;
    }

    /// <summary>
    /// One stat change applied by an attachment. <see cref="Percent"/> is whole
    /// (50 = +50%, -10 = -10%) and changes the named axis's stat value directly
    /// (raw-change semantics — see <see cref="WeaponStatAxis"/>).
    /// </summary>
    [Serializable]
    public struct StatDelta
    {
        public WeaponStatAxis Axis;
        public float Percent;
    }
}
