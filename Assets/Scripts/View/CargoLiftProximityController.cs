using UnityEngine;

namespace View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CargoLiftProximityController : MonoBehaviour
    {
        private const float ProximityCheckInterval = 0.1f;
        private const float PlayerSearchInterval = 1f;

        [SerializeField, Min(0f)] private float _activationRadius = 4f;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.1f;
        [Tooltip("Animator state to play when the player is near, e.g. Base Layer.Open. Leave empty to keep the current animation.")]
        [SerializeField] private string _nearStateName = "Base Layer.A_CargoLiftPlatform_A_Down";
        [Tooltip("Animator state to play when the player is far, e.g. Base Layer.Close. Leave empty to keep the current animation.")]
        [SerializeField] private string _farStateName = "Base Layer.A_CargoLiftPlatform_A_UP";

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
            string targetStateName = isPlayerNear ? _nearStateName : _farStateName;
            if (!string.IsNullOrWhiteSpace(targetStateName))
                _animator.CrossFadeInFixedTime(Animator.StringToHash(targetStateName), _transitionDuration);
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
