using System.Collections.Generic;

namespace State
{
    public class BTTrace
    {
        readonly Dictionary<object, int> _statuses = new();

        public void Clear() => _statuses.Clear();
        public void Record(object node, int status) => _statuses[node] = status;
        public bool TryGetStatus(object node, out int status) => _statuses.TryGetValue(node, out status);
    }
}
