namespace State
{
    /// <summary>
    /// Composes the human-readable archetype label for a weapon build.
    /// Pure template: <c>"{payload.DisplayName} {delivery.FormFactor}"</c>.
    ///
    /// Examples:
    ///   (Ballistic, Pistol)  → "Ballistic Pistol"
    ///   (Laser, Rifle)       → "Laser Rifle"
    ///   (Foam, Shotgun)      → "Foam Shotgun"
    ///   (Rocket, Launcher)   → "Rocket Launcher"
    ///
    /// Overrides for special combos are deferred to Tier 5 polish.
    /// Exotic mods are intentionally NOT part of the label.
    /// See docs/ai/weapon-builder/architecture.md §D8.
    /// </summary>
    public static class WeaponArchetypeLabel
    {
        public static string Compose(PayloadCoreDefinition payload, DeliveryCoreDefinition delivery)
        {
            var payloadName = payload != null ? payload.DisplayName : null;
            var formFactor  = delivery != null ? delivery.FormFactor : null;

            var hasPayload  = !string.IsNullOrEmpty(payloadName);
            var hasDelivery = !string.IsNullOrEmpty(formFactor);

            if (hasPayload && hasDelivery) return $"{payloadName} {formFactor}";
            if (hasPayload)                return payloadName;
            if (hasDelivery)               return formFactor;
            return string.Empty;
        }
    }
}
