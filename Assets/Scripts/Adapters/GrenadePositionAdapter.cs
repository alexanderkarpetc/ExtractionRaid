using System.Collections.Generic;
using State;
using UnityEngine;

namespace Adapters
{
    public class GrenadePositionAdapter : IGrenadePositionAdapter
    {
        readonly Dictionary<EId, Transform> _tracked = new();

        public void Register(EId id, Transform transform) => _tracked[id] = transform;
        public void Unregister(EId id) => _tracked.Remove(id);

        public Vector3? GetPosition(EId id)
        {
            if (_tracked.TryGetValue(id, out var t) && t != null)
                return t.position;
            return null;
        }

        public void Clear() => _tracked.Clear();
    }
}
