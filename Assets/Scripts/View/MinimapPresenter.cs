using ApplicationCore;
using Quests;
using State;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using View.SpawnPoints;
using View.UI.Minimap;

namespace View
{
    /// <summary>
    /// Owns the minimap lifecycle:
    ///   1. Capture pass at raid start — finds <see cref="MinimapBoundsMarker"/>,
    ///      spins up a temporary ortho camera that renders the env layers to a
    ///      RenderTexture, hands it to the window.
    ///   2. Auto-registers the well-known markers (player live position, NPCs,
    ///      extraction points, current-map quest find-item targets). Anything else
    ///      can register via <see cref="MinimapMarkerRegistry"/>.
    ///   3. Toggles corner ↔ expanded mode on <c>M</c>.
    /// </summary>
    public class MinimapPresenter : MonoBehaviour
    {
        MinimapWindow _window;
        bool _triedFind;

        // Per-raid capture state — RenderTexture stays alive while the window references
        // it; replaced cleanly on each new raid.
        RenderTexture _envTexture;
        bool _hasCapturedThisRaid;
        string _capturedLevelId;
        bool _deployMarkersShown;

        const string PlayerMarkerId = "player";

        void Update()
        {
            if (!App.IsInitialized) return;
            if (!EnsureWindow()) return;

            var session = App.Instance.RaidSession;
            if (session == null)
            {
                if (_hasCapturedThisRaid) ResetForNextRaid();
                return;
            }

            // (1) First-frame-of-raid capture.
            if (!_hasCapturedThisRaid || session.LevelState.LevelId != _capturedLevelId)
            {
                CaptureForCurrentRaid(session);
                _hasCapturedThisRaid = true;
                _capturedLevelId = session.LevelState.LevelId;
            }

            // (2) Deploy markers gate on live quest state (can unlock mid-session in the
            // hideout), so reconcile them before refreshing the overlay.
            ReconcileDeployMarkers(session);

            // Refresh marker overlay every frame (cheap; supports live positions).
            _window.RefreshMarkers();

            // (3) Hold-to-expand on M. Read directly from Keyboard.current — gameplay
            // input gating doesn't apply because M isn't a gameplay key, and we want
            // the hold behavior to work even inside menus / dialogues.
            var kb = Keyboard.current;
            bool wantExpanded = kb != null && kb[Key.M].isPressed;
            if (wantExpanded != _window.IsExpanded)
                _window.SetExpanded(wantExpanded);
        }

        bool EnsureWindow()
        {
            if (_triedFind) return _window != null;
            _triedFind = true;
            _window = MinimapWindow.Instance
                      ?? FindObjectOfType<MinimapWindow>(includeInactive: true);
            return _window != null;
        }

        void CaptureForCurrentRaid(Session.RaidSession session)
        {
            MinimapMarkerRegistry.Clear();

            // Pull defaults from the bounds marker if present; otherwise fall back to
            // the same constants the marker uses so manual + no-marker behavior matches.
            var marker = FindObjectOfType<MinimapBoundsMarker>(includeInactive: false);
            bool autoFit = marker == null || marker.autoFit;
            LayerMask layers = marker != null ? marker.captureLayers : MinimapBoundsMarker.DefaultCaptureLayers;
            int texSize = marker != null ? Mathf.Max(64, marker.textureSize) : 2048;
            Color clear = marker != null ? marker.clearColor : new Color(0.18f, 0.22f, 0.30f, 1f);
            float minCamHeight = marker != null ? marker.cameraHeight : 60f;
            float padding = marker != null ? marker.autoFitPadding : 4f;

            // Resolve the captured rectangle (XZ) + camera Y.
            Vector3 centerWorld;
            Vector2 size;
            float camHeight;

            if (autoFit && TryComputeSceneBounds(layers, out var sceneBounds))
            {
                centerWorld = new Vector3(sceneBounds.center.x, sceneBounds.max.y + 5f, sceneBounds.center.z);
                size = new Vector2(sceneBounds.size.x + padding * 2f, sceneBounds.size.z + padding * 2f);
                camHeight = Mathf.Max(minCamHeight, sceneBounds.size.y + 20f);
                Debug.Log($"[Minimap] AutoFit captured bounds — center=({sceneBounds.center.x:0.0},{sceneBounds.center.z:0.0}) " +
                          $"size=({size.x:0.0}×{size.y:0.0}) camY={camHeight:0.0} layerMask=0x{(int)layers:X}");
            }
            else if (marker != null)
            {
                centerWorld = marker.transform.position;
                size = marker.size;
                camHeight = minCamHeight;
            }
            else
            {
                Debug.LogWarning("[Minimap] No MinimapBoundsMarker and AutoFit found 0 renderers — " +
                                 "minimap will be blank. Add a MinimapBoundsMarker to the scene or " +
                                 "check the capture layer mask.");
                centerWorld = Vector3.zero;
                size = new Vector2(80f, 80f);
                camHeight = minCamHeight;
            }

            // Force a square capture region so the rectangular world doesn't get
            // squashed into the square RenderTexture. The frame's scale-and-crop
            // hides any extra so the visible UI still looks rectangular.
            float square = Mathf.Max(size.x, size.y);
            size = new Vector2(square, square);

            // Replace any prior RT — old textures can't outlive the raid swap.
            if (_envTexture != null) _envTexture.Release();
            _envTexture = new RenderTexture(texSize, texSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Minimap_EnvCapture",
            };
            _envTexture.Create();

            // Hand the (still-empty) RT to the window now so it becomes visible — the
            // coroutine populates the texture one frame later. Markers register now too;
            // they only depend on positions and don't need the capture to be finished.
            _window.SetCapture(_envTexture, new Vector2(centerWorld.x, centerWorld.z), size);
            RegisterWellKnownMarkers(session);

            // Enable a real camera in the scene for exactly one frame. URP render
            // callbacks temporarily suppress Environment Fog for this camera only.
            StartCoroutine(CaptureOnce(centerWorld, size, camHeight, layers, clear));
        }

        System.Collections.IEnumerator CaptureOnce(Vector3 centerWorld, Vector2 size,
            float camHeight, LayerMask layers, Color clear)
        {
            var camGo = new GameObject("MinimapCaptureCam");
            camGo.transform.position = new Vector3(centerWorld.x, centerWorld.y + camHeight, centerWorld.z);
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size.y * 0.5f;
            cam.aspect = size.x / size.y;
            cam.cullingMask = layers;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = clear;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = camHeight * 2f + 100f;
            cam.targetTexture = _envTexture;

            bool fogOverrideActive = false;
            bool fogBeforeCapture = false;

            void RestoreEnvironmentFog()
            {
                if (!fogOverrideActive) return;
                RenderSettings.fog = fogBeforeCapture;
                fogOverrideActive = false;
            }

            void OnBeginCameraRendering(ScriptableRenderContext _, Camera renderingCamera)
            {
                if (renderingCamera != cam) return;
                fogBeforeCapture = RenderSettings.fog;
                fogOverrideActive = true;
                RenderSettings.fog = false;
            }

            void OnEndCameraRendering(ScriptableRenderContext _, Camera renderingCamera)
            {
                if (renderingCamera == cam)
                    RestoreEnvironmentFog();
            }

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

            try
            {
                cam.enabled = true;

                // Wait until the next frame's render has executed. Environment fog is
                // disabled only between this camera's begin/end render callbacks.
                yield return new WaitForEndOfFrame();
            }
            finally
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                RestoreEnvironmentFog();

                cam.targetTexture = null;
                Object.Destroy(camGo);
            }
        }

        // Renderers with any-axis bounds beyond this are treated as skyboxes / infinite
        // ground planes / debug helpers and excluded from the auto-fit. 300m is much
        // larger than any reasonable level prop but well below the 1000m+ scale common
        // to skyboxes and giant Plane primitives.
        const float OutlierBoundsSize = 300f;

        // Combined world bounds of every enabled renderer on the capture layers. Skips
        // particle systems (huge animated bounds) and outsized props (see comment above).
        static bool TryComputeSceneBounds(LayerMask layers, out Bounds bounds)
        {
            bounds = default;
            bool hasAny = false;
            int considered = 0, accepted = 0, skippedOutlier = 0;

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled) continue;
                if (r is ParticleSystemRenderer) continue;
                int layer = r.gameObject.layer;
                if ((layers.value & (1 << layer)) == 0) continue;
                considered++;

                var rb = r.bounds;
                if (rb.size.x > OutlierBoundsSize || rb.size.z > OutlierBoundsSize)
                {
                    skippedOutlier++;
                    continue;
                }

                if (!hasAny) { bounds = rb; hasAny = true; }
                else bounds.Encapsulate(rb);
                accepted++;
            }

            if (considered > 0)
                Debug.Log($"[Minimap] AutoFit renderers — considered={considered}, " +
                          $"accepted={accepted}, skipped-as-outlier={skippedOutlier} " +
                          $"(threshold={OutlierBoundsSize}m).");
            return hasAny;
        }

        // Built-in markers populated once per raid. External callers can still
        // Register/Unregister whatever they like — these IDs are namespaced with
        // prefixes so they won't collide.
        void RegisterWellKnownMarkers(Session.RaidSession session)
        {
            // Player — live position via lambda; no need for the player presenter to
            // ping us every frame.
            MinimapMarkerRegistry.Register(
                PlayerMarkerId,
                MinimapMarkerType.Player,
                () =>
                {
                    var p = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
                    return p?.Position ?? Vector3.zero;
                },
                tooltip: "You",
                // FacingDirection is world-space XZ. Atan2(x, z) gives clockwise yaw
                // from world +Z, which matches minimap-up (camera looks down -Y, so
                // larger Z renders at the top of the texture).
                liveRotationFn: () =>
                {
                    var p = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
                    if (p == null) return 0f;
                    var f = p.FacingDirection;
                    if (f.sqrMagnitude < 0.0001f) return 0f;
                    return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                });

            var state = session.RaidState;

            // Quest lookups — also used to flag NPCs that currently have an offer so they
            // render as a "!" quest marker instead of a plain NPC dot. Snapshot at capture
            // time (raid/hideout start), same as the rest of this registration.
            var app = App.Instance;
            var db = app?.QuestDatabase;
            var progress = app?.Player?.QuestProgress;
            int level = app?.Player?.ProfileState?.Level ?? 1;

            for (int i = 0; i < state.Npcs.Count; i++)
            {
                var npc = state.Npcs[i];
                bool hasOffer = NpcHasQuestOffer(npc.NpcId, db, progress, level);
                MinimapMarkerRegistry.Register(
                    $"npc:{npc.Id.Value}",
                    hasOffer ? MinimapMarkerType.Quest : MinimapMarkerType.Npc,
                    npc.Position,
                    tooltip: string.IsNullOrEmpty(npc.NpcId) ? "NPC" : npc.NpcId);
            }

            for (int i = 0; i < state.ExtractionPoints.Count; i++)
            {
                var ep = state.ExtractionPoints[i];
                MinimapMarkerRegistry.Register(
                    $"extract:{ep.Id.Value}",
                    MinimapMarkerType.Extraction,
                    ep.Position,
                    tooltip: string.IsNullOrEmpty(ep.Label) ? "Extraction" : ep.Label);
            }

            // Active find-item quests on the current map — the same source RaidSession
            // uses to spawn ground items for these tasks.
            if (db == null || progress == null) return;
            var currentMap = Constants.MapIds.FromLevelId(session.LevelState.LevelId);
            if (currentMap == Constants.MapId.None) return;

            foreach (var entry in db.Entries)
            {
                var q = entry.Quest;
                if (q?.Tasks == null) continue;
                if (progress.GetStatus(q.Id) != QuestStatus.Active) continue;
                foreach (var task in q.Tasks)
                {
                    if (task is not FindItemTask f) continue;
                    if (f.Map != currentMap) continue;
                    if (string.IsNullOrEmpty(f.ItemId)) continue;
                    MinimapMarkerRegistry.Register(
                        $"quest:{q.Id}:{f.ItemId}",
                        MinimapMarkerType.Quest,
                        f.Coordinates,
                        tooltip: $"{q.DisplayName}: {f.ItemId}");
                }
            }
        }

        // Mirrors NpcQuestIndicator.Refresh: an NPC "has an offer" when it has a quest
        // available to the player, or an active quest with all tasks done (ready to turn in).
        static bool NpcHasQuestOffer(string npcId, QuestDatabase db, QuestProgressState progress, int level)
        {
            if (string.IsNullOrEmpty(npcId) || db == null || progress == null) return false;
            if (QuestSystem.GetAvailableQuests(progress, db, level, npcId).Count > 0) return true;
            var active = QuestSystem.GetActiveQuestsForNpc(progress, db, npcId);
            for (int i = 0; i < active.Count; i++)
            {
                var qp = progress.GetProgress(active[i].Id);
                if (qp != null && QuestSystem.AreAllTasksDone(active[i], qp)) return true;
            }
            return false;
        }

        // Deploy-point minimap markers are gated behind accepting the first quest and can
        // unlock mid-session (in the hideout), so they can't be registered once at capture.
        // Register them the frame the gate opens; ResetForNextRaid re-arms via Clear().
        void ReconcileDeployMarkers(Session.RaidSession session)
        {
            if (_deployMarkersShown) return;
            var app = App.Instance;
            if (!app.IsInHideout) return;
            var state = session.RaidState;
            if (state == null || state.DeployPoints.Count == 0) return;
            if (!Systems.QuestSystem.HasAcceptedAnyQuest(app.Player?.QuestProgress)) return;

            for (int i = 0; i < state.DeployPoints.Count; i++)
            {
                var dp = state.DeployPoints[i];
                MinimapMarkerRegistry.Register(
                    $"deploy:{dp.Id.Value}", MinimapMarkerType.Deploy, dp.Position, "Deploy");
            }
            _deployMarkersShown = true;
        }

        void ResetForNextRaid()
        {
            _hasCapturedThisRaid = false;
            _capturedLevelId = null;
            _deployMarkersShown = false;
            MinimapMarkerRegistry.Clear();
            if (_envTexture != null)
            {
                _envTexture.Release();
                _envTexture = null;
            }
            if (_window != null) _window.SetCapture(null, Vector2.zero, Vector2.one);
        }

        void OnDestroy()
        {
            if (_envTexture != null)
            {
                _envTexture.Release();
                _envTexture = null;
            }
        }
    }
}
