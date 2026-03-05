using State;
using UnityEngine;

namespace Tests.EditMode
{
    public static class EditModeTestsUtils
    {
        public static RaidState CreateStateWithPlayer(Vector3 startPos)
        {
            var state = RaidState.Create();
            var id = state.AllocateEId();
            state.PlayerEntity = PlayerEntityState.Create(id, startPos);
            return state;
        }
    }
}