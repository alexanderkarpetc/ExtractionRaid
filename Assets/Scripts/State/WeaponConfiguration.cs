using System;
using UnityEngine;

namespace State
{
    /// <summary>
    /// Persistent weapon assembly — lives inside an InventoryItem.
    /// Holds references to the chosen Payload + Delivery cores (+ optional Exotic)
    /// plus magazine contents that persist between equip/unequip cycles.
    ///
    /// Runtime form (WeaponEntityState) is created by WeaponSyncSystem when the weapon
    /// is equipped, from this configuration.
    ///
    /// Unity serialization does not support Nullable&lt;T&gt; for structs cleanly, so the
    /// optional Exotic slot is stored as (bool HasExotic, ExoticModInstance Exotic).
    /// External code should access it through the <see cref="Exotic"/> property,
    /// which returns a C# nullable value type.
    ///
    /// See docs/ai/weapon-builder/architecture.md §1, §D4.
    /// </summary>
    [Serializable]
    public struct WeaponConfiguration
    {
        public PayloadCoreInstance  Payload;
        public DeliveryCoreInstance Delivery;

        // Optional Exotic — Unity-serializable nullable pattern (bool flag + value).
        [SerializeField] bool              _hasExotic;
        [SerializeField] ExoticModInstance _exotic;

        public int AmmoInMagazine;

        public ExoticModInstance? Exotic
        {
            get => _hasExotic ? _exotic : (ExoticModInstance?)null;
            set
            {
                _hasExotic = value.HasValue;
                _exotic    = value ?? default;
            }
        }

        public WeaponConfiguration(
            PayloadCoreInstance  payload,
            DeliveryCoreInstance delivery,
            ExoticModInstance?   exotic,
            int                  ammoInMagazine)
        {
            Payload        = payload;
            Delivery       = delivery;
            _hasExotic     = exotic.HasValue;
            _exotic        = exotic ?? default;
            AmmoInMagazine = ammoInMagazine;
        }
    }
}
