using System.Collections.Generic;
using ApplicationCore;
using Dev;
using State;
using UnityEngine;
using UnityEngine.UI;
using View.UI;

namespace View
{
    /// <summary>
    /// Worldspace status mini-icons row — sits under the existing <see cref="WorldHealthBar"/>
    /// on player + every bot (universal pattern). Peripheral signal only: small colored
    /// squares whose tint mirrors the corresponding HUD tile (see
    /// <see cref="StatusEffectVisualMap"/>). No tooltips, no interaction.
    ///
    /// Hides itself when the owner has no active status effects (zero perf cost).
    /// Created at Initialize-time by <c>PlayerView</c>/<c>BotView</c> after
    /// <c>WorldHealthBar.Create</c>.
    /// </summary>
    public class WorldStatusIcons : MonoBehaviour
    {
        const string IconMaterialPath = "Vfx/Materials/StatusEffectIcon";

        static readonly int _BgColorProp   = Shader.PropertyToID("_BgColor");
        static readonly int _FgColorProp   = Shader.PropertyToID("_FgColor");
        static readonly int _IconShapeProp = Shader.PropertyToID("_IconShape");

        EId _ownerId;
        Canvas _canvas;
        RectTransform _rowRect;
        Material _iconMaterial;
        readonly Dictionary<string, IconCell> _cells = new();
        readonly HashSet<string> _seenThisFrame = new();
        readonly List<string> _staleBuffer = new();

        struct IconCell
        {
            public GameObject Go;
            public Image Image;
            public Material Material;
            public RectTransform Rect;
        }

        public static WorldStatusIcons Create(Transform parent, EId ownerId)
        {
            var go = new GameObject("StatusIcons");
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 101; // sits just above WorldHealthBar (100)

            // RectTransform for the row container — children are positioned manually each frame.
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1f, 0.2f); // arbitrary; children use absolute positions

            var comp = go.AddComponent<WorldStatusIcons>();
            comp._ownerId = ownerId;
            comp._canvas = canvas;
            comp._rowRect = rt;
            comp._iconMaterial = Resources.Load<Material>(IconMaterialPath);
            if (comp._iconMaterial == null)
                Debug.LogWarning($"[WorldStatusIcons] Material missing at Resources/{IconMaterialPath}");
            // Toggle visibility via Canvas.enabled, NOT GameObject.SetActive — inactive GOs
            // skip LateUpdate, so the row could never re-show after status spawned.
            canvas.enabled = false;
            return comp;
        }

        void LateUpdate()
        {
            var session = App.Instance?.RaidSession;
            var state = session?.RaidState;
            if (state == null) { HideRow(); return; }

            if (!state.StatusEffects.TryGetValue(_ownerId, out var effects) || effects.Count == 0)
            {
                HideRow();
                return;
            }

            if (!_canvas.enabled) _canvas.enabled = true;

            var cfg = ViewCheats.Config?.BattleHud;
            float size = cfg != null ? cfg.WorldStatusIconSize : 0.12f;
            float gap  = cfg != null ? cfg.WorldStatusIconGap  : 0.04f;
            float yOff = cfg != null ? cfg.WorldStatusYOffset  : -0.18f;

            transform.localPosition = new Vector3(0f, yOff, 0f);

            _seenThisFrame.Clear();

            // Diff active statuses vs spawned cells.
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                var key = StatusEffectVisualMap.KeyFor(e);
                _seenThisFrame.Add(key);

                if (!_cells.TryGetValue(key, out var cell))
                {
                    cell = CreateCell(size);
                    _cells[key] = cell;
                }

                cell.Rect.sizeDelta = new Vector2(size, size);
                if (cell.Material != null)
                {
                    cell.Material.SetColor(_BgColorProp, StatusEffectVisualMap.BgColorFor(e));
                    cell.Material.SetColor(_FgColorProp, StatusEffectVisualMap.FgColorFor(e));
                    cell.Material.SetFloat(_IconShapeProp, StatusEffectVisualMap.IconShapeFor(e));
                }
            }

            // Remove cells whose status no longer active.
            _staleBuffer.Clear();
            foreach (var kvp in _cells)
                if (!_seenThisFrame.Contains(kvp.Key)) _staleBuffer.Add(kvp.Key);
            for (int i = 0; i < _staleBuffer.Count; i++)
            {
                var k = _staleBuffer[i];
                Destroy(_cells[k].Go);
                _cells.Remove(k);
            }

            // Layout — horizontal row, anchored to HP bar edge per WorldStatusAlignment.
            // Bar width comes from DevCheats so row alignment tracks bar geometry as user tunes it.
            int count = effects.Count;
            float rowWidth = count * size + (count - 1) * gap;
            float barHalf = DevCheats.HBarWidth * 0.5f;
            var alignment = cfg != null ? cfg.WorldStatusAlignment : Dev.WorldStatusAlignment.Left;
            float startX = alignment switch
            {
                Dev.WorldStatusAlignment.Right  =>  barHalf - rowWidth + size * 0.5f,
                Dev.WorldStatusAlignment.Center => -rowWidth * 0.5f + size * 0.5f,
                _                               => -barHalf + size * 0.5f, // Left default
            };
            int idx = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                var key = StatusEffectVisualMap.KeyFor(effects[i]);
                if (!_cells.TryGetValue(key, out var cell)) continue;
                cell.Rect.anchoredPosition = new Vector2(startX + idx * (size + gap), 0f);
                idx++;
            }
        }

        void HideRow()
        {
            if (_canvas != null && _canvas.enabled) _canvas.enabled = false;
        }

        IconCell CreateCell(float size)
        {
            var go = new GameObject("Icon");
            go.transform.SetParent(_rowRect, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(size, size);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            // Per-cell material instance so SetColor/SetFloat don't leak to the shared asset.
            // Reading Image.material auto-instances if assigned shared first.
            if (_iconMaterial != null)
            {
                img.material = _iconMaterial;
                var mat = img.material;
                return new IconCell { Go = go, Image = img, Material = mat, Rect = rt };
            }
            return new IconCell { Go = go, Image = img, Material = null, Rect = rt };
        }

        void OnDestroy()
        {
            foreach (var kvp in _cells) if (kvp.Value.Go != null) Destroy(kvp.Value.Go);
            _cells.Clear();
        }
    }
}
