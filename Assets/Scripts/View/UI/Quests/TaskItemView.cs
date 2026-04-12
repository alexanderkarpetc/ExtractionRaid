using Quests;
using State;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Quests
{
    public class TaskItemView : MonoBehaviour
    {
        [SerializeField] private Toggle _isCompletedToggle;
        [SerializeField] private TMP_Text _taskDescription;
        [SerializeField] private TMP_Text _taskProgress;

        public void Setup(QuestTask task, TaskProgress tp)
        {
            int current = tp?.CurrentCount ?? 0;
            int required = task.RequiredCount;
            bool done = current >= required;

            _taskDescription.text = task.Description;
            _taskProgress.text = $"{current}/{required}";
            _isCompletedToggle.isOn = done;
            _isCompletedToggle.interactable = false;
        }
    }
}