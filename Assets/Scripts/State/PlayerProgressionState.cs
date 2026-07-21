using System.Collections.Generic;

namespace State
{
    /// <summary>
    /// Persistent per-profile progression. Lives on <c>Session.Player</c> next to
    /// <see cref="PlayerProfileState"/> and survives raids (and death). Stores only ids —
    /// node definitions live in the <c>ProgressionTreeConfig</c> asset.
    ///
    /// No refund: once a node id is in <see cref="AllocatedNodeIds"/> it stays there.
    /// </summary>
    public class PlayerProgressionState
    {
        public List<string> AllocatedNodeIds = new();
        public int AvailablePoints;
    }
}
