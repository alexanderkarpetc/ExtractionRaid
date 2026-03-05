using State;
using UnityEngine;

namespace View
{
    public class PlayerPresenter : MonoBehaviour
    {
        [SerializeField] GameObject _playerPrefab;

        PlayerView _playerView;
        EId _trackedId;

        void LateUpdate()
        {
            var session = App.App.Instance.RaidSession;
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
            var go = Instantiate(_playerPrefab, playerState.Position, Quaternion.identity);
            _playerView = go.GetComponent<PlayerView>();
            _playerView.Initialize(_trackedId);
            Debug.Log($"[PlayerPresenter] Spawned player view for {_trackedId}");
        }

        void OnDestroy()
        {
            if (_playerView != null)
            {
                Destroy(_playerView.gameObject);
            }
        }
    }
}
