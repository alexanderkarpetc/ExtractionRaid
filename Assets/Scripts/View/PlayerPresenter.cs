using System;
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

            if (events.HasPlayerSpawned && _playerView == null)
            {
                _trackedId = events.SpawnedPlayerId;
                SpawnView(session.RaidState.PlayerEntity);
            }

            if (_playerView != null && session.RaidState.PlayerEntity != null)
            {
                _playerView.SyncFromState(session.RaidState.PlayerEntity);
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
            _playerView.Initialize(_trackedId);

            var cam = Camera.main;
            if (cam != null)
            {
                var cameraController = cam.GetComponent<RaidCameraController>();
                if (cameraController != null)
                    cameraController.SetTarget(_playerView.transform);
            }

            if (_playerView.MuzzlePoint != null)
                _onMuzzlePointReady?.Invoke(_playerView.MuzzlePoint);

            Debug.Log($"[PlayerPresenter] Spawned player view for {_trackedId}");
        }

        public void Dispose()
        {
            if (_playerView != null)
            {
                Object.Destroy(_playerView.gameObject);
                _playerView = null;
            }
        }
    }
}
