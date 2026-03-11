using System;
using Adapters;
using Session;
using State;
using UnityEngine;
using Object = UnityEngine.Object;

namespace View
{
    public class PlayerPresenter
    {
        readonly GameObject _playerPrefab;
        readonly Action<Transform> _onMuzzlePointReady;

        PlayerView _playerView;
        GrenadeTrajectoryOverlay _trajectoryOverlay;
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
                        _playerView?.WeaponView?.PlayMuzzleFlash();
                        break;
                    case RaidEventType.WeaponEquipStarted:
                        // Future: trigger equip animation
                        break;
                    case RaidEventType.WeaponUnequipStarted:
                        // Future: trigger unequip animation
                        break;
                    case RaidEventType.WeaponEquipFinished:
                        // Future: finalize equip animation
                        break;
                    case RaidEventType.WeaponReloadStarted:
                        // Future: trigger reload animation
                        break;
                    case RaidEventType.WeaponReloadFinished:
                        // Future: end reload animation
                        break;
                    case RaidEventType.WeaponDryFired:
                        // Future: play dry fire click sound
                        break;
                }

                if (e.Type == RaidEventType.EntityDamaged && e.Id == _trackedId && _playerView != null)
                {
                    _playerView.OnDamaged(e.CurrentHp, e.MaxHp);
                }
            }

            if (_playerView != null && session.RaidState.PlayerEntity != null)
            {
                _playerView.SyncFromState(session.RaidState.PlayerEntity);
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

            Debug.Log($"[PlayerPresenter] Spawned player view for {_trackedId}");
        }

        public void Dispose()
        {
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
