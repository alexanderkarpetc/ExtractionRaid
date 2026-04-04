using System.Collections.Generic;
using ApplicationCore;
using Quests;
using State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace View
{
    public class QuestUI : MonoBehaviour
    {
        bool _isOpen;
        int _selectedTab;
        Vector2 _scrollPos;

        Texture2D _panelBg;
        Texture2D _tabActiveBg;
        Texture2D _tabInactiveBg;
        Texture2D _questBg;
        Texture2D _progressBg;
        Texture2D _progressFill;
        Texture2D _progressDoneFill;

        GUIStyle _headerStyle;
        GUIStyle _tabActiveStyle;
        GUIStyle _tabInactiveStyle;
        GUIStyle _questNameStyle;
        GUIStyle _questDescStyle;
        GUIStyle _taskStyle;
        GUIStyle _emptyStyle;

        void Awake()
        {
            _panelBg = MakeTex(new Color(0.1f, 0.1f, 0.12f, 0.95f));
            _tabActiveBg = MakeTex(new Color(0.25f, 0.35f, 0.25f, 1f));
            _tabInactiveBg = MakeTex(new Color(0.18f, 0.18f, 0.2f, 1f));
            _questBg = MakeTex(new Color(0.15f, 0.15f, 0.18f, 0.9f));
            _progressBg = MakeTex(new Color(0.2f, 0.2f, 0.22f, 1f));
            _progressFill = MakeTex(new Color(0.3f, 0.65f, 0.3f, 1f));
            _progressDoneFill = MakeTex(new Color(0.5f, 0.75f, 0.35f, 1f));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Key.I].wasPressedThisFrame)
                _isOpen = !_isOpen;

            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player != null)
                player.IsQuestLogOpen = _isOpen;
        }

        void OnGUI()
        {
            if (!_isOpen) return;

            if (!App.IsInitialized) return;
            var app = App.Instance;
            var db = app.QuestDatabase;
            var progress = app.Player.QuestProgress;
            if (db == null || progress == null) return;

            EnsureStyles();

            float panelW = Mathf.Min(930f, Screen.width * 0.63f);
            float panelH = Mathf.Min(1050f, Screen.height - 40f);
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;
            float padding = 20f;

            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUI.DrawTexture(panelRect, _panelBg);

            float curY = panelY + padding;

            GUI.Label(new Rect(panelX + padding, curY, panelW - padding * 2, 50f),
                "QUESTS", _headerStyle);
            curY += 58f;

            float tabW = (panelW - padding * 2 - 6f) * 0.5f;
            float tabH = 48f;

            if (GUI.Button(new Rect(panelX + padding, curY, tabW, tabH), "Active",
                    _selectedTab == 0 ? _tabActiveStyle : _tabInactiveStyle))
            {
                _selectedTab = 0;
                _scrollPos = Vector2.zero;
            }
            if (GUI.Button(new Rect(panelX + padding + tabW + 4f, curY, tabW, tabH), "Completed",
                    _selectedTab == 1 ? _tabActiveStyle : _tabInactiveStyle))
            {
                _selectedTab = 1;
                _scrollPos = Vector2.zero;
            }

            curY += tabH + 12f;

            float contentTop = curY;
            float contentH = panelY + panelH - contentTop - padding;
            var viewRect = new Rect(panelX, contentTop, panelW, contentH);

            var filter = _selectedTab == 0 ? QuestStatus.Active : QuestStatus.Completed;
            DrawQuestList(viewRect, db, progress, filter, padding);
        }

        void DrawQuestList(Rect viewRect, QuestDatabase db, QuestProgressState progress,
            QuestStatus filter, float padding)
        {
            var quests = new List<(QuestDatabaseEntry entry, QuestProgress p)>();
            foreach (var entry in db.Entries)
            {
                if (entry.Quest == null) continue;
                var p = progress.GetProgress(entry.Quest.Id);
                if (p == null || p.Status != filter) continue;
                quests.Add((entry, p));
            }

            float totalH = 0f;
            foreach (var (entry, p) in quests)
                totalH += MeasureQuestBlock(entry.Quest, filter) + 8f;
            totalH = Mathf.Max(totalH, viewRect.height);

            var contentRect = new Rect(0f, 0f, viewRect.width - 16f, totalH);
            _scrollPos = GUI.BeginScrollView(viewRect, _scrollPos, contentRect);

            if (quests.Count == 0)
            {
                GUI.Label(new Rect(padding, 20f, contentRect.width - padding * 2, 50f),
                    filter == QuestStatus.Active ? "No active quests." : "No completed quests.",
                    _emptyStyle);
                GUI.EndScrollView();
                return;
            }

            float y = 0f;
            foreach (var (entry, p) in quests)
            {
                float blockH = DrawQuestBlock(entry.Quest, p, filter, padding, y, contentRect.width);
                y += blockH + 8f;
            }

            GUI.EndScrollView();
        }

        float MeasureQuestBlock(QuestDefinition quest, QuestStatus filter)
        {
            float h = 90f;
            if (filter == QuestStatus.Active && quest.Tasks != null)
                h += quest.Tasks.Count * 40f + 8f;
            return h;
        }

        float DrawQuestBlock(QuestDefinition quest, QuestProgress p, QuestStatus filter,
            float padding, float y, float totalW)
        {
            float blockH = MeasureQuestBlock(quest, filter);
            var questRect = new Rect(padding, y, totalW - padding * 2, blockH);
            GUI.DrawTexture(questRect, _questBg);

            float innerPad = 14f;

            GUI.Label(new Rect(questRect.x + innerPad, questRect.y + 8f,
                questRect.width - innerPad * 2, 36f), quest.DisplayName, _questNameStyle);

            GUI.Label(new Rect(questRect.x + innerPad, questRect.y + 46f,
                questRect.width - innerPad * 2, 32f), quest.Description, _questDescStyle);

            if (filter == QuestStatus.Active && quest.Tasks != null)
            {
                float taskY = questRect.y + 90f;
                for (int i = 0; i < quest.Tasks.Count; i++)
                {
                    var task = quest.Tasks[i];
                    var tp = i < p.Tasks.Count ? p.Tasks[i] : null;
                    int current = tp?.CurrentCount ?? 0;
                    int required = task.RequiredCount;
                    bool done = current >= required;

                    float barW = questRect.width - innerPad * 2;
                    var barRect = new Rect(questRect.x + innerPad, taskY, barW, 32f);
                    GUI.DrawTexture(barRect, _progressBg);

                    float ratio = required > 0 ? Mathf.Clamp01((float)current / required) : 0f;
                    if (ratio > 0f)
                    {
                        var fillRect = new Rect(barRect.x, barRect.y,
                            barRect.width * ratio, barRect.height);
                        GUI.DrawTexture(fillRect, done ? _progressDoneFill : _progressFill);
                    }

                    string label = $"  {task.Description}: {current}/{required}";
                    if (done) label += "  \u2713";
                    GUI.Label(barRect, label, _taskStyle);

                    taskY += 40f;
                }
            }

            return blockH;
        }

        void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _headerStyle.normal.textColor = Color.white;

            _tabActiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
            };
            _tabActiveStyle.normal.background = _tabActiveBg;
            _tabActiveStyle.normal.textColor = Color.white;

            _tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 28,
            };
            _tabInactiveStyle.normal.background = _tabInactiveBg;
            _tabInactiveStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            _questNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
            };
            _questNameStyle.normal.textColor = new Color(0.9f, 0.85f, 0.6f);

            _questDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                wordWrap = true,
            };
            _questDescStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            _taskStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
            };
            _taskStyle.normal.textColor = Color.white;

            _emptyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
            };
            _emptyStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
        }

        static Texture2D MakeTex(Color c)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            if (_panelBg) Destroy(_panelBg);
            if (_tabActiveBg) Destroy(_tabActiveBg);
            if (_tabInactiveBg) Destroy(_tabInactiveBg);
            if (_questBg) Destroy(_questBg);
            if (_progressBg) Destroy(_progressBg);
            if (_progressFill) Destroy(_progressFill);
            if (_progressDoneFill) Destroy(_progressDoneFill);
        }
    }
}
