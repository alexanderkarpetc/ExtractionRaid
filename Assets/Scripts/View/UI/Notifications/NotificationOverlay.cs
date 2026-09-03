using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Notifications
{
    public enum NotificationKind { Success, Info, Loot, Danger }

    /// <summary>
    /// Bottom-right toast stack. UI Toolkit port of notification_banner_concept.html:
    /// newest toast slides in at the bottom and pushes older ones up, each runs an
    /// auto-dismiss timer bar, and hovering a toast pauses (and restarts) its timer.
    /// Callers raise banners through <see cref="Push"/>; no game logic lives here.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class NotificationOverlay : MonoBehaviour
    {
        public static NotificationOverlay Instance { get; private set; }

        const int MaxVisible = 4;
        const float DefaultTtlSeconds = 5f;

        UIDocument _doc;
        VisualElement _root;
        VisualElement _stack;

        void Awake()
        {
            Instance = this;
            BuildDocument();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/Notifications/NotificationOverlay");
            var styles = LoadStyleSheet("UI/Notifications", "NotificationOverlay");
            var panel = Resources.Load<PanelSettings>("UI/Notifications/NotificationPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[NotificationOverlay] Missing UXML or PanelSettings in Resources/UI/Notifications/.");
                return;
            }

            // Re-apply scale config in code — Unity caches PanelSettings asset
            // edits unreliably across domain reloads, so the asset's scale fields
            // can be ignored (toasts render tiny on high-DPI / 4K displays).
            // Mirrors InventoryWindow / docs/ai/ui-styling.md.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            // Toasts belong to the overlay tier: they must stay readable while a modal is
            // up (quest popup 90, inventory 100, attachment editor 200, end of raid 200),
            // otherwise a banner raised *by* a window — "backpack full", say — renders
            // behind the very window that raised it. Set in code because the asset's
            // sorting order is both too low and unreliable across domain reloads
            // (same reason the scale fields are re-applied above).
            _doc.sortingOrder = 900; // below the tooltip layer (1000), above every window

            _root = _doc.rootVisualElement;
            if (styles != null)
            {
                if (!_root.styleSheets.Contains(styles)) _root.styleSheets.Add(styles);
            }
            else
            {
                // Without the sheet the toasts still build and lay out — full-width, no
                // background, default dark text — so they're invisible rather than absent.
                // Make that loud instead of shipping a silent no-op notification system.
                Debug.LogError("[NotificationOverlay] NotificationOverlay.uss failed to load; " +
                               "toasts will render unstyled and invisible.");
            }

            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Ignore;

            _stack = _root.Q<VisualElement>("stack");
        }

        /// <summary>
        /// Loads a USS out of Resources by folder + asset name.
        ///
        /// A plain <c>Resources.Load&lt;StyleSheet&gt;("UI/Notifications/NotificationOverlay")</c>
        /// is not reliable here: Resources keys assets by their extension-less path, and this
        /// folder holds both NotificationOverlay.uxml and NotificationOverlay.uss under that
        /// same key. The lookup can settle on the VisualTreeAsset and hand back null for the
        /// StyleSheet — which is what left every toast unstyled (and therefore invisible).
        /// LoadAll filters by type first, so the sheet is found regardless of the collision.
        /// </summary>
        static StyleSheet LoadStyleSheet(string folder, string assetName)
        {
            var direct = Resources.Load<StyleSheet>($"{folder}/{assetName}");
            if (direct != null) return direct;

            foreach (var sheet in Resources.LoadAll<StyleSheet>(folder))
                if (sheet != null && sheet.name == assetName) return sheet;

            return null;
        }

        public void Push(NotificationKind kind, string kicker, string title, string desc,
            float ttlSeconds = DefaultTtlSeconds)
        {
            if (_stack == null) return;

            var toast = BuildToast(kind, kicker, title, desc);
            var state = new ToastState { Ttl = Mathf.Max(1f, ttlSeconds) };
            toast.userData = state;

            // Newest at index 0 → column-reverse renders it at the bottom of the stack.
            _stack.Insert(0, toast);
            TrimOverflow();

            // Click to dismiss; hover pauses the timer and restarts it on leave.
            toast.RegisterCallback<PointerDownEvent>(_ => Dismiss(toast));
            toast.RegisterCallback<PointerEnterEvent>(_ => PauseTimer(toast));
            toast.RegisterCallback<PointerLeaveEvent>(_ => StartTimer(toast));

            // Defer the entrance + timer one tick so the transition has a "from" state to animate.
            toast.schedule.Execute(() =>
            {
                toast.AddToClassList("is-shown");
                StartTimer(toast);
            }).ExecuteLater(20);
        }

        VisualElement BuildToast(NotificationKind kind, string kicker, string title, string desc)
        {
            var toast = new VisualElement();
            toast.AddToClassList("toast");
            toast.AddToClassList(TypeClass(kind));

            var accent = new VisualElement();
            accent.AddToClassList("toast-accent");
            toast.Add(accent);

            var icon = new Label(IconGlyph(kind));
            icon.AddToClassList("toast-icon");
            toast.Add(icon);

            var body = new VisualElement();
            body.AddToClassList("toast-body");

            if (!string.IsNullOrEmpty(kicker))
            {
                var kickerLabel = new Label(kicker.ToUpperInvariant());
                kickerLabel.AddToClassList("toast-kicker");
                body.Add(kickerLabel);
            }

            var titleLabel = new Label(title ?? "");
            titleLabel.AddToClassList("toast-title");
            body.Add(titleLabel);

            if (!string.IsNullOrEmpty(desc))
            {
                var descLabel = new Label(desc);
                descLabel.AddToClassList("toast-desc");
                body.Add(descLabel);
            }

            toast.Add(body);

            var timer = new VisualElement();
            timer.name = "timer";
            timer.AddToClassList("toast-timer");
            toast.Add(timer);

            return toast;
        }

        void StartTimer(VisualElement toast)
        {
            if (toast.userData is not ToastState state) return;

            state.DismissItem?.Pause();

            var timer = toast.Q<VisualElement>("timer");
            if (timer != null)
            {
                // Snap the bar back to full instantly (duration 0), then on the next tick
                // arm the full-TTL transition and let is-counting shrink it to empty.
                // Scale is driven entirely by the USS class so the transition actually runs
                // (an inline scale would outrank the class rule and freeze it).
                timer.style.transitionDuration = new List<TimeValue> { new(0f, TimeUnit.Second) };
                toast.RemoveFromClassList("is-counting");

                timer.schedule.Execute(() =>
                {
                    timer.style.transitionDuration = new List<TimeValue> { new(state.Ttl, TimeUnit.Second) };
                    toast.AddToClassList("is-counting");
                }).ExecuteLater(16);
            }

            var dismissItem = toast.schedule.Execute(() => Dismiss(toast));
            dismissItem.ExecuteLater((long)(state.Ttl * 1000f) + 60);
            state.DismissItem = dismissItem;
        }

        void PauseTimer(VisualElement toast)
        {
            if (toast.userData is not ToastState state) return;
            state.DismissItem?.Pause();

            var timer = toast.Q<VisualElement>("timer");
            if (timer == null) return;

            // Pull the bar back to full while hovered (matches the concept's hover-pause feel).
            timer.style.transitionDuration = new List<TimeValue> { new(0.15f, TimeUnit.Second) };
            toast.RemoveFromClassList("is-counting");
        }

        void Dismiss(VisualElement toast)
        {
            if (toast == null || toast.ClassListContains("is-leaving")) return;
            if (toast.userData is ToastState state) state.DismissItem?.Pause();

            toast.AddToClassList("is-leaving");
            bool removed = false;
            void Remove()
            {
                if (removed) return;
                removed = true;
                toast.RemoveFromHierarchy();
            }

            toast.RegisterCallback<TransitionEndEvent>(_ => Remove());
            // Fallback in case the transition-end event doesn't fire (e.g. no layout change).
            toast.schedule.Execute(Remove).ExecuteLater(320);
        }

        void TrimOverflow()
        {
            // children beyond MaxVisible are the oldest (newest inserted at index 0).
            for (int i = _stack.childCount - 1; i >= MaxVisible; i--)
                Dismiss(_stack[i]);
        }

        static string TypeClass(NotificationKind kind) => kind switch
        {
            NotificationKind.Success => "t-success",
            NotificationKind.Loot    => "t-loot",
            NotificationKind.Danger  => "t-danger",
            _                        => "t-info",
        };

        static string IconGlyph(NotificationKind kind) => kind switch
        {
            NotificationKind.Success => "✓",
            NotificationKind.Loot    => "◆",
            NotificationKind.Danger  => "!",
            _                        => "◈",
        };

        class ToastState
        {
            public float Ttl;
            public IVisualElementScheduledItem DismissItem;
        }
    }
}
