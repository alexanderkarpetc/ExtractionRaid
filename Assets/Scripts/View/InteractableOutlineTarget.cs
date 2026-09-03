using ApplicationCore;
using Systems;
using UnityEngine;

namespace View
{
    [DisallowMultipleComponent]
    public class InteractableOutlineTarget : MonoBehaviour
    {
        [SerializeField] bool _highlighted = true;
        [SerializeField] bool _includeInactiveChildren;
        [SerializeField] bool _hideWhilePlayerInMenu = true;

        [Header("Proximity")]
        [SerializeField]
        [Tooltip("Use a custom outline radius instead of the shared loot interaction radius.")]
        bool _overrideActivationRadius;
        [SerializeField, Min(0f)]
        [Tooltip("Used only when Override Activation Radius is enabled.")]
        float _activationRadius = 3f;

        [Header("Fade")]
        [SerializeField, Min(0f)] float _fadeSeconds = 0.18f;
        [SerializeField, Range(0f, 1f)] float _opacityFrom = 0f;
        [SerializeField, Range(0f, 1f)] float _opacityTo = 1f;
        [SerializeField] AnimationCurve _opacityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Material Tweeners")]
        [Tooltip("Simple case: these tweeners play forward on enter and reverse on exit.")]
        [SerializeField] MaterialPropertyTweener[] _proximityTweeners;
        [Tooltip("Custom enter effects: these tweeners restart and play forward when the player enters radius.")]
        [SerializeField] MaterialPropertyTweener[] _activationTweeners;
        [Tooltip("Custom exit effects: these tweeners restart and play forward when the player exits radius.")]
        [SerializeField] MaterialPropertyTweener[] _deactivationTweeners;

        Renderer[] _renderers;
        float _fade01;
        float _currentOpacity;
        bool _hasTweenState;
        bool _lastTweenActive;

        // ApplyOpacity does a GetPropertyBlock/SetPropertyBlock round trip per renderer.
        // With ~135 of these components in the scene that ran every frame even though most
        // sit idle at opacity 0, well outside the activation radius. Track what was last
        // pushed so a steady state costs nothing but the distance check.
        bool _hasAppliedOpacity;
        float _lastAppliedOpacity;

        static readonly int OutlineMaskOpacityId = Shader.PropertyToID("_OutlineMaskOpacity");
        static MaterialPropertyBlock _propertyBlock;

        public bool Highlighted
        {
            get => _highlighted;
            set
            {
                if (_highlighted == value) return;
                _highlighted = value;
            }
        }

        public float ActivationRadius
        {
            get => _activationRadius;
            set => _activationRadius = Mathf.Max(0f, value);
        }

        public bool OverrideActivationRadius
        {
            get => _overrideActivationRadius;
            set => _overrideActivationRadius = value;
        }

        public float EffectiveActivationRadius =>
            _overrideActivationRadius ? _activationRadius : LootSystem.LootRange;

        public float CurrentOpacity => _currentOpacity;

        void OnEnable()
        {
            CacheRenderers();
            _hasAppliedOpacity = false;
            UpdateFade(0f);
        }

        void OnDisable()
        {
            SetTweenersActive(false, true);
            Restore();
            _hasAppliedOpacity = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            UnregisterAll();
            CacheRenderers();
            _hasAppliedOpacity = false;
            _activationRadius = Mathf.Max(0f, _activationRadius);
            _fadeSeconds = Mathf.Max(0f, _fadeSeconds);
            _opacityFrom = Mathf.Clamp01(_opacityFrom);
            _opacityTo = Mathf.Clamp01(_opacityTo);
            UpdateFade(0f);
        }
#endif

        void Update()
        {
            UpdateFade(Time.deltaTime);
        }

        public void RefreshRenderers()
        {
            UnregisterAll();
            CacheRenderers();
            _hasAppliedOpacity = false;
            UpdateFade(0f);
        }

        void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(_includeInactiveChildren);
        }

        void UpdateFade(float deltaTime)
        {
            bool active = ShouldBeVisible();
            SetTweenersActive(active);
            float target = active ? 1f : 0f;

            if (_fadeSeconds <= 0f)
                _fade01 = target;
            else
                _fade01 = Mathf.MoveTowards(_fade01, target, deltaTime / _fadeSeconds);

            float curved = _opacityCurve != null ? _opacityCurve.Evaluate(_fade01) : _fade01;
            _currentOpacity = Mathf.Lerp(_opacityFrom, _opacityTo, Mathf.Clamp01(curved));

            float applied = active || _fade01 > 0f ? _currentOpacity : 0f;
            // Registry membership is decided by the same opacity, so an unchanged value
            // means the registry is already correct too — nothing to redo.
            if (_hasAppliedOpacity && _lastAppliedOpacity.Equals(applied)) return;
            _hasAppliedOpacity = true;
            _lastAppliedOpacity = applied;

            ApplyOpacity(applied);
        }

        void SetTweenersActive(bool active, bool force = false)
        {
            if (!force && _hasTweenState && _lastTweenActive == active) return;

            _hasTweenState = true;
            _lastTweenActive = active;

            for (int i = 0; _proximityTweeners != null && i < _proximityTweeners.Length; i++)
            {
                if (_proximityTweeners[i] != null)
                    _proximityTweeners[i].SetActive(active);
            }

            if (active)
            {
                StopTweeners(_deactivationTweeners);
                RestartForward(_activationTweeners);
            }
            else
            {
                StopTweeners(_activationTweeners);
                RestartForward(_deactivationTweeners);
            }
        }

        static void RestartForward(MaterialPropertyTweener[] tweeners)
        {
            if (tweeners == null) return;

            for (int i = 0; i < tweeners.Length; i++)
            {
                if (tweeners[i] != null)
                    tweeners[i].RestartForward();
            }
        }

        static void StopTweeners(MaterialPropertyTweener[] tweeners)
        {
            if (tweeners == null) return;

            for (int i = 0; i < tweeners.Length; i++)
            {
                if (tweeners[i] != null)
                    tweeners[i].Stop();
            }
        }

        bool ShouldBeVisible()
        {
            if (!_highlighted) return false;
            if (!App.IsInitialized) return false;

            var player = App.Instance.RaidSession?.RaidState?.PlayerEntity;
            if (player == null) return false;
            if (_hideWhilePlayerInMenu && player.IsInMenu) return false;

            float sqrDist = (player.Position - transform.position).sqrMagnitude;
            float activationRadius = EffectiveActivationRadius;
            return sqrDist <= activationRadius * activationRadius;
        }

        void ApplyOpacity(float opacity)
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || (r is MeshRenderer && r.GetComponent<TextMesh>() != null)) continue;

                if (opacity > 0.001f)
                {
                    SetRendererOpacity(r, opacity);
                    InteractableOutlineRegistry.Register(r, opacity);
                }
                else
                {
                    SetRendererOpacity(r, 0f);
                    InteractableOutlineRegistry.Unregister(r);
                }
            }
        }

        static void SetRendererOpacity(Renderer renderer, float opacity)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(OutlineMaskOpacityId, Mathf.Clamp01(opacity));
            renderer.SetPropertyBlock(_propertyBlock);
        }

        void Restore()
        {
            UnregisterAll();
        }

        void UnregisterAll()
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    SetRendererOpacity(_renderers[i], 0f);
                InteractableOutlineRegistry.Unregister(_renderers[i]);
            }
        }
    }
}
