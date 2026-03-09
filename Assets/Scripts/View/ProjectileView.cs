using State;
using UnityEngine;

namespace View
{
    public class ProjectileView : MonoBehaviour
    {
        public EId EId { get; private set; }
        float _damage;

        public void Initialize(EId id, float damage)
        {
            EId = id;
            _damage = damage;
        }

        public void SyncFromState(ProjectileEntityState state)
        {
            transform.position = state.Position;
        }

        private void OnTriggerEnter(Collider other)
        {
            var damageable = other.GetComponent<IDamageableView>();
            if (damageable == null) return;

            var session = App.App.Instance.RaidSession;
            if (session == null) return;

            session.ReportHit(new HitSignal
            {
                ProjectileId = EId,
                TargetId = damageable.EId,
                Damage = _damage,
            });
        }
    }
}
