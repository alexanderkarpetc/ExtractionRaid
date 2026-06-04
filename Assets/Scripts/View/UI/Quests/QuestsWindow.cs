using System;
using System.Collections.Generic;
using ApplicationCore;
using Quests;
using State;
using Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Quests
{
    /// <summary>Which surface opened the quest popup.</summary>
    public enum QuestPopupMode { Journal, Npc }

    /// <summary>Tab selection within the quest popup.</summary>
    public enum QuestTab { Available, Active, Completed }

    /// <summary>
    /// UI Toolkit quest popup — canonical quest UI, replacing the former uGUI
    /// quest popup. Layout follows the concept at
    /// Assets/Concepts/quest-ui-popup.html: centered modal with a header
    /// (title + optional NPC pill + close), a tab strip (Available / Active /
    /// Completed) and a scrolling list of expandable quest cards.
    ///
    /// Mirrors the window pattern used by <see cref="Inventory.InventoryWindow"/>
    /// and <see cref="Dialogue.NpcDialogueWindow"/>: a singleton MonoBehaviour
    /// that owns a <see cref="UIDocument"/>, loads its UXML/USS/PanelSettings from
    /// Resources, and toggles visibility via DisplayStyle.
    ///
    /// Keeps no gameplay rules of its own — all reads/writes go through
    /// <see cref="QuestSystem"/>. The public API (OpenJournal / OpenForNpc /
    /// RequestClose / Closed / IsOpen / Refresh) matches the old view so the
    /// presenters need only swap the reference.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(UIDocument))]
    public class QuestsWindow : MonoBehaviour
    {
        public static QuestsWindow Instance { get; private set; }

        UIDocument _doc;
        VisualElement _root;
        VisualElement _overlay;
        Label _titleLabel;
        VisualElement _npcPill;
        Label _npcPillName;
        Button _closeBtn;
        VisualElement _tabsBox;
        ScrollView _body;

        Button _tabAvailable;
        Button _tabActive;
        Button _tabCompleted;

        QuestPopupMode _mode;
        QuestTab _selectedTab;
        string _npcId;
        bool _isVisible;

        public bool IsOpen => _isVisible;

        public event Action Closed;

        void Awake()
        {
            Instance = this;
            BuildDocument();
            HideImmediate();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Build ─────────────────────────────────────────────

        void BuildDocument()
        {
            var tree = Resources.Load<VisualTreeAsset>("UI/Quests/QuestsWindow");
            var styles = Resources.Load<StyleSheet>("UI/Quests/QuestsWindow");
            var panel = Resources.Load<PanelSettings>("UI/Quests/QuestsPanelSettings");

            if (tree == null || panel == null)
            {
                Debug.LogError("[QuestsWindow] Missing UXML or PanelSettings in Resources/UI/Quests/.");
                return;
            }

            _doc = GetComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.visualTreeAsset = tree;

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            // Attach the stylesheet in code as well as via the UXML <Style src>.
            // The UXML reference can bake in broken if the .uss lacks an import
            // meta at the moment the .uxml is first imported; the code path makes
            // the styling resilient to that. If it's null the panel renders as
            // raw default controls — make that loud instead of silently ugly.
            if (styles != null)
            {
                if (!_root.styleSheets.Contains(styles))
                    _root.styleSheets.Add(styles);
            }
            else
            {
                Debug.LogError("[QuestsWindow] StyleSheet missing at " +
                               "Resources/UI/Quests/QuestsWindow.uss — popup will render unstyled. " +
                               "Reimport the asset (right-click → Reimport).");
            }

            _root.style.flexGrow = 1;

            _overlay      = _root.Q<VisualElement>("overlay");
            _titleLabel   = _root.Q<Label>("title");
            _npcPill      = _root.Q<VisualElement>("npcPill");
            _npcPillName  = _root.Q<Label>("npcPillName");
            _closeBtn     = _root.Q<Button>("closeBtn");
            _tabsBox      = _root.Q<VisualElement>("tabs");
            _body         = _root.Q<ScrollView>("body");

            if (_closeBtn != null)
                _closeBtn.clicked += RequestClose;

            // Click on the dim backdrop (but not the popup itself) closes.
            if (_overlay != null)
                _overlay.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.target == _overlay) RequestClose();
                });

            BuildTabs();
        }

        void BuildTabs()
        {
            if (_tabsBox == null) return;

            _tabAvailable = MakeTab("Available", QuestTab.Available);
            _tabActive    = MakeTab("Active",    QuestTab.Active);
            _tabCompleted = MakeTab("Completed", QuestTab.Completed);

            _tabsBox.Add(_tabAvailable);
            _tabsBox.Add(_tabActive);
            _tabsBox.Add(_tabCompleted);
        }

        Button MakeTab(string label, QuestTab tab)
        {
            var btn = new Button { text = label };
            btn.AddToClassList("qp-tab");
            btn.clicked += () => SelectTab(tab);
            return btn;
        }

        // ── Public API ────────────────────────────────────────

        public void OpenJournal()
        {
            _mode = QuestPopupMode.Journal;
            _npcId = null;
            if (_titleLabel != null) _titleLabel.text = "Quests";
            if (_npcPill != null) _npcPill.style.display = DisplayStyle.None;
            // No "Available" tab in the journal — those are offered at NPCs.
            if (_tabAvailable != null) _tabAvailable.style.display = DisplayStyle.None;
            Show();
            SelectTab(QuestTab.Active);
        }

        public void OpenForNpc(string npcId, string npcDisplayName)
        {
            _mode = QuestPopupMode.Npc;
            _npcId = npcId;
            if (_titleLabel != null) _titleLabel.text = "Quests";
            if (_npcPill != null)
            {
                _npcPill.style.display = DisplayStyle.Flex;
                if (_npcPillName != null)
                    _npcPillName.text = string.IsNullOrEmpty(npcDisplayName) ? "NPC" : npcDisplayName;
            }
            if (_tabAvailable != null) _tabAvailable.style.display = DisplayStyle.Flex;
            Show();
            SelectTab(QuestTab.Available);
        }

        public void RequestClose()
        {
            HideImmediate();
            Closed?.Invoke();
        }

        // ── Visibility ────────────────────────────────────────

        void Show()
        {
            if (_root == null) return;
            _isVisible = true;
            _root.style.display = DisplayStyle.Flex;
            if (_overlay != null) _overlay.AddToClassList("qp-overlay--visible");
        }

        void HideImmediate()
        {
            _isVisible = false;
            if (_root != null) _root.style.display = DisplayStyle.None;
            if (_overlay != null) _overlay.RemoveFromClassList("qp-overlay--visible");
        }

        // ── Tabs ──────────────────────────────────────────────

        void SelectTab(QuestTab tab)
        {
            _selectedTab = tab;
            SetTabSelected(_tabAvailable, tab == QuestTab.Available);
            SetTabSelected(_tabActive,    tab == QuestTab.Active);
            SetTabSelected(_tabCompleted, tab == QuestTab.Completed);
            Refresh();
        }

        static void SetTabSelected(Button tab, bool selected)
        {
            if (tab == null) return;
            if (selected) tab.AddToClassList("qp-tab--active");
            else tab.RemoveFromClassList("qp-tab--active");
        }

        // ── Content ───────────────────────────────────────────

        public void Refresh()
        {
            if (_body == null) return;
            _body.Clear();

            if (!App.IsInitialized) return;
            var app = App.Instance;
            var db = app.QuestDatabase;
            var progress = app.Player?.QuestProgress;
            if (db == null || progress == null) return;

            int level = app.Player.ProfileState?.Level ?? 0;
            var quests = GetQuestsForCurrentTab(db, progress, level);

            if (quests.Count == 0)
            {
                _body.Add(BuildEmptyState());
                return;
            }

            for (int i = 0; i < quests.Count; i++)
            {
                // First card opens by default so the popup never reads as a flat list.
                _body.Add(BuildCard(quests[i], progress, expanded: i == 0));
            }
        }

        List<QuestDefinition> GetQuestsForCurrentTab(
            QuestDatabase db, QuestProgressState progress, int playerLevel)
        {
            switch (_selectedTab)
            {
                case QuestTab.Available:
                    return QuestSystem.GetAvailableQuests(progress, db, playerLevel, _npcId);

                case QuestTab.Active:
                    return _mode == QuestPopupMode.Npc
                        ? QuestSystem.GetActiveQuestsForNpc(progress, db, _npcId)
                        : QuestSystem.GetAllActiveQuests(progress, db);

                case QuestTab.Completed:
                    return _mode == QuestPopupMode.Npc
                        ? QuestSystem.GetCompletedQuestsForNpc(progress, db, _npcId)
                        : QuestSystem.GetAllCompletedQuests(progress, db);

                default:
                    return new List<QuestDefinition>();
            }
        }

        // ── Card building ─────────────────────────────────────

        VisualElement BuildCard(QuestDefinition quest, QuestProgressState progressState, bool expanded)
        {
            var p = progressState.GetProgress(quest.Id);
            var status = p?.Status ?? QuestStatus.NotStarted;

            var card = new VisualElement();
            card.AddToClassList("qp-card");
            if (status == QuestStatus.Active) card.AddToClassList("qp-card--active");

            // ── Header (click toggles expand) ──
            var header = new VisualElement();
            header.AddToClassList("qp-card-hdr");

            var left = new VisualElement();
            left.AddToClassList("qp-card-hdr-left");

            var icon = new VisualElement();
            icon.AddToClassList("qp-icon");
            icon.AddToClassList(status switch
            {
                QuestStatus.Active    => "qp-icon--active",
                QuestStatus.Completed => "qp-icon--done",
                _                     => "qp-icon--available",
            });
            left.Add(icon);

            var name = new Label(quest.DisplayName);
            name.AddToClassList("qp-name");
            left.Add(name);
            header.Add(left);

            var right = new VisualElement();
            right.AddToClassList("qp-card-hdr-right");

            // In the journal the cards aren't grouped by NPC, so tag each one.
            if (_mode == QuestPopupMode.Journal && !string.IsNullOrEmpty(quest.NpcId))
            {
                var npcTag = new VisualElement();
                npcTag.AddToClassList("qp-npc-tag");
                var dot = new VisualElement();
                dot.AddToClassList("qp-npc-dot");
                npcTag.Add(dot);
                npcTag.Add(new Label(quest.NpcId));
                right.Add(npcTag);
            }

            var badge = new Label(BadgeText(status));
            badge.AddToClassList("qp-badge");
            badge.AddToClassList(status switch
            {
                QuestStatus.Active    => "qp-badge--active",
                QuestStatus.Completed => "qp-badge--done",
                _                     => "qp-badge--new",
            });
            right.Add(badge);

            var chevron = new Label("▾"); // ▾
            chevron.AddToClassList("qp-chevron");
            right.Add(chevron);
            header.Add(right);

            card.Add(header);

            // ── Body ──
            var body = BuildCardBody(quest, p, status);
            card.Add(body);

            // Expand toggle.
            void SetExpanded(bool open)
            {
                body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = open ? "▴" : "▾"; // ▴ / ▾
                if (open) card.AddToClassList("qp-card--open");
                else card.RemoveFromClassList("qp-card--open");
            }
            SetExpanded(expanded);
            header.RegisterCallback<PointerDownEvent>(_ =>
                SetExpanded(body.style.display == DisplayStyle.None));

            return card;
        }

        VisualElement BuildCardBody(QuestDefinition quest, QuestProgress p, QuestStatus status)
        {
            var body = new VisualElement();
            body.AddToClassList("qp-card-body");

            if (!string.IsNullOrEmpty(quest.Description))
            {
                var desc = new Label(quest.Description);
                desc.AddToClassList("qp-desc");
                body.Add(desc);
            }

            // Objectives.
            if (quest.Tasks != null && quest.Tasks.Count > 0)
            {
                var objList = new VisualElement();
                objList.AddToClassList("qp-obj-list");

                int doneCount = 0;
                for (int i = 0; i < quest.Tasks.Count; i++)
                {
                    var task = quest.Tasks[i];
                    int current = p != null && i < p.Tasks.Count ? p.Tasks[i].CurrentCount : 0;
                    int required = Mathf.Max(1, task.RequiredCount);
                    bool done = current >= required;
                    if (done) doneCount++;

                    objList.Add(BuildObjectiveRow(task.Description, current, required, done));
                }
                body.Add(objList);

                // Progress bar — only meaningful once the quest is in progress.
                if (status != QuestStatus.NotStarted)
                    body.Add(BuildProgressRow(doneCount, quest.Tasks.Count));
            }

            // Rewards.
            if (quest.Rewards != null && quest.Rewards.Count > 0)
                body.Add(BuildRewardsRow(quest.Rewards));

            // Actions.
            var actions = BuildActions(quest, p, status);
            if (actions != null) body.Add(actions);

            return body;
        }

        static VisualElement BuildObjectiveRow(string text, int current, int required, bool done)
        {
            var row = new VisualElement();
            row.AddToClassList("qp-obj-row");

            var check = new VisualElement();
            check.AddToClassList("qp-obj-check");
            if (done) check.AddToClassList("qp-obj-check--done");
            row.Add(check);

            var label = new Label(string.IsNullOrEmpty(text) ? "Objective" : text);
            label.AddToClassList("qp-obj-text");
            if (done) label.AddToClassList("qp-obj-text--done");
            row.Add(label);

            var prog = new Label($"{current}/{required}");
            prog.AddToClassList("qp-obj-progress");
            if (done) prog.AddToClassList("qp-obj-progress--full");
            row.Add(prog);

            return row;
        }

        static VisualElement BuildProgressRow(int done, int total)
        {
            var row = new VisualElement();
            row.AddToClassList("qp-progress-row");

            var track = new VisualElement();
            track.AddToClassList("qp-progress-track");
            var fill = new VisualElement();
            fill.AddToClassList("qp-progress-fill");
            float pct = total > 0 ? (float)done / total * 100f : 0f;
            fill.style.width = Length.Percent(pct);
            if (done >= total && total > 0) fill.AddToClassList("qp-progress-fill--green");
            track.Add(fill);
            row.Add(track);

            var label = new Label($"{done}/{total}");
            label.AddToClassList("qp-progress-label");
            row.Add(label);

            return row;
        }

        static VisualElement BuildRewardsRow(List<QuestReward> rewards)
        {
            var row = new VisualElement();
            row.AddToClassList("qp-rewards-row");

            var label = new Label("Rewards");
            label.AddToClassList("qp-rewards-label");
            row.Add(label);

            foreach (var reward in rewards)
            {
                var def = ItemDefinition.Get(reward.ItemId);
                string itemName = def?.DisplayName ?? reward.ItemId;
                string chipText = reward.Count > 1 ? $"{itemName} ×{reward.Count}" : itemName;

                var chip = new VisualElement();
                chip.AddToClassList("qp-reward-chip");
                var dot = new VisualElement();
                dot.AddToClassList("qp-reward-dot");
                chip.Add(dot);
                chip.Add(new Label(chipText));
                row.Add(chip);
            }

            return row;
        }

        VisualElement BuildActions(QuestDefinition quest, QuestProgress p, QuestStatus status)
        {
            bool showAccept = status == QuestStatus.NotStarted;
            bool canClaim = p != null && QuestSystem.AreAllTasksDone(quest, p);
            bool showClaim = _mode == QuestPopupMode.Npc
                             && status == QuestStatus.Active && canClaim;

            if (!showAccept && !showClaim) return null;

            var actions = new VisualElement();
            actions.AddToClassList("qp-actions");

            if (showAccept)
            {
                var accept = new Button { text = "Accept" };
                accept.AddToClassList("qp-btn");
                accept.AddToClassList("qp-btn--primary");
                accept.clicked += () =>
                {
                    QuestSystem.TryAccept(App.Instance?.Player?.QuestProgress, quest, App.Instance?.Player);
                    Refresh();
                };
                actions.Add(accept);
            }

            if (showClaim)
            {
                var claim = new Button { text = "Claim Reward" };
                claim.AddToClassList("qp-btn");
                claim.AddToClassList("qp-btn--primary");
                claim.clicked += () =>
                {
                    var session = App.Instance?.RaidSession;
                    if (session == null) return;
                    QuestSystem.TryCompleteAndGrantRewards(
                        App.Instance.Player.QuestProgress, quest,
                        session.RaidState, App.Instance.Player.Inventory);
                    Refresh();
                };
                actions.Add(claim);
            }

            return actions;
        }

        static VisualElement BuildEmptyState()
        {
            var empty = new VisualElement();
            empty.AddToClassList("qp-empty");
            var icon = new VisualElement();
            icon.AddToClassList("qp-empty-icon");
            empty.Add(icon);
            var text = new Label("No quests here");
            text.AddToClassList("qp-empty-text");
            empty.Add(text);
            return empty;
        }

        static string BadgeText(QuestStatus status) => status switch
        {
            QuestStatus.Active    => "Active",
            QuestStatus.Completed => "Done",
            _                     => "New",
        };
    }
}
