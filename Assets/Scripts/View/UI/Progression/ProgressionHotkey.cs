using UnityEngine;
using UnityEngine.InputSystem;

namespace View.UI.Progression
{
    /// <summary>K toggles the character-progression tree during play mode.</summary>
    public class ProgressionHotkey : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[Key.K].wasPressedThisFrame && ProgressionWindow.Instance != null)
                ProgressionWindow.Instance.Toggle();
        }
    }
}
