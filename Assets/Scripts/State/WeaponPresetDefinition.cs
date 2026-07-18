using System.Collections.Generic;
using UnityEngine;

namespace State
{
    /// <summary>
    /// A designer-authored, ready-to-spawn weapon build — one asset per gun.
    ///
    /// A weapon is only valid when it has both a Payload core (what it fires) and a
    /// Delivery core (how it fires), plus an optional Exotic and optional attachment
    /// mods. Rather than re-picking those pieces every time in the Dev Cheats window,
    /// designers configure a full build once here and the "Give Weapon Preset" cheat
    /// spawns it directly.
    ///
    /// References <see cref="PayloadCoreDefinition"/> / <see cref="DeliveryCoreDefinition"/>
    /// (etc.) by asset — their <c>Id</c>s are read at <see cref="BuildConfiguration"/>
    /// time to produce the runtime <see cref="WeaponConfiguration"/>. Same identity-only
    /// pattern as the cores themselves.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewWeaponPreset",
        menuName = "Weapon Builder/Weapon Preset")]
    public class WeaponPresetDefinition : ScriptableObject
    {
        [Tooltip("Label shown in the Dev Cheats preset dropdown. Empty = asset file name.")]
        [SerializeField] string _presetName;

        [Tooltip("ItemState.DefinitionId the spawned weapon carries. Leave as \"Weapon\" " +
                 "unless a specific legacy definition is required.")]
        [SerializeField] string _weaponDefinitionId = "Weapon";

        [Header("Payload — required (what it fires)")]
        [SerializeField] PayloadCoreDefinition _payload;
        [SerializeField] RarityTier _payloadRarity = RarityTier.Common;

        [Header("Delivery — required (how it fires)")]
        [SerializeField] DeliveryCoreDefinition _delivery;
        [SerializeField] RarityTier _deliveryRarity = RarityTier.Common;

        [Header("Exotic — optional")]
        [SerializeField] ExoticModDefinition _exotic;

        [Header("Attachments — optional (one per occupied slot)")]
        [SerializeField] List<AttachmentDefinition> _attachments = new();

        /// <summary>Label for menus — falls back to the asset file name when unset.</summary>
        public string PresetName => string.IsNullOrEmpty(_presetName) ? name : _presetName;

        public string WeaponDefinitionId =>
            string.IsNullOrEmpty(_weaponDefinitionId) ? "Weapon" : _weaponDefinitionId;

        /// <summary>A weapon needs both cores to be assemblable; attachments/exotic are optional.</summary>
        public bool IsValid => _payload != null && _delivery != null;

        /// <summary>
        /// Builds the runtime <see cref="WeaponConfiguration"/> from the authored pieces.
        /// Magazine starts full (delivery's magazine size for the chosen rarity).
        /// Caller should check <see cref="IsValid"/> first.
        /// </summary>
        public WeaponConfiguration BuildConfiguration()
        {
            var deliveryStats = _delivery.StatsByTier(_deliveryRarity);

            var config = new WeaponConfiguration(
                payload:        new PayloadCoreInstance(_payload.Id, _payloadRarity),
                delivery:       new DeliveryCoreInstance(_delivery.Id, _deliveryRarity),
                exotic:         _exotic != null ? new ExoticModInstance(_exotic.Id) : (ExoticModInstance?)null,
                ammoInMagazine: deliveryStats.MagazineSize);

            if (_attachments != null && _attachments.Count > 0)
            {
                var installed = new List<AttachmentInstance>(_attachments.Count);
                var occupied  = new HashSet<AttachmentSlot>();
                foreach (var attachment in _attachments)
                {
                    if (attachment == null) continue;
                    // One mod per slot — mirrors the Weapon Builder's install rule. The
                    // custom inspector already enforces this, but a hand-edited asset
                    // (debug inspector) could sneak a duplicate slot in; first wins.
                    if (!occupied.Add(attachment.Slot)) continue;
                    installed.Add(new AttachmentInstance(attachment.Slot, attachment.Id));
                }
                if (installed.Count > 0)
                    config.Attachments = installed.ToArray();
            }

            return config;
        }
    }
}
