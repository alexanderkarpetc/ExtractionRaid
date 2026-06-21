# New UI Toolkit Window — Fast Recipe

Copy-paste scaffold for adding a runtime UI Toolkit window/overlay. This is the
**structural** companion to [`ui-styling.md`](ui-styling.md) — that doc owns the
visual tokens & sizing; this one owns the files, the C# skeleton, registration,
and input/gating conventions. Read this first; only open `ui-styling.md` /
[`_tokens.uss`](../../Assets/Resources/UI/Theme/_tokens.uss) when you need exact colors/sizes.

Canonical example to copy from: **`Assets/Scripts/View/UI/PauseMenu/PauseMenuWindow.cs`**
(a self-contained modal with Esc, freeze, and gating). For a passive HUD overlay,
copy `ControlsOverlay.cs`.

---

## 1. Files (4 per window)

```
Assets/Resources/UI/<Feature>/<Feature>.uxml                  # layout
Assets/Resources/UI/<Feature>/<Feature>.uss                   # styles (use var(--token))
Assets/Resources/UI/<Feature>/<Feature>PanelSettings.asset    # + .meta with a fresh guid
Assets/Scripts/View/UI/<Feature>/<Feature>Window.cs           # controller (Window/Overlay/Presenter)
```

Naming: **`<Feature>Window`** for modals, **`<Feature>Overlay`** for HUD, `<Feature>Presenter`
for a logic/hotkey bridge that drives a Window. CSS classes are feature-prefixed
(`.pm-*`, `.co-*`, `.wb-*`).

The `.asset` references the shared theme so `var(--token)` resolves — copy the
YAML from an existing `*PanelSettings.asset` and only change `m_Name` +
`m_SortingOrder`. The `themeUss` line must stay
(`guid: 44c86844d046b453485a3865df422930`). Give the `.meta` a unique 32-hex guid.

**Sort order** — see the table in [`ui-styling.md`](ui-styling.md#panel-sort-order).
Current high-end: Hotbar 50, Crafting 100, Builder 110, EndOfRaid 200,
PauseMenu 250, Tooltip 1000 (always top).

---

## 2. Controller skeleton

```csharp
[DefaultExecutionOrder(-100)]                 // -200 if it must read other windows' state before they react to Esc
[RequireComponent(typeof(UIDocument))]
public class FeatureWindow : MonoBehaviour
{
    public static FeatureWindow Instance { get; private set; }   // view-layer service locator (allowed; see ui-styling.md)

    UIDocument _doc;
    VisualElement _root;
    bool _isVisible;
    public bool IsOpen => _isVisible;

    void Awake()      { Instance = this; BuildDocument(); }
    void OnDestroy()  { if (Instance == this) Instance = null; }

    void BuildDocument()
    {
        var tree   = Resources.Load<VisualTreeAsset>("UI/Feature/Feature");
        var styles = Resources.Load<StyleSheet>("UI/Feature/Feature");
        var panel  = Resources.Load<PanelSettings>("UI/Feature/FeaturePanelSettings");
        if (tree == null || panel == null) { Debug.LogError("[Feature] Missing UXML/PanelSettings."); return; }

        // Re-apply scale in code — Unity caches PanelSettings asset edits unreliably.
        panel.scaleMode          = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1920, 1080);
        panel.screenMatchMode     = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match               = 0.5f;

        _doc = GetComponent<UIDocument>();
        _doc.panelSettings = panel;
        _doc.visualTreeAsset = tree;

        _root = _doc.rootVisualElement;
        if (styles != null && !_root.styleSheets.Contains(styles)) _root.styleSheets.Add(styles);
        _root.style.flexGrow = 1;
        // _root.pickingMode = PickingMode.Ignore;   // HUD overlays only — pass clicks through

        var btn = _root.Q<Button>("someBtn");
        if (btn != null) btn.clicked += OnClick;

        _root.style.display = DisplayStyle.None;     // hidden until Open()
    }

    public void Open()  { _isVisible = true;  _root.style.display = DisplayStyle.Flex; }
    public void Close() { _isVisible = false; _root.style.display = DisplayStyle.None; }
}
```

Registration — spawn a host GameObject in
[`AppBootstrap.Awake`](../../Assets/Scripts/ApplicationCore/AppBootstrap.cs)
next to the other UI hosts:

```csharp
var host = new GameObject("FeatureWindow");
host.transform.SetParent(transform, false);
host.AddComponent<View.UI.Feature.FeatureWindow>();
```

---

## 3. Input & gating conventions (important)

- **Hotkeys are polled per-presenter** via `UnityEngine.InputSystem.Keyboard.current[Key.X].wasPressedThisFrame`
  inside `Update()` — there is no central input map for UI. Each window/presenter owns its key.
- **Esc cascade**: Esc closes the topmost open popup first, opens the pause menu only when nothing is open.
  This works because `PauseMenuWindow` runs at `-200` (before the popup owners) and only opens when no
  blocking surface is up. **A new popup that should close on Esc must handle Esc itself** (mirror
  `NotesPresenter` / `InventoryUI`), and be listed in `PauseMenuWindow.CanOpen()` if it should block pause.
- **Gameplay freeze flags** live on `PlayerEntityState`:
  - `IsInMenu` (OR of `IsQuestLogOpen | IsNotesOpen | IsPaused | Craft/Deploy/Npc targets`) — systems
    (`ShootingSystem`, `WeaponStateMachineSystem`) early-out on it; presenters gate opens on `!IsInMenu`.
    A fully-blocking modal should set one of these flags.
  - Inventory / Loot / Builder are **deliberately NOT** in `IsInMenu` — the player keeps walking/shooting.
- **Block firing through a blocking modal**: `App.Instance.SetGameplayInputBlocked(true)` on open, `false` on close.
- **Full pause** (PauseMenu): `Time.timeScale = 0` + `SetGameplayInputBlocked(true)` + `player.IsPaused = true`.
  `Update()` still runs at `timeScale 0`, so Esc-to-resume works. Always restore `timeScale` in `OnDestroy`.
- **Exit to main menu**: `AppBootstrap.QuitToMainMenu()` (restores timeScale, tears down App + all UI hosts, loads `MainMenu`).

---

## 4. Styling quick pointers

- Use `var(--token)` for shared colors/sizes; tokens + tier tables are in
  [`ui-styling.md`](ui-styling.md). Don't hardcode `rgb()`/`px` for shared concerns.
- **Menu family** (MainMenu, PauseMenu) is intentionally *not* token-migrated — it uses a
  private dark palette. To match it, copy `MainMenu.uss` / `PauseMenu.uss` button styles
  (`.menu-btn` / `.pm-btn`): `rgb(34,42,58)` bg, `rgb(60,72,96)` border, gold/red danger variant.
- `resolvedStyle.width/height` is `0` while `display:None` — defer reads with
  `_root.schedule.Execute(...).StartingIn(0)`. (See `ui-styling.md` gotchas.)
