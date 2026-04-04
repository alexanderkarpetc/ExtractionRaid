using System.Collections.Generic;
using ApplicationCore;
using Quests;
using State;
using Systems;
using UnityEngine;

namespace View
{
    public class NpcUI : MonoBehaviour
    {
        bool _isOpen;
        int _selectedTab;
        Vector2 _scrollPos;

        Texture2D _panelBg;
        Texture2D _promptBg;
        Texture2D _tabActiveBg;
        Texture2D _tabInactiveBg;
        Texture2D _questBg;
        Texture2D _progressBg;
        Texture2D _progressFill;
        Texture2D _progressDoneFill;
        Texture2D _acceptBtnBg;
        Texture2D _completeBtnBg;
        Texture2D _completeBtnDisabled;
        Texture2D _rewardBg;

        GUIStyle _headerStyle;
        GUIStyle _promptStyle;
        GUIStyle _tabActiveStyle;
        GUIStyle _tabInactiveStyle;
        GUIStyle _questNameStyle;
        GUIStyle _questDescStyle;
        GUIStyle _taskStyle;
        GUIStyle _emptyStyle;
        GUIStyle _acceptBtnStyle;
        GUIStyle _completeBtnStyle;
        GUIStyle _rewardLabelStyle;
        GUIStyle _rewardItemStyle;
        GUIStyle _noSpaceStyle;

        void Awake()
        {
            _panelBg = MakeTex(new Color(0.1f, 0.1f, 0.12f, 0.95f));
            _promptBg = MakeTex(new Color(0.1f, 0.1f, 0.1f, 0.8f));
            _tabActiveBg = MakeTex(new Color(0.25f, 0.35f, 0.25f, 1f));
            _tabInactiveBg = MakeTex(new Color(0.18f, 0.18f, 0.2f, 1f));
            _questBg = MakeTex(new Color(0.15f, 0.15f, 0.18f, 0.9f));
            _progressBg = MakeTex(new Color(0.2f, 0.2f, 0.22f, 1f));
            _progressFill = MakeTex(new Color(0.3f, 0.65f, 0.3f, 1f));
            _progressDoneFill = MakeTex(new Color(0.5f, 0.75f, 0.35f, 1f));
            _acceptBtnBg = MakeTex(new Color(0.2f, 0.5f, 0.7f, 0.9f));
            _completeBtnBg = MakeTex(new Color(0.25f, 0.6f, 0.25f, 0.9f));
            _completeBtnDisabled = MakeTex(new Color(0.3f, 0.3f, 0.3f, 0.7f));
            _rewardBg = MakeTex(new Color(0.18f, 0.2f, 0.15f, 0.9f));
        }

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;

            bool shouldBeOpen = player.NpcTargetId != EId.None;

            if (shouldBeOpen && !_isOpen)
            {
                _isOpen = true;
                _selectedTab = 0;
                _scrollPos = Vector2.zero;
            }
            else if (!shouldBeOpen && _isOpen)
            {
                _isOpen = false;
            }
        }

        void OnGUI()
        {
            var session = App.Instance?.RaidSession;
            if (session == null) return;
            var state = session.RaidState;
            if (state?.PlayerEntity == null) return;

            if (!_isOpen)
            {
                DrawNpcPrompt(state, state.PlayerEntity);
                return;
            }

            EnsureStyles();

            var npcState = FindNpcState(state, state.PlayerEntity.NpcTargetId);
            if (npcState == null) return;

            string npcId = npcState.NpcId;
            var app = App.Instance;
            var db = app.QuestDatabase;
            var progress = app.Player.QuestProgress;
            int playerLevel = app.Player.ProfileState.Level;
            if (db == null || progress == null) return;

            float panelW = Mathf.Min(930f, Screen.width * 0.63f);
            float panelH = Mathf.Min(1050f, Screen.height - 40f);
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;
            float padding = 20f;

            var panelRect = new Rect(panelX, panelY, panelW, panelH);
            GUI.DrawTexture(panelRect, _panelBg);

            float curY = panelY + padding;

            string title = string.IsNullOrEmpty(npcId) ? "NPC" : npcId.ToUpper();
            GUI.Label(new Rect(panelX + padding, curY, panelW - padding * 2, 50f), title, _headerStyle);
            curY += 58f;

            float tabW = (panelW - padding * 2 - 4f) * 0.5f;
            float tabH = 48f;

            if (GUI.Button(new Rect(panelX + padding, curY, tabW, tabH), "Available",
                    _selectedTab == 0 ? _tabActiveStyle : _tabInactiveStyle))
            {
                _selectedTab = 0;
                _scrollPos = Vector2.zero;
            }
            if (GUI.Button(new Rect(panelX + padding + tabW + 4f, curY, tabW, tabH), "Active",
                    _selectedTab == 1 ? _tabActiveStyle : _tabInactiveStyle))
            {
                _selectedTab = 1;
                _scrollPos = Vector2.zero;
            }

            curY += tabH + 12f;

            float contentH = panelY + panelH - curY - padding;
            var viewRect = new Rect(panelX, curY, panelW, contentH);

            switch (_selectedTab)
            {
                case 0:
                    DrawAvailableQuests(viewRect, db, progress, playerLevel, npcId, padding);
                    break;
                case 1:
                    DrawActiveQuests(viewRect, db, progress, npcId, padding, state);
                    break;
            }
        }

        void DrawAvailableQuests(Rect viewRect, QuestDatabase db, QuestProgressState progress,
            int playerLevel, string npcId, float padding)
        {
            var quests = QuestSystem.GetAvailableQuests(progress, db, playerLevel, npcId);
            float totalH = 0f;
            foreach (var q in quests)
                totalH += MeasureAvailableBlock(q) + 8f;
            totalH = Mathf.Max(totalH, viewRect.height);

            var contentRect = new Rect(0f, 0f, viewRect.width - 16f, totalH);
            _scrollPos = GUI.BeginScrollView(viewRect, _scrollPos, contentRect);

            if (quests.Count == 0)
            {
                GUI.Label(new Rect(padding, 20f, contentRect.width - padding * 2, 50f),
                    "No available quests.", _emptyStyle);
                GUI.EndScrollView();
                return;
            }

            float y = 0f;
            foreach (var quest in quests)
            {
                float blockH = DrawAvailableBlock(quest, progress, padding, y, contentRect.width);
                y += blockH + 8f;
            }

            GUI.EndScrollView();
        }

        float MeasureAvailableBlock(QuestDefinition quest)
        {
            float h = 90f;
            if (quest.Tasks != null)
                h += quest.Tasks.Count * 30f;
            int rewardCount = quest.Rewards != null ? quest.Rewards.Count : 0;
            if (rewardCount > 0)
                h += 34f + rewardCount * 30f + 8f;
            h += 50f;
            return h;
        }

        float DrawAvailableBlock(QuestDefinition quest, QuestProgressState progress,
            float padding, float y, float totalW)
        {
            float blockH = MeasureAvailableBlock(quest);
            var questRect = new Rect(padding, y, totalW - padding * 2, blockH);
            GUI.DrawTexture(questRect, _questBg);

            float innerPad = 14f;
            GUI.Label(new Rect(questRect.x + innerPad, questRect.y + 8f,
                questRect.width - innerPad * 2, 36f), quest.DisplayName, _questNameStyle);

            GUI.Label(new Rect(questRect.x + innerPad, questRect.y + 46f,
                questRect.width - innerPad * 2, 32f), quest.Description, _questDescStyle);

            float curY = questRect.y + 86f;
            if (quest.Tasks != null)
            {
                foreach (var task in quest.Tasks)
                {
                    GUI.Label(new Rect(questRect.x + innerPad, curY,
                        questRect.width - innerPad * 2, 28f),
                        $"  - {task.Description} (0/{task.RequiredCount})", _taskStyle);
                    curY += 30f;
                }
            }

            if (quest.Rewards != null && quest.Rewards.Count > 0)
            {
                curY += 4f;
                GUI.Label(new Rect(questRect.x + innerPad, curY,
                    questRect.width - innerPad * 2, 30f), "Rewards:", _rewardLabelStyle);
                curY += 34f;

                foreach (var reward in quest.Rewards)
                {
                    var def = ItemDefinition.Get(reward.ItemId);
                    string itemName = def != null ? def.DisplayName : reward.ItemId;
                    string rewardText = reward.Count > 1 ? $"  {itemName}  x{reward.Count}" : $"  {itemName}";

                    var rewardRect = new Rect(questRect.x + innerPad, curY,
                        questRect.width - innerPad * 2, 28f);
                    GUI.DrawTexture(rewardRect, _rewardBg);
                    GUI.Label(rewardRect, rewardText, _rewardItemStyle);
                    curY += 30f;
                }
            }

            float btnW = 160f;
            float btnH = 40f;
            var btnRect = new Rect(questRect.x + questRect.width - innerPad - btnW,
                questRect.y + blockH - btnH - 8f, btnW, btnH);

            if (GUI.Button(btnRect, "ACCEPT", _acceptBtnStyle))
            {
                QuestSystem.TryAccept(progress, quest);
            }

            return blockH;
        }

        void DrawActiveQuests(Rect viewRect, QuestDatabase db, QuestProgressState progress,
            string npcId, float padding, RaidState raidState)
        {
            var quests = QuestSystem.GetActiveQuestsForNpc(progress, db, npcId);
            float totalH = 0f;
            foreach (var q in quests)
                totalH += MeasureActiveBlock(q, progress) + 8f;
            totalH = Mathf.Max(totalH, viewRect.height);

            var contentRect = new Rect(0f, 0f, viewRect.width - 16f, totalH);
            _scrollPos = GUI.BeginScrollView(viewRect, _scrollPos, contentRect);

            if (quests.Count == 0)
            {
                GUI.Label(new Rect(padding, 20f, contentRect.width - padding * 2, 50f),
                    "No active quests from this NPC.", _emptyStyle);
                GUI.EndScrollView();
                return;
            }

            float y = 0f;
            foreach (var quest in quests)
            {
                var p = progress.GetProgress(quest.Id);
                if (p == null) continue;
                float blockH = DrawActiveBlock(quest, p, progress, padding, y, contentRect.width, raidState);
                y += blockH + 8f;
            }

            GUI.EndScrollView();
        }

        float MeasureActiveBlock(QuestDefinition quest, QuestProgressState progress)
        {
            float h = 90f;
            if (quest.Tasks != null)
                h += quest.Tasks.Count * 40f + 8f;

            var p = progress.GetProgress(quest.Id);
            bool allDone = AreAllTasksDone(quest, p);
            if (allDone)
            {
                int rewardCount = quest.Rewards != null ? quest.Rewards.Count : 0;
                if (rewardCount > 0)
                    h += 34f + rewardCount * 30f + 8f;
                h += 50f;
            }

            return h;
        }

        float DrawActiveBlock(QuestDefinition quest, QuestProgress p,
            QuestProgressState progress, float padding, float y, float totalW, RaidState raidState)
        {
            float blockH = MeasureActiveBlock(quest, progress);
            var questRect = new Rect(padding, y, totalW - padding * 2, blockH);
            GUI.DrawTexture(questRect, _questBg);

            float innerPad = 14f;
            GUI.Label(new Rect(questRect.x + innerPad, questRect.y + 8f,
                questRect.width - innerPad * 2, 36f), quest.DisplayName, _questNameStyle);

            GUI.Label(new Rect(questRect.x + innerPad, questRect.y + 46f,
                questRect.width - innerPad * 2, 32f), quest.Description, _questDescStyle);

            float curY = questRect.y + 90f;

            if (quest.Tasks != null)
            {
                for (int i = 0; i < quest.Tasks.Count; i++)
                {
                    var task = quest.Tasks[i];
                    var tp = i < p.Tasks.Count ? p.Tasks[i] : null;
                    int current = tp?.CurrentCount ?? 0;
                    int required = task.RequiredCount;
                    bool done = current >= required;

                    float barW = questRect.width - innerPad * 2;
                    var barRect = new Rect(questRect.x + innerPad, curY, barW, 32f);
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

                    curY += 40f;
                }
                curY += 8f;
            }

            bool allTasksDone = AreAllTasksDone(quest, p);

            if (allTasksDone)
            {
                if (quest.Rewards != null && quest.Rewards.Count > 0)
                {
                    GUI.Label(new Rect(questRect.x + innerPad, curY,
                        questRect.width - innerPad * 2, 30f), "Rewards:", _rewardLabelStyle);
                    curY += 34f;

                    foreach (var reward in quest.Rewards)
                    {
                        var def = ItemDefinition.Get(reward.ItemId);
                        string itemName = def != null ? def.DisplayName : reward.ItemId;
                        string rewardText = reward.Count > 1 ? $"  {itemName}  x{reward.Count}" : $"  {itemName}";

                        var rewardRect = new Rect(questRect.x + innerPad, curY,
                            questRect.width - innerPad * 2, 28f);
                        GUI.DrawTexture(rewardRect, _rewardBg);
                        GUI.Label(rewardRect, rewardText, _rewardItemStyle);
                        curY += 30f;
                    }
                }

                bool hasSpace = QuestSystem.CanFitRewards(quest.Rewards, App.Instance.Player.Inventory);

                float btnW = 220f;
                float btnH = 40f;
                var btnRect = new Rect(questRect.x + questRect.width - innerPad - btnW,
                    questRect.y + blockH - btnH - 8f, btnW, btnH);

                GUI.enabled = hasSpace;
                _completeBtnStyle.normal.background = hasSpace ? _completeBtnBg : _completeBtnDisabled;
                if (GUI.Button(btnRect, "CLAIM REWARD", _completeBtnStyle))
                {
                    QuestSystem.TryCompleteAndGrantRewards(progress, quest, raidState, App.Instance.Player.Inventory);
                }
                GUI.enabled = true;

                if (!hasSpace)
                {
                    var warnRect = new Rect(questRect.x + innerPad,
                        questRect.y + blockH - btnH - 8f, btnRect.x - questRect.x - innerPad * 2, btnH);
                    GUI.Label(warnRect, "Not enough inventory space!", _noSpaceStyle);
                }
            }

            return blockH;
        }

        static bool AreAllTasksDone(QuestDefinition quest, QuestProgress p)
        {
            if (quest.Tasks == null || quest.Tasks.Count == 0) return true;
            for (int i = 0; i < quest.Tasks.Count; i++)
            {
                var tp = i < p.Tasks.Count ? p.Tasks[i] : null;
                int current = tp?.CurrentCount ?? 0;
                if (current < quest.Tasks[i].RequiredCount) return false;
            }
            return true;
        }

        void DrawNpcPrompt(RaidState state, PlayerEntityState player)
        {
            if (player.LootTargetId != EId.None) return;
            if (player.CraftTargetId != EId.None) return;
            if (player.DeployTargetId != EId.None) return;
            if (player.NpcTargetId != EId.None) return;

            var nearest = LootSystem.FindNearestInteractable(state, player.Position, player.FacingDirection);
            if (nearest.Type != InteractableType.Npc) return;

            EnsureStyles();

            float w = 200f;
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

            GUI.Label(rect, "Press F to talk", _promptStyle);
        }

        static NpcState FindNpcState(RaidState state, EId npcTargetId)
        {
            for (int i = 0; i < state.Npcs.Count; i++)
                if (state.Npcs[i].Id == npcTargetId)
                    return state.Npcs[i];
            return null;
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
                fontSize = 24,
                fontStyle = FontStyle.Bold,
            };
            _tabActiveStyle.normal.background = _tabActiveBg;
            _tabActiveStyle.normal.textColor = Color.white;

            _tabInactiveStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
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

            _acceptBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _acceptBtnStyle.normal.background = _acceptBtnBg;
            _acceptBtnStyle.normal.textColor = Color.white;
            _acceptBtnStyle.hover.background = _acceptBtnBg;

            _completeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _completeBtnStyle.normal.background = _completeBtnBg;
            _completeBtnStyle.normal.textColor = Color.white;
            _completeBtnStyle.hover.background = _completeBtnBg;

            _rewardLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
            };
            _rewardLabelStyle.normal.textColor = new Color(0.9f, 0.8f, 0.4f);

            _rewardItemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
            };
            _rewardItemStyle.normal.textColor = new Color(0.85f, 0.9f, 0.75f);

            _noSpaceStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
            };
            _noSpaceStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
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
            if (_promptBg) Destroy(_promptBg);
            if (_tabActiveBg) Destroy(_tabActiveBg);
            if (_tabInactiveBg) Destroy(_tabInactiveBg);
            if (_questBg) Destroy(_questBg);
            if (_progressBg) Destroy(_progressBg);
            if (_progressFill) Destroy(_progressFill);
            if (_progressDoneFill) Destroy(_progressDoneFill);
            if (_acceptBtnBg) Destroy(_acceptBtnBg);
            if (_completeBtnBg) Destroy(_completeBtnBg);
            if (_completeBtnDisabled) Destroy(_completeBtnDisabled);
            if (_rewardBg) Destroy(_rewardBg);
        }
    }
}
