using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Thin static accessor over <see cref="ViewCheatsConfig"/> ScriptableObject.
    /// Mirror of <see cref="DevCheats"/> but scoped до view-layer tunables (camera, VFX,
    /// HUD polish — not gameplay balance / cheats).
    ///
    /// The SO asset lives at Resources/Configs/ViewCheatsConfig.
    /// </summary>
    public static class ViewCheats
    {
        static ViewCheatsConfig _cfg;

        public static ViewCheatsConfig Config
        {
            get
            {
                if (_cfg == null)
                    _cfg = Resources.Load<ViewCheatsConfig>("Configs/ViewCheatsConfig");
#if UNITY_EDITOR
                // Fallback: create in-memory instance so editor never NPEs
                if (_cfg == null)
                {
                    Debug.LogWarning("[ViewCheats] ViewCheatsConfig asset not found in Resources. Using in-memory defaults. " +
                                     "Run Window → View Cheats → \"Create Section Assets\" to materialize.");
                    _cfg = ScriptableObject.CreateInstance<ViewCheatsConfig>();
                }
#endif
                return _cfg;
            }
        }
    }
}
