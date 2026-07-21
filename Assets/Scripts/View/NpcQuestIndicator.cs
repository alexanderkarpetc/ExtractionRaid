using ApplicationCore;
using Systems;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Floating quest badge above an NPC's head when they have an offer for the player:
    /// a new quest available (yellow), or an active quest з all tasks done — ready to
    /// turn in (green). All procedural (no sprites / prefab):
    ///   1. an SDF badge (<c>UI/NpcQuestIcon</c>) — disc + rim + glow + drawn "!";
    ///   2. a soft additive POOL of light on the ground (<c>VFX/QuestGroundGlow</c>) +
    ///      a camera-billboarded vertical glow column (<c>VFX/QuestBeam</c>). Both use
    ///      soft-particle depth fade so they dissolve into geometry instead of clipping
    ///      through it, and read as a light SOURCE (bloom-friendly), spottable across
    ///      the map.
    ///
    /// Billboard mirrors <see cref="WorldHealthBar"/>; auto-spawned by
    /// <see cref="View.SpawnPoints.NpcSpawnPoint"/>.
    /// </summary>
    public class NpcQuestIndicator : MonoBehaviour
    {
        // ── Badge layout — square so the disc stays circular.
        const float OffsetY = 2.7f;
        const float Width   = 0.9f;
        const float Height  = 0.9f;

        // ── Vertical glow column (billboarded quad, additive, soft-particle occluded).
        const float BeamHeight    = 4.5f;
        const float BeamHalfWidth = 0.40f;
        const float BeamBaseY     = 0.10f;
        const float BeamAlphaMin  = 0.50f;   // powerful — soft particles keep it clean near geo
        const float BeamAlphaMax  = 0.95f;

        // ── Ground light pool (horizontal additive disc, soft-particle occluded).
        const float GroundRadius   = 1.6f;
        const float GroundY        = 0.06f;
        const float GroundAlphaMin = 0.50f;
        const float GroundAlphaMax = 0.90f;

        // ── Behavior
        const float PollInterval = 0.4f;
        const float PulseHz      = 0.9f;

        // ── SDF badge palette per state.
        readonly struct Palette
        {
            public readonly Color Fill, Border, Mark, MarkTop, MarkBot, MarkOutline, Glow;
            public Palette(Color fill, Color border, Color mark, Color markTop, Color markBot, Color markOutline, Color glow)
            {
                Fill = fill; Border = border; Mark = mark; MarkTop = markTop;
                MarkBot = markBot; MarkOutline = markOutline; Glow = glow;
            }
        }

        static readonly Palette Avail = new(
            fill:        new Color(0.16f, 0.12f, 0.02f, 1f),
            border:      new Color(1.00f, 0.80f, 0.15f, 1f),
            mark:        new Color(1.00f, 0.87f, 0.32f, 1f),
            markTop:     new Color(1.00f, 0.93f, 0.50f, 1f),
            markBot:     new Color(0.95f, 0.72f, 0.12f, 1f),
            markOutline: new Color(0.06f, 0.05f, 0.00f, 1f),
            glow:        new Color(1.00f, 0.78f, 0.20f, 1f));

        static readonly Palette Ready = new(
            fill:        new Color(0.03f, 0.13f, 0.05f, 1f),
            border:      new Color(0.35f, 0.95f, 0.45f, 1f),
            mark:        new Color(0.55f, 1.00f, 0.62f, 1f),
            markTop:     new Color(0.72f, 1.00f, 0.78f, 1f),
            markBot:     new Color(0.30f, 0.90f, 0.42f, 1f),
            markOutline: new Color(0.02f, 0.07f, 0.03f, 1f),
            glow:        new Color(0.35f, 0.95f, 0.45f, 1f));

        // ── Badge shader property IDs
        static readonly int PropBorder       = Shader.PropertyToID("_BorderColor");
        static readonly int PropFill         = Shader.PropertyToID("_FillColor");
        static readonly int PropMark         = Shader.PropertyToID("_MarkColor");
        static readonly int PropMarkTop      = Shader.PropertyToID("_MarkTopColor");
        static readonly int PropMarkBot      = Shader.PropertyToID("_MarkBotColor");
        static readonly int PropMarkOutline  = Shader.PropertyToID("_MarkOutlineColor");
        static readonly int PropGlow         = Shader.PropertyToID("_GlowColor");
        static readonly int PropAspect       = Shader.PropertyToID("_Aspect");
        static readonly int PropPulse        = Shader.PropertyToID("_PulseT");
        static readonly int PropAlpha        = Shader.PropertyToID("_Alpha");
        static readonly int PropBorderWidth  = Shader.PropertyToID("_BorderWidth");
        static readonly int PropGlowStrength = Shader.PropertyToID("_GlowStrength");
        static readonly int PropGlowRadius   = Shader.PropertyToID("_GlowRadius");

        // ── Beam / ground-glow shader property IDs (both use _Color + _Alpha)
        static readonly int PropVfxColor = Shader.PropertyToID("_Color");
        static readonly int PropVfxAlpha = Shader.PropertyToID("_Alpha");

        static Mesh s_beamMesh;
        static Mesh s_groundMesh;

        string _npcId;
        GameObject _root;
        Image _badge;
        Material _material;
        GameObject _beam;
        Material _beamMaterial;
        GameObject _ground;
        Material _groundMaterial;
        float _pollClock;
        bool _isVisible;

        public static NpcQuestIndicator Create(Transform parent, string npcId)
        {
            var holder = new GameObject("QuestIndicator");
            holder.transform.SetParent(parent, false);
            var ind = holder.AddComponent<NpcQuestIndicator>();
            ind._npcId = npcId;
            ind.Build();
            return ind;
        }

        void Build()
        {
            _root = new GameObject("Canvas");
            _root.transform.SetParent(transform, false);
            _root.transform.localPosition = new Vector3(0f, OffsetY, 0f);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 110;

            var rt = _root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(Width, Height);
            rt.localScale = Vector3.one;

            BuildBadge();
            BuildGround();
            BuildBeam();
            ApplyPalette(Avail);

            SetVisible(false);
        }

        void BuildBadge()
        {
            var badgeGo = new GameObject("Badge");
            badgeGo.transform.SetParent(_root.transform, false);
            var badgeRt = badgeGo.AddComponent<RectTransform>();
            badgeRt.anchorMin = Vector2.zero;
            badgeRt.anchorMax = Vector2.one;
            badgeRt.offsetMin = Vector2.zero;
            badgeRt.offsetMax = Vector2.zero;

            _badge = badgeGo.AddComponent<Image>();
            _badge.raycastTarget = false;

            var shader = Shader.Find("UI/NpcQuestIcon");
            if (shader == null)
            {
                Debug.LogError("[NpcQuestIndicator] Shader 'UI/NpcQuestIcon' not found.");
                return;
            }

            _material = new Material(shader);
            _material.SetFloat(PropAspect, Width / Mathf.Max(0.001f, Height));
            _material.SetFloat(PropBorderWidth, 0.06f);
            _material.SetFloat(PropGlowStrength, 0.9f);
            _material.SetFloat(PropGlowRadius, 0.30f);
            _badge.material = _material;
        }

        void BuildGround()
        {
            var shader = Shader.Find("VFX/QuestGroundGlow");
            if (shader == null)
            {
                Debug.LogWarning("[NpcQuestIndicator] Shader 'VFX/QuestGroundGlow' not found; ground glow disabled.");
                return;
            }

            _ground = new GameObject("GroundGlow");
            _ground.transform.SetParent(transform, false);
            _ground.transform.localPosition = new Vector3(0f, GroundY, 0f);

            var mf = _ground.AddComponent<MeshFilter>();
            mf.sharedMesh = s_groundMesh != null ? s_groundMesh : (s_groundMesh = BuildGroundMesh());

            var mr = _ground.AddComponent<MeshRenderer>();
            DisableRendererExtras(mr);

            _groundMaterial = new Material(shader);
            mr.sharedMaterial = _groundMaterial;
        }

        void BuildBeam()
        {
            var shader = Shader.Find("VFX/QuestBeam");
            if (shader == null)
            {
                Debug.LogWarning("[NpcQuestIndicator] Shader 'VFX/QuestBeam' not found; beam disabled.");
                return;
            }

            _beam = new GameObject("Beam");
            _beam.transform.SetParent(transform, false);
            _beam.transform.localPosition = Vector3.zero;

            var mf = _beam.AddComponent<MeshFilter>();
            mf.sharedMesh = s_beamMesh != null ? s_beamMesh : (s_beamMesh = BuildBeamMesh());

            var mr = _beam.AddComponent<MeshRenderer>();
            DisableRendererExtras(mr);

            _beamMaterial = new Material(shader);
            mr.sharedMaterial = _beamMaterial;
        }

        static void DisableRendererExtras(MeshRenderer mr)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        // Single vertical quad in local XY at Z=0 (billboarded toward camera each frame).
        static Mesh BuildBeamMesh()
        {
            float top = BeamBaseY + BeamHeight;
            var verts = new[]
            {
                new Vector3(-BeamHalfWidth, BeamBaseY, 0f),
                new Vector3( BeamHalfWidth, BeamBaseY, 0f),
                new Vector3( BeamHalfWidth, top,       0f),
                new Vector3(-BeamHalfWidth, top,       0f),
            };
            var uvs  = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            var tris = new[] { 0, 2, 1, 0, 3, 2 };
            var mesh = new Mesh { name = "QuestBeam" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // Horizontal quad on the XZ plane (normal +Y), UV 0..1 for the radial shader.
        static Mesh BuildGroundMesh()
        {
            float r = GroundRadius;
            var verts = new[]
            {
                new Vector3(-r, 0f, -r),
                new Vector3( r, 0f, -r),
                new Vector3( r, 0f,  r),
                new Vector3(-r, 0f,  r),
            };
            var uvs  = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            var tris = new[] { 0, 1, 2, 0, 2, 3 };
            var mesh = new Mesh { name = "QuestGroundGlow" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        void ApplyPalette(in Palette p)
        {
            if (_material != null)
            {
                _material.SetColor(PropFill,        p.Fill);
                _material.SetColor(PropBorder,      p.Border);
                _material.SetColor(PropMark,        p.Mark);
                _material.SetColor(PropMarkTop,     p.MarkTop);
                _material.SetColor(PropMarkBot,     p.MarkBot);
                _material.SetColor(PropMarkOutline, p.MarkOutline);
                _material.SetColor(PropGlow,        p.Glow);
            }
            if (_beamMaterial != null)   _beamMaterial.SetColor(PropVfxColor, p.Glow);
            if (_groundMaterial != null) _groundMaterial.SetColor(PropVfxColor, p.Glow);
        }

        void LateUpdate()
        {
            _pollClock += Time.deltaTime;
            if (_pollClock >= PollInterval)
            {
                _pollClock = 0f;
                Refresh();
            }

            if (!_isVisible) return;

            var cam = Camera.main;
            if (cam != null)
            {
                _root.transform.rotation = cam.transform.rotation;
                if (_beam != null)
                {
                    Vector3 toCam = cam.transform.position - _beam.transform.position;
                    float yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
                    _beam.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }

            float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseHz * Mathf.PI * 2f);
            if (_material != null)
            {
                _material.SetFloat(PropPulse, Mathf.Lerp(0.5f, 1f, breath));
                _material.SetFloat(PropAlpha, 1f);
            }
            if (_beamMaterial != null)
                _beamMaterial.SetFloat(PropVfxAlpha, Mathf.Lerp(BeamAlphaMin, BeamAlphaMax, breath));
            if (_groundMaterial != null)
                _groundMaterial.SetFloat(PropVfxAlpha, Mathf.Lerp(GroundAlphaMin, GroundAlphaMax, breath));
        }

        void Refresh()
        {
            if (string.IsNullOrEmpty(_npcId)) { SetVisible(false); return; }
            if (!App.IsInitialized) { SetVisible(false); return; }

            var app = App.Instance;
            var db = app.QuestDatabase;
            var player = app.Player;
            var progress = player?.QuestProgress;
            if (db == null || progress == null) { SetVisible(false); return; }

            int level = player.ProfileState?.Level ?? 1;

            bool hasAvailable = QuestSystem.GetAvailableQuests(progress, db, level, _npcId).Count > 0;

            bool hasReady = false;
            if (!hasAvailable)
            {
                var active = QuestSystem.GetActiveQuestsForNpc(progress, db, _npcId);
                for (int i = 0; i < active.Count; i++)
                {
                    var qp = progress.GetProgress(active[i].Id);
                    if (qp != null && QuestSystem.AreAllTasksDone(active[i], qp))
                    {
                        hasReady = true;
                        break;
                    }
                }
            }

            bool visible = hasAvailable || hasReady;
            SetVisible(visible);
            if (visible) ApplyPalette(hasAvailable ? Avail : Ready);
        }

        void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;
            _isVisible = visible;
            if (_root != null)   _root.SetActive(visible);
            if (_beam != null)   _beam.SetActive(visible);
            if (_ground != null) _ground.SetActive(visible);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_beamMaterial != null) Destroy(_beamMaterial);
            if (_groundMaterial != null) Destroy(_groundMaterial);
        }
    }
}
