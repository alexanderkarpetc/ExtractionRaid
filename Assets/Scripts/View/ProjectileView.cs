using State;
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
    }
}
