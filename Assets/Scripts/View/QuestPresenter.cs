using ApplicationCore;
using State;
using UnityEngine;
using UnityEngine.InputSystem;
using View.UI;
using View.UI.Quests;

namespace View
{
    public class QuestPresenter : MonoBehaviour
    {
        PopupManager _popupManager;
        QuestsPopupView _questsPopupView;
        bool _triedFind;

        EId _lastNpcTargetId;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;

            if (player == null) return;

            bool popupOpen = HasPopup() && _popupManager.IsOpen(_questsPopupView);

            // Key.I toggles journal
            var kb = Keyboard.current;
            if (kb != null && kb[Key.I].wasPressedThisFrame)
            {
                if (popupOpen)
                    _questsPopupView.RequestClose();
                else if (!player.IsInMenu && HasPopup())
                {
                    _popupManager.Open(_questsPopupView);
                    _questsPopupView.OpenJournal();
                }
            }

            // NPC interaction: open/close popup when NpcTargetId changes
            if (player.NpcTargetId != _lastNpcTargetId)
            {
                if (_lastNpcTargetId != EId.None)
                    CloseNpcPopup();

                _lastNpcTargetId = player.NpcTargetId;

                if (player.NpcTargetId != EId.None)
                    OpenNpcPopup(session.RaidState, player.NpcTargetId);
            }

            popupOpen = HasPopup() && _popupManager.IsOpen(_questsPopupView);
            player.IsQuestLogOpen = popupOpen;

            if (popupOpen)
                App.Instance.SetGameplayInputBlocked(true);
        }

        void OpenNpcPopup(RaidState state, EId npcTargetId)
        {
            if (!HasPopup()) return;

            var npcState = FindNpcState(state, npcTargetId);
            if (npcState == null) return;

            string npcId = npcState.NpcId;
            string displayName = string.IsNullOrEmpty(npcId) ? "NPC" : npcId;

            _popupManager.Open(_questsPopupView);
            _questsPopupView.OpenForNpc(npcId, displayName);
        }

        void CloseNpcPopup()
        {
            if (HasPopup() && _popupManager.IsOpen(_questsPopupView))
                _popupManager.Close();
        }

        bool HasPopup()
        {
            if (!_triedFind)
            {
                _triedFind = true;
                _popupManager = FindObjectOfType<PopupManager>(includeInactive: true);
                _questsPopupView = FindObjectOfType<QuestsPopupView>(includeInactive: true);

                if (_questsPopupView != null)
                    _questsPopupView.Closed += OnPopupClosed;
            }
            return _popupManager != null && _questsPopupView != null;
        }

        void OnPopupClosed()
        {
            _popupManager?.Close();
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player != null)
            {
                // If closed while in NPC mode, clear the target so the game knows we left
                if (player.NpcTargetId != EId.None)
                {
                    player.NpcTargetId = EId.None;
                    _lastNpcTargetId = EId.None;
                }
                player.IsQuestLogOpen = false;
                App.Instance.SetGameplayInputBlocked(false);
            }
        }

        static NpcState FindNpcState(RaidState state, EId npcTargetId)
        {
            for (int i = 0; i < state.Npcs.Count; i++)
                if (state.Npcs[i].Id == npcTargetId)
                    return state.Npcs[i];
            return null;
        }

        void OnDestroy()
        {
            if (_questsPopupView != null)
                _questsPopupView.Closed -= OnPopupClosed;
        }
    }
}
