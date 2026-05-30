using Adapters;
using ApplicationCore;
using Dev;
using Session;
using State;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Aim cursor presenter (uGUI + SDF shader). Owns the in-game reticle for both ballistic
    /// (4-arm + center dot + flame charge bars) and laser (segmented ring) archetypes,
    /// plus reload/charge arcs, hit pulse (EFD-style 4-stub spread), focus blur,
    /// overheat tremble, and per-archetype firing animation.
    ///
    /// Pipeline:
    ///   - One Screen-Space Overlay Canvas з RawImage fullscreen carrying SDF shader (`CrosshairSDF`).
    ///   - Per-instance material clone written each frame via `SetFloat`/`SetColor`/`SetVector`
    ///     (~35 shader params). Branches on `_LaserMode` for archetype.
    ///   - Consumes `HitConfirmed` (drives hit pulse) + `WeaponFired` for laser archetype
    ///     (captures chargeRatio for cooldown decay + pulse trigger).
    ///
    /// Lives як plain class у App; LateTick called from <c>App.LateTick</c> after damage
    /// numbers, before event-buffer clear.
    /// </summary>
    public class CrosshairPresenter
    {
        const string CrosshairPrefabPath = "Vfx/Prefabs/UI/Crosshair";

        // Shader property IDs (cached)
        static readonly int _Color         = Shader.PropertyToID("_Color");
        static readonly int _Alpha         = Shader.PropertyToID("_Alpha");
        static readonly int _CenterPx      = Shader.PropertyToID("_CenterPx");
        static readonly int _Gap           = Shader.PropertyToID("_Gap");
        static readonly int _LineLength    = Shader.PropertyToID("_LineLength");
        static readonly int _LineThickness = Shader.PropertyToID("_LineThickness");
        static readonly int _DotRadius     = Shader.PropertyToID("_DotRadius");
        static readonly int _LinesHidden   = Shader.PropertyToID("_LinesHidden");
        static readonly int _RingFill      = Shader.PropertyToID("_RingFill");
        static readonly int _RingRadius    = Shader.PropertyToID("_RingRadius");
        static readonly int _RingThickness = Shader.PropertyToID("_RingThickness");
        static readonly int _ChargeFill         = Shader.PropertyToID("_ChargeFill");
        static readonly int _ChargeColorCold    = Shader.PropertyToID("_ChargeColorCold");
        static readonly int _ChargeColorMid     = Shader.PropertyToID("_ChargeColorMid");
        static readonly int _ChargeColorHot     = Shader.PropertyToID("_ChargeColorHot");
        static readonly int _ChargeBarThicknessRatio = Shader.PropertyToID("_ChargeBarThicknessRatio");
        static readonly int _EdgeSoftness  = Shader.PropertyToID("_EdgeSoftness");
        static readonly int _OutlineColor  = Shader.PropertyToID("_OutlineColor");
        static readonly int _OutlineWidth  = Shader.PropertyToID("_OutlineWidth");
        static readonly int _TopArmAlpha   = Shader.PropertyToID("_TopArmAlpha");
        static readonly int _HitPulseProgress   = Shader.PropertyToID("_HitPulseProgress");
        static readonly int _HitPulseColor      = Shader.PropertyToID("_HitPulseColor");
        static readonly int _HitPulseInnerStart = Shader.PropertyToID("_HitPulseInnerStart");
        static readonly int _HitPulseInnerEnd   = Shader.PropertyToID("_HitPulseInnerEnd");
        static readonly int _HitPulseLength     = Shader.PropertyToID("_HitPulseLength");
        static readonly int _HitPulseThickness  = Shader.PropertyToID("_HitPulseThickness");
        static readonly int _HitPulseRotationRad         = Shader.PropertyToID("_HitPulseRotationRad");
        static readonly int _HitPulseThicknessTaperStart = Shader.PropertyToID("_HitPulseThicknessTaperStart");
        static readonly int _HitPulseThicknessTaperEnd   = Shader.PropertyToID("_HitPulseThicknessTaperEnd");
        static readonly int _HitPulseBurstPhaseEnd       = Shader.PropertyToID("_HitPulseBurstPhaseEnd");
        static readonly int _HitPulseHoldPhaseEnd        = Shader.PropertyToID("_HitPulseHoldPhaseEnd");
        static readonly int _LaserMode          = Shader.PropertyToID("_LaserMode");
        static readonly int _LaserSegmentCount  = Shader.PropertyToID("_LaserSegmentCount");
        static readonly int _LaserInnerRadius   = Shader.PropertyToID("_LaserInnerRadius");
        static readonly int _LaserOuterRadius   = Shader.PropertyToID("_LaserOuterRadius");
        static readonly int _LaserSegmentGapDeg = Shader.PropertyToID("_LaserSegmentGapDeg");
        static readonly int _LaserInactiveAlpha = Shader.PropertyToID("_LaserInactiveAlpha");

        // Loaded resources
        GameObject _crosshairPrefab;
        // Scene-spawned root
        Canvas _canvas;
        RawImage _reticle;
        Material _reticleMat; // per-instance material clone (auto-instanced via Image.material accessor)
        // ADS visual interpolant (mirrors v1's _adsAmount)
        float _adsAmount;
        bool  _resourcesLoaded;
        bool  _disabled;
        // Hit pulse — single-slot animation (EFD-style). New hit RESTARTS pulse з updated profile.
        // Snapshot profile values at trigger time → animation continues з locked-in values even
        // if user tweaks DevCheats mid-pulse (avoids weird mid-animation jumps).
        float _hitPulseStartTime;   // unscaledTime
        HitPulseProfile _activeHitPulse;
        bool  _hitPulseActive;
        // Laser firing animation — A+B from Stage 1.8 plan:
        //   _capturedChargeAtFire — chargeRatio snapshot at WeaponFired event. Drives chargeFill decay
        //                            during Cooldown so ring "bleeds back to empty" instead of instant drop.
        //   _firePulseT           — 0..1 pulse envelope (1 = just fired). Drives radial radius expansion
        //                            (inner shrinks / outer grows) so ring "inhales" on shot, springs back.
        // Both decay together over weapon.Stats.FireInterval during Cooldown phase. Bursting holds them at 1
        // (sustained feel). Ready/Reloading/Charging resets them to 0.
        float _capturedChargeAtFire;
        float _firePulseT;

        public CrosshairPresenter() { /* lazy init */ }

        void LoadResources()
        {
            if (_resourcesLoaded) return;
            _resourcesLoaded = true;
            _crosshairPrefab = Resources.Load<GameObject>(CrosshairPrefabPath);
            if (_crosshairPrefab == null)
            {
                Debug.LogWarning($"[CrosshairPresenter] Prefab missing at Resources/{CrosshairPrefabPath}");
                _disabled = true;
            }
        }

        void EnsureScene()
        {
            if (_canvas != null) return;
            if (_crosshairPrefab == null) return;
            var go = Object.Instantiate(_crosshairPrefab);
            go.name = "[CrosshairV2]";
            _canvas = go.GetComponentInChildren<Canvas>(true);
            _reticle = go.GetComponentInChildren<RawImage>(true);
            // Force a real material instance. uGUI Graphic.material (RawImage/Image) does NOT
            // auto-instance (unlike Renderer.material) — it returns the assigned reference as-is.
            // Without an explicit `new Material(...)` every per-frame SetFloat/SetColor (esp.
            // _CenterPx = live screen position) mutates the shared Crosshair.mat Resources asset,
            // which persists on play-mode exit → endless git churn across machines.
            if (_reticle != null && _reticle.material != null)
            {
                _reticleMat = new Material(_reticle.material);
                _reticle.material = _reticleMat;
            }
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;
            var cfg = ViewCheats.Config?.CrosshairV2;
            if (cfg == null) return;

            // Pointer over a UI Toolkit element (inv window/slot/builder/etc) —
            // hide v2 reticle, OS cursor takes over. PointerOverUiTracker (MonoBehaviour
            // on AppBootstrap GO) sets the flag in Update; this LateTick reads it in
            // the same frame. Attack/ADS gating in input adapter is driven by the same flag.
            if (App.Instance.IsPointerOverUi)
            {
                if (_canvas != null && _canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(false);
                return;
            }

            LoadResources();
            if (_disabled) return;
            EnsureScene();
            if (_canvas == null) return;

            if (!_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

            // Consume events to drive hit pulse + laser firing animation.
            //  - HitConfirmed: single-slot pulse (latest hit restarts animation).
            //  - WeaponFired (Laser-only): capture chargeRatio (packed у Damage by RaidEventBuffer.WeaponFired) +
            //                  trigger fire pulse for laser segmented ring. **Filter by archetype** — if ballistic
            //                  fires, captured stays 0 so shader's flame-bars path (gated on _ChargeFill > 0)
            //                  doesn't accidentally light up over the 4 arms during ballistic Firing/Cooldown.
            //                  Note: WeaponFired packs ratio in Damage, ProjectileSpawned packs it in CurrentHp — different events, different packing.
            foreach (var e in session.ConsumeEvents().All)
            {
                if (e.Type == RaidEventType.HitConfirmed) TriggerHitPulse(e, cfg);
                else if (e.Type == RaidEventType.WeaponFired && e.StringPayload == "Laser")
                {
                    _capturedChargeAtFire = e.Damage; // chargeRatio (see RaidEventBuffer.WeaponFired packing)
                    _firePulseT = 1f;
                }
            }

            // Drive reticle from player + weapon state
            UpdateReticle(session.RaidState, cfg);
        }

        // RaidEventBuffer.HitConfirmed packs (DIFFERENT from EntityHit):
        //   Damage      = isKill ? 1 : 0
        //   Direction.x = isHeadshot ? 1 : 0
        //   CurrentHp   = absorptionRatio
        //   MaxHp       = isRicochet ? 1 : 0
        void TriggerHitPulse(RaidEvent e, ViewCheatsCrosshairV2Section cfg)
        {
            bool isKill     = e.Damage > 0.5f;
            bool isHeadshot = e.Direction.x > 0.5f;
            bool isRicochet = e.MaxHp > 0.5f;

            // Priority: Ricochet > Kill > Headshot > Normal
            if      (isRicochet) _activeHitPulse = cfg.RicochetProfile;
            else if (isKill)     _activeHitPulse = cfg.KillProfile;
            else if (isHeadshot) _activeHitPulse = cfg.HeadshotProfile;
            else                 _activeHitPulse = cfg.NormalProfile;

            _hitPulseStartTime = Time.unscaledTime;
            _hitPulseActive = true;
        }

        // Phase-driven reticle update — gap, color, alpha, ring fills, laser charge/pulse.
        void UpdateReticle(RaidState state, ViewCheatsCrosshairV2Section cfg)
        {
            var player = state.PlayerEntity;
            if (player == null) return;
            var weapon = player.EquippedWeapon;

            // ADS blend toward target gap.
            float adsTarget = player.IsADS ? 1f : 0f;
            _adsAmount = Mathf.MoveTowards(_adsAmount, adsTarget, Time.unscaledDeltaTime * 8f);

            // Resolve cursor screen position. Use cam.WorldToScreenPoint of WeaponAimPoint
            // — mirrors v1 behavior + accounts for parallax/aim drift у same system.
            var cam = Camera.main;
            Vector3 sp = cam != null
                ? cam.WorldToScreenPoint(player.WeaponAimPoint)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

            // Phase-driven params
            float adsGap = Mathf.Lerp(cfg.Gap, cfg.AdsGap, _adsAmount);
            float adsBloomExtra = Mathf.Lerp(cfg.BloomExtraGap, cfg.AdsBloomExtraGap, _adsAmount);

            float gap = adsGap;
            float alpha = 1f;
            Color color = cfg.NormalColor;
            float ringFill = 0f;
            float chargeFill = 0f;
            float linesHidden = 0f;

            if (weapon == null)
            {
                // Unarmed — dot only
                linesHidden = 1f;
                alpha = 0.5f;
            }
            else
            {
                float elapsed = state.ElapsedTime - weapon.PhaseStartTime;
                bool hasAmmo = !string.IsNullOrEmpty(weapon.AmmoType)
                    ? weapon.AmmoInMagazine > 0
                    : true;

                switch (weapon.Phase)
                {
                    case WeaponPhase.Ready:
                        color = hasAmmo ? cfg.NormalColor : cfg.WarningColor;
                        // Settled — drop captured charge + fire pulse (laser segments return to dim silhouette).
                        _capturedChargeAtFire = 0f;
                        _firePulseT = 0f;
                        break;

                    case WeaponPhase.Firing:
                        // 1-frame spike — full bloom (ballistic). Laser: hold captured charge у ring (sustained burn).
                        gap = adsGap + adsBloomExtra;
                        color = cfg.BloomColor;
                        chargeFill = _capturedChargeAtFire;
                        break;

                    case WeaponPhase.Bursting:
                        // Treat як sustained firing — keep bloomed. Laser: ring stays at captured charge between burst shots
                        // (each WeaponFired re-triggers _firePulseT = 1 → staccato pulse feel).
                        gap = adsGap + adsBloomExtra * 0.8f;
                        color = cfg.BloomColor;
                        chargeFill = _capturedChargeAtFire;
                        break;

                    case WeaponPhase.Cooldown:
                    {
                        float cooldownT = weapon.Stats.FireInterval > 0f
                            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / weapon.Stats.FireInterval))
                            : 1f;
                        gap = adsGap + adsBloomExtra * (1f - cooldownT);
                        color = Color.Lerp(cfg.BloomColor, cfg.NormalColor, cooldownT);
                        // Laser ring "bleeds back to empty" + pulse springs back over FireInterval.
                        chargeFill = _capturedChargeAtFire * (1f - cooldownT);
                        _firePulseT = 1f - cooldownT;
                        break;
                    }

                    case WeaponPhase.Reloading:
                    {
                        float reloadProgress = weapon.Stats.ReloadTime > 0f
                            ? Mathf.Clamp01(elapsed / weapon.Stats.ReloadTime)
                            : 1f;
                        ringFill = reloadProgress;
                        linesHidden = 1f;
                        color = cfg.NormalColor;
                        // Reload restarts the round — drop fire animation state.
                        _capturedChargeAtFire = 0f;
                        _firePulseT = 0f;
                        break;
                    }

                    case WeaponPhase.Charging:
                    {
                        // Apply per-delivery charge multiplier so ring matches actual gameplay time.
                        // DevCheats override (>0) replaces payload-asset baseline — must mirror ShootingSystem.
                        var laserCfg = DevCheats.Config?.Laser;
                        float deliveryMult = laserCfg != null
                            ? laserCfg.ChargeTimeMultiplierFor(weapon.DeliveryDefinition?.Pattern ?? FiringPattern.Single)
                            : 1f;
                        float overrideSeconds = laserCfg != null ? laserCfg.ChargeTimeOverrideSeconds : 0f;
                        float chargeTime = Systems.WeaponChargeResolver.GetChargeTime(
                            weapon, deliveryMult, overrideSeconds);
                        // Linear t → shaped chargeRatio via DevCheatsLaserSection.EvaluateChargeRatio.
                        // Mirrors ShootingSystem so cursor fill matches gameplay charge in lockstep.
                        float linearT = chargeTime > 0f
                            ? Mathf.Clamp01((state.ElapsedTime - weapon.ChargeStartTime) / chargeTime)
                            : 1f;
                        chargeFill = laserCfg != null ? laserCfg.EvaluateChargeRatio(linearT) : linearT;
                        color = cfg.NormalColor;
                        // New charge cycle — drop captured snapshot from previous shot.
                        _capturedChargeAtFire = 0f;
                        _firePulseT = 0f;
                        break;
                    }

                    case WeaponPhase.Equipping:
                        alpha = weapon.Stats.EquipTime > 0f
                            ? Mathf.Clamp01(elapsed / weapon.Stats.EquipTime)
                            : 1f;
                        color = cfg.NormalColor;
                        break;

                    case WeaponPhase.Unequipping:
                        alpha = weapon.Stats.UnequipTime > 0f
                            ? Mathf.Clamp01(1f - elapsed / weapon.Stats.UnequipTime)
                            : 0f;
                        color = cfg.NormalColor;
                        break;

                    default:
                        color = cfg.NormalColor;
                        break;
                }
            }

            // Rolling — lower alpha
            if (player.IsRolling) alpha *= cfg.RollingAlpha;

            // Overheat tremble — perlin-driven jitter on cursor center при near-max charge.
            Vector2 trembleOffset = Vector2.zero;
            if (chargeFill >= cfg.ChargeOverheatThreshold && cfg.ChargeOverheatTremblePx > 0f)
            {
                float intensity = Mathf.InverseLerp(cfg.ChargeOverheatThreshold, 1f, chargeFill);
                float t = Time.unscaledTime * cfg.ChargeOverheatTrembleFreq;
                // 2-axis perlin noise (offset y sampling so x/y decorrelated)
                float jx = Mathf.PerlinNoise(t, 0.37f) * 2f - 1f;
                float jy = Mathf.PerlinNoise(7.91f, t) * 2f - 1f;
                trembleOffset = new Vector2(jx, jy) * cfg.ChargeOverheatTremblePx * intensity;
            }

            // Laser archetype routing — payload "Laser" gets the segmented ring cursor instead of 4-arm.
            // Unarmed / ballistic / unknown → ballistic 4-arm (laser mode 0).
            bool laserMode = weapon?.PayloadDefinition?.Archetype == "Laser";

            // Push to shader via per-instance material. Lazy re-instance if lost (domain reload
            // etc.) — never write the shared asset directly (see SetupReticle).
            if (_reticleMat == null && _reticle != null && _reticle.material != null)
            {
                _reticleMat = new Material(_reticle.material);
                _reticle.material = _reticleMat;
            }
            if (_reticleMat == null) return;
            _reticleMat.SetColor(_Color, color);
            _reticleMat.SetFloat(_Alpha, alpha);
            _reticleMat.SetVector(_CenterPx, new Vector4(sp.x + trembleOffset.x, sp.y + trembleOffset.y, Screen.width, Screen.height));
            _reticleMat.SetFloat(_Gap, gap);
            _reticleMat.SetFloat(_LineLength, cfg.LineLength);
            _reticleMat.SetFloat(_LineThickness, cfg.LineThickness);
            _reticleMat.SetFloat(_DotRadius, cfg.DotRadius);
            _reticleMat.SetFloat(_LinesHidden, linesHidden);
            _reticleMat.SetFloat(_RingFill, ringFill);
            _reticleMat.SetFloat(_RingRadius, cfg.RingRadius);
            _reticleMat.SetFloat(_RingThickness, cfg.RingThickness);
            _reticleMat.SetFloat(_ChargeFill, chargeFill);
            _reticleMat.SetColor(_ChargeColorCold, cfg.ChargeColorCold);
            _reticleMat.SetColor(_ChargeColorMid,  cfg.ChargeColorMid);
            _reticleMat.SetColor(_ChargeColorHot,  cfg.ChargeColorHot);
            _reticleMat.SetFloat(_ChargeBarThicknessRatio, cfg.ChargeBarThicknessRatio);
            // Focus blur — Stage 3. _EdgeSoftness driven by accuracy state (recoil pressure + ADS settle).
            // Disabled → static cfg.EdgeSoftness (Stage 1/2 fallback, no regression). Same softness applies
            // to ALL SDF groups (main + charge + hit pulse) since shader uses single `_EdgeSoftness`.
            float blurPx = cfg.EdgeSoftness;
            if (cfg.FocusBlurEnabled)
            {
                // Recoil contribution: 0 = settled, 1 = saturated. Reads gameplay-rooted RecoilOffset
                // (set by ShootingSystem, decayed by AimingSystem) so blur tracks the same shift
                // the cursor already follows visually — single source of truth.
                float recoilMag = weapon != null ? weapon.RecoilOffset.magnitude : 0f;
                float recoilPressure = Mathf.Clamp01(recoilMag / Mathf.Max(0.001f, cfg.BlurRecoilSaturation));

                // ADS contribution: 0 when fully ADS, 1 when hip-fire. Scaled by BlurHipFireAmount
                // so designer can dial how much hip-vs-ADS baseline differs (0 = no diff).
                float adsContribution = (1f - player.AdsBlend) * cfg.BlurHipFireAmount;

                // Combined accuracy deficit via max() — whichever source causes the larger blur wins.
                // Sum-style would double-count (recoil during hip fire = additive), max-style ceiling-clamps
                // to BlurMaxPx via single-source path.
                float deficit = Mathf.Max(recoilPressure * cfg.BlurRecoilWeight, adsContribution);
                blurPx = Mathf.Lerp(cfg.BlurMinPx, cfg.BlurMaxPx, deficit);
            }
            _reticleMat.SetFloat(_EdgeSoftness, blurPx);
            _reticleMat.SetColor(_OutlineColor, cfg.OutlineColor);
            _reticleMat.SetFloat(_OutlineWidth, cfg.OutlineWidth);
            // ADS — binary cutoff (Stage 1). adsAmount below threshold = top arm shown, above = hidden.
            // Smooth alpha fade requires per-arm SDF composition rewrite — deferred to later stage.
            // Laser mode has no arms — push 1 (no-op for that path; shader skips arm logic anyway).
            float topArmAlpha = laserMode ? 1f : (_adsAmount >= cfg.AdsTopArmFadeStart ? 0f : 1f);
            _reticleMat.SetFloat(_TopArmAlpha, topArmAlpha);

            // Laser mode block. When archetype = Laser, segmented ring overrides 4-arm rendering.
            // Always-on dim silhouette: shader uses _LaserInactiveAlpha for sub-active segments,
            // so the ring is visible even at chargeFill=0 (anchor point for the player's eye).
            _reticleMat.SetFloat(_LaserMode,          laserMode ? 1f : 0f);
            _reticleMat.SetFloat(_LaserSegmentCount,  cfg.LaserSegmentCount);
            // Radial pulse on fire — inner shrinks / outer grows by `LaserFirePulseRadiusPx × _firePulseT`,
            // settling back to baseline as cooldown progresses (decay set inside Cooldown phase case).
            // Min clamp on inner keeps ring from inverting if user dials radii close together.
            float pulseDelta      = cfg.LaserFirePulseRadiusPx * _firePulseT;
            float effectiveInnerR = Mathf.Max(2f, cfg.LaserRingInnerRadius - pulseDelta);
            float effectiveOuterR = cfg.LaserRingOuterRadius + pulseDelta;
            _reticleMat.SetFloat(_LaserInnerRadius,   effectiveInnerR);
            _reticleMat.SetFloat(_LaserOuterRadius,   effectiveOuterR);
            _reticleMat.SetFloat(_LaserSegmentGapDeg, cfg.LaserSegmentGapDeg);
            _reticleMat.SetFloat(_LaserInactiveAlpha, cfg.LaserInactiveAlpha);

            // Hit pulse animation — single-slot, 0..1 progress. 1 = ended / inactive. Values come з
            // _activeHitPulse profile snapshot taken at trigger time (immune до mid-animation tweaks).
            float pulseProgress = 1f;
            if (_hitPulseActive)
            {
                float t = (Time.unscaledTime - _hitPulseStartTime) / Mathf.Max(0.001f, _activeHitPulse.Duration);
                if (t >= 1f) _hitPulseActive = false;
                pulseProgress = Mathf.Clamp01(t);
            }
            _reticleMat.SetFloat(_HitPulseProgress, pulseProgress);
            _reticleMat.SetColor(_HitPulseColor, _activeHitPulse.Color);
            _reticleMat.SetFloat(_HitPulseInnerStart, _activeHitPulse.InnerStart);
            _reticleMat.SetFloat(_HitPulseInnerEnd,   _activeHitPulse.InnerEnd);
            _reticleMat.SetFloat(_HitPulseLength,     _activeHitPulse.Length);
            _reticleMat.SetFloat(_HitPulseThickness,  _activeHitPulse.Thickness);
            _reticleMat.SetFloat(_HitPulseRotationRad,         _activeHitPulse.RotationRad);
            _reticleMat.SetFloat(_HitPulseThicknessTaperStart, _activeHitPulse.ThicknessTaperStart);
            _reticleMat.SetFloat(_HitPulseThicknessTaperEnd,   _activeHitPulse.ThicknessTaperEnd);
            _reticleMat.SetFloat(_HitPulseBurstPhaseEnd,       _activeHitPulse.BurstPhaseEnd);
            _reticleMat.SetFloat(_HitPulseHoldPhaseEnd,        _activeHitPulse.HoldPhaseEnd);
        }

        public void Dispose()
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
            _canvas = null;
        }
    }
}
