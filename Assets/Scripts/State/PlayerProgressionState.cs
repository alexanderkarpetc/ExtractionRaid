using System.Collections.Generic;

namespace State
{
    /// <summary>
    /// Persistent per-profile progression. Lives on <c>Session.Player</c> next to
    /// <see cref="PlayerProfileState"/> and survives raids (and death). Stores only ids —
    /// node definitions live in the <c>ProgressionTreeConfig</c> asset.
    ///
    /// No refund: once a node id is in <see cref="AllocatedNodeIds"/> it stays there.
    /// There is no point pool — a node's only cost is its materials (see
    /// <c>Systems.ProgressionCostSystem</c>), so allocation state is just the id list.
    /// </summary>
    public class PlayerProgressionState
    {
        public List<string> AllocatedNodeIds = new();
    }
}
