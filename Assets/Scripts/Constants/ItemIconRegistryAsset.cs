using System;
using System.Collections.Generic;
using UnityEngine;

namespace Constants
{
    [CreateAssetMenu(fileName = "ItemIconRegistry", menuName = "Items/Icon Registry")]
    public class ItemIconRegistryAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string DefinitionId;
            public Sprite Icon;
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        public Entry[] Entries => _entries;

        Dictionary<string, Sprite> _map;

        public Sprite GetIcon(string definitionId)
        {
            if (_map == null) BuildMap();
            _map.TryGetValue(definitionId, out var sprite);
            return sprite;
        }

        void BuildMap()
        {
            _map = new Dictionary<string, Sprite>(_entries.Length, StringComparer.Ordinal);
            foreach (var e in _entries)
                if (!string.IsNullOrEmpty(e.DefinitionId) && e.Icon != null)
                    _map[e.DefinitionId] = e.Icon;
        }

        void OnValidate() => _map = null;
    }
}
