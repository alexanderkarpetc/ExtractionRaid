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
        EId _ownerId;
        Canvas _canvas;
        RectTransform _rowRect;
        readonly Dictionary<string, IconCell> _cells = new();
        readonly HashSet<string> _seenThisFrame = new();
        readonly List<string> _staleBuffer = new();

        struct IconCell
        {
            public GameObject Go;
            public Image Image;
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
            go.SetActive(false); // hide until first status appears
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

            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

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

                cell.Image.color = StatusEffectVisualMap.BgColorFor(e);
                cell.Rect.sizeDelta = new Vector2(size, size);
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

            // Layout — horizontal left-to-right, centered around parent x=0.
            int count = effects.Count;
            float rowWidth = count * size + (count - 1) * gap;
            float startX = -rowWidth * 0.5f + size * 0.5f;
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
            if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
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
            return new IconCell { Go = go, Image = img, Rect = rt };
        }

        void OnDestroy()
        {
            foreach (var kvp in _cells) if (kvp.Value.Go != null) Destroy(kvp.Value.Go);
            _cells.Clear();
        }
    }
}
