using State;
using UnityEngine;

namespace View
{
    public class PlayerView : MonoBehaviour
    {
        [SerializeField] Transform _muzzlePoint;

        public EId EId { get; private set; }
        public Transform MuzzlePoint => _muzzlePoint;

        public void Initialize(EId id)
        {
            EId = id;
        }

        public void SyncFromState(PlayerEntityState state)
        {
            transform.position = state.Position;

            if (state.FacingDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);
            }
        }
    }
}
