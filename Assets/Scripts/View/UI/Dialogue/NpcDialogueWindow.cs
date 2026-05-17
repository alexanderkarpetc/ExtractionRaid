using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Dialogue
{
    /// <summary>
    /// One UI Toolkit window per NPC interaction. Visual layout follows the concept at
    /// Assets/Concepts/dialogue_ui_concept.html — portrait + nameplate + typewriter line +
    /// vertical choice list. Choices are passed in via <see cref="Show"/>; the window
    /// exposes them as buttons and fires the click handler back. Keeps no business logic
    /// of its own — caller decides what each action does.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class NpcDialogueWindow : MonoBehaviour
    {
        public static NpcDialogueWindow Instance { get; private set; }

        public struct Choice
        {
            public string Label;
            public Action OnClick;
            // Defaults to true via the constructor pattern below — a default-initialized
            // struct (Enabled = false) is the rare path. Set explicitly false to render
            // the button greyed out and non-clickable (e.g. upgrade with no materials).
            public bool? EnabledOverride;
            public bool Enabled => EnabledOverride ?? true;
        }

        UIDocument _doc;
        VisualElement _root;
        Label _nameplateLabel;
        Label _textBox;
        VisualElement _choicesBox;

        bool _isVisible;

        // Typewriter state
        string _fullLine = "";
        float _charsPerSecond = 38f;
        float _typeStartTime;
        bool _isTyping;

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
            var tree = Resources.Load<VisualTreeAsset>("UI/Dialogue/NpcDialogue");
            var styles = Resources.Load<StyleSheet>("UI/Dialogue/NpcDialogue");
            var panel = Resources.Load<PanelSettings>("UI/Dialogue/NpcDialoguePanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[NpcDialogue] Missing UXML or PanelSettings in Resources/UI/Dialogue/.");
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

            _nameplateLabel = _root.Q<Label>("speakerName");
            _textBox = _root.Q<Label>("textBox");
            _choicesBox = _root.Q<VisualElement>("choicesBox");
        }

        public void Show(string speakerName, string introLine, IList<Choice> choices)
        {
            if (_root == null) return;

            _nameplateLabel.text = string.IsNullOrEmpty(speakerName) ? "NPC" : speakerName;
            BeginTyping(introLine ?? "");
            BuildChoiceButtons(choices);

            _root.style.display = DisplayStyle.Flex;
            _isVisible = true;
        }

        public void Hide()
        {
            HideImmediate();
        }

        public bool IsVisible => _isVisible;

        void HideImmediate()
        {
            _isTyping = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (_choicesBox != null) _choicesBox.Clear();
            _isVisible = false;
        }

        void BuildChoiceButtons(IList<Choice> choices)
        {
            _choicesBox.Clear();
            if (choices == null) return;

            for (int i = 0; i < choices.Count; i++)
            {
                var c = choices[i];
                var btn = new Button { text = "" };
                btn.AddToClassList("choice");
                if (!c.Enabled)
                {
                    btn.AddToClassList("choice--disabled");
                    btn.SetEnabled(false);
                }

                var index = new Label((i + 1) + ".") { pickingMode = PickingMode.Ignore };
                index.AddToClassList("index");
                btn.Add(index);

                var label = new Label(c.Label ?? "") { pickingMode = PickingMode.Ignore };
                label.AddToClassList("choice-label");
                btn.Add(label);

                var captured = c;
                btn.clicked += () => captured.OnClick?.Invoke();
                _choicesBox.Add(btn);
            }
        }

        void BeginTyping(string line)
        {
            _fullLine = line;
            _typeStartTime = Time.unscaledTime;
            _isTyping = true;
            if (_textBox != null) _textBox.text = "";
        }

        void Update()
        {
            if (!_isVisible || !_isTyping || _textBox == null) return;

            float elapsed = Time.unscaledTime - _typeStartTime;
            int target = Mathf.Clamp(Mathf.FloorToInt(elapsed * _charsPerSecond), 0, _fullLine.Length);
            _textBox.text = _fullLine.Substring(0, target);
            if (target >= _fullLine.Length)
                _isTyping = false;
        }
    }
}
