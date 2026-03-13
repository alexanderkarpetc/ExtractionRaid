using State;
using UnityEngine;

namespace View
{
    public class ProjectileView : MonoBehaviour
    {
        public EId EId { get; private set; }
        float _damage;
        bool _hit;

        public void Initialize(EId id, float damage)
        {
            EId = id;
            _damage = damage;
        }

        public void SyncFromState(ProjectileEntityState state)
        {
            if (_hit) return;

            var oldPos = transform.position;
            var newPos = state.Position;
            var delta = newPos - oldPos;
            float dist = delta.magnitude;

            if (dist > 0.001f)
            {
                // Raycast along movement path — frame-rate independent, no FixedUpdate dependency
                if (Physics.Raycast(oldPos, delta / dist, out var hit, dist,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    // Skip other projectiles
                    if (hit.collider.GetComponent<ProjectileView>() == null)
                    {
                        _hit = true;
                        ReportHit(hit.collider, hit.point);
                        return;
                    }
                }
            }

            transform.position = newPos;
        }

        void ReportHit(Collider other, Vector3 hitPoint)
        {
            var session = App.App.Instance.RaidSession;
            if (session == null) return;

            var damageable = other.GetComponent<IDamageableView>();
            if (damageable != null)
            {
                session.ReportHit(new HitSignal
                {
                    ProjectileId = EId,
                    TargetId = damageable.EId,
                    Damage = _damage,
                });
            }
            else
            {
                session.ReportCollision(new CollisionSignal
                {
                    ProjectileId = EId,
                    Position = hitPoint,
                });
            }
        }
    }
}
