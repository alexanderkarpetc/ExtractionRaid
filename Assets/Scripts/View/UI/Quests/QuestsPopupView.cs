using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Quests
{
    public class QuestsPopupView : PopupBase
    {
        [SerializeField] private TMP_Text _headerText; // npc name or "Quests"
        [SerializeField] private Button _availableTabButton;
        [SerializeField] private Button _activeTabButton;
        [SerializeField] private Button _completedTabButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Transform _availableContent;
        [SerializeField] private QuestItemView _questItemViewPrefab;
    }
}