using ApplicationCore;
using Cysharp.Threading.Tasks;
using State;
using Systems;
using UnityEngine;

namespace View
{
    public class DeployUI : MonoBehaviour
    {
        static readonly MapEntry[] Maps =
        {
            new("TestScene", "test_level", "Test Scene"),
            new("Test_Map", "main_map", "Main Map"),
        };

        bool _isOpen;

        Texture2D _panelBg;
        Texture2D _promptBg;
        GUIStyle _headerStyle;
        GUIStyle _buttonStyle;
        GUIStyle _promptStyle;

        void Awake()
        {
            _panelBg = MakeTex(new Color(0.12f, 0.12f, 0.14f, 0.95f));
            _promptBg = MakeTex(new Color(0.1f, 0.1f, 0.1f, 0.8f));
        }

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            bool shouldBeOpen = player.DeployTargetId != EId.None;

            if (shouldBeOpen && !_isOpen)
                _isOpen = true;
            else if (!shouldBeOpen && _isOpen)
                _isOpen = false;
        }

        void OnGUI()
        {
            var session = App.Instance?.RaidSession;
            if (session == null) return;
            var state = session.RaidState;
            if (state?.PlayerEntity == null) return;

            if (!_isOpen)
            {
                DrawDeployPrompt(state, state.PlayerEntity);
                return;
            }

            EnsureStyles();

            float panelW = 280f;
            float headerH = 36f;
            float gap = 8f;
            float padding = 14f;
            float mapBtnH = 40f;
            float panelH = padding + headerH + gap + Maps.Length * (mapBtnH + gap) + mapBtnH + padding;

            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUI.DrawTexture(panelRect, _panelBg);

            float curY = panelY + padding;
            GUI.Label(new Rect(panelX + padding, curY, panelW - padding * 2f, headerH),
                "SELECT MAP", _headerStyle);
            curY += headerH + gap;

            for (int i = 0; i < Maps.Length; i++)
            {
                var map = Maps[i];
                var btnRect = new Rect(panelX + padding, curY, panelW - padding * 2f, mapBtnH);
                if (GUI.Button(btnRect, map.DisplayName, _buttonStyle))
                {
                    App.Instance.DeployToRaid(map.SceneName, map.LevelId).Forget();
                }
                curY += mapBtnH + gap;
            }

            if (GUI.Button(new Rect(panelX + padding, curY, panelW - padding * 2f, mapBtnH),
                "Cancel", _buttonStyle))
            {
                var player = state.PlayerEntity;
                if (player != null)
                    player.DeployTargetId = EId.None;
            }
        }

        void DrawDeployPrompt(RaidState state, PlayerEntityState player)
        {
            if (player.LootTargetId != EId.None) return;
            if (player.CraftTargetId != EId.None) return;
            if (player.DeployTargetId != EId.None) return;

            var nearest = LootSystem.FindNearestInteractable(state, player.Position, player.FacingDirection);
            if (nearest.Type != InteractableType.DeployPoint) return;

            EnsureStyles();

            float w = 220f;
            float h = 32f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.65f;

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

            GUI.Label(rect, "Press F to deploy", _promptStyle);
        }

        void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _headerStyle.normal.textColor = Color.white;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
            };
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
            if (_panelBg != null) Destroy(_panelBg);
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
