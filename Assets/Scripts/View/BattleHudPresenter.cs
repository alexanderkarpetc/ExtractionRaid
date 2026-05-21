using ApplicationCore;
using Dev;
using Session;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Battle HUD presenter — armor paper-doll + status effects row + tooltips.
    /// Foundation Stage 1 — empty scaffold that spawns the canvas + hides it when toggled off.
    /// Subsequent stages (2+) attach paper-doll material, status row layout, tooltips, etc.
    ///
    /// Pairs з worldspace components (`WorldStatusIcons`, `WorldStaminaRing`) for the
    /// non-HUD half of the battle layer. Lives як plain class у App; LateTick from
    /// <c>App.LateTick</c>. Mirror lifecycle of <see cref="CrosshairPresenter"/> +
    /// <see cref="HudDamagePresenter"/>.
    /// </summary>
    public class BattleHudPresenter
    {
        const string PrefabPath = "Vfx/Prefabs/UI/BattleHud";

        GameObject _prefab;
        Canvas _canvas;
        bool _resourcesLoaded;
        bool _disabled;

        public BattleHudPresenter() { /* lazy init */ }

        void LoadResources()
        {
            if (_resourcesLoaded) return;
            _resourcesLoaded = true;
            _prefab = Resources.Load<GameObject>(PrefabPath);
            if (_prefab == null)
            {
                Debug.LogWarning($"[BattleHudPresenter] Prefab missing at Resources/{PrefabPath}");
                _disabled = true;
            }
        }

        void EnsureScene()
        {
            if (_canvas != null) return;
            if (_prefab == null) return;
            var go = Object.Instantiate(_prefab);
            go.name = "[BattleHud]";
            _canvas = go.GetComponentInChildren<Canvas>(true);
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.BattleHud;
            if (cfg == null || !cfg.Enabled)
            {
                if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
                return;
            }

            LoadResources();
            if (_disabled) return;
            EnsureScene();
            if (_canvas == null) return;
            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

            // Hide while pointer is over UI (consistent with crosshair + HUD damage).
            if (App.Instance.IsPointerOverUi)
            {
                _canvas.gameObject.SetActive(false);
                return;
            }

            // Stage 2+: pull player state from session and push to paper-doll / status row.
            // var state = session.RaidState;
            // var player = state?.PlayerEntity;
            // if (player == null) return;
            // ...
        }

        public void Dispose()
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
            _canvas = null;
        }
    }
}
