using Dev;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Shared armor break visual effects used by both PlayerPresenter and BotPresenter.
    /// </summary>
    public static class ArmorBreakHelper
    {
        /// <summary>
        /// Detaches helmet from parent, adds Rigidbody with upward + random impulse,
        /// and schedules destruction after a delay. Call after DetachHelmetModel().
        /// </summary>
        public static void FlyOffHelmet(GameObject helmet)
        {
            if (helmet == null) return;

            // Unparent so it stays in world space
            helmet.transform.SetParent(null);

            // Add physics
            var rb = helmet.GetComponent<Rigidbody>();
            if (rb == null)
                rb = helmet.AddComponent<Rigidbody>();

            rb.mass = 0.5f;
            rb.drag = 0.5f;
            rb.angularDrag = 0.3f;

            // Impulse: upward + random horizontal
            float force = DevCheats.Config.Armor != null ? 4f : 4f; // future: DevCheats.HelmetFlyForce
            var flyDir = Vector3.up + new Vector3(
                Random.Range(-0.5f, 0.5f),
                0f,
                Random.Range(-0.5f, 0.5f));
            rb.AddForce(flyDir.normalized * force, ForceMode.Impulse);

            // Random spin
            float torque = 8f; // future: DevCheats.HelmetFlyTorque
            rb.AddTorque(Random.insideUnitSphere * torque, ForceMode.Impulse);

            // Destroy after delay
            float lifetime = 3f; // future: DevCheats.HelmetFlyLifetime
            Object.Destroy(helmet, lifetime);
        }
    }
}
