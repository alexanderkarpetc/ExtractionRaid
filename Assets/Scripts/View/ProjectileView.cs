using State;
using Systems;
using UnityEngine;

namespace View
{
    public class ProjectileView : MonoBehaviour
    {
        public EId EId { get; private set; }

        public void Initialize(EId id)
        {
            EId = id;
        }

        public void SyncFromState(ProjectileEntityState state)
        {
            transform.position = state.Position;
        }

        private void OnTriggerEnter(Collider other)
        {
            var destructible = other.GetComponent<DestructibleView>();
            if (destructible == null) return;

            var session = App.App.Instance.RaidSession;
            if (session == null) return;

            session.ReportHit(new HitSignal
            {
                ProjectileId = EId,
                TargetId = destructible.EId,
                Damage = ShootingSystem.ProjectileDamage,
            });
        }
    }
}
