using System.Collections.Generic;
using System.Reflection;
using Adapters;
using State;
using UnityEngine;

namespace Tests.EditMode.Fakes
{
    /// <summary>
    /// Single entry point for building Weapon Builder ScriptableObject fixtures in
    /// EditMode tests. Consolidates the ~9 ad-hoc Make{Ballistic,Laser,Delivery,Exotic}
    /// + <c>SetPrivateField</c> copies that used to live in individual test files.
    ///
    /// Every Make* method returns a fresh <see cref="ScriptableObject"/> — call
    /// <see cref="DestroyAll"/> in TearDown to clean up.
    ///
    /// Private fields are poked via reflection (DeclaredOnly + BaseType walk) because
    /// the SOs hide their fields behind <c>[SerializeField]</c> for Inspector authoring.
    /// </summary>
    public static class WeaponBuilderTestFactory
    {
        // ── Payload definitions ───────────────────────────────

        /// <summary>
        /// Ballistic payload (most common test subject). All fields optional — pass
        /// only what your test cares about. Unspecified stats default to zeroed
        /// <see cref="CommonPayloadStats"/>.
        /// </summary>
        public static BallisticPayloadDefinition MakeBallistic(
            string id = "BallisticRound",
            string displayName = null,
            string ammoType = null,
            CommonPayloadStats? commonStats = null,
            RarityTier statsTier = RarityTier.Common)
        {
            return MakePayload<BallisticPayloadDefinition>(
                id, displayName, ammoType, commonStats, statsTier);
        }

        /// <summary>
        /// Laser payload with a charge-up time. <paramref name="chargeTime"/> populates
        /// <see cref="LaserSpecificStats"/> at <paramref name="statsTier"/>.
        /// </summary>
        public static LaserPayloadDefinition MakeLaser(
            string id = "LaserCharge",
            string displayName = null,
            string ammoType = null,
            CommonPayloadStats? commonStats = null,
            float chargeTime = 1f,
            RarityTier statsTier = RarityTier.Common)
        {
            var def = MakePayload<LaserPayloadDefinition>(id, displayName, ammoType, commonStats, statsTier);
            var specific = new LaserSpecificStats[5];
            specific[(int)statsTier] = new LaserSpecificStats { ChargeTime = chargeTime };
            SetPrivateField(def, "_specificByTier", specific);
            return def;
        }

        /// <summary>
        /// Generic payload factory — use for concrete subclasses that don't have a
        /// dedicated helper yet (e.g. <c>RocketPayloadDefinition</c>, <c>FoamPayloadDefinition</c>).
        /// </summary>
        public static T MakePayload<T>(
            string id,
            string displayName = null,
            string ammoType = null,
            CommonPayloadStats? commonStats = null,
            RarityTier statsTier = RarityTier.Common)
            where T : PayloadCoreDefinition
        {
            var def = ScriptableObject.CreateInstance<T>();
            SetPrivateField(def, "_id", id);
            if (displayName != null) SetPrivateField(def, "_displayName", displayName);
            if (ammoType    != null) SetPrivateField(def, "_ammoType",    ammoType);

            var array = new CommonPayloadStats[5];
            array[(int)statsTier] = commonStats ?? default;
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        // ── Delivery definitions ──────────────────────────────

        public static DeliveryCoreDefinition MakeDelivery(
            string id = "Auto",
            string formFactor = null,
            FiringPattern pattern = FiringPattern.Single,
            DeliveryStats? commonStats = null,
            RarityTier statsTier = RarityTier.Common,
            GameObject weaponPrefab = null)
        {
            var def = ScriptableObject.CreateInstance<DeliveryCoreDefinition>();
            SetPrivateField(def, "_id", id);
            if (formFactor != null)   SetPrivateField(def, "_formFactor", formFactor);
            if (weaponPrefab != null) SetPrivateField(def, "_weaponPrefab", weaponPrefab);
            SetPrivateField(def, "_pattern", pattern);

            var array = new DeliveryStats[5];
            array[(int)statsTier] = commonStats ?? default;
            SetPrivateField(def, "_statsByTier", array);
            return def;
        }

        /// <summary>
        /// Creates a stub <see cref="GameObject"/> for use as a Delivery's <c>WeaponPrefab</c>
        /// in EditMode tests. The GO's <c>.name</c> drives <see cref="WeaponEntityState.PrefabId"/>
        /// after assembly — pass e.g. "Weapon_Pistol" to mirror production prefab naming.
        /// Caller is responsible for cleanup via <see cref="DestroyAll"/> in TearDown.
        /// </summary>
        public static GameObject MakeStubWeaponPrefab(string name)
        {
            return new GameObject(name);
        }

        // ── Exotic definitions ────────────────────────────────

        public static ExoticModDefinition MakeExotic(string id)
        {
            var def = ScriptableObject.CreateInstance<ExoticModDefinition>();
            SetPrivateField(def, "_id", id);
            return def;
        }

        // ── Database + Registry ───────────────────────────────

        public static CoreDefinitionDatabase MakeDatabase(
            IEnumerable<PayloadCoreDefinition> payloads = null,
            IEnumerable<DeliveryCoreDefinition> deliveries = null,
            IEnumerable<ExoticModDefinition> exotics = null)
        {
            var db = ScriptableObject.CreateInstance<CoreDefinitionDatabase>();
            db.SetEntries(
                payloads   == null ? new List<PayloadCoreDefinition>()  : new List<PayloadCoreDefinition>(payloads),
                deliveries == null ? new List<DeliveryCoreDefinition>() : new List<DeliveryCoreDefinition>(deliveries),
                exotics    == null ? new List<ExoticModDefinition>()    : new List<ExoticModDefinition>(exotics));
            return db;
        }

        public static ICoreDefinitionRegistry MakeRegistry(CoreDefinitionDatabase db)
            => new DatabaseCoreDefinitionRegistry(db);

        // ── Cleanup ───────────────────────────────────────────

        /// <summary>
        /// Destroys a batch of ScriptableObjects (convenience for TearDown).
        /// Null entries are skipped silently.
        /// </summary>
        public static void DestroyAll(params Object[] objects)
        {
            if (objects == null) return;
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            }
        }

        // ── Reflection helper ─────────────────────────────────

        /// <summary>
        /// Sets a private (or protected) field by name. Walks the type hierarchy to
        /// find fields declared on base classes (needed for Payload subclasses that
        /// inherit <c>_id</c> / <c>_statsByTier</c> from <see cref="PayloadCoreDefinition"/>).
        /// </summary>
        public static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            throw new System.InvalidOperationException(
                $"Field '{fieldName}' not found on {target.GetType()}.");
        }
    }
}
