using Adapters;
using State;

namespace Systems
{
    /// <summary>
    /// Validates a <see cref="WeaponConfiguration"/> against the core definition
    /// registry and, on success, composes its <see cref="WeaponStats"/>.
    ///
    /// Per D7 (strict ghost-weapon): any missing definition fails the assembly —
    /// no auto-repair of optional fields. Callers (typically <c>WeaponSyncSystem</c>)
    /// handle failure by leaving the inventory item in place and emitting
    /// <see cref="IRaidEvents.WeaponAssemblyFailed"/>.
    ///
    /// This system intentionally does NOT build a <c>WeaponEntityState</c> — that
    /// responsibility moves to <c>WeaponSyncSystem</c> after Cluster C's state
    /// refactor. For now the assembly produces the composed stats plus the
    /// resolved definitions, which are enough for the caller to finish the job.
    ///
    /// See docs/ai/weapons.md
    /// </summary>
    public static class WeaponAssemblySystem
    {
        public readonly struct AssemblyResult
        {
            public readonly WeaponStats            Stats;
            public readonly PayloadCoreDefinition  PayloadDefinition;
            public readonly DeliveryCoreDefinition DeliveryDefinition;
            public readonly ExoticModDefinition    ExoticDefinition;

            public AssemblyResult(
                WeaponStats            stats,
                PayloadCoreDefinition  payloadDefinition,
                DeliveryCoreDefinition deliveryDefinition,
                ExoticModDefinition    exoticDefinition)
            {
                Stats              = stats;
                PayloadDefinition  = payloadDefinition;
                DeliveryDefinition = deliveryDefinition;
                ExoticDefinition   = exoticDefinition;
            }
        }

        public static bool TryAssemble(
            WeaponConfiguration      config,
            ICoreDefinitionRegistry  registry,
            out AssemblyResult       result,
            out string               failReason)
        {
            result     = default;
            failReason = null;

            if (registry == null)
            {
                failReason = "Core definition registry is not available.";
                return false;
            }

            if (!registry.TryGetPayload(config.Payload.DefinitionId, out var payloadDef) || payloadDef == null)
            {
                failReason = $"Payload definition '{config.Payload.DefinitionId}' not found in registry.";
                return false;
            }

            if (!registry.TryGetDelivery(config.Delivery.DefinitionId, out var deliveryDef) || deliveryDef == null)
            {
                failReason = $"Delivery definition '{config.Delivery.DefinitionId}' not found in registry.";
                return false;
            }

            ExoticModDefinition exoticDef = null;
            if (config.Exotic.HasValue)
            {
                var exoticId = config.Exotic.Value.DefinitionId;
                if (!registry.TryGetExotic(exoticId, out exoticDef) || exoticDef == null)
                {
                    failReason = $"Exotic mod definition '{exoticId}' not found in registry.";
                    return false;
                }
            }

            var stats = WeaponStatComposer.Compose(
                payloadDef,  config.Payload.Rarity,
                deliveryDef, config.Delivery.Rarity,
                exoticDef);

            // Attachments tune the composed stats (non-critical: unknown ones are skipped).
            stats = WeaponStatComposer.ApplyAttachments(stats, config, registry);

            result = new AssemblyResult(stats, payloadDef, deliveryDef, exoticDef);
            return true;
        }
    }
}
