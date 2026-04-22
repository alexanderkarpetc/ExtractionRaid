using System.Collections.Generic;
using State;
using UnityEngine;

namespace Adapters
{
    /// <summary>
    /// Default implementation of <see cref="ICoreDefinitionRegistry"/> that wraps
    /// a single <see cref="CoreDefinitionDatabase"/> ScriptableObject.
    /// Lookups are indexed lazily on first access.
    /// Duplicate Ids log a warning and the last occurrence wins.
    /// </summary>
    public class DatabaseCoreDefinitionRegistry : ICoreDefinitionRegistry
    {
        readonly CoreDefinitionDatabase _database;

        Dictionary<string, PayloadCoreDefinition>  _payloadIndex;
        Dictionary<string, DeliveryCoreDefinition> _deliveryIndex;
        Dictionary<string, ExoticModDefinition>    _exoticIndex;

        public DatabaseCoreDefinitionRegistry(CoreDefinitionDatabase database)
        {
            _database = database ?? throw new System.ArgumentNullException(nameof(database));
        }

        public IReadOnlyList<PayloadCoreDefinition>  AllPayloads   => _database.Payloads;
        public IReadOnlyList<DeliveryCoreDefinition> AllDeliveries => _database.Deliveries;
        public IReadOnlyList<ExoticModDefinition>    AllExotics    => _database.Exotics;

        public PayloadCoreDefinition GetPayload(string id)
        {
            EnsurePayloadIndex();
            if (_payloadIndex.TryGetValue(id, out var def)) return def;
            throw new KeyNotFoundException($"Payload core definition not found: '{id}'");
        }

        public DeliveryCoreDefinition GetDelivery(string id)
        {
            EnsureDeliveryIndex();
            if (_deliveryIndex.TryGetValue(id, out var def)) return def;
            throw new KeyNotFoundException($"Delivery core definition not found: '{id}'");
        }

        public ExoticModDefinition GetExotic(string id)
        {
            EnsureExoticIndex();
            if (_exoticIndex.TryGetValue(id, out var def)) return def;
            throw new KeyNotFoundException($"Exotic mod definition not found: '{id}'");
        }

        public bool TryGetPayload(string id, out PayloadCoreDefinition definition)
        {
            EnsurePayloadIndex();
            return _payloadIndex.TryGetValue(id, out definition);
        }

        public bool TryGetDelivery(string id, out DeliveryCoreDefinition definition)
        {
            EnsureDeliveryIndex();
            return _deliveryIndex.TryGetValue(id, out definition);
        }

        public bool TryGetExotic(string id, out ExoticModDefinition definition)
        {
            EnsureExoticIndex();
            return _exoticIndex.TryGetValue(id, out definition);
        }

        // ── Index builders ──────────────────────────────────────

        void EnsurePayloadIndex()
        {
            if (_payloadIndex != null) return;
            _payloadIndex = new Dictionary<string, PayloadCoreDefinition>(_database.Payloads.Count);
            foreach (var def in _database.Payloads)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (_payloadIndex.ContainsKey(def.Id))
                    Debug.LogWarning($"[CoreDefinitionRegistry] Duplicate payload id '{def.Id}'. Last occurrence wins.");
                _payloadIndex[def.Id] = def;
            }
        }

        void EnsureDeliveryIndex()
        {
            if (_deliveryIndex != null) return;
            _deliveryIndex = new Dictionary<string, DeliveryCoreDefinition>(_database.Deliveries.Count);
            foreach (var def in _database.Deliveries)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (_deliveryIndex.ContainsKey(def.Id))
                    Debug.LogWarning($"[CoreDefinitionRegistry] Duplicate delivery id '{def.Id}'. Last occurrence wins.");
                _deliveryIndex[def.Id] = def;
            }
        }

        void EnsureExoticIndex()
        {
            if (_exoticIndex != null) return;
            _exoticIndex = new Dictionary<string, ExoticModDefinition>(_database.Exotics.Count);
            foreach (var def in _database.Exotics)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (_exoticIndex.ContainsKey(def.Id))
                    Debug.LogWarning($"[CoreDefinitionRegistry] Duplicate exotic id '{def.Id}'. Last occurrence wins.");
                _exoticIndex[def.Id] = def;
            }
        }
    }
}
