using System.Collections.Generic;
using ApplicationCore;
using State;
using Systems;
using UnityEngine;
using View.UI;
using View.UI.Dialogue;
using View.UI.Quests;

namespace View
{
    /// <summary>
    /// Bridges NPC interaction state (player.NpcTargetId) to the UI Toolkit
    /// dialogue window. Drives the dialogue flow:
    ///   * NpcTargetId set     → show dialogue with the NPC's choices
    ///   * "Open Quests"       → hand control to QuestsPopupView for that NPC
    ///   * QuestsPopup closed  → return to dialogue (NPC still targeted)
    ///   * "Exit" / NpcTargetId cleared → hide dialogue
    /// </summary>
    public class NpcDialoguePresenter : MonoBehaviour
    {
        NpcDialogueWindow _window;
        PopupManager _popupManager;
        QuestsPopupView _questsPopupView;
        bool _triedFind;

        EId _lastNpcTargetId = EId.None;

        // True while we explicitly opened the quest popup from the dialogue. Used to
        // distinguish "user closed quest popup → go back to dialogue" from the
        // journal-mode close path (which QuestPresenter owns).
        bool _expectingQuestPopupReturn;

        void Update()
        {
            var session = App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;
            if (player == null) return;
            if (!EnsureRefs()) return;

            if (player.NpcTargetId != _lastNpcTargetId)
            {
                _lastNpcTargetId = player.NpcTargetId;

                if (player.NpcTargetId != EId.None)
                    OpenDialogueFor(session.RaidState, player.NpcTargetId);
                else
                    CloseEverything();
            }

            // Keep gameplay input blocked while either UI is up.
            if (_window != null && _window.IsVisible)
                App.Instance.SetGameplayInputBlocked(true);
        }

        bool EnsureRefs()
        {
            if (_triedFind) return _window != null;
            _triedFind = true;

            _window = NpcDialogueWindow.Instance
                      ?? FindObjectOfType<NpcDialogueWindow>(includeInactive: true);
            _popupManager = FindObjectOfType<PopupManager>(includeInactive: true);
            _questsPopupView = FindObjectOfType<QuestsPopupView>(includeInactive: true);

            if (_questsPopupView != null)
                _questsPopupView.Closed += OnQuestsPopupClosed;

            return _window != null;
        }

        void OpenDialogueFor(RaidState state, EId npcId)
        {
            var npc = FindNpc(state, npcId);
            if (npc == null) return;

            string displayName = string.IsNullOrEmpty(npc.NpcId) ? "NPC" : npc.NpcId;
            string introLine = GetIntroLineFor(npc.NpcId);
            ShowDialogue(displayName, npc.NpcId, introLine);
        }

        void ShowDialogue(string displayName, string npcId, string introLine)
        {
            var choices = new List<NpcDialogueWindow.Choice>();

            // Hand-over choices: one per active transfer-style task on this NPC's quests
            // for which the player has at least one matching item. Inserted before the
            // generic actions so they read as the "obvious thing to do right now".
            var app = App.Instance;
            var inventory = app?.Player?.Inventory;
            var progress = app?.Player?.QuestProgress;
            var db = app?.QuestDatabase;
            if (inventory != null && progress != null && db != null)
            {
                var ops = QuestSystem.GetHandoverOpportunities(progress, db, inventory, npcId);
                foreach (var op in ops)
                {
                    var def = ItemDefinition.Get(op.ItemId);
                    string itemName = def?.DisplayName ?? op.ItemId;

                    int current = 0;
                    var p = progress.GetProgress(op.QuestId);
                    if (p != null && op.TaskIndex < p.Tasks.Count)
                        current = p.Tasks[op.TaskIndex].CurrentCount;
                    int required = current + op.RequiredRemaining;

                    string label = $"Hand over {op.DeliverableNow}× {itemName}  ({current}/{required})";

                    var captured = op;
                    string capturedNpcId = npcId;
                    string capturedDisplayName = displayName;
                    string capturedIntro = introLine;
                    choices.Add(new NpcDialogueWindow.Choice
                    {
                        Label = label,
                        OnClick = () => OnHandoverClicked(captured, capturedNpcId, capturedDisplayName, capturedIntro),
                    });
                }
            }

            choices.Add(new NpcDialogueWindow.Choice
            {
                Label = "Open Quests",
                OnClick = () => OpenQuests(npcId, displayName),
            });
            choices.Add(new NpcDialogueWindow.Choice
            {
                Label = "Exit",
                OnClick = ExitDialogue,
            });

            _window.Show(displayName, introLine, choices);
        }

        void OnHandoverClicked(QuestSystem.HandoverOpportunity op, string npcId,
            string displayName, string introLine)
        {
            var app = App.Instance;
            var inventory = app?.Player?.Inventory;
            var progress = app?.Player?.QuestProgress;
            var db = app?.QuestDatabase;
            if (inventory == null || progress == null || db == null) return;

            int delivered = QuestSystem.HandOver(progress, db, inventory, op);
            if (delivered <= 0) return;

            var def = ItemDefinition.Get(op.ItemId);
            Debug.Log($"[NpcDialogue] Handed over {delivered}× {def?.DisplayName ?? op.ItemId} for quest '{op.QuestId}'.");

            // Re-render dialogue so the choice list reflects new task progress / inventory.
            ShowDialogue(displayName, npcId, introLine);
        }

        void OpenQuests(string npcId, string displayName)
        {
            if (_popupManager == null || _questsPopupView == null) return;

            _expectingQuestPopupReturn = true;
            _window.Hide();
            _popupManager.Open(_questsPopupView);
            _questsPopupView.OpenForNpc(npcId, displayName);
        }

        void OnQuestsPopupClosed()
        {
            if (!_expectingQuestPopupReturn) return;
            _expectingQuestPopupReturn = false;

            // If player still has the NPC targeted, hand control back to dialogue.
            // Otherwise the NpcTargetId watcher will tear everything down next frame.
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player == null || player.NpcTargetId == EId.None) return;

            OpenDialogueFor(App.Instance.RaidSession.RaidState, player.NpcTargetId);
        }

        void ExitDialogue()
        {
            var player = App.Instance?.RaidSession?.RaidState?.PlayerEntity;
            if (player == null) return;
            player.NpcTargetId = EId.None; // triggers CloseEverything next Update
        }

        void CloseEverything()
        {
            _expectingQuestPopupReturn = false;
            if (_window != null) _window.Hide();
            if (_popupManager != null && _questsPopupView != null && _popupManager.IsOpen(_questsPopupView))
                _popupManager.Close();
            App.Instance?.SetGameplayInputBlocked(false);
        }

        static NpcState FindNpc(RaidState state, EId id)
        {
            for (int i = 0; i < state.Npcs.Count; i++)
                if (state.Npcs[i].Id == id) return state.Npcs[i];
            return null;
        }

        // Stub greeting until per-NPC dialogue data lands. Lookup by NpcId so
        // designers can add lines without touching the presenter; the empty-fallback
        // keeps the window from looking broken if an NPC isn't listed.
        static string GetIntroLineFor(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return "...";
            return _intros.TryGetValue(npcId, out var line)
                ? line
                : "What do you need?";
        }

        static readonly Dictionary<string, string> _intros = new()
        {
            { "Mechanic", "Hey. Headed to the lower sector? Keep it quiet — turrets there wake up faster than you can blink." },
            { "Trader",   "Browse what I've got, or get out of the way." },
        };

        void OnDestroy()
        {
            if (_questsPopupView != null)
                _questsPopupView.Closed -= OnQuestsPopupClosed;
        }
    }
}
