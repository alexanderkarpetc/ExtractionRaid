using System.Collections.Generic;
using UnityEngine;

namespace State
{
    /// <summary>
    /// Central ScriptableObject aggregator that holds references to every
    /// Payload / Delivery / Exotic definition used by the Weapon Builder.
    ///
    /// Designers populate the lists in Inspector. At runtime, lookups go through
    /// <see cref="ICoreDefinitionRegistry"/> which wraps this database and builds
    /// indexed lookups lazily on first access.
    ///
    /// Same pattern as <see cref="Quests.QuestDatabase"/>.
    /// See docs/ai/weapon-builder/README.md
    /// </summary>
    [CreateAssetMenu(
        fileName = "CoreDefinitionDatabase",
        menuName = "Weapon Builder/Core Definition Database")]
    public class CoreDefinitionDatabase : ScriptableObject
    {
        [SerializeField] List<PayloadCoreDefinition>  _payloads    = new();
        [SerializeField] List<DeliveryCoreDefinition> _deliveries  = new();
        [SerializeField] List<ExoticModDefinition>    _exotics     = new();
        [SerializeField] List<AttachmentDefinition>   _attachments = new();

        public IReadOnlyList<PayloadCoreDefinition>  Payloads    => _payloads;
        public IReadOnlyList<DeliveryCoreDefinition> Deliveries  => _deliveries;
        public IReadOnlyList<ExoticModDefinition>    Exotics     => _exotics;
        public IReadOnlyList<AttachmentDefinition>   Attachments => _attachments;

#if UNITY_EDITOR
        /// <summary>Editor-only helper used by tests and tooling to populate the database.</summary>
        public void SetEntries(
            List<PayloadCoreDefinition>  payloads,
            List<DeliveryCoreDefinition> deliveries,
            List<ExoticModDefinition>    exotics,
            List<AttachmentDefinition>   attachments = null)
        {
            _payloads    = payloads    ?? new List<PayloadCoreDefinition>();
            _deliveries  = deliveries  ?? new List<DeliveryCoreDefinition>();
            _exotics     = exotics     ?? new List<ExoticModDefinition>();
            _attachments = attachments ?? new List<AttachmentDefinition>();
        }
#endif
    }
}
