using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace View.UI.Quests
{
    public class QuestItemView : MonoBehaviour
    {
        [SerializeField] private List<GameObject> _availableStateObjects;
        [SerializeField] private List<GameObject> _activeStateObjects;
        [SerializeField] private List<GameObject> _completedStateObjects;
        [SerializeField] private Button _expandButton;
        [SerializeField] private GameObject _expandContent;
        [SerializeField] private Transform _expandImageToFlip;
        [SerializeField] private TMP_Text _npcName;
        [SerializeField] private TMP_Text _questDescription;
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _claimButton;
        [SerializeField] private TaskItemView _taskPrefab;
        [SerializeField] private Transform _taskContainer;
        [SerializeField] private InventorySlotView _rewardPrefab;
        [SerializeField] private Transform _rewardContainer;

    }
}