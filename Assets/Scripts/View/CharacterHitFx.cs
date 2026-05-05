using Dev;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Drives the VibeCharacterShader hit feedback (rim flash + bullet decals) на
    /// character renderers. Lives on the character body root so it survives ragdoll
    /// detach (when BotView is destroyed but body GO continues into ragdoll).
    ///
    /// Decals track per-bone — at hit time we find the closest skeleton bone and
    /// store the impact in that bone's local space. Each frame we re-transform via
    /// that bone's current world transform, so decals follow:
    ///   * live bot animation (Animator drives bones)
    ///   * ragdoll physics (bones become rigidbodies and move via Unity physics)
    /// </summary>
    public class CharacterHitFx : MonoBehaviour
    {
        const int HitDecalCapacity = 8;

        static readonly int HitFlashColorId     = Shader.PropertyToID("_HitFlashColor");
        static readonly int HitFlashIntensityId = Shader.PropertyToID("_HitFlashIntensity");
        static readonly int HitFlashRimPowerId  = Shader.PropertyToID("_HitFlashRimPower");
        static readonly int HitFlashRimWidthId  = Shader.PropertyToID("_HitFlashRimWidth");
        static readonly int HitDecalsId         = Shader.PropertyToID("_HitDecals");
        static readonly int HitDecalCountId     = Shader.PropertyToID("_HitDecalCount");
        static readonly int HitDecalColorId     = Shader.PropertyToID("_HitDecalColor");
        static readonly int HitDecalRadiusId    = Shader.PropertyToID("_HitDecalRadius");
        static readonly int HitDecalSoftnessId  = Shader.PropertyToID("_HitDecalSoftness");

        Renderer[]            _renderers;
        MaterialPropertyBlock _mpb;
        Transform[]           _bones;

        // Rim flash state.
        Color _flashColor;
        float _flashPeakIntensity;
        float _flashDuration;
        float _flashElapsed;
        bool  _flashActive;

        // Decal ring buffer.
        //   _localPositions[i] = decal's position у local space of bone _decalBones[i]
        //   _intensity[i]      = current decal strength (0..1, fades over time)
        //   _worldDecals[i]    = per-frame computed world position (sent to shader)
        Vector3[]   _localPositions = new Vector3[HitDecalCapacity];
        Transform[] _decalBones     = new Transform[HitDecalCapacity];
        float[]     _intensity      = new float[HitDecalCapacity];
        Vector4[]   _worldDecals    = new Vector4[HitDecalCapacity];
        int         _nextSlot;
        int         _activeCount;
        bool        _dirty;

        void Awake()
        {
            CacheRenderersAndBones();
        }

        void CacheRenderersAndBones()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            _renderers = GetComponentsInChildren<Renderer>(true);

            // Skinned mesh renderer's `bones` array is the authoritative skeleton.
            // Multiple SMRs (e.g., body + helmet) may share the same skeleton —
            // collecting from the first non-null bones list is sufficient.
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
            {
                if (smrs[i].bones != null && smrs[i].bones.Length > 0)
                {
                    _bones = smrs[i].bones;
                    break;
                }
            }
            if (_bones == null) _bones = new Transform[0];
        }

        public void TriggerRimFlash(Color color, float intensity, float durationUnscaled)
        {
            if (durationUnscaled <= 0f || intensity <= 0f) return;
            _flashColor         = color;
            _flashPeakIntensity = Mathf.Max(0.01f, intensity);
            _flashDuration      = durationUnscaled;
            _flashElapsed       = 0f;
            _flashActive        = true;
        }

        public void AddHitDecal(Vector3 worldPos)
        {
            if (_bones == null || _bones.Length == 0) return;

            // Apply Y offset + jitter before finding closest bone — moves the
            // decal anchor down to belly area (rifle hits land at face level),
            // and adds spread so consecutive hits don't stack on top of each other.
            var cfg = ViewCheats.Config?.HitFlash;
            if (cfg != null)
            {
                worldPos.y += cfg.DecalYOffset
                            + Random.Range(-cfg.DecalYJitter, cfg.DecalYJitter);
            }

            // Find closest bone to the (offset) impact point. Walking ~30 transforms
            // is cheap; happens once per hit, not per frame.
            Transform closest = null;
            float closestSqr = float.MaxValue;
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                float d = (_bones[i].position - worldPos).sqrMagnitude;
                if (d < closestSqr) { closestSqr = d; closest = _bones[i]; }
            }
            if (closest == null) return;

            int slot = _nextSlot;
            _decalBones[slot]     = closest;
            _localPositions[slot] = closest.InverseTransformPoint(worldPos);
            _intensity[slot]      = 1f;

            _nextSlot = (_nextSlot + 1) % HitDecalCapacity;
            if (_activeCount < HitDecalCapacity) _activeCount++;
            _dirty = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // ── Rim flash decay (ease-out quad) ──
            float rimIntensity = 0f;
            if (_flashActive)
            {
                _flashElapsed += dt;
                float t = _flashElapsed / _flashDuration;
                if (t >= 1f) _flashActive = false;
                else rimIntensity = _flashPeakIntensity * (1f - t) * (1f - t);
            }

            // ── Decal age + re-transform per-bone ──
            bool anyDecal = false;
            if (_activeCount > 0)
            {
                float lifetime = Mathf.Max(0.5f, ViewCheats.Config?.HitFlash?.DecalLifetime ?? 8f);
                float decay = dt / lifetime;
                for (int i = 0; i < HitDecalCapacity; i++)
                {
                    if (_intensity[i] <= 0f)
                    {
                        _worldDecals[i] = Vector4.zero;
                        continue;
                    }
                    _intensity[i] = Mathf.Max(0f, _intensity[i] - decay);
                    if (_intensity[i] <= 0f)
                    {
                        _worldDecals[i] = Vector4.zero;
                        _decalBones[i]  = null;
                        _activeCount = Mathf.Max(0, _activeCount - 1);
                        continue;
                    }
                    if (_decalBones[i] == null)
                    {
                        _worldDecals[i] = Vector4.zero;
                        continue;
                    }
                    var world = _decalBones[i].TransformPoint(_localPositions[i]);
                    _worldDecals[i] = new Vector4(world.x, world.y, world.z, _intensity[i]);
                    anyDecal = true;
                }
                _dirty = true;
            }

            if (!_flashActive && !anyDecal && !_dirty) return;

            PushHitState(rimIntensity, anyDecal);
            _dirty = false;
        }

        void PushHitState(float rimIntensity, bool anyDecal)
        {
            if (_renderers == null || _mpb == null) return;
            var cfg = ViewCheats.Config?.HitFlash;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(HitFlashColorId,     _flashColor);
                _mpb.SetFloat(HitFlashIntensityId, rimIntensity);
                _mpb.SetVectorArray(HitDecalsId,   _worldDecals);
                _mpb.SetFloat(HitDecalCountId,     anyDecal ? HitDecalCapacity : 0f);
                if (cfg != null)
                {
                    _mpb.SetFloat(HitFlashRimPowerId, cfg.RimPower);
                    _mpb.SetFloat(HitFlashRimWidthId, cfg.RimWidth);
                    _mpb.SetColor(HitDecalColorId,    cfg.DecalColor);
                    _mpb.SetFloat(HitDecalRadiusId,   cfg.DecalRadius);
                    _mpb.SetFloat(HitDecalSoftnessId, cfg.DecalSoftness);
                }
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
