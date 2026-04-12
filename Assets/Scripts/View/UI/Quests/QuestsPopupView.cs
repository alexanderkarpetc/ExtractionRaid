using System;
using System.Collections.Generic;
using ApplicationCore;
using Quests;
using State;
using Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Quests
{
    public enum QuestPopupMode { Journal, Npc }

    public enum QuestTab { Available, Active, Completed }

    public class QuestsPopupView : PopupBase
    {
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TabItemView _availableTabButton;
        [SerializeField] private TabItemView _activeTabButton;
        [SerializeField] private TabItemView _completedTabButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Transform _availableContent;
        [SerializeField] private QuestItemView _questItemViewPrefab;

        QuestPopupMode _mode;
        QuestTab _selectedTab;
        string _npcId;

        public event Action Closed;

        protected override void Awake()
        {
            base.Awake();

            _availableTabButton.Button.onClick.AddListener(() => SelectTab(QuestTab.Available));
            _activeTabButton.Button.onClick.AddListener(() => SelectTab(QuestTab.Active));
            _completedTabButton.Button.onClick.AddListener(() => SelectTab(QuestTab.Completed));
            _closeButton.onClick.AddListener(RequestClose);
        }

        public void OpenJournal()
        {
            _mode = QuestPopupMode.Journal;
            _npcId = null;
            _headerText.text = "Quests";
            _availableTabButton.gameObject.SetActive(false);
            SelectTab(QuestTab.Active);
            Show();
        }

        public void OpenForNpc(string npcId, string npcDisplayName)
        {
            _mode = QuestPopupMode.Npc;
            _npcId = npcId;
            _headerText.text = string.IsNullOrEmpty(npcDisplayName) ? "NPC" : npcDisplayName;
            _availableTabButton.gameObject.SetActive(true);
            SelectTab(QuestTab.Available);
            Show();
        }

        public void RequestClose()
        {
            Hide();
            Closed?.Invoke();
        }

        void SelectTab(QuestTab tab)
        {
            _selectedTab = tab;
            SetTabVisual(_availableTabButton, tab == QuestTab.Available);
            SetTabVisual(_activeTabButton, tab == QuestTab.Active);
            SetTabVisual(_completedTabButton, tab == QuestTab.Completed);
            Refresh();
        }

        static void SetTabVisual(TabItemView tab, bool selected)
        {
            tab?.SetSelected(selected);
        }

        public void Refresh()
        {
            ClearContent();

            if (!App.IsInitialized) return;
            var app = App.Instance;
            var db = app.QuestDatabase;
            var progress = app.Player.QuestProgress;
            if (db == null || progress == null) return;

            var quests = GetQuestsForCurrentTab(db, progress, app.Player.ProfileState.Level);
            foreach (var quest in quests)
                CreateQuestItem(quest, progress, db);
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

        void CreateQuestItem(QuestDefinition quest, QuestProgressState progress, QuestDatabase db)
        {
            if (_questItemViewPrefab == null || _availableContent == null) return;

            var p = progress.GetProgress(quest.Id);
            var status = p?.Status ?? QuestStatus.NotStarted;

            var item = Instantiate(_questItemViewPrefab, _availableContent);
            item.Setup(quest, p, status,
                onAccept: () =>
                {
                    QuestSystem.TryAccept(progress, quest);
                    Refresh();
                },
                onClaim: () =>
                {
                    if (!QuestSystem.AreAllTasksDone(quest, p)) return;
                    var session = App.Instance?.RaidSession;
                    if (session == null) return;
                    QuestSystem.TryCompleteAndGrantRewards(
                        progress, quest, session.RaidState, App.Instance.Player.Inventory);
                    Refresh();
                });
        }

        void ClearContent()
        {
            if (_availableContent == null) return;
            for (int i = _availableContent.childCount - 1; i >= 0; i--)
                Destroy(_availableContent.GetChild(i).gameObject);
        }
    }
}