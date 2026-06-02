using ApplicationCore;
using Systems;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// Floating SDF "!" badge above an NPC's head when they have an offer for the
    /// player: either a new quest available (yellow), or an active quest з all tasks
    /// done — ready to turn in (green).
    ///
    /// Visual is rendered entirely by <c>UI/NpcQuestIcon</c> — a procedural SDF
    /// shader that draws a rounded triangle frame з a "!" mark inside, plus a soft
    /// breathing glow. No sprites, no prefab, no font dependency.
    ///
    /// World-space canvas mirrors <see cref="WorldHealthBar"/>'s billboard pattern.
    /// Auto-spawned by <see cref="View.SpawnPoints.NpcSpawnPoint"/>.
    /// </summary>
    public class NpcQuestIndicator : MonoBehaviour
    {
        // ── Layout
        const float OffsetY = 2.2f;            // meters above the NPC root
        const float Width   = 0.55f;
        const float Height  = 0.55f;

        // ── Behavior
        const float PollInterval = 0.4f;       // quest-state recheck cadence (seconds)
        const float PulseHz      = 0.9f;       // breath frequency for the outer glow

        // ── Palette
        static readonly Color FillColor    = new(0.08f, 0.08f, 0.10f, 0.95f);
        static readonly Color BorderAvail  = new(1.00f, 0.82f, 0.15f, 1f);
        static readonly Color MarkAvail    = new(1.00f, 0.95f, 0.55f, 1f);
        static readonly Color GlowAvail    = new(1.00f, 0.78f, 0.20f, 1f);
        static readonly Color BorderReady  = new(0.30f, 0.95f, 0.40f, 1f);
        static readonly Color MarkReady    = new(0.75f, 1.00f, 0.80f, 1f);
        static readonly Color GlowReady    = new(0.35f, 0.95f, 0.45f, 1f);

        // ── Shader property IDs (cached)
        static readonly int PropBorder    = Shader.PropertyToID("_BorderColor");
        static readonly int PropFill      = Shader.PropertyToID("_FillColor");
        static readonly int PropMark      = Shader.PropertyToID("_MarkColor");
        static readonly int PropGlow      = Shader.PropertyToID("_GlowColor");
        static readonly int PropAspect    = Shader.PropertyToID("_Aspect");
        static readonly int PropPulse     = Shader.PropertyToID("_PulseT");
        static readonly int PropAlpha     = Shader.PropertyToID("_Alpha");

        string _npcId;
        GameObject _root;
        Image _badge;
        Material _material;
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
            canvas.sortingOrder = 110; // above WorldHealthBar (100)

            var rt = _root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(Width, Height);
            rt.localScale = Vector3.one;

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
            ApplyPalette(BorderAvail, MarkAvail, GlowAvail);
            _badge.material = _material;

            SetVisible(false);
        }

        void ApplyPalette(Color border, Color mark, Color glow)
        {
            if (_material == null) return;
            _material.SetColor(PropBorder, border);
            _material.SetColor(PropFill,   FillColor);
            _material.SetColor(PropMark,   mark);
            _material.SetColor(PropGlow,   glow);
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

            // Billboard toward camera (same pattern as WorldHealthBar).
            var cam = Camera.main;
            if (cam != null)
                _root.transform.rotation = cam.transform.rotation;

            // Breathing pulse — drives _PulseT in [0..1] на a slow sine.
            if (_material != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulseHz * Mathf.PI * 2f);
                _material.SetFloat(PropPulse, pulse);
                _material.SetFloat(PropAlpha, 1f);
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
            if (visible)
            {
                if (hasAvailable) ApplyPalette(BorderAvail, MarkAvail, GlowAvail);
                else              ApplyPalette(BorderReady, MarkReady, GlowReady);
            }
        }

        void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;
            _isVisible = visible;
            if (_root != null) _root.SetActive(visible);
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
