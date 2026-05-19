using System;
using Adapters;
using ApplicationCore;
using Constants;
using Dev;
using Session;
using State;
using UnityEngine;
using View.FogOfWar;
using Object = UnityEngine.Object;

namespace View
{
    public class PlayerPresenter
    {
        readonly GameObject _shellPrefab;
        readonly GameObject _bodyPrefab;
        readonly Action<Transform> _onMuzzlePointReady;

        PlayerView _playerView;
        GrenadeTrajectoryOverlay _trajectoryOverlay;
        FogOfWarController _fogOfWarController;
        EId _trackedId;

        const string ShellPrefabPath = "Prefabs/PlayerShell";
        const string BodyPrefabPath = "Prefabs/Bodies/CharacterBody";

        public PlayerPresenter(Action<Transform> onMuzzlePointReady)
        {
            _onMuzzlePointReady = onMuzzlePointReady;
            _shellPrefab = Resources.Load<GameObject>(ShellPrefabPath);
            _bodyPrefab = Resources.Load<GameObject>(BodyPrefabPath);

            if (_shellPrefab == null)
                Debug.LogError($"[PlayerPresenter] Shell prefab not found: {ShellPrefabPath}");
            if (_bodyPrefab == null)
                Debug.LogError($"[PlayerPresenter] Body prefab not found: {BodyPrefabPath}");
        }

        public void LateTick(RaidSession session)
        {
            if (session == null) return;

            var events = session.ConsumeEvents();

            foreach (var e in events.All)
            {
                switch (e.Type)
                {
                    case RaidEventType.PlayerSpawned when _playerView == null:
                        _trackedId = e.Id;
                        SpawnView(session.RaidState.PlayerEntity, session);
                        break;
                    case RaidEventType.WeaponFired:
                    {
                        var weapon = session.RaidState.PlayerEntity?.EquippedWeapon;
                        _playerView?.WeaponView?.PlayMuzzleFlash();
                        if (weapon != null)
                            _playerView?.WeaponView?.PlayFire(weapon.Stats.FireInterval);
                        break;
                    }
                    case RaidEventType.WeaponEquipStarted:
                    {
                        var weapon = session.RaidState.PlayerEntity?.EquippedWeapon;
                        if (weapon != null)
                            _playerView?.WeaponView?.PlayEquip(weapon.Stats.EquipTime);
                        break;
                    }
                    case RaidEventType.WeaponUnequipStarted:
                    {
                        // Cache unequip duration — weapon may become null during unequip
                        var weapon = session.RaidState.PlayerEntity?.EquippedWeapon;
                        if (weapon != null)
                            _playerView?.WeaponView?.PlayUnequip(weapon.Stats.UnequipTime);
                        break;
                    }
                    case RaidEventType.WeaponEquipFinished:
                        break;
                    case RaidEventType.WeaponReloadStarted:
                    {
                        var weapon = session.RaidState.PlayerEntity?.EquippedWeapon;
                        if (weapon != null)
                            _playerView?.WeaponView?.PlayReload(weapon.Stats.ReloadTime);
                        break;
                    }
                    case RaidEventType.WeaponReloadFinished:
                        break;
                    case RaidEventType.WeaponDryFired:
                        _playerView?.WeaponView?.PlayDryFire();
                        break;
                    case RaidEventType.ArmorBroken when e.Id == _trackedId && _playerView != null:
                    {
                        bool isHelmet = e.Damage > 0.5f; // packed in RaidEventBuffer
                        if (isHelmet)
                            ArmorBreakHelmetFlyOff(_playerView);
                        else
                            _playerView.ClearArmorModel();
                        break;
                    }
                    case RaidEventType.EntityHit when e.Id == _trackedId && _playerView != null:
                    {
                        ApplyHitFlash(_playerView, e);
                        break;
                    }
                }

                if (e.Type == RaidEventType.EntityDamaged && e.Id == _trackedId && _playerView != null)
                {
                    _playerView.OnDamaged(e.CurrentHp, e.MaxHp);
                }
            }

            if (_playerView != null && session.RaidState.PlayerEntity != null)
            {
                _playerView.SyncFromState(session.RaidState.PlayerEntity, session.RaidState.ElapsedTime);
                _trajectoryOverlay?.UpdateTrajectory(session.RaidState.PlayerEntity);

                // Armor sync — bar durability + mesh attach/detach. State-driven (NOT event-
                // driven) so that ANY mutation of HelmetSlot/BodyArmorSlot from any source
                // (inventory drag-out → backpack/loot/stash/floor, drag-in, swap, future
                // cheats) updates the visual without needing each mutation site to emit an
                // event. SwapHelmetModel/SwapArmorModel are idempotent — no-op when the
                // prefab ID hasn't changed, so per-tick calls are free. Fly-off path
                // (ArmorBroken event handled above) detaches via DetachHelmetModel and
                // sets CharacterBody's tracked prefab to null; this block then writes the
                // null state ВПЕРЕД with SwapHelmetModel(null) → no-op. When the broken
                // armor item is later removed from inventory, ArmorSlotState.IsBroken
                // ensures we don't re-attach до a replacement is equipped.
                session.RaidState.ArmorMap.TryGetValue(_trackedId, out var armorSlots);

                float helmetDur = armorSlots?.Helmet?.DurabilityPercent ?? 0f;
                float vestDur   = armorSlots?.BodyArmor?.DurabilityPercent ?? 0f;
                _playerView.UpdateArmor(helmetDur, vestDur);

                var inventory = App.Instance?.Player?.Inventory;
                _playerView.SwapHelmetModel(ResolveArmorPrefab(inventory?.HelmetSlot,    armorSlots?.Helmet));
                _playerView.SwapArmorModel (ResolveArmorPrefab(inventory?.BodyArmorSlot, armorSlots?.BodyArmor));

                // B1 — push barrel heat into WeaponView for emission glow telegraph.
                var equipped = session.RaidState.PlayerEntity.EquippedWeapon;
                _playerView.WeaponView?.SetHeat(equipped?.HeatLevel ?? 0f);
            }
        }

        void SpawnView(PlayerEntityState playerState, RaidSession session)
        {
            if (_shellPrefab == null) return;

            var initialRotation = playerState.FacingDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(playerState.FacingDirection, Vector3.up)
                : Quaternion.identity;

            // 1. Shell (View + Collider)
            var go = Object.Instantiate(_shellPrefab, playerState.Position, initialRotation);
            _playerView = go.GetComponent<PlayerView>();

            // 2. Body as child (CharacterBody + visual)
            if (_bodyPrefab != null)
            {
                var bodyGo = Object.Instantiate(_bodyPrefab, go.transform);
                bodyGo.transform.localPosition = Vector3.zero;
                bodyGo.transform.localRotation = Quaternion.identity;

                var body = bodyGo.GetComponent<CharacterBody>();
                if (body != null)
                {
                    _playerView.BindBody(body);
                    // Mark as local player — enables every-frame pullback (bots are throttled).
                    body.SetIsPlayerPullback(true);
                }
            }

            // Authoritative layer assignment — overrides any prefab-baked layers (CharacterBody
            // prefab is shared с bots, тому layer must be set per-instance).
            LayerUtils.SetLayerRecursively(go, LayerUtils.Player);

            _playerView.Initialize(_trackedId, _onMuzzlePointReady, BotConstants.PlayerMaxHp);

            var cam = Camera.main;
            if (cam != null)
            {
                var cameraController = cam.GetComponent<RaidCameraController>();
                if (cameraController != null)
                    cameraController.SetTarget(_playerView.transform);
            }

            var overlayGo = new GameObject("GrenadeTrajectoryOverlay");
            _trajectoryOverlay = overlayGo.AddComponent<GrenadeTrajectoryOverlay>();

            var fowGo = new GameObject("FogOfWarController");
            _fogOfWarController = fowGo.AddComponent<FogOfWarController>();
            _fogOfWarController.Initialize(_playerView.transform);

            // Armor visuals are state-driven у LateTick — first post-spawn tick
            // attaches the meshes (idempotent SwapXxxModel handles the diff).
            // No spawn-time one-shot call needed.

            Debug.Log($"[PlayerPresenter] Spawned player view for {_trackedId}");
        }

        // Maps inventory equipment slot + ArmorMap durability state → prefab ID for the
        // attached mesh. Null = "should not be visible" — каже SwapXxxModel detach.
        // Hides the mesh in three cases: slot is empty, definition has no prefab id,
        // or armor is broken (Durability ≤ 0 — fly-off path already detached the model;
        // this guard prevents a re-attach before the broken item leaves the slot).
        static string ResolveArmorPrefab(ItemState item, ArmorState armor)
        {
            if (item?.Definition == null) return null;
            if (string.IsNullOrEmpty(item.Definition.ArmorPrefabId)) return null;
            if (armor != null && armor.IsBroken) return null;
            return item.Definition.ArmorPrefabId;
        }

        static void ArmorBreakHelmetFlyOff(PlayerView view)
        {
            var helmet = view.DetachHelmetModel();
            if (helmet == null) return;
            ArmorBreakHelper.FlyOffHelmet(helmet);
        }

        // Mirrors BotPresenter.ApplyHitFlash — picks rim color per hit kind, routes
        // to PlayerView. Ricochet skips bullet decal (helmet bounced, no flesh wound).
        static void ApplyHitFlash(PlayerView view, RaidEvent e)
        {
            var cfg = ViewCheats.Config?.HitFlash;
            if (cfg == null || !cfg.Enabled) return;

            // EntityHit packs:
            //   CurrentHp = isHeadshot ? 1 : 0
            //   MaxHp     = isKill     ? 1 : 0
            //   KillerId.Value = isRicochet ? 1 : 0
            //   Position  = hitPoint (world space)
            bool isHeadshot = e.CurrentHp > 0.5f;
            bool isKill     = e.MaxHp     > 0.5f;
            bool isRicochet = e.KillerId.Value == 1;

            Color color;
            if      (isRicochet) color = cfg.RicochetColor;
            else if (isKill)     color = cfg.KillColor;
            else if (isHeadshot) color = cfg.HeadshotColor;
            else                 color = cfg.NormalColor;

            // A2 — blend toward laser tint якщо archetype = Laser.
            var impactCfg = ViewCheats.Config?.ImpactVfx;
            if (e.Archetype == State.PayloadArchetypeKey.Laser
                && impactCfg != null && impactCfg.Enabled)
                color = Color.Lerp(color, impactCfg.LaserRimFlashTint, impactCfg.LaserRimFlashBlend);

            view.TriggerHitFlash(color, cfg.Intensity, cfg.Duration);

            if (!isRicochet)
            {
                // Per-decal tint — Laser sends a scorch tint у shader's _HitDecalColors array.
                Color decalTint = default;
                if (e.Archetype == State.PayloadArchetypeKey.Laser
                    && impactCfg != null && impactCfg.Enabled)
                {
                    decalTint   = impactCfg.LaserDecalTint;
                    decalTint.a = 1f;
                }
                view.AddHitDecal(e.Position, decalTint);
            }
        }

        public void Dispose()
        {
            if (_fogOfWarController != null)
            {
                Object.Destroy(_fogOfWarController.gameObject);
                _fogOfWarController = null;
            }

            if (_trajectoryOverlay != null)
            {
                Object.Destroy(_trajectoryOverlay.gameObject);
                _trajectoryOverlay = null;
            }

            if (_playerView != null)
            {
                Object.Destroy(_playerView.gameObject);
                _playerView = null;
            }
        }
    }
}
