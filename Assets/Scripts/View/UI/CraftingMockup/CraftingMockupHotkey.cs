using UnityEngine;
using UnityEngine.InputSystem;

namespace View.UI.CraftingMockup
{
    /// <summary>F10 toggles the UI Toolkit crafting mockup during play mode.</summary>
    public class CraftingMockupHotkey : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[Key.F10].wasPressedThisFrame && CraftingMockupWindow.Instance != null)
                CraftingMockupWindow.Instance.Toggle();
        }
    }
}
