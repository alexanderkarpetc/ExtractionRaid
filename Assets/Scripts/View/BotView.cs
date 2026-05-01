using Constants;
using Dev;
using State;
using UnityEngine;

namespace View
{
    public class BotView : MonoBehaviour, IDamageableView
    {
        CharacterBody _body; // bound at runtime via BindBody()

        WorldHealthBar _healthBar;
        BotDebugLabel _debugLabel;

        // Gunplay A.2 — Hit flash state. Tints character renderers via MaterialPropertyBlock.
        // Project's character shader (ExtractShaders/MainBase) computes emission as
        //   Emission = _ColorEmission.rgb * _BrightnessEmission
        // so we drive both. _BaseColor / _Color / _EmissionColor are written too для
        // future-compat з URP Lit / Standard / shaders що exposing them — MPB silently
        // skips properties that don't exist on the shader.
        static readonly int BaseColorId          = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId              = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId      = Shader.PropertyToID("_EmissionColor");
        static readonly int ColorEmissionId      = Shader.PropertyToID("_ColorEmission");
        static readonly int BrightnessEmissionId = Shader.PropertyToID("_BrightnessEmission");
        Renderer[]                _flashRenderers;
        Color[]                   _flashOriginalEmissionColor;       // _EmissionColor original
        Color[]                   _flashOriginalColorEmission;       // _ColorEmission original
        float[]                   _flashOriginalBrightnessEmission;  // _BrightnessEmission original
        MaterialPropertyBlock     _flashMpb;
        Color                     _flashColor;
        float                     _flashIntensity;
        float                     _flashEmissionBoost;
        float                     _flashDuration;
        float                     _flashElapsedUnscaled;
        bool                      _flashActive;

        public EId EId { get; private set; }
        public string TypeId { get; private set; }
        public CharacterBody Body => _body;

        /// <summary>Bind a CharacterBody at runtime (shell+body composition).</summary>
        public void BindBody(CharacterBody body)
        {
            _body = body;
        }

        public void Initialize(EId id, string typeId, string weaponPrefabId, float maxHp)
        {
            EId = id;
            TypeId = typeId;
            _healthBar = WorldHealthBar.Create(transform, maxHp);
            _debugLabel = BotDebugLabel.Create(transform);

            if (!string.IsNullOrEmpty(weaponPrefabId) && _body != null)
                _body.SwapWeaponModel(weaponPrefabId);
        }

        public void OnDamaged(float currentHp, float maxHp)
        {
            if (_healthBar != null)
                _healthBar.UpdateHealth(currentHp, maxHp);
        }

        public void UpdateArmor(float helmetDurPercent, float vestDurPercent)
        {
            if (_healthBar != null)
                _healthBar.UpdateArmor(helmetDurPercent, vestDurPercent);
        }

        // Gizmo data cached from state
        internal float GizmoVisionRange;
        internal float GizmoVisionAngle;
        internal bool GizmoHasTarget;
        internal Vector3 GizmoTargetPos;
        internal Vector3[] GizmoPatrolWaypoints;
        internal int GizmoPatrolIndex;

        public void SyncFromState(BotEntityState state, float currentHp, float maxHp)
        {
            // FOV visibility toggle
            bool shouldShow = state.IsVisibleToPlayer || !DevCheats.FOVEnabled || DevCheats.ForceShowAllBots;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = shouldShow;

            transform.position = state.Position;

            if (state.FacingDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(state.FacingDirection, Vector3.up);

            if (_body != null)
            {
                if (_body.WeaponPivot != null && state.AimDirection.sqrMagnitude > 0.001f)
                    _body.WeaponPivot.rotation = Quaternion.LookRotation(state.AimDirection, Vector3.up);

                float maxSpeed = BotConstants.TryGetConfig(TypeId, out var botCfg) ? botCfg.ChaseSpeed : 5f;
                _body.SyncAnimatorState(state.IsRolling, state.Velocity, maxSpeed);
                _body.SyncRollVisual(state.IsRolling, state.RollDirection, transform);
            }

            if (_debugLabel != null)
                _debugLabel.UpdateLabel(state, currentHp, maxHp);

            var bb = state.Blackboard;
            GizmoHasTarget = bb.HasTarget;
            GizmoTargetPos = bb.LastKnownTargetPos;
            GizmoPatrolWaypoints = bb.PatrolWaypoints;
            GizmoPatrolIndex = bb.PatrolWaypointIndex;
        }

        // ── Hit flash (Gunplay A.2) ──────────────────────────

        /// <summary>
        /// Briefly tints character renderers via MaterialPropertyBlock — universal
        /// "this thing took a hit" feedback. Stack rule: incoming flash overrides ongoing
        /// (latest win) — kill on top of headshot still flashes red.
        /// </summary>
        public void TriggerHitFlash(Color color, float intensity, float durationUnscaled, float emissionBoost)
        {
            if (durationUnscaled <= 0f || (intensity <= 0f && emissionBoost <= 0f)) return;

            CacheFlashRenderers();
            _flashColor = color;
            _flashIntensity = Mathf.Clamp01(intensity);
            _flashEmissionBoost = Mathf.Max(0f, emissionBoost);
            _flashDuration  = durationUnscaled;
            _flashElapsedUnscaled = 0f;
            _flashActive = true;
        }

        void CacheFlashRenderers()
        {
            if (_flashRenderers != null) return;
            _flashRenderers = GetComponentsInChildren<Renderer>(true);
            int n = _flashRenderers.Length;
            _flashOriginalEmissionColor      = new Color[n];
            _flashOriginalColorEmission      = new Color[n];
            _flashOriginalBrightnessEmission = new float[n];
            _flashMpb = new MaterialPropertyBlock();
            for (int i = 0; i < n; i++)
            {
                var r = _flashRenderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                var mat = r.sharedMaterial;
                if (mat.HasProperty(EmissionColorId))      _flashOriginalEmissionColor[i]      = mat.GetColor(EmissionColorId);
                if (mat.HasProperty(ColorEmissionId))      _flashOriginalColorEmission[i]      = mat.GetColor(ColorEmissionId);
                if (mat.HasProperty(BrightnessEmissionId)) _flashOriginalBrightnessEmission[i] = mat.GetFloat(BrightnessEmissionId);
            }
        }

        void Update()
        {
            if (!_flashActive) return;

            // Use unscaled deltaTime so hit pause (Time.timeScale slowdown) doesn't
            // freeze the flash decay — flash should still be perceptible during pause.
            _flashElapsedUnscaled += Time.unscaledDeltaTime;
            float t = _flashElapsedUnscaled / _flashDuration;
            if (t >= 1f)
            {
                ClearFlashTint();
                _flashActive = false;
                return;
            }

            // Ease-out quad — perceived faster than linear; matches procedural recoil convention.
            float eased = (1f - t) * (1f - t);
            ApplyFlashTint(_flashColor, _flashIntensity * eased, _flashEmissionBoost * eased);
        }

        void ApplyFlashTint(Color color, float tintStrength, float emissionStrength)
        {
            if (_flashRenderers == null) return;
            // Tint: blends white → color (URP Lit / Standard / built-in via _BaseColor + _Color).
            // Emission #1: _EmissionColor as HDR — used by URP Lit / Standard.
            // Emission #2: _ColorEmission * _BrightnessEmission — used by custom
            //   ExtractShaders/MainBase. Drive BOTH so flash visible across shader variants.
            var tint = Color.Lerp(Color.white, color, tintStrength);
            for (int i = 0; i < _flashRenderers.Length; i++)
            {
                var r = _flashRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_flashMpb);
                _flashMpb.SetColor(BaseColorId, tint);
                _flashMpb.SetColor(ColorId,     tint);
                _flashMpb.SetColor(EmissionColorId,
                    _flashOriginalEmissionColor[i] + color * emissionStrength);
                _flashMpb.SetColor(ColorEmissionId, color);                  // tint
                _flashMpb.SetFloat(BrightnessEmissionId, emissionStrength);  // HDR multiplier
                r.SetPropertyBlock(_flashMpb);
            }
        }

        void ClearFlashTint()
        {
            if (_flashRenderers == null) return;
            // Restore originals — tint to white + emission props back to per-renderer baseline.
            for (int i = 0; i < _flashRenderers.Length; i++)
            {
                var r = _flashRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_flashMpb);
                _flashMpb.SetColor(BaseColorId,          Color.white);
                _flashMpb.SetColor(ColorId,              Color.white);
                _flashMpb.SetColor(EmissionColorId,      _flashOriginalEmissionColor[i]);
                _flashMpb.SetColor(ColorEmissionId,      _flashOriginalColorEmission[i]);
                _flashMpb.SetFloat(BrightnessEmissionId, _flashOriginalBrightnessEmission[i]);
                r.SetPropertyBlock(_flashMpb);
            }
        }

        // ── Armor delegation ────────────────────────────────

        public void SwapHelmetModel(string prefabId) => _body?.SwapHelmetModel(prefabId);
        public void SwapArmorModel(string prefabId) => _body?.SwapArmorModel(prefabId);
        public void ClearHelmetModel() => _body?.ClearHelmetModel();
        public void ClearArmorModel() => _body?.ClearArmorModel();
        public GameObject DetachHelmetModel() => _body?.DetachHelmetModel();

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var pos = transform.position + Vector3.up * 0.5f;
            var forward = transform.forward;

            DrawVisionCone(pos, forward);
            DrawTargetLine(pos);
            DrawPatrolPath();
        }

        void DrawVisionCone(Vector3 pos, Vector3 forward)
        {
            if (GizmoVisionRange <= 0f) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            float halfAngle = GizmoVisionAngle * 0.5f;

            var leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
            var rightDir = Quaternion.Euler(0f, halfAngle, 0f) * forward;

            Gizmos.DrawRay(pos, leftDir * GizmoVisionRange);
            Gizmos.DrawRay(pos, rightDir * GizmoVisionRange);

            int segments = 20;
            var prevPoint = pos + leftDir * GizmoVisionRange;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                var point = pos + dir * GizmoVisionRange;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }

        void DrawTargetLine(Vector3 pos)
        {
            if (!GizmoHasTarget) return;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, GizmoTargetPos + Vector3.up * 0.5f);
            Gizmos.DrawWireSphere(GizmoTargetPos + Vector3.up * 0.5f, 0.3f);
        }

        void DrawPatrolPath()
        {
            if (GizmoPatrolWaypoints == null || GizmoPatrolWaypoints.Length == 0) return;

            Gizmos.color = Color.green;
            for (int i = 0; i < GizmoPatrolWaypoints.Length; i++)
            {
                var wp = GizmoPatrolWaypoints[i];
                var next = GizmoPatrolWaypoints[(i + 1) % GizmoPatrolWaypoints.Length];
                Gizmos.DrawLine(wp + Vector3.up * 0.2f, next + Vector3.up * 0.2f);

                float sphereSize = (i == GizmoPatrolIndex) ? 0.5f : 0.2f;
                Gizmos.DrawWireSphere(wp + Vector3.up * 0.2f, sphereSize);
            }
        }
#endif
    }
}
