using System.Collections;
using Constants;
using State;
using UnityEngine;

namespace View
{
    public class GrenadeView : MonoBehaviour
    {
        public EId EId { get; private set; }

        Rigidbody _rb;
        LineRenderer _radiusLine;

        const int RadiusSegments = 48;
        const float IgnoreOwnerDuration = 0.5f;

        public void Initialize(EId id, Vector3 velocity)
        {
            EId = id;
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
                _rb = gameObject.AddComponent<Rigidbody>();

            _rb.mass = 0.6f;
            _rb.linearDamping = 0f;
            _rb.angularDamping = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            _rb.linearVelocity = velocity;

            CreateRadiusCircle();
        }

        public void IgnoreCollisionWith(GameObject owner)
        {
            if (owner == null) return;

            var grenadeColliders = GetComponentsInChildren<Collider>();
            var ownerColliders = owner.GetComponentsInChildren<Collider>();

            foreach (var gc in grenadeColliders)
                foreach (var oc in ownerColliders)
                    Physics.IgnoreCollision(gc, oc, true);

            StartCoroutine(ReenableCollision(grenadeColliders, ownerColliders));
        }

        IEnumerator ReenableCollision(Collider[] grenadeColliders, Collider[] ownerColliders)
        {
            yield return new WaitForSeconds(IgnoreOwnerDuration);

            foreach (var gc in grenadeColliders)
            {
                if (gc == null) yield break;
                foreach (var oc in ownerColliders)
                {
                    if (oc == null) continue;
                    Physics.IgnoreCollision(gc, oc, false);
                }
            }
        }

        void CreateRadiusCircle()
        {
            var go = new GameObject("ExplosionRadius");
            go.transform.SetParent(transform);
            _radiusLine = go.AddComponent<LineRenderer>();
            _radiusLine.startWidth = 0.03f;
            _radiusLine.endWidth = 0.03f;
            _radiusLine.material = new Material(Shader.Find("Sprites/Default"));
            _radiusLine.startColor = new Color(1f, 0.3f, 0f, 0.35f);
            _radiusLine.endColor = new Color(1f, 0.3f, 0f, 0.35f);
            _radiusLine.loop = true;
            _radiusLine.useWorldSpace = true;
            _radiusLine.positionCount = RadiusSegments;
        }

        void LateUpdate()
        {
            if (_radiusLine == null) return;

            var center = transform.position;
            center.y = 0.05f;
            float radius = GrenadeConstants.ExplosionRadius;

            for (int i = 0; i < RadiusSegments; i++)
            {
                float angle = (float)i / RadiusSegments * Mathf.PI * 2f;
                _radiusLine.SetPosition(i,
                    center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.2f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, GrenadeConstants.ExplosionRadius);
        }
    }
}
