using System.Collections.Generic;
using Constants;
using State;
using Systems.Bot;
using Systems.Bot.BT;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class BotBTDebuggerWindow : EditorWindow
    {
        const float NodeW = 130f;
        const float NodeH = 44f;
        const float HGap = 16f;
        const float VGap = 44f;
        const float Pad = 30f;
        const float ToolbarH = 22f;
        const float LegendH = 22f;
        const float LineThickness = 2f;

        int _selectedBot;
        Vector2 _scroll;

        static readonly Color ColSuccess = new(0.30f, 0.69f, 0.31f, 1f);
        static readonly Color ColFailure = new(0.84f, 0.30f, 0.25f, 1f);
        static readonly Color ColRunning = new(1.00f, 0.76f, 0.03f, 1f);
        static readonly Color ColIdle = new(0.35f, 0.35f, 0.35f, 1f);
        static readonly Color ColBorder = new(0.10f, 0.10f, 0.10f, 1f);
        static readonly Color ColLine = new(0.65f, 0.65f, 0.65f, 0.85f);
        static readonly Color ColShadow = new(0f, 0f, 0f, 0.25f);
        static readonly Color ColTypeTxt = new(1f, 1f, 1f, 0.55f);

        static GUIStyle _nameStyle;
        static GUIStyle _typeStyle;
        static GUIStyle _legendStyle;

        [MenuItem("Window/BT Debugger")]
        static void Open() => GetWindow<BotBTDebuggerWindow>("BT Debugger");

        void Update()
        {
            if (EditorApplication.isPlaying)
                Repaint();
        }

        void OnGUI()
        {
            EnsureStyles();

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to debug Behaviour Trees.", MessageType.Info);
                return;
            }

            RaidState state;
            try
            {
                var session = App.App.Instance?.RaidSession;
                if (session == null)
                {
                    EditorGUILayout.HelpBox("No active raid session.", MessageType.Warning);
                    return;
                }

                state = session.RaidState;
            }
            catch
            {
                EditorGUILayout.HelpBox("App not initialized.", MessageType.Warning);
                return;
            }

            if (state == null || state.Bots.Count == 0)
            {
                EditorGUILayout.HelpBox("No bots in the scene.", MessageType.Info);
                return;
            }

            DrawToolbar(state);
            DrawLegend();

            var bot = state.Bots[_selectedBot];
            if (!BotConstants.TryGetConfig(bot.TypeId, out var config))
            {
                EditorGUILayout.HelpBox("Unknown bot type config.", MessageType.Warning);
                return;
            }

            var tree = BotTreeBuilder.GetOrBuild(in config);
            var trace = bot.Blackboard.Trace;

            var widths = new Dictionary<IBTNode, float>();
            CalcWidths(tree, widths);
            float totalW = widths[tree];
            float totalH = CalcHeight(tree);

            float topOffset = ToolbarH + LegendH + 8;
            var viewRect = new Rect(0, topOffset, position.width, position.height - topOffset);
            var contentRect = new Rect(0, 0,
                Mathf.Max(totalW + Pad * 2, viewRect.width),
                totalH + Pad * 2);

            _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect);

            float rootX = contentRect.width / 2f - NodeW / 2f;
            DrawNode(tree, trace, rootX, Pad, widths);

            GUI.EndScrollView();
        }

        void DrawToolbar(RaidState state)
        {
            var names = new string[state.Bots.Count];
            for (int i = 0; i < state.Bots.Count; i++)
            {
                var b = state.Bots[i];
                bool alive = state.HealthMap.TryGetValue(b.Id, out var hp) && hp.IsAlive;
                names[i] = $"[{b.TypeId}] {b.Id}{(alive ? "" : " (Dead)")}";
            }

            _selectedBot = Mathf.Clamp(_selectedBot, 0, state.Bots.Count - 1);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Bot:", GUILayout.Width(28));
            _selectedBot = EditorGUILayout.Popup(_selectedBot, names, EditorStyles.toolbarPopup);
            EditorGUILayout.EndHorizontal();
        }

        void DrawLegend()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            DrawLegendItem("Success", ColSuccess);
            GUILayout.Space(8);
            DrawLegendItem("Failure", ColFailure);
            GUILayout.Space(8);
            DrawLegendItem("Running", ColRunning);
            GUILayout.Space(8);
            DrawLegendItem("Idle", ColIdle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawLegendItem(string label, Color color)
        {
            var rect = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
            rect.y += 4;
            EditorGUI.DrawRect(rect, color);
            GUILayout.Label(label, _legendStyle);
        }

        // ── Layout calculation ───────────────────────────────────

        void CalcWidths(IBTNode node, Dictionary<IBTNode, float> widths)
        {
            var children = GetChildren(node);
            if (children == null || children.Count == 0)
            {
                widths[node] = NodeW;
                return;
            }

            float total = 0;
            for (int i = 0; i < children.Count; i++)
            {
                CalcWidths(children[i], widths);
                if (i > 0) total += HGap;
                total += widths[children[i]];
            }

            widths[node] = Mathf.Max(NodeW, total);
        }

        float CalcHeight(IBTNode node)
        {
            var children = GetChildren(node);
            if (children == null || children.Count == 0)
                return NodeH;

            float maxH = 0;
            for (int i = 0; i < children.Count; i++)
            {
                float h = CalcHeight(children[i]);
                if (h > maxH) maxH = h;
            }

            return NodeH + VGap + maxH;
        }

        // ── Tree drawing ─────────────────────────────────────────

        void DrawNode(IBTNode node, BTTrace trace, float x, float y,
            Dictionary<IBTNode, float> widths)
        {
            DrawNodeBox(node, trace, x, y);

            var children = GetChildren(node);
            if (children == null || children.Count == 0) return;

            float totalChildW = 0;
            for (int i = 0; i < children.Count; i++)
            {
                if (i > 0) totalChildW += HGap;
                totalChildW += widths[children[i]];
            }

            float startX = x + NodeW / 2f - totalChildW / 2f;
            float childY = y + NodeH + VGap;
            float parentCX = x + NodeW / 2f;
            float parentBY = y + NodeH;

            var childCXs = new float[children.Count];
            var childXs = new float[children.Count];
            float cx = startX;
            for (int i = 0; i < children.Count; i++)
            {
                float childW = widths[children[i]];
                childXs[i] = cx + childW / 2f - NodeW / 2f;
                childCXs[i] = childXs[i] + NodeW / 2f;
                cx += childW + HGap;
            }

            DrawConnections(parentCX, parentBY, childCXs, childY);

            for (int i = 0; i < children.Count; i++)
                DrawNode(children[i], trace, childXs[i], childY, widths);
        }

        void DrawNodeBox(IBTNode node, BTTrace trace, float x, float y)
        {
            Color bg = ColIdle;
            if (trace != null && trace.TryGetStatus(node, out int statusInt))
            {
                bg = (BTStatus)statusInt switch
                {
                    BTStatus.Success => ColSuccess,
                    BTStatus.Failure => ColFailure,
                    BTStatus.Running => ColRunning,
                    _ => ColIdle,
                };
            }

            var rect = new Rect(x, y, NodeW, NodeH);

            // Shadow
            EditorGUI.DrawRect(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), ColShadow);

            // Background
            EditorGUI.DrawRect(rect, bg);

            // Border
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), ColBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), ColBorder);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), ColBorder);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), ColBorder);

            // Type label (top)
            string typeLabel = GetTypeLabel(node);
            GUI.Label(new Rect(rect.x, rect.y + 1, rect.width, 14), typeLabel, _typeStyle);

            // Name label (center)
            GUI.Label(new Rect(rect.x + 2, rect.y + 13, rect.width - 4, rect.height - 15),
                node.Name, _nameStyle);
        }

        void DrawConnections(float parentCX, float parentBY, float[] childCXs, float childY)
        {
            float midY = parentBY + (childY - parentBY) * 0.5f;
            float t = LineThickness;

            // Vertical from parent down to mid
            EditorGUI.DrawRect(new Rect(parentCX - t / 2, parentBY, t, midY - parentBY), ColLine);

            // Horizontal bar spanning all children
            if (childCXs.Length > 1)
            {
                float left = childCXs[0];
                float right = childCXs[childCXs.Length - 1];
                EditorGUI.DrawRect(new Rect(left, midY - t / 2, right - left, t), ColLine);
            }

            // Vertical drops to each child
            for (int i = 0; i < childCXs.Length; i++)
                EditorGUI.DrawRect(new Rect(childCXs[i] - t / 2, midY, t, childY - midY), ColLine);
        }

        // ── Helpers ──────────────────────────────────────────────

        static string GetTypeLabel(IBTNode node)
        {
            if (node is BTSelector) return "\u25cf Selector";
            if (node is BTSequence) return "\u25cf Sequence";
            if (node is BTCondition) return "? Condition";
            if (node is BTCooldown) return "\u23f1 Cooldown";
            return "\u25b6 Action";
        }

        static IReadOnlyList<IBTNode> GetChildren(IBTNode node)
        {
            if (node is BTSelector sel) return sel.Children;
            if (node is BTSequence seq) return seq.Children;
            if (node is BTCooldown cd) return new[] { cd.Child };
            return null;
        }

        static void EnsureStyles()
        {
            _nameStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 11,
                wordWrap = true,
            };

            _typeStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = ColTypeTxt },
                fontSize = 9,
            };

            _legendStyle ??= new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) },
            };
        }
    }
}
