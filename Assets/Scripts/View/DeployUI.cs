using ApplicationCore;
using Cysharp.Threading.Tasks;
using State;
using Systems;
using UnityEngine;

namespace View
{
    /// <summary>
    /// Deploy point (exit-to-raid) in the hideout. Shows a "Press F to deploy" prompt when
    /// the player is near an unlocked deploy point; on interact it deploys straight to the
    /// Main Map — no map-select popup (single destination for now). The prompt + interaction
    /// are gated behind accepting the first quest (see RaidSession + DeployBeaconPresenter;
    /// QuestSystem.HasAcceptedAnyQuest).
    /// </summary>
    public class DeployUI : MonoBehaviour
    {
        static readonly MapEntry[] Maps =
        {
            new("TestScene", "test_level", "Test Scene"),
            new("Test_Map", "main_map", "Main Map"),
        };

        // Single deploy destination for now. When map selection returns, this becomes a
        // choice again (see task M3.1 — second map).
        static MapEntry MainMap
        {
            get
            {
                foreach (var m in Maps)
                    if (m.LevelId == "main_map") return m;
                return Maps[Maps.Length - 1];
            }
        }

        Texture2D _promptBg;
        GUIStyle _promptStyle;

        void Awake()
        {
            _promptBg = MakeTex(new Color(0.1f, 0.1f, 0.1f, 0.8f));
        }

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            // Interacting with the deploy point deploys straight to the Main Map (no
            // map-select popup). Clear the target first so the async scene load can't
            // re-trigger on a later frame.
            if (player.DeployTargetId != EId.None)
            {
                player.DeployTargetId = EId.None;
                var map = MainMap;
                App.Instance.DeployToRaid(map.SceneName, map.LevelId).Forget();
            }
        }

        void OnGUI()
        {
            var session = App.Instance?.RaidSession;
            var state = session?.RaidState;
            if (state?.PlayerEntity == null) return;
            DrawInteractPrompt(state, state.PlayerEntity);
        }

        void DrawInteractPrompt(RaidState state, PlayerEntityState player)
        {
            if (player.IsInMenu) return;
            if (player.LootTargetId != EId.None) return;
            if (player.CraftTargetId != EId.None) return;
            if (player.DeployTargetId != EId.None) return;
            if (player.NpcTargetId != EId.None) return;

            var nearest = LootSystem.FindNearestInteractable(state, player.Position, player.FacingDirection);
            if (!nearest.IsValid) return;

            // Exit-to-raid is gated behind accepting the first quest (onboarding) — no
            // "Press F to deploy" prompt until then.
            if (nearest.Type == InteractableType.DeployPoint
                && !QuestSystem.HasAcceptedAnyQuest(App.Instance?.Player?.QuestProgress))
                return;

            string label = nearest.Type switch
            {
                InteractableType.Lootable    => "Press F to loot",
                InteractableType.GroundItem  => "Press F to pick up",
                InteractableType.Workbench   => "Press F to craft",
                InteractableType.DeployPoint => "Press F to deploy",
                InteractableType.Npc         => "Press F to talk",
                _ => null,
            };
            if (label == null) return;

            float w = 220f;
            float h = 32f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.78f;

            var rect = new Rect(x, y, w, h);
            GUI.DrawTexture(rect, _promptBg);

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _promptStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }

            GUI.Label(rect, label, _promptStyle);
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            if (_promptBg != null) Destroy(_promptBg);
        }

        readonly struct MapEntry
        {
            public readonly string SceneName;
            public readonly string LevelId;
            public readonly string DisplayName;

            public MapEntry(string sceneName, string levelId, string displayName)
            {
                SceneName = sceneName;
                LevelId = levelId;
                DisplayName = displayName;
            }
        }
    }
}
