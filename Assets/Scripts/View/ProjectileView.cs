using ApplicationCore;
using State;
using UnityEngine;

namespace View
{
    public class ProjectileView : MonoBehaviour
    {
        public EId EId { get; private set; }
        float _damage;
        float _penetration;
        float _armorDamage;
        float _bleedChance;
        bool _hit;
        EId _targetedEntityId;

        public void Initialize(EId id, float damage, EId targetedEntityId = default,
            float penetration = 0f, float armorDamage = 0f, float bleedChance = 0f)
        {
            EId = id;
            _damage = damage;
            _penetration = penetration;
            _armorDamage = armorDamage;
            _bleedChance = bleedChance;
            _targetedEntityId = targetedEntityId;
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
                // SphereCast along movement path — small radius compensates for
                // camera-angle parallax between crosshair and bullet trajectory
                float hitRadius = Dev.DevCheats.ProjectileHitRadius;
                if (Physics.SphereCast(oldPos, hitRadius, delta / dist, out var hit, dist,
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
            var session = App.Instance.RaidSession;
            if (session == null) return;

            var damageable = other.GetComponent<IDamageableView>();
            if (damageable != null)
            {
                session.ReportHit(new HitSignal
                {
                    ProjectileId = EId,
                    TargetId = damageable.EId,
                    Damage = _damage,
                    Penetration = _penetration,
                    ArmorDamage = _armorDamage,
                    BleedChance = _bleedChance,
                    HitPoint = hitPoint,
                    TargetedEntityId = _targetedEntityId,
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
