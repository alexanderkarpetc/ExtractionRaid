using UnityEngine;

namespace View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CargoLiftProximityController : MonoBehaviour
    {
        private const float ProximityCheckInterval = 0.1f;
        private const float PlayerSearchInterval = 1f;

        private static readonly int DownState = Animator.StringToHash("Base Layer.A_CargoLiftPlatform_A_Down");
        private static readonly int UpState = Animator.StringToHash("Base Layer.A_CargoLiftPlatform_A_UP");

        [SerializeField, Min(0f)] private float _activationRadius = 4f;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.1f;

        private Animator _animator;
        private PlayerView _player;
        private bool? _isPlayerNear;
        private float _nextProximityCheckTime;
        private float _nextPlayerSearchTime;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now < _nextProximityCheckTime)
                return;

            _nextProximityCheckTime = now + ProximityCheckInterval;

            // Retry slowly while the player is not spawned; cache it once found.
            if (_player == null && now >= _nextPlayerSearchTime)
            {
                _nextPlayerSearchTime = now + PlayerSearchInterval;
                _player = FindAnyObjectByType<PlayerView>();
            }

            bool isPlayerNear = _player != null && IsWithinActivationRadius(_player.transform.position);
            if (_isPlayerNear == isPlayerNear)
                return;

            _isPlayerNear = isPlayerNear;
            int targetState = isPlayerNear ? DownState : UpState;
            _animator.CrossFadeInFixedTime(targetState, _transitionDuration);
        }

        private bool IsWithinActivationRadius(Vector3 playerPosition)
        {
            Vector3 offset = playerPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= _activationRadius * _activationRadius;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _activationRadius);
        }
#endif
    }
}
