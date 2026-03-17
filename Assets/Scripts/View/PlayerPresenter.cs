using System;
using Adapters;
using Session;
using State;
using UnityEngine;
using View.FogOfWar;
using Object = UnityEngine.Object;

namespace View
{
    public class PlayerPresenter
    {
        readonly GameObject _playerPrefab;
        readonly Action<Transform> _onMuzzlePointReady;

        PlayerView _playerView;
        GrenadeTrajectoryOverlay _trajectoryOverlay;
        FogOfWarController _fogOfWarController;
        EId _trackedId;

        public PlayerPresenter(Action<Transform> onMuzzlePointReady)
        {
            _onMuzzlePointReady = onMuzzlePointReady;
            _playerPrefab = Resources.Load<GameObject>("Prefabs/PlayerCapsule");

            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerPresenter] Failed to load prefab at Resources/Prefabs/PlayerCapsule.");
            }
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
                        SpawnView(session.RaidState.PlayerEntity);
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
            }
        }

        void SpawnView(PlayerEntityState playerState)
        {
            if (_playerPrefab == null) return;

            var initialRotation = playerState.FacingDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(playerState.FacingDirection, Vector3.up)
                : Quaternion.identity;

            var go = Object.Instantiate(_playerPrefab, playerState.Position, initialRotation);
            _playerView = go.GetComponent<PlayerView>();
            _playerView.Initialize(_trackedId, _onMuzzlePointReady);

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

            Debug.Log($"[PlayerPresenter] Spawned player view for {_trackedId}");
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
