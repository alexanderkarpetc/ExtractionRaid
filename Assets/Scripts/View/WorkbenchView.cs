using ApplicationCore;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI.WeaponBuilder;

namespace View
{
    /// <summary>
    /// Physical workbench scene object that opens the Weapon Builder modal when the
    /// player is in range and presses E.
    ///
    /// Pure view component — no gameplay state. Reads player position off
    /// <see cref="App.Instance.RaidSession"/>. Shows a world-space TextMesh prompt
    /// ("Press E to craft") while in range. Input is read directly via Unity's
    /// <see cref="Keyboard.current"/> — view-layer access is fine here.
    ///
    /// See docs/ai/weapon-builder/architecture.md §D13.
    /// </summary>
    public class WorkbenchView : MonoBehaviour
    {
        [Tooltip("Player must be within this radius (world units) to interact.")]
        [SerializeField] float _interactRange = 2.5f;

        [Tooltip("Vertical offset above the workbench origin where the prompt is shown.")]
        [SerializeField] float _promptHeight = 1.4f;

        [Tooltip("Prompt text shown while in range. Empty → hides the prompt entirely.")]
        [SerializeField] string _promptText = "Press E to craft";

        GameObject _promptGo;
        TextMesh _promptText3D;

        void Awake()
        {
            BuildPrompt();
        }

        void Update()
        {
            if (!App.IsInitialized) return;
            var session = App.Instance.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null)
            {
                SetPromptVisible(false);
                return;
            }

            float sqrDist = (player.Position - transform.position).sqrMagnitude;
            bool nowInRange = sqrDist <= _interactRange * _interactRange;

            // Don't show prompt (or allow interact) while another menu already steals input.
            bool menuOpen = player.IsInMenu;

            SetPromptVisible(nowInRange && !menuOpen);

            if (nowInRange && !menuOpen)
            {
                // View-layer: read Unity input directly. Gated on menuOpen above so the
                // modal's own ESC/Cancel handle close — workbench only handles "open".
                var kb = Keyboard.current;
                if (kb != null && kb[Key.E].wasPressedThisFrame)
                    WeaponBuilderWindow.Instance?.Open();
            }
        }

        // ── Prompt setup ──────────────────────────────────────

        void BuildPrompt()
        {
            if (string.IsNullOrEmpty(_promptText)) return;

            _promptGo = new GameObject("WorkbenchPrompt");
            _promptGo.transform.SetParent(transform, false);
            _promptGo.transform.localPosition = new Vector3(0f, _promptHeight, 0f);

            _promptText3D = _promptGo.AddComponent<TextMesh>();
            _promptText3D.text = _promptText;
            _promptText3D.fontSize = 40;
            _promptText3D.characterSize = 0.08f;
            _promptText3D.anchor = TextAnchor.MiddleCenter;
            _promptText3D.alignment = TextAlignment.Center;
            _promptText3D.color = new Color(0.9f, 0.85f, 0.55f);

            _promptGo.SetActive(false);
        }

        void SetPromptVisible(bool visible)
        {
            if (_promptGo == null) return;
            if (_promptGo.activeSelf != visible)
                _promptGo.SetActive(visible);

            if (visible && Camera.main != null)
            {
                // Billboard: face camera each frame while visible.
                var camPos = Camera.main.transform.position;
                _promptGo.transform.rotation = Quaternion.LookRotation(
                    _promptGo.transform.position - camPos);
            }
        }

        // ── Editor gizmo ──────────────────────────────────────

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.85f, 0.55f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}
