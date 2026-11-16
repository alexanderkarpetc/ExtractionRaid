using UnityEngine;
using View.SpawnPoints;

namespace View
{
    /// <summary>Schedules cosmetic animation triggers independently of gameplay RNG.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class RandomAnimatorTrigger : MonoBehaviour
    {
        [System.Serializable]
        private sealed class TriggerEntry
        {
            public string TriggerName = "RandomIdle";
            [Min(0.01f)] public float MinIntervalSeconds = 15f;
            [Min(0.01f)] public float MaxIntervalSeconds = 50f;
            [Tooltip("Chance per attempt. A new interval is scheduled even when the attempt fails.")]
            [Range(0f, 100f)] public float ChancePercent = 50f;

            [System.NonSerialized] public int Hash;
            [System.NonSerialized] public float RemainingSeconds;
            [System.NonSerialized] public bool IsValid;
        }

        [SerializeField] private TriggerEntry[] _triggers;

        [Header("Player Proximity")]
        [Tooltip("Reuse this building's interaction box and its resolved player proximity. Resize the zone on the referenced WorkbenchSpawnPoint.")]
        [SerializeField] private WorkbenchSpawnPoint _interactionZone;
        [Tooltip("Trigger sent once when the player enters the interaction zone. Random triggers stay paused until the player leaves.")]
        [SerializeField] private string _playerNearTrigger = "Idle";

        // Preserve serialized settings from the original single-trigger component.
        [SerializeField, HideInInspector] private string _triggerName = "RandomIdle";
        [SerializeField, HideInInspector] private float _minIntervalSeconds = 15f;
        [SerializeField, HideInInspector] private float _maxIntervalSeconds = 50f;
        [SerializeField, HideInInspector] private float _chancePercent = 50f;
        [SerializeField, HideInInspector] private bool _listInitialized;

        private Animator _animator;
        private System.Random _random;
        private int _playerNearTriggerHash;
        private bool _hasPlayerNearTrigger;
        private bool _wasPlayerNear;

        private void OnEnable()
        {
            _animator = GetComponent<Animator>();
            _random = _random ?? new System.Random();
            NormalizeSettings();
            _wasPlayerNear = false;
            _playerNearTriggerHash = Animator.StringToHash(_playerNearTrigger ?? string.Empty);
            _hasPlayerNearTrigger = HasTrigger(_playerNearTrigger, _playerNearTriggerHash);
            if (_interactionZone != null && !_hasPlayerNearTrigger)
                Debug.LogWarning($"{nameof(RandomAnimatorTrigger)}: Animator needs a Trigger parameter named '{_playerNearTrigger}' for player proximity.", this);

            foreach (TriggerEntry entry in _triggers)
            {
                if (entry == null)
                    continue;

                entry.Hash = Animator.StringToHash(entry.TriggerName ?? string.Empty);
                entry.IsValid = HasTrigger(entry.TriggerName, entry.Hash);
                if (!entry.IsValid)
                {
                    Debug.LogWarning($"{nameof(RandomAnimatorTrigger)}: Animator needs a Trigger parameter named '{entry.TriggerName}'. Skipping this entry.", this);
                    continue;
                }

                ScheduleNextAttempt(entry);
            }
        }

        private void Update()
        {
            // Pause with game time and while the Animator is disabled.
            if (!_animator.isActiveAndEnabled || Time.deltaTime <= 0f)
                return;

            // Consume eligibility resolved by the session, just like the building outline.
            bool isPlayerNear = _interactionZone != null
                && _interactionZone.RuntimeState != null
                && _interactionZone.RuntimeState.IsPlayerInInteractionZone;
            if (isPlayerNear != _wasPlayerNear)
            {
                _wasPlayerNear = isPlayerNear;
                foreach (TriggerEntry entry in _triggers)
                {
                    if (entry == null || !entry.IsValid)
                        continue;

                    if (isPlayerNear)
                        _animator.ResetTrigger(entry.Hash);
                    else
                        ScheduleNextAttempt(entry);
                }

                if (_hasPlayerNearTrigger)
                {
                    if (isPlayerNear)
                        _animator.SetTrigger(_playerNearTriggerHash);
                    else
                        _animator.ResetTrigger(_playerNearTriggerHash);
                }
            }

            if (isPlayerNear)
                return;

            foreach (TriggerEntry entry in _triggers)
            {
                if (entry == null || !entry.IsValid)
                    continue;

                entry.RemainingSeconds -= Time.deltaTime;
                if (entry.RemainingSeconds > 0f)
                    continue;

                if (_random.NextDouble() * 100d < entry.ChancePercent)
                    _animator.SetTrigger(entry.Hash);

                ScheduleNextAttempt(entry);
            }
        }

        private bool HasTrigger(string triggerName, int triggerHash)
        {
            if (_animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(triggerName))
                return false;

            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.nameHash == triggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                    return true;
            }

            return false;
        }

        private void ScheduleNextAttempt(TriggerEntry entry)
        {
            entry.RemainingSeconds = Mathf.Lerp(entry.MinIntervalSeconds, entry.MaxIntervalSeconds, (float)_random.NextDouble());
        }

        private void OnValidate()
        {
            NormalizeSettings();
        }

        private void NormalizeSettings()
        {
            if (!_listInitialized)
            {
                if (_triggers == null || _triggers.Length == 0)
                {
                    _triggers = new[]
                    {
                        new TriggerEntry
                        {
                            TriggerName = _triggerName,
                            MinIntervalSeconds = _minIntervalSeconds,
                            MaxIntervalSeconds = _maxIntervalSeconds,
                            ChancePercent = _chancePercent
                        }
                    };
                }
                _listInitialized = true;
            }

            _triggers = _triggers ?? System.Array.Empty<TriggerEntry>();
            foreach (TriggerEntry entry in _triggers)
            {
                if (entry == null)
                    continue;

                entry.MinIntervalSeconds = Mathf.Max(0.01f, entry.MinIntervalSeconds);
                entry.MaxIntervalSeconds = Mathf.Max(entry.MinIntervalSeconds, entry.MaxIntervalSeconds);
                entry.ChancePercent = Mathf.Clamp(entry.ChancePercent, 0f, 100f);
            }
        }
    }
}
