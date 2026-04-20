using UnityEngine;

namespace State
{
    /// <summary>
    /// Ballistic Round — standard solid projectile, grounded baseline payload.
    /// No payload-specific stats; only <see cref="CommonPayloadStats"/> apply.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewBallisticPayload",
        menuName = "Weapon Builder/Payload/Ballistic")]
    public class BallisticPayloadDefinition : PayloadCoreDefinition
    {
    }
}
