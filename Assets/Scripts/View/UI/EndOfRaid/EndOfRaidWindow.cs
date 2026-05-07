using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.EndOfRaid
{
    /// <summary>
    /// End-of-raid result screen, built at runtime from the assets in
    /// <c>Resources/UI/EndOfRaid/</c>. Owns no game logic — caller passes a title
    /// (e.g. "EXTRACTED" / "YOU DIED"), a subtitle line, and a click handler for
    /// the Next button.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class EndOfRaidWindow : MonoBehaviour
    {
        public static EndOfRaidWindow Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        VisualElement _card;
        Label _titleLabel;
        Label _subtitleLabel;
        Button _nextButton;

        Action _onNextClicked;
        bool _isVisible;

        public bool IsVisible => _isVisible;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            HideImmediate();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/EndOfRaid/EndOfRaid");
            var styles = Resources.Load<StyleSheet>("UI/EndOfRaid/EndOfRaid");
            var panel = Resources.Load<PanelSettings>("UI/EndOfRaid/EndOfRaidPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[EndOfRaid] Missing UXML or PanelSettings in Resources/UI/EndOfRaid/.");
                return;
            }

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (styles != null && !_root.styleSheets.Contains(styles))
                _root.styleSheets.Add(styles);

            _root.style.flexGrow = 1;

            _card = _root.Q<VisualElement>("card");
            _titleLabel = _root.Q<Label>("title");
            _subtitleLabel = _root.Q<Label>("subtitle");
            _nextButton = _root.Q<Button>("nextBtn");

            if (_nextButton != null)
                _nextButton.clicked += OnNextClickedInternal;
        }

        public void Show(string title, string subtitle, bool success, Action onNext)
        {
            if (_root == null) return;

            _titleLabel.text = title ?? "";
            _subtitleLabel.text = subtitle ?? "";
            _onNextClicked = onNext;

            if (_card != null)
            {
                _card.RemoveFromClassList("card--success");
                _card.RemoveFromClassList("card--failure");
                _card.AddToClassList(success ? "card--success" : "card--failure");
            }

            _root.style.display = DisplayStyle.Flex;
            _isVisible = true;
        }

        public void Hide()
        {
            HideImmediate();
        }

        void HideImmediate()
        {
            _onNextClicked = null;
            if (_root != null) _root.style.display = DisplayStyle.None;
            _isVisible = false;
        }

        void OnNextClickedInternal()
        {
            // Disable the button to swallow double-clicks while the scene swap runs.
            if (_nextButton != null) _nextButton.SetEnabled(false);
            var cb = _onNextClicked;
            _onNextClicked = null;
            cb?.Invoke();
        }
    }
}
