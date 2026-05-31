using Systems;
using UnityEngine;
using View.UI.Notifications;

namespace View
{
    /// <summary>
    /// Bridges gameplay events to the <see cref="NotificationOverlay"/> banner stack.
    /// v1 wires only quest task progression: <see cref="QuestSystem.TaskCompleted"/>
    /// raises a banner whenever a quest task hits its required count. More event
    /// sources (loot, level-up, raid alerts) plug in here later.
    /// </summary>
    public class NotificationPresenter : MonoBehaviour
    {
        void OnEnable()
        {
            QuestSystem.TaskCompleted += OnQuestTaskCompleted;
        }

        void OnDisable()
        {
            QuestSystem.TaskCompleted -= OnQuestTaskCompleted;
        }

        void OnQuestTaskCompleted(QuestSystem.QuestTaskCompletion c)
        {
            var overlay = NotificationOverlay.Instance;
            if (overlay == null) return;

            string title = string.IsNullOrEmpty(c.TaskDescription)
                ? (string.IsNullOrEmpty(c.QuestDisplayName) ? "Objective complete" : c.QuestDisplayName)
                : c.TaskDescription;

            string kicker = c.QuestReady ? "Quest ready" : "Objective complete";

            string desc = c.QuestReady
                ? $"{c.QuestDisplayName} — claim your reward."
                : c.QuestDisplayName;

            overlay.Push(NotificationKind.Success, kicker, title, desc);
        }
    }
}
