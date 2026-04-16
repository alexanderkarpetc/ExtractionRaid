using System;
using Adapters;
using ApplicationCore;
using Constants;
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
                            _playerView?.WeaponView?.PlayFire(weapon.FireInterval);
                        break;
                    }
                    case RaidEventType.WeaponEquipStarted:
                    {
                        var weapon = session.RaidState.PlayerEntity?.EquippedWeapon;
                        if (weapon != null)
                            _playerView?.WeaponView?.PlayEquip(weapon.EquipTime);
                        break;
                    }
                    case RaidEventType.WeaponUnequipStarted:
                    {
                        // Cache unequip duration — weapon may become null during unequip
                        var weapon = session.RaidState.PlayerEntity?.EquippedWeapon;
                        if (weapon != null)
                            _playerView?.WeaponView?.PlayUnequip(weapon.UnequipTime);
                        break;
                    }
                    case RaidEventType.WeaponEquipFinished:
                        break;
                    case RaidEventType.WeaponReloadStarted:
                    {
                        var weapon = session.RaidState.PlayerEntity?.EquippedWeapon;
                        if (weapon != null)
                            _playerView?.WeaponView?.PlayReload(weapon.ReloadTime);
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

                // Sync armor bar on player healthbar
                if (session.RaidState.ArmorMap.TryGetValue(_trackedId, out var armorSlots))
                {
                    float helmetDur = armorSlots.Helmet?.DurabilityPercent ?? 0f;
                    float vestDur = armorSlots.BodyArmor?.DurabilityPercent ?? 0f;
                    _playerView.UpdateArmor(helmetDur, vestDur);
                }
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

            // Equip armor visuals from inventory
            EquipArmorVisuals(session);

            Debug.Log($"[PlayerPresenter] Spawned player view for {_trackedId}");
        }

        void EquipArmorVisuals(RaidSession session)
        {
            if (_playerView == null || session == null) return;
            var inventory = App.Instance?.Player?.Inventory;

            var helmetDef = inventory.HelmetSlot?.Definition;
            if (helmetDef != null && !string.IsNullOrEmpty(helmetDef.ArmorPrefabId))
                _playerView.SwapHelmetModel(helmetDef.ArmorPrefabId);

            var armorDef = inventory.BodyArmorSlot?.Definition;
            if (armorDef != null && !string.IsNullOrEmpty(armorDef.ArmorPrefabId))
                _playerView.SwapArmorModel(armorDef.ArmorPrefabId);
        }

        static void ArmorBreakHelmetFlyOff(PlayerView view)
        {
            var helmet = view.DetachHelmetModel();
            if (helmet == null) return;
            ArmorBreakHelper.FlyOffHelmet(helmet);
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
