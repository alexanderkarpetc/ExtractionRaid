using UnityEngine;

namespace Dev
{
    /// <summary>
    /// Root dev-cheat configuration. Holds references to per-section ScriptableObject assets.
    /// Lives in Resources/DevCheatsConfig.asset — loaded once via Resources.Load.
    /// Each section is a separate asset in Resources/DevCheats/ for independent VCS tracking.
    /// </summary>
    [CreateAssetMenu(fileName = "DevCheatsConfig", menuName = "Dev/Cheats Config")]
    public class DevCheatsConfig : ScriptableObject
    {
        [SerializeField] DevCheatsCheatsSection _cheats;
        [SerializeField] DevCheatsWeaponSection _weapon;
        [SerializeField] DevCheatsRecoilSection _recoil;
        [SerializeField] DevCheatsAimSection _aim;
        [SerializeField] DevCheatsPlayerSection _player;
        [SerializeField] DevCheatsFOVSection _fov;
        [SerializeField] DevCheatsFogSection _fog;
        [SerializeField] DevCheatsCrosshairSection _crosshair;
        [SerializeField] DevCheatsADSSection _ads;
        [SerializeField] DevCheatsScopeSection _scope;
        [SerializeField] DevCheatsHealthBarSection _healthBar;
        [SerializeField] DevCheatsParallaxSection _parallax;
        [SerializeField] DevCheatsStatusEffectsSection _statusEffects;
        [SerializeField] DevCheatsArmorSection _armor;
        [SerializeField] DevCheatsHitPauseSection _hitPause;
        [SerializeField] DevCheatsMuzzleVfxSection _muzzleVfx;
        [SerializeField] DevCheatsStaggerSection _stagger;
        [SerializeField] DevCheatsHordeSection _horde;
        [SerializeField] DevCheatsBotEngagementSection _botEngagement;
        [SerializeField] DevCheatsLaserSection _laser;
        [SerializeField] DevCheatsBarrelHeatSection _barrelHeat;
        [SerializeField] DevCheatsStaminaSection _stamina;

        // Lazy-create fallbacks for null sections (in-memory defaults)
        public DevCheatsCheatsSection Cheats => _cheats ? _cheats : (_cheats = CreateInstance<DevCheatsCheatsSection>());
        public DevCheatsWeaponSection Weapon => _weapon ? _weapon : (_weapon = CreateInstance<DevCheatsWeaponSection>());
        public DevCheatsRecoilSection Recoil => _recoil ? _recoil : (_recoil = CreateInstance<DevCheatsRecoilSection>());
        public DevCheatsAimSection Aim => _aim ? _aim : (_aim = CreateInstance<DevCheatsAimSection>());
        public DevCheatsPlayerSection Player => _player ? _player : (_player = CreateInstance<DevCheatsPlayerSection>());
        public DevCheatsFOVSection FOV => _fov ? _fov : (_fov = CreateInstance<DevCheatsFOVSection>());
        public DevCheatsFogSection Fog => _fog ? _fog : (_fog = CreateInstance<DevCheatsFogSection>());
        public DevCheatsCrosshairSection Crosshair => _crosshair ? _crosshair : (_crosshair = CreateInstance<DevCheatsCrosshairSection>());
        public DevCheatsADSSection ADS => _ads ? _ads : (_ads = CreateInstance<DevCheatsADSSection>());
        public DevCheatsScopeSection Scope => _scope ? _scope : (_scope = CreateInstance<DevCheatsScopeSection>());
        public DevCheatsHealthBarSection HealthBar => _healthBar ? _healthBar : (_healthBar = CreateInstance<DevCheatsHealthBarSection>());
        public DevCheatsParallaxSection Parallax => _parallax ? _parallax : (_parallax = CreateInstance<DevCheatsParallaxSection>());
        public DevCheatsStatusEffectsSection StatusEffects => _statusEffects ? _statusEffects : (_statusEffects = CreateInstance<DevCheatsStatusEffectsSection>());
        public DevCheatsArmorSection Armor => _armor ? _armor : (_armor = CreateInstance<DevCheatsArmorSection>());
        public DevCheatsHitPauseSection HitPause => _hitPause ? _hitPause : (_hitPause = CreateInstance<DevCheatsHitPauseSection>());
        public DevCheatsMuzzleVfxSection MuzzleVfx => _muzzleVfx ? _muzzleVfx : (_muzzleVfx = CreateInstance<DevCheatsMuzzleVfxSection>());
        public DevCheatsStaggerSection Stagger => _stagger ? _stagger : (_stagger = CreateInstance<DevCheatsStaggerSection>());
        public DevCheatsHordeSection Horde => _horde ? _horde : (_horde = CreateInstance<DevCheatsHordeSection>());
        public DevCheatsBotEngagementSection BotEngagement => _botEngagement ? _botEngagement : (_botEngagement = CreateInstance<DevCheatsBotEngagementSection>());
        public DevCheatsLaserSection Laser => _laser ? _laser : (_laser = CreateInstance<DevCheatsLaserSection>());
        public DevCheatsBarrelHeatSection BarrelHeat => _barrelHeat ? _barrelHeat : (_barrelHeat = CreateInstance<DevCheatsBarrelHeatSection>());
        public DevCheatsStaminaSection Stamina => _stamina ? _stamina : (_stamina = CreateInstance<DevCheatsStaminaSection>());
    }
}
