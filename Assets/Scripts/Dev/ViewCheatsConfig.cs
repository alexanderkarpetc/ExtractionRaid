using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Root view-layer tweaks configuration. Mirrors <see cref="DevCheatsConfig"/> в
    /// окремий asset so view polish (camera shake, hit feedback, post-processing tweaks
    /// тощо) живуть окремо від gameplay/balance/cheats.
    ///
    /// Lives in Resources/Configs/ViewCheatsConfig.asset — loaded once via Resources.Load.
    /// Each section is a separate asset in Resources/Configs/ViewCheats/.
    ///
    /// Migration plan: existing view-related sections (HitPause, HitFlash, MuzzleVfx,
    /// DamageNumbers, Crosshair, ADS, HealthBar, Parallax, FOV, Fog) move з DevCheatsConfig
    /// сюди over time, поступово, не за один pass — щоб не блокувати feature work.
    /// </summary>
    [CreateAssetMenu(fileName = "ViewCheatsConfig", menuName = "Dev/View Cheats Config")]
    public class ViewCheatsConfig : ScriptableObject
    {
        [SerializeField] ViewCheatsCameraShakeSection _cameraShake;
        [SerializeField] ViewCheatsBloodDecalSection _bloodDecal;
        [SerializeField] ViewCheatsBulletHoleSection _bulletHole;
        [SerializeField] ViewCheatsCasingsSection _casings;

        // Lazy-create fallbacks for null sections (in-memory defaults)
        public ViewCheatsCameraShakeSection CameraShake =>
            _cameraShake ? _cameraShake : (_cameraShake = CreateInstance<ViewCheatsCameraShakeSection>());

        public ViewCheatsBloodDecalSection BloodDecal =>
            _bloodDecal ? _bloodDecal : (_bloodDecal = CreateInstance<ViewCheatsBloodDecalSection>());

        public ViewCheatsBulletHoleSection BulletHole =>
            _bulletHole ? _bulletHole : (_bulletHole = CreateInstance<ViewCheatsBulletHoleSection>());

        public ViewCheatsCasingsSection Casings =>
            _casings ? _casings : (_casings = CreateInstance<ViewCheatsCasingsSection>());
    }
}
