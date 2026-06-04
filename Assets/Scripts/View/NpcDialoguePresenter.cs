using System.Collections.Generic;
using ApplicationCore;
using Constants;
using State;
using Systems;
using UnityEngine;
using View.UI;
using View.UI.Dialogue;
using View.UI.Inventory;
using View.UI.Quests;

namespace View
{
    /// <summary>
    /// Bridges NPC interaction state (player.NpcTargetId) to the UI Toolkit
    /// dialogue window. Drives the dialogue flow:
    ///   * NpcTargetId set     → show dialogue with the NPC's choices
    ///   * "Open Quests"       → hand control to QuestsWindow for that NPC
    ///   * QuestsPopup closed  → return to dialogue (NPC still targeted)
    ///   * "Exit" / NpcTargetId cleared → hide dialogue
    /// </summary>
    public class NpcDialoguePresenter : MonoBehaviour
    {
        NpcDialogueWindow _window;
        QuestsWindow _questsWindow;
        bool _triedFind;

        EId _lastNpcTargetId = EId.None;

        // True while we explicitly opened the quest popup from the dialogue. Used to
        // distinguish "user closed quest popup → go back to dialogue" from the
        // journal-mode close path (which QuestPresenter owns).
        bool _expectingQuestPopupReturn;

        // NpcId whose shop is currently spawned in state.Lootables. Tracked here so
        // CloseEverything can despawn cleanly without needing to re-resolve which NPC
        // we were talking to (NpcTargetId is already gone by then).
        string _activeShopOwnerId;

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

            // Shop session lifecycle: when the player closes the inventory window
            // (Esc / Tab) while a shop is up, despawn the shop and bring the
            // dialogue back. NpcTargetId is still set, so this is the same return
            // path as the quest popup.
            if (!string.IsNullOrEmpty(_activeShopOwnerId)
                && (InventoryWindow.Instance == null || !InventoryWindow.Instance.IsOpen))
            {
                ShopSystem.CloseShopFor(session.RaidState, _activeShopOwnerId);
                _activeShopOwnerId = null;
                player.LootTargetId = EId.None;
                if (player.NpcTargetId != EId.None)
                    OpenDialogueFor(session.RaidState, player.NpcTargetId);
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
            _questsWindow = QuestsWindow.Instance;

            if (_questsWindow != null)
                _questsWindow.Closed += OnQuestsPopupClosed;

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
                var stash = app.Player.Stash;
                var ops = QuestSystem.GetHandoverOpportunities(progress, db, inventory, npcId, stash);
                foreach (var op in ops)
                {
                    var def = ItemDefinition.Get(op.ItemId);
                    string itemName = def?.DisplayName ?? op.ItemId;

                    // Y/X is raw "have/need" — even when the player is over-stocked
                    // (e.g. 10/3) the full count is shown so they know how much they're
                    // carrying. Click is gated until have ≥ need.
                    string label = $"Hand over {itemName} ({op.Available}/{op.RequiredRemaining})";

                    bool canDeliver = op.Available >= op.RequiredRemaining;

                    var captured = op;
                    string capturedNpcId = npcId;
                    string capturedDisplayName = displayName;
                    string capturedIntro = introLine;
                    choices.Add(new NpcDialogueWindow.Choice
                    {
                        Label = label,
                        EnabledOverride = canDeliver,
                        OnClick = () => OnHandoverClicked(captured, capturedNpcId, capturedDisplayName, capturedIntro),
                    });
                }
            }

            // Trade — only offered if a ShopDefinitionAsset exists for this NpcId.
            // Assets live at Resources/Configs/Shops/<NpcId>.asset; see ShopDefinitionAsset.
            var shopDef = LoadShopDefinition(npcId);
            if (shopDef != null && IsShopUnlocked(shopDef, progress))
            {
                string capturedNpcId = npcId;
                string capturedDisplayName = displayName;
                choices.Add(new NpcDialogueWindow.Choice
                {
                    Label = "Trade",
                    OnClick = () => OpenShop(shopDef, capturedNpcId, capturedDisplayName),
                });
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

            int delivered = QuestSystem.HandOver(progress, db, inventory, op, app.Player.Stash);
            if (delivered <= 0) return;

            var def = ItemDefinition.Get(op.ItemId);
            Debug.Log($"[NpcDialogue] Handed over {delivered}× {def?.DisplayName ?? op.ItemId} for quest '{op.QuestId}'.");

            // Re-render dialogue so the choice list reflects new task progress / inventory.
            ShowDialogue(displayName, npcId, introLine);
        }

        void OpenShop(ShopDefinitionAsset shopDef, string npcId, string displayName)
        {
            var app = App.Instance;
            var session = app?.RaidSession;
            var state = session?.RaidState;
            var npc = state != null ? FindNpcByNpcId(state, npcId) : null;
            if (state == null || npc == null) return;

            var shop = ShopSystem.OpenShopFor(state, npc, shopDef);
            if (shop == null) return;
            _activeShopOwnerId = npcId;

            // Route through the LootTargetId open-reason so InventoryUI keeps the
            // window open. Without this, InventoryUI.Update would close it next
            // frame because none of its open-reason flags would be set.
            state.PlayerEntity.LootTargetId = shop.Id;

            _window.Hide();
        }

        void OpenQuests(string npcId, string displayName)
        {
            if (_questsWindow == null) return;

            _expectingQuestPopupReturn = true;
            _window.Hide();
            _questsWindow.OpenForNpc(npcId, displayName);
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
            if (_questsWindow != null && _questsWindow.IsOpen)
                _questsWindow.RequestClose();

            // Despawn the active shop (if any). Shop stock is per-trade-session — next
            // dialogue opens a freshly rolled shop.
            if (!string.IsNullOrEmpty(_activeShopOwnerId))
            {
                var state = App.Instance?.RaidSession?.RaidState;
                if (state != null)
                {
                    ShopSystem.CloseShopFor(state, _activeShopOwnerId);
                    if (state.PlayerEntity != null) state.PlayerEntity.LootTargetId = EId.None;
                }
                _activeShopOwnerId = null;
            }

            App.Instance?.SetGameplayInputBlocked(false);
        }

        static NpcState FindNpc(RaidState state, EId id)
        {
            for (int i = 0; i < state.Npcs.Count; i++)
                if (state.Npcs[i].Id == id) return state.Npcs[i];
            return null;
        }

        static NpcState FindNpcByNpcId(RaidState state, string npcId)
        {
            for (int i = 0; i < state.Npcs.Count; i++)
                if (state.Npcs[i].NpcId == npcId) return state.Npcs[i];
            return null;
        }

        // A shop with no RequiredQuestId is always open; otherwise the Trade option
        // stays hidden until that quest is Completed.
        static bool IsShopUnlocked(ShopDefinitionAsset shopDef, QuestProgressState progress)
        {
            if (string.IsNullOrEmpty(shopDef.RequiredQuestId)) return true;
            return progress != null
                   && progress.GetStatus(shopDef.RequiredQuestId) == QuestStatus.Completed;
        }

        // ShopDefinitionAsset lookup. Cached per-NpcId so repeated dialogue opens
        // don't pay the Resources.Load cost. A null cache entry means "we already
        // looked and there's no shop for this NPC" — distinct from "not yet checked".
        static readonly Dictionary<string, ShopDefinitionAsset> _shopDefCache = new();

        static ShopDefinitionAsset LoadShopDefinition(string npcId)
        {
            if (string.IsNullOrEmpty(npcId)) return null;
            if (_shopDefCache.TryGetValue(npcId, out var cached)) return cached;

            // Match by OwnerNpcId field — decouples filename from lookup.
            ShopDefinitionAsset found = null;
            var all = Resources.LoadAll<ShopDefinitionAsset>("Configs/Shops");
            foreach (var a in all)
            {
                if (a != null && a.OwnerNpcId == npcId) { found = a; break; }
            }
            _shopDefCache[npcId] = found;
            return found;
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
            if (_questsWindow != null)
                _questsWindow.Closed -= OnQuestsPopupClosed;
        }
    }
}
