using Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace View.UI.MainMenu
{
    /// <summary>
    /// Main menu screen built with UI Toolkit. Lives in the MainMenu scene as a
    /// standalone component — does not depend on App / AppBootstrap. Save state
    /// is read directly via <see cref="SaveManager"/>; the actual game systems
    /// initialize once HideoutScene loads (its own AppBootstrap takes over).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        const string HideoutSceneName = "HideoutScene";

        UIDocument _doc;

        VisualElement _root;
        Button _continueBtn;
        Button _newGameBtn;
        Button _quitBtn;

        VisualElement _confirmOverlay;
        Label _confirmMessage;
        Button _confirmYes;
        Button _confirmNo;

        System.Action _pendingConfirmAction;

        void Awake()
        {
            BuildDocument();
        }

        void OnEnable()
        {
            RefreshButtons();
        }

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/MainMenu/MainMenu");
            var styles = Resources.Load<StyleSheet>("UI/MainMenu/MainMenu");
            var panel = Resources.Load<PanelSettings>("UI/MainMenu/MainMenuPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[MainMenu] Missing UXML or PanelSettings in Resources/UI/MainMenu/.");
                return;
            }

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (styles != null && !_root.styleSheets.Contains(styles))
                _root.styleSheets.Add(styles);

            _root.style.flexGrow = 1;

            _continueBtn = _root.Q<Button>("continueBtn");
            _newGameBtn = _root.Q<Button>("newGameBtn");
            _quitBtn = _root.Q<Button>("quitBtn");

            _confirmOverlay = _root.Q<VisualElement>("confirmOverlay");
            _confirmMessage = _root.Q<Label>("confirmMessage");
            _confirmYes = _root.Q<Button>("confirmYes");
            _confirmNo = _root.Q<Button>("confirmNo");

            _continueBtn.clicked += OnContinueClicked;
            _newGameBtn.clicked += OnNewGameClicked;
            _quitBtn.clicked += OnQuitClicked;
            _confirmYes.clicked += OnConfirmYes;
            _confirmNo.clicked += HideConfirm;

            HideConfirm();
        }

        void RefreshButtons()
        {
            if (_continueBtn == null) return;

            bool hasSave = SaveManager.HasSave();
            _continueBtn.SetEnabled(hasSave);
            _continueBtn.tooltip = hasSave ? "" : "No save found";
        }

        void OnContinueClicked()
        {
            if (!SaveManager.HasSave()) return;
            LoadHideout();
        }

        void OnNewGameClicked()
        {
            if (SaveManager.HasSave())
            {
                ShowConfirm(
                    "Starting a new game will permanently delete your current save. Continue?",
                    () =>
                    {
                        SaveManager.Delete();
                        LoadHideout();
                    });
            }
            else
            {
                LoadHideout();
            }
        }

        void OnQuitClicked()
        {
            ShowConfirm("Quit to desktop?", QuitApplication);
        }

        void OnConfirmYes()
        {
            var action = _pendingConfirmAction;
            HideConfirm();
            action?.Invoke();
        }

        void ShowConfirm(string message, System.Action onYes)
        {
            _pendingConfirmAction = onYes;
            if (_confirmMessage != null) _confirmMessage.text = message;
            if (_confirmOverlay != null) _confirmOverlay.style.display = DisplayStyle.Flex;
        }

        void HideConfirm()
        {
            _pendingConfirmAction = null;
            if (_confirmOverlay != null) _confirmOverlay.style.display = DisplayStyle.None;
        }

        void LoadHideout()
        {
            SceneManager.LoadScene(HideoutSceneName);
        }

        static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
