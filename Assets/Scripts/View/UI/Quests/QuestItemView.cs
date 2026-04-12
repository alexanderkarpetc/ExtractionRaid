using System;
using System.Collections.Generic;
using Quests;
using State;
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

        bool _expanded;

        public void Setup(QuestDefinition quest, QuestProgress progress, QuestStatus status,
            Action onAccept, Action onClaim)
        {
            SetStateObjects(_availableStateObjects, status == QuestStatus.NotStarted);
            SetStateObjects(_activeStateObjects, status == QuestStatus.Active);
            SetStateObjects(_completedStateObjects, status == QuestStatus.Completed);

            if (_npcName != null)
                _npcName.text = quest.DisplayName;

            if (_questDescription != null)
                _questDescription.text = quest.Description;

            SetupTasks(quest, progress, status);
            SetupRewards(quest);
            SetupButtons(status, onAccept, onClaim);
            SetupExpand();
        }

        void SetupTasks(QuestDefinition quest, QuestProgress progress, QuestStatus status)
        {
            ClearContainer(_taskContainer);
            if (_taskPrefab == null || _taskContainer == null) return;
            if (quest.Tasks == null || status == QuestStatus.NotStarted) return;

            for (int i = 0; i < quest.Tasks.Count; i++)
            {
                var task = quest.Tasks[i];
                var tp = progress != null && i < progress.Tasks.Count ? progress.Tasks[i] : null;
                var view = Instantiate(_taskPrefab, _taskContainer);
                view.Setup(task, tp);
            }
        }

        void SetupRewards(QuestDefinition quest)
        {
            ClearContainer(_rewardContainer);
            if (_rewardPrefab == null || _rewardContainer == null) return;
            if (quest.Rewards == null) return;

            foreach (var reward in quest.Rewards)
            {
                var def = ItemDefinition.Get(reward.ItemId);
                if (def == null) continue;

                var slot = Instantiate(_rewardPrefab, _rewardContainer);
                var dummyRef = new InventorySlotRef(SlotType.Backpack, -1);
                var item = new ItemState
                {
                    DefinitionId = reward.ItemId,
                    StackCount = reward.Count,
                };
                slot.Bind(dummyRef, item, false, -1);
            }
        }

        void SetupButtons(QuestStatus status, Action onAccept, Action onClaim)
        {
            if (_acceptButton != null)
            {
                _acceptButton.gameObject.SetActive(status == QuestStatus.NotStarted);
                _acceptButton.onClick.RemoveAllListeners();
                if (onAccept != null)
                    _acceptButton.onClick.AddListener(() => onAccept());
            }

            if (_claimButton != null)
            {
                _claimButton.gameObject.SetActive(status == QuestStatus.Active);
                _claimButton.onClick.RemoveAllListeners();
                if (onClaim != null)
                    _claimButton.onClick.AddListener(() => onClaim());
            }
        }

        void SetupExpand()
        {
            _expanded = false;
            if (_expandContent != null)
                _expandContent.SetActive(false);
            UpdateExpandArrow();

            if (_expandButton != null)
            {
                _expandButton.onClick.RemoveAllListeners();
                _expandButton.onClick.AddListener(ToggleExpand);
            }
        }

        void ToggleExpand()
        {
            _expanded = !_expanded;
            if (_expandContent != null)
                _expandContent.SetActive(_expanded);
            UpdateExpandArrow();
        }

        void UpdateExpandArrow()
        {
            if (_expandImageToFlip == null) return;
            _expandImageToFlip.localScale = new Vector3(1f, _expanded ? -1f : 1f, 1f);
        }

        static void SetStateObjects(List<GameObject> objects, bool active)
        {
            if (objects == null) return;
            foreach (var obj in objects)
                if (obj != null) obj.SetActive(active);
        }

        static void ClearContainer(Transform container)
        {
            if (container == null) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }
    }
}