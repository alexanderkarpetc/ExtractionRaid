using Session;
using State;
using UnityEngine;

namespace View
{
    public class PlayerPresenter
    {
        readonly GameObject _playerPrefab;

        PlayerView _playerView;
        EId _trackedId;

        public PlayerPresenter()
        {
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

            var go = Object.Instantiate(_playerPrefab, playerState.Position, Quaternion.identity);
            _playerView = go.GetComponent<PlayerView>();
            _playerView.Initialize(_trackedId);

            var cam = Camera.main;
            if (cam != null)
            {
                var cameraController = cam.GetComponent<RaidCameraController>();
                if (cameraController != null)
                    cameraController.SetTarget(_playerView.transform);
            }

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
