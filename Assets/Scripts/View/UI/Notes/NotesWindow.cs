using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Notes
{
    /// <summary>
    /// UI Toolkit "Field Notes" popup — the in-game tutorial / field guide.
    /// Layout follows the concept at Assets/Concepts/notes-ui-popup.html:
    /// centered modal with a header (title + entry count + close), a sidebar
    /// (search box + category-grouped note list with unread dots) and a detail
    /// pane (kicker chip, title, paragraphs, tip/warning callout, related
    /// control chips).
    ///
    /// Mirrors the window pattern used by <see cref="Quests.QuestsWindow"/>:
    /// a singleton MonoBehaviour that owns a <see cref="UIDocument"/>, loads
    /// its UXML/USS/PanelSettings from Resources, and toggles visibility via
    /// DisplayStyle.
    ///
    /// Pure view — note content is static data below (mirroring the real
    /// bindings in UnityInputAdapter / ControlsOverlay; update both if those
    /// change). Read/unread state is per-session only.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class NotesWindow : MonoBehaviour
    {
        public static NotesWindow Instance { get; private set; }

        // ── Note data ─────────────────────────────────────────

        enum CalloutKind { None, Tip, Warn }

        readonly struct ControlHint
        {
            public readonly string Key;
            public readonly string Desc;
            public ControlHint(string key, string desc) { Key = key; Desc = desc; }
        }

        class NoteEntry
        {
            public string Id;
            public string Category;
            public string Icon;     // single glyph from a font-safe set (no emoji)
            public Color IconColor; // tints the glyph + icon chip bg/border
            public string Title;
            public bool StartsUnread;
            public string[] Paragraphs; // rich text (<b>) supported by Label
            public CalloutKind Callout;
            public string CalloutText;
            public ControlHint[] Controls;
        }

        static readonly NoteEntry[] Notes =
        {
            new NoteEntry
            {
                Id = "welcome", Category = "Basics", Icon = "◆", IconColor = new Color(0.29f, 0.49f, 1f), Title = "Welcome to the Raid",
                Paragraphs = new[]
                {
                    "You drop into a hostile zone with whatever gear you brought. The goal is simple to say and hard to do: <b>get in, find what you came for, and reach an extraction point alive</b>.",
                    "Anything you carry out is yours. Anything you leave behind — or die holding — is gone.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Take it slow on your first runs. A quiet extraction with a little loot beats a loud death with a full bag.",
                Controls = Array.Empty<ControlHint>(),
            },
            new NoteEntry
            {
                Id = "movement", Category = "Basics", Icon = "►", IconColor = new Color(0.18f, 0.80f, 0.44f), Title = "Moving & Dodging",
                Paragraphs = new[]
                {
                    "Use WASD to move and hold Shift to sprint. Sprinting is faster but drains stamina.",
                    "Tap Space to <b>dodge-roll</b> — a short burst of movement. Use it to break line of sight or escape a grenade.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Rolling costs stamina. Don't empty the bar — you still need it to sprint away.",
                Controls = new[]
                {
                    new ControlHint("WASD", "Move"),
                    new ControlHint("Shift", "Sprint"),
                    new ControlHint("Space", "Dodge"),
                },
            },
            new NoteEntry
            {
                Id = "extraction", Category = "Basics", Icon = "▲", IconColor = new Color(0.83f, 0.63f, 0.09f), Title = "Extraction", StartsUnread = true,
                Paragraphs = new[]
                {
                    "Extraction points are marked on your map. Reach one and stay inside the zone to run down the <b>extraction timer</b>.",
                    "Hold M to check the map if you are not sure where to go.",
                },
                Callout = CalloutKind.Warn,
                CalloutText = "Leaving the zone resets the timer. Don't wander off to grab one more crate.",
                Controls = new[] { new ControlHint("M", "Map (hold)") },
            },
            new NoteEntry
            {
                Id = "looting", Category = "Combat & Gear", Icon = "■", IconColor = new Color(0.90f, 0.55f, 0.24f), Title = "Looting & Inventory",
                Paragraphs = new[]
                {
                    "Open your inventory with Tab. Nearby containers, corpses and floor items show up as panels you can drag from.",
                    "Press F to pick up a highlighted item, or drag it into a free backpack slot. Space is limited — prioritise.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Right-click an item for context actions — drop, bind to a quick slot, and more.",
                Controls = new[]
                {
                    new ControlHint("Tab", "Inventory"),
                    new ControlHint("F", "Pick up"),
                    new ControlHint("E", "Interact"),
                },
            },
            new NoteEntry
            {
                Id = "quickslots", Category = "Combat & Gear", Icon = "◇", IconColor = new Color(0.95f, 0.77f, 0.06f), Title = "Quick Slots",
                Paragraphs = new[]
                {
                    "Consumables like medkits and bandages can be bound to keys 3–9 for instant use mid-fight.",
                    "While the inventory is open, right-click a consumable and choose <b>Bind to N</b>. Weapons live on 1 and 2.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Bind a medkit before you need it. Fumbling through the bag while bleeding gets you killed.",
                Controls = new[]
                {
                    new ControlHint("1 2", "Weapons"),
                    new ControlHint("3–9", "Quick slots"),
                },
            },
            new NoteEntry
            {
                Id = "combat", Category = "Combat & Gear", Icon = "●", IconColor = new Color(0.88f, 0.31f, 0.25f), Title = "Shooting & Reloading",
                Paragraphs = new[]
                {
                    "Aim with the mouse, fire with LMB, hold RMB to steady your aim. Reload with R and throw a grenade with G.",
                    "Armour soaks hits but degrades as it takes damage. Unarmoured spots take full damage.",
                },
                Callout = CalloutKind.Warn,
                CalloutText = "Reloading mid-fight takes time. Top up between fights, not during them.",
                Controls = new[]
                {
                    new ControlHint("LMB", "Fire"),
                    new ControlHint("RMB", "Aim"),
                    new ControlHint("R", "Reload"),
                    new ControlHint("G", "Grenade"),
                },
            },
            new NoteEntry
            {
                Id = "armor", Category = "Combat & Gear", Icon = "○", IconColor = new Color(0.47f, 0.76f, 0.92f), Title = "Armour & Medical",
                Paragraphs = new[]
                {
                    "Equip a <b>helmet</b> and <b>body armour</b> from the equipment slots. Each has its own durability and protection class.",
                    "Use a <b>bandage</b> to stop bleeding and a <b>medkit</b> to restore health. Heavier meds heal more but take longer to apply.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Your hands are busy while applying meds. Break contact first.",
                Controls = Array.Empty<ControlHint>(),
            },
            new NoteEntry
            {
                Id = "stash", Category = "Hideout & Economy", Icon = "□", IconColor = new Color(0.76f, 0.56f, 0.35f), Title = "The Stash",
                Paragraphs = new[]
                {
                    "Your <b>stash</b> in the hideout is permanent storage — anything placed there survives between raids, even if you die on the next run.",
                    "Bring gear home and deposit it to keep it safe. Treat the stash as your long-term progress.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Stash your best loot before risky runs. Only take in what you're willing to lose.",
                Controls = Array.Empty<ControlHint>(),
            },
            new NoteEntry
            {
                Id = "quests", Category = "Hideout & Economy", Icon = "★", IconColor = new Color(0.90f, 0.78f, 0.55f), Title = "Quests & Traders", StartsUnread = true,
                Paragraphs = new[]
                {
                    "NPCs offer <b>quests</b> — kill targets, find items, reach places. Press I to open your quest journal at any time.",
                    "Hand in completed objectives at the giving NPC to claim rewards.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Carry quest items in your bag — you hand them over directly in the NPC dialogue.",
                Controls = new[] { new ControlHint("I", "Quests") },
            },
            new NoteEntry
            {
                Id = "weapons", Category = "Hideout & Economy", Icon = "▼", IconColor = new Color(0.63f, 0.47f, 1f), Title = "Weapon Building",
                Paragraphs = new[]
                {
                    "At the workbench you assemble weapons from a <b>payload</b> (the round or effect) and a <b>delivery</b> (the form factor). Combinations change range, spread and damage type.",
                    "Built weapons go to your inventory like any other gear — take them into a raid or stash them.",
                },
                Callout = CalloutKind.Tip,
                CalloutText = "Some quests ask for a weapon built from specific parts. Check the objective before you commit a build.",
                Controls = new[] { new ControlHint("E", "Use workbench") },
            },
        };

        // ── State ─────────────────────────────────────────────

        UIDocument _doc;
        VisualElement _root;
        VisualElement _overlay;
        Label _countLabel;
        Button _closeBtn;
        TextField _search;
        ScrollView _list;
        ScrollView _detail;

        string _activeId;
        readonly HashSet<string> _unread = new HashSet<string>();
        bool _isVisible;

        public bool IsOpen => _isVisible;

        public event Action Closed;

        void Awake()
        {
            Instance = this;
            foreach (var note in Notes)
                if (note.StartsUnread) _unread.Add(note.Id);
            _activeId = Notes[0].Id;
            BuildDocument();
            HideImmediate();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Build ─────────────────────────────────────────────

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/Notes/NotesWindow");
            var styles = Resources.Load<StyleSheet>("UI/Notes/NotesWindow");
            var panel = Resources.Load<PanelSettings>("UI/Notes/NotesPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[NotesWindow] Missing UXML or PanelSettings in Resources/UI/Notes/.");
                return;
            }

            // Re-apply scale config in code — Unity caches PanelSettings asset
            // edits unreliably across domain reloads, so the asset's scale fields
            // can be ignored (popup renders tiny on high-DPI / 4K displays).
            // Mirrors QuestsWindow / docs/ai/ui-styling.md.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            // Attach the stylesheet in code as well as via the UXML <Style src> —
            // resilient to the src reference baking in broken on first import.
            if (styles != null)
            {
                if (!_root.styleSheets.Contains(styles))
                    _root.styleSheets.Add(styles);
            }
            else
            {
                Debug.LogError("[NotesWindow] StyleSheet missing at " +
                               "Resources/UI/Notes/NotesWindow.uss — popup will render unstyled. " +
                               "Reimport the asset (right-click → Reimport).");
            }

            _root.style.flexGrow = 1;

            _overlay    = _root.Q<VisualElement>("overlay");
            _countLabel = _root.Q<Label>("count");
            _closeBtn   = _root.Q<Button>("closeBtn");
            _search     = _root.Q<TextField>("search");
            _list       = _root.Q<ScrollView>("list");
            _detail     = _root.Q<ScrollView>("detail");

            if (_closeBtn != null)
                _closeBtn.clicked += RequestClose;

            // Click on the dim backdrop (but not the popup itself) closes.
            if (_overlay != null)
                _overlay.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.target == _overlay) RequestClose();
                });

            if (_search != null)
                _search.RegisterValueChangedCallback(_ => RenderList());

            // Content always fits the width — never show a horizontal bar;
            // vertical only when the content overflows (thin themed thumb in USS).
            if (_list != null)
            {
                _list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _list.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }
            if (_detail != null)
            {
                _detail.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                _detail.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }
        }

        // ── Public API ────────────────────────────────────────

        public void Open()
        {
            if (_root == null) return;
            _isVisible = true;
            _root.style.display = DisplayStyle.Flex;
            RenderList();
            RenderDetail(FindNote(_activeId));
            UpdateCount();
        }

        public void RequestClose()
        {
            HideImmediate();
            Closed?.Invoke();
        }

        void HideImmediate()
        {
            _isVisible = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        // ── Content ───────────────────────────────────────────

        static NoteEntry FindNote(string id)
        {
            foreach (var note in Notes)
                if (note.Id == id) return note;
            return Notes[0];
        }

        void UpdateCount()
        {
            if (_countLabel == null) return;
            int unread = _unread.Count;
            _countLabel.text = unread > 0
                ? $"{Notes.Length} entries · {unread} new"
                : $"{Notes.Length} entries";
        }

        void RenderList()
        {
            if (_list == null) return;
            _list.Clear();

            string q = _search?.value?.Trim().ToLowerInvariant() ?? "";
            var matches = new List<NoteEntry>();
            foreach (var note in Notes)
            {
                if (q.Length == 0
                    || note.Title.ToLowerInvariant().Contains(q)
                    || note.Category.ToLowerInvariant().Contains(q))
                    matches.Add(note);
            }

            if (matches.Count == 0)
            {
                var empty = new Label($"No notes match \"{_search?.value}\"");
                empty.AddToClassList("np-list-empty");
                _list.Add(empty);
                return;
            }

            // Group by category, preserving first-seen order.
            var cats = new List<string>();
            foreach (var note in matches)
                if (!cats.Contains(note.Category)) cats.Add(note.Category);

            foreach (var cat in cats)
            {
                var label = new Label(cat.ToUpperInvariant());
                label.AddToClassList("np-group-label");
                _list.Add(label);

                foreach (var note in matches)
                {
                    if (note.Category != cat) continue;
                    _list.Add(BuildListItem(note));
                }
            }
        }

        VisualElement BuildListItem(NoteEntry note)
        {
            var item = new VisualElement();
            item.AddToClassList("np-item");
            if (note.Id == _activeId) item.AddToClassList("np-item--active");

            var ico = new Label(note.Icon);
            ico.AddToClassList("np-item-ico");
            // Per-note tint, stand-in for the concept's emoji icons.
            var c = note.IconColor;
            ico.style.color = c;
            ico.style.backgroundColor = new Color(c.r, c.g, c.b, 0.10f);
            var border = new Color(c.r, c.g, c.b, 0.30f);
            ico.style.borderTopColor = border;
            ico.style.borderBottomColor = border;
            ico.style.borderLeftColor = border;
            ico.style.borderRightColor = border;
            item.Add(ico);

            var title = new Label(note.Title);
            title.AddToClassList("np-item-title");
            item.Add(title);

            if (_unread.Contains(note.Id))
            {
                var dot = new VisualElement();
                dot.AddToClassList("np-new-dot");
                item.Add(dot);
            }

            item.RegisterCallback<PointerDownEvent>(_ => SelectNote(note.Id));
            return item;
        }

        void SelectNote(string id)
        {
            _activeId = id;
            _unread.Remove(id); // reading clears the unread dot
            RenderList();
            RenderDetail(FindNote(id));
            UpdateCount();
        }

        void RenderDetail(NoteEntry note)
        {
            if (_detail == null) return;
            _detail.Clear();
            if (note == null) return;

            var kicker = new Label(note.Category.ToUpperInvariant());
            kicker.AddToClassList("np-kicker-chip");
            _detail.Add(kicker);

            var title = new Label(note.Title);
            title.AddToClassList("np-detail-title");
            _detail.Add(title);

            foreach (var para in note.Paragraphs)
            {
                var p = new Label(para);
                p.AddToClassList("np-para");
                _detail.Add(p);
            }

            if (note.Callout != CalloutKind.None && !string.IsNullOrEmpty(note.CalloutText))
            {
                bool warn = note.Callout == CalloutKind.Warn;
                var callout = new VisualElement();
                callout.AddToClassList("np-callout");
                if (warn) callout.AddToClassList("np-callout--warn");

                var kind = new Label(warn ? "CAUTION" : "TIP");
                kind.AddToClassList("np-callout-kind");
                callout.Add(kind);

                var text = new Label(note.CalloutText);
                text.AddToClassList("np-callout-text");
                callout.Add(text);

                _detail.Add(callout);
            }

            if (note.Controls != null && note.Controls.Length > 0)
            {
                var divider = new VisualElement();
                divider.AddToClassList("np-divider");
                _detail.Add(divider);

                var label = new Label("RELATED CONTROLS");
                label.AddToClassList("np-related-label");
                _detail.Add(label);

                var row = new VisualElement();
                row.AddToClassList("np-related-row");
                foreach (var hint in note.Controls)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("np-related-chip");
                    var kbd = new Label(hint.Key);
                    kbd.AddToClassList("np-kbd");
                    chip.Add(kbd);
                    chip.Add(new Label(hint.Desc));
                    row.Add(chip);
                }
                _detail.Add(row);
            }

            _detail.scrollOffset = Vector2.zero;
        }
    }
}
