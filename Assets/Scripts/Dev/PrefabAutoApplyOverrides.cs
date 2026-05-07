using UnityEngine;

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

namespace Dev
{
    /// <summary>
    /// In edit mode, periodically applies overrides from this scene prefab instance
    /// back to its source prefab asset so level-design changes are harder to lose.
    /// Put it on the outermost root of a prefab instance in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    [AddComponentMenu("Dev/Prefab Auto Apply Overrides")]
    public sealed class PrefabAutoApplyOverrides : MonoBehaviour
    {
        [SerializeField] bool _autoApplyInEditMode = true;
        [SerializeField, Min(1f)] float _applyIntervalSeconds = 30f;
        [SerializeField] bool _logEachApply;

#if UNITY_EDITOR
        [System.NonSerialized] double _nextApplyAt;

        public bool AutoApplyInEditMode => _autoApplyInEditMode;
        public float ApplyIntervalSeconds => _applyIntervalSeconds;
        public bool LogEachApply => _logEachApply;

        void OnEnable()
        {
            ResetTimer();
            PrefabAutoApplyOverridesScheduler.RequestRefresh();
        }

        void OnDisable()
        {
            PrefabAutoApplyOverridesScheduler.RequestRefresh();
        }

        void OnValidate()
        {
            _applyIntervalSeconds = Mathf.Max(1f, _applyIntervalSeconds);
            ResetTimer();
            PrefabAutoApplyOverridesScheduler.RequestRefresh();
        }

        public void ResetTimer()
        {
            _nextApplyAt = EditorApplication.timeSinceStartup + _applyIntervalSeconds;
        }

        public double SecondsUntilNextApply
        {
            get
            {
                return Mathf.Max(0f, (float)(_nextApplyAt - EditorApplication.timeSinceStartup));
            }
        }

        public bool HasPendingOverrides()
        {
            if (!string.IsNullOrEmpty(GetInactiveReason()))
                return false;

            return PrefabUtility.HasPrefabInstanceAnyOverrides(gameObject, false);
        }

        public string GetInactiveReason()
        {
            if (Application.isPlaying)
                return "Auto apply works only in Edit Mode.";

            if (!enabled)
                return "Component is disabled.";

            if (!_autoApplyInEditMode)
                return "Auto apply is turned off on this component.";

            if (EditorUtility.IsPersistent(this))
                return "Component is on a prefab asset, not on a scene instance.";

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
                return "GameObject is not in a loaded scene.";

            if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
                return "GameObject is not a prefab instance.";

            if (PrefabUtility.IsPartOfImmutablePrefab(gameObject))
                return "Immutable/model prefabs are not supported by auto apply.";

            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
            if (prefabRoot != gameObject)
                return "Put this component on the outermost prefab root in the scene.";

            return null;
        }

        public bool TryApplyNow()
        {
            ResetTimer();

            var inactiveReason = GetInactiveReason();
            if (!string.IsNullOrEmpty(inactiveReason))
                return false;

            if (!PrefabUtility.HasPrefabInstanceAnyOverrides(gameObject, false))
                return false;

            PrefabUtility.ApplyPrefabInstance(gameObject, InteractionMode.AutomatedAction);

            if (_logEachApply)
            {
                var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
                Debug.Log($"[PrefabAutoApply] Applied overrides from '{name}' to '{prefabPath}'.", this);
            }

            return true;
        }

        internal bool IsDue(double now)
        {
            return now >= _nextApplyAt;
        }

        [InitializeOnLoad]
        static class PrefabAutoApplyOverridesScheduler
        {
            static readonly List<PrefabAutoApplyOverrides> Tracked = new();
            static double _nextRefreshAt;

            static PrefabAutoApplyOverridesScheduler()
            {
                EditorApplication.update += Update;
                EditorApplication.hierarchyChanged += RequestRefresh;
                RequestRefresh();
            }

            public static void RequestRefresh()
            {
                _nextRefreshAt = 0d;
            }

            static void Update()
            {
                if (Application.isPlaying)
                    return;

                var now = EditorApplication.timeSinceStartup;
                if (now >= _nextRefreshAt)
                {
                    RefreshTrackedComponents();
                    _nextRefreshAt = now + 1d;
                }

                for (int i = Tracked.Count - 1; i >= 0; i--)
                {
                    var component = Tracked[i];
                    if (component == null)
                    {
                        Tracked.RemoveAt(i);
                        continue;
                    }

                    if (!component.IsDue(now))
                        continue;

                    component.TryApplyNow();
                }
            }

            static void RefreshTrackedComponents()
            {
                Tracked.Clear();
                Tracked.AddRange(Resources.FindObjectsOfTypeAll<PrefabAutoApplyOverrides>());
            }
        }
#endif
    }
}
