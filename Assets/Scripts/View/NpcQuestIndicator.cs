using ApplicationCore;
using Dev;
using Systems;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Floating quest badge above an NPC's head when they have an offer for the player:
    /// a new quest available (yellow), or an active quest ready to claim / hand over
    /// in full (green). All procedural (no sprites / prefab):
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

        // Fallback defaults — used only when the ViewCheats QuestMarker section asset is
        // absent (e.g. a build without it). Normally ViewCheats.Config.QuestMarker drives
        // these live (tune in Raid → Dev Cheats → ❗ Quest Marker).
        const float BeamHeight    = 13.5f;
        const float BeamHalfWidth = 1.2f;
        const float BeamBaseY     = 0.10f;
        const float BeamAlphaMin  = 0.50f;
        const float BeamAlphaMax  = 0.95f;

        // ── Ground light pool (horizontal additive disc, soft-particle occluded).
        const float GroundRadius   = 4.8f;
        const float GroundY        = 0.06f;
        const float GroundAlphaMin = 0.50f;
        const float GroundAlphaMax = 0.90f;
        const float GroundSoftFade = 0.5f;

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
        static readonly int PropSoftFade = Shader.PropertyToID("_SoftFade");

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
        // SetVisible early-outs when the state already matches, so the initial hide in
        // Build() would never reach the just-created (active by default) child objects —
        // an indicator rebuilt with no offer pending (hideout reload after a raid) stayed
        // lit forever. Track the first application so it always lands.
        bool _visibilityApplied;

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
            ApplyLayout(ViewCheats.Config?.QuestMarker);

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

        // Unit vertical quad: x∈[-0.5,0.5] (width 1), y∈[0,1] (height 1), Z=0. World size
        // comes from _beam.transform.localScale (ApplyLayout); billboarded on yaw each frame.
        static Mesh BuildBeamMesh()
        {
            var verts = new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3( 0.5f, 0f, 0f),
                new Vector3( 0.5f, 1f, 0f),
                new Vector3(-0.5f, 1f, 0f),
            };
            var uvs  = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            var tris = new[] { 0, 2, 1, 0, 3, 2 };
            var mesh = new Mesh { name = "QuestBeam" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // Unit horizontal quad on XZ (normal +Y), x,z∈[-0.5,0.5], UV 0..1 for the radial
        // shader. World radius comes from _ground.transform.localScale (ApplyLayout).
        static Mesh BuildGroundMesh()
        {
            var verts = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f, -0.5f),
                new Vector3( 0.5f, 0f,  0.5f),
                new Vector3(-0.5f, 0f,  0.5f),
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

            var c = ViewCheats.Config?.QuestMarker;
            ApplyLayout(c);

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

            float pulseHz = c != null ? c.PulseHz : PulseHz;
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);
            if (_material != null)
            {
                _material.SetFloat(PropPulse, Mathf.Lerp(0.5f, 1f, breath));
                _material.SetFloat(PropAlpha, 1f);
            }
            if (_beamMaterial != null)
                _beamMaterial.SetFloat(PropVfxAlpha, Mathf.Lerp(
                    c != null ? c.BeamAlphaMin : BeamAlphaMin,
                    c != null ? c.BeamAlphaMax : BeamAlphaMax, breath));
            if (_groundMaterial != null)
                _groundMaterial.SetFloat(PropVfxAlpha, Mathf.Lerp(
                    c != null ? c.GroundAlphaMin : GroundAlphaMin,
                    c != null ? c.GroundAlphaMax : GroundAlphaMax, breath));
        }

        // Live size / position / soft-fade from the ViewCheats section (consts as fallback
        // when the asset is absent). Applied every frame while visible so Dev Cheats sliders
        // update the beacon in play mode. Meshes are unit-sized; size = transform.localScale.
        void ApplyLayout(ViewCheatsQuestMarkerSection c)
        {
            float gRadius = c != null ? c.GroundRadius : GroundRadius;
            float gY      = c != null ? c.GroundY : GroundY;
            float gSoft   = c != null ? c.GroundSoftFade : GroundSoftFade;
            float bH      = c != null ? c.BeamHeight : BeamHeight;
            float bHalf   = c != null ? c.BeamHalfWidth : BeamHalfWidth;
            float bBaseY  = c != null ? c.BeamBaseY : BeamBaseY;

            if (_ground != null)
            {
                _ground.transform.localPosition = new Vector3(0f, gY, 0f);
                _ground.transform.localScale    = new Vector3(gRadius * 2f, 1f, gRadius * 2f);
            }
            if (_groundMaterial != null) _groundMaterial.SetFloat(PropSoftFade, gSoft);
            if (_beam != null)
            {
                _beam.transform.localPosition = new Vector3(0f, bBaseY, 0f);
                _beam.transform.localScale    = new Vector3(bHalf * 2f, bH, 1f);
            }
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

            var attention = QuestSystem.GetNpcQuestAttention(
                progress, db, level, _npcId, player.Inventory, player.Stash);
            bool visible = attention != QuestSystem.NpcQuestAttention.None;
            SetVisible(visible);
            if (visible)
                ApplyPalette(attention == QuestSystem.NpcQuestAttention.Available ? Avail : Ready);
        }

        void SetVisible(bool visible)
        {
            if (_visibilityApplied && _isVisible == visible) return;
            _visibilityApplied = true;
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
