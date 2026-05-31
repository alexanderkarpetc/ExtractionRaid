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
            var styles = Resources.Load<StyleSheet>("UI/Notifications/NotificationOverlay");
            var panel = Resources.Load<PanelSettings>("UI/Notifications/NotificationPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[NotificationOverlay] Missing UXML or PanelSettings in Resources/UI/Notifications/.");
                return;
            }

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (styles != null && !_root.styleSheets.Contains(styles))
                _root.styleSheets.Add(styles);

            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Ignore;

            _stack = _root.Q<VisualElement>("stack");
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
