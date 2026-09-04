using UnityEngine;

namespace Adapters
{
    public interface ICombatViewportAdapter
    {
        bool IsInside(Vector3 worldPosition, float normalizedMargin);
    }
}
