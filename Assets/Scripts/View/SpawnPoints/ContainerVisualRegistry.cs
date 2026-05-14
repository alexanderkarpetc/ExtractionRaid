using System.Collections.Generic;
using UnityEngine;

namespace View.SpawnPoints
{
    /// <summary>
    /// View-side stash of per-position visual prefab overrides for loot containers.
    /// <see cref="Session.RaidSession"/> registers an entry for every
    /// <see cref="LootContainerSpawnPoint"/> with a non-null
    /// <c>visualPrefab</c> before firing the LootableSpawned event;
    /// <see cref="View.LootablePresenter"/> consumes the entry when it processes the
    /// matching event. Lives outside RaidState because state forbids GameObject
    /// references (CLAUDE.md rule 6).
    /// </summary>
    public static class ContainerVisualRegistry
    {
        static readonly Dictionary<Vector3, GameObject> _overrides = new();

        public static void Register(Vector3 position, GameObject prefab)
        {
            if (prefab == null) return;
            _overrides[position] = prefab;
        }

        /// <summary>
        /// Returns and removes the registered prefab for this position, or null.
        /// One-shot consumption avoids stale entries leaking across raids if
        /// <see cref="Clear"/> isn't called.
        /// </summary>
        public static GameObject Consume(Vector3 position)
        {
            if (_overrides.TryGetValue(position, out var prefab))
            {
                _overrides.Remove(position);
                return prefab;
            }
            return null;
        }

        public static void Clear() => _overrides.Clear();
    }
}
