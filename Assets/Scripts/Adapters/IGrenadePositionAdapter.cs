using State;
using UnityEngine;

namespace Adapters
{
    public interface IGrenadePositionAdapter
    {
        Vector3? GetPosition(EId id);
    }
}
