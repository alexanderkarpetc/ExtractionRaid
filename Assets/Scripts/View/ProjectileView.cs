using ApplicationCore;
using State;
using UnityEngine;
using View.Audio;

namespace View
{
    public class ProjectileView : MonoBehaviour
    {
        // Shared buffer for the start-overlap probe. Size 8 covers worst-case overlap
        // count (character capsule + own armor + nearby props) without GC alloc.
        static readonly Collider[] OverlapBuffer = new Collider[8];

        public EId EId { get; private set; }
        float _damage;
        float _penetration;
        float _armorDamage;
        float _bleedChance;
        bool _hit;
        EId _targetedEntityId;
        EId _ownerId;

        public void Initialize(EId id, float damage, EId targetedEntityId = default,
            float penetration = 0f, float armorDamage = 0f, float bleedChance = 0f,
            EId ownerId = default)
        {
            EId = id;
            _damage = damage;
            _penetration = penetration;
            _armorDamage = armorDamage;
            _bleedChance = bleedChance;
            _targetedEntityId = targetedEntityId;
            _ownerId = ownerId;
        }

        public void SyncFromState(ProjectileEntityState state)
        {
            if (_hit) return;

            var oldPos = transform.position;
            var newPos = state.Position;
            float hitRadius = Dev.DevCheats.ProjectileHitRadius;

            // Start-overlap probe. SphereCast skips colliders the sphere already overlaps
            // at the start (Unity behaviour — backfaces don't generate hits by default).
            // At point-blank, CharacterBody pullback can retract the muzzle inside an enemy
            // capsule, so the bullet spawns inside its target and the SphereCast below
            // would silently miss. Resolve the damageable hit directly.
            int overlapCount = Physics.OverlapSphereNonAlloc(oldPos, hitRadius, OverlapBuffer,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlapCount; i++)
            {
                var col = OverlapBuffer[i];
                if (col == null) continue;
                if (col.GetComponent<ProjectileView>() != null) continue;
                var dmg = col.GetComponent<IDamageableView>();
                if (dmg == null) continue;
                // Skip the shooter's own capsule — spawn pos sits inside it when the bot fires
                // straight forward (FeedbackRange turrets, point-blank shots). Without this gate
                // the projectile self-hits on frame 0 and stalls visually.
                if (dmg.EId == _ownerId) continue;

                _hit = true;
                var startDelta = newPos - oldPos;
                var startNormal = startDelta.sqrMagnitude > 0.0001f
                    ? -startDelta.normalized
                    : Vector3.up;
                ReportHit(col, oldPos, startNormal);
                return;
            }

            var delta = newPos - oldPos;
            float dist = delta.magnitude;

            if (dist > 0.001f)
            {
                // SphereCast along movement path — small radius compensates for
                // camera-angle parallax between crosshair and bullet trajectory
                if (Physics.SphereCast(oldPos, hitRadius, delta / dist, out var hit, dist,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    // Skip other projectiles + the shooter's own capsule.
                    if (hit.collider.GetComponent<ProjectileView>() == null)
                    {
                        var dmg = hit.collider.GetComponent<IDamageableView>();
                        bool isOwner = dmg != null && dmg.EId == _ownerId;
                        if (!isOwner)
                        {
                            _hit = true;
                            ReportHit(hit.collider, hit.point, hit.normal);
                            return;
                        }
                    }
                }
            }

            transform.position = newPos;
        }

        void ReportHit(Collider other, Vector3 hitPoint, Vector3 hitNormal)
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
                    Normal   = hitNormal,
                    SurfaceType = SurfaceAudioClassifier.Resolve(other),
                });
            }
        }
    }
}
