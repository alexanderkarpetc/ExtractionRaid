using State;
using UnityEngine;

namespace View
{
    public class PlayerView : MonoBehaviour
    {
        public EId EId { get; private set; }

        public void Initialize(EId id)
        {
            EId = id;
        }

        public void SyncFromState(PlayerEntityState state)
        {
            transform.position = state.Position;
        }
    }
}
