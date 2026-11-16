using UnityEngine;

namespace View
{
    /// <summary>Opt-in physical response for scenery. Attach beside the prop's Rigidbody.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ImpactReactiveProp : MonoBehaviour
    {
        Rigidbody _body;

        void Awake() => _body = GetComponent<Rigidbody>();

        public void ApplyImpulse(Vector3 impulse, Vector3 point)
        {
            if (!isActiveAndEnabled || _body == null || _body.isKinematic) return;
            _body.AddForceAtPosition(impulse, point, ForceMode.Impulse);
        }
    }
}
