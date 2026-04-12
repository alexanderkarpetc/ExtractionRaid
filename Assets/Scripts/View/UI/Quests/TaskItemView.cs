using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Quests
{
    public class TaskItemView : MonoBehaviour
    {
        [SerializeField] private Toggle _isCompletedToggle;
        [SerializeField] private TMP_Text _taskDescription;
        [SerializeField] private TMP_Text _taskProgress; // format "current/required"
    }
}