using State;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace View
{
    public class InventoryUI : MonoBehaviour
    {
        const int BackpackColumns = 5;
        const float ScreenMargin = 20f;
        const float PanelGap = 10f;

        bool _isOpen;
        bool _openedByLoot;

        Texture2D _slotBg;
        Texture2D _slotHighlight;
        Texture2D _equipBg;
        Texture2D _dragBg;
        Texture2D _panelBg;
        Texture2D _promptBg;
        GUIStyle _slotStyle;
        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        GUIStyle _dropBtnStyle;
        GUIStyle _promptStyle;

        InventorySlotRef? _dragSource;
        bool _dragFromLoot;
        string _dragLabel;

        bool _showContextMenu;
        InventorySlotRef _contextMenuSlot;
        bool _contextMenuFromLoot;
        Vector2 _contextMenuPos;
        Rect _playerPanelRect;
        Rect _lootPanelRect;

        Vector2 _playerScrollPos;
        Vector2 _lootScrollPos;

        void Awake()
        {
            _slotBg = MakeTex(new Color(0.2f, 0.2f, 0.2f, 0.9f));
            _slotHighlight = MakeTex(new Color(0.4f, 0.6f, 0.3f, 0.9f));
            _equipBg = MakeTex(new Color(0.25f, 0.25f, 0.35f, 0.9f));
            _dragBg = MakeTex(new Color(0.5f, 0.5f, 0.2f, 0.85f));
            _panelBg = MakeTex(new Color(0.12f, 0.12f, 0.14f, 0.95f));
            _promptBg = MakeTex(new Color(0.1f, 0.1f, 0.1f, 0.8f));
        }

        void Update()
        {
            var session = App.App.Instance?.RaidSession;
            var player = session?.RaidState?.PlayerEntity;

            var kb = Keyboard.current;
            if (kb != null && kb[Key.Tab].wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    _isOpen = false;
                    _openedByLoot = false;
                    if (player != null)
                        player.LootTargetId = EId.None;
                }
                else
                {
                    _isOpen = true;
                }
            }

            if (player == null) return;

            if (player.LootTargetId != EId.None && !_isOpen)
            {
                _isOpen = true;
                _openedByLoot = true;
            }

            if (player.LootTargetId == EId.None && _openedByLoot)
            {
                _isOpen = false;
                _openedByLoot = false;
            }
        }

        void OnGUI()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;
            var state = session.RaidState;
            if (state == null) return;
            var player = state.PlayerEntity;
            if (player == null) return;

            DrawLootPrompt(state, player);

            if (!_isOpen) return;

            var inventory = state.Inventory;
            if (inventory == null) return;

            EnsureStyles();

            LootableContainerState lootTarget = null;
            if (player.LootTargetId != EId.None)
                lootTarget = LootSystem.GetLootable(state, player.LootTargetId);

            float totalH = Screen.height - ScreenMargin * 2f;
            float panelW;
            if (lootTarget != null)
                panelW = (Screen.width - ScreenMargin * 2f - PanelGap) * 0.5f;
            else
                panelW = (Screen.width - ScreenMargin * 2f) * 0.5f;

            float panelX = ScreenMargin;
            float panelY = ScreenMargin;

            _playerPanelRect = new Rect(panelX, panelY, panelW, totalH);
            GUI.DrawTexture(_playerPanelRect, _panelBg);
            DrawInventoryPanel(_playerPanelRect, "INVENTORY", inventory, false, session, state,
                ref _playerScrollPos);

            if (lootTarget != null)
            {
                float lootX = panelX + panelW + PanelGap;
                _lootPanelRect = new Rect(lootX, panelY, panelW, totalH);
                GUI.DrawTexture(_lootPanelRect, _panelBg);
                string header = $"{lootTarget.TypeId.ToUpper()} LOOT";
                DrawInventoryPanel(_lootPanelRect, header, lootTarget.Inventory, true, session, state,
                    ref _lootScrollPos);
            }
            else
            {
                _lootPanelRect = Rect.zero;
            }

            HandleDrag(session, state, lootTarget);
            DrawContextMenu(session, state, lootTarget);
        }

        void DrawInventoryPanel(Rect panelRect, string title, InventoryState inventory,
            bool isLoot, Session.RaidSession session, RaidState state,
            ref Vector2 scrollPos)
        {
            float padding = panelRect.width * 0.04f;
            float scrollBarW = 16f;
            float availableW = panelRect.width - padding * 2f - scrollBarW;

            const float maxSlotSize = 135f;
            const int equipCount = 4;

            float slotGap = Mathf.Max(3f, availableW * 0.015f);
            float slotSize = (availableW - (BackpackColumns - 1) * slotGap) / BackpackColumns;
            slotSize = Mathf.Min(Mathf.Floor(slotSize), maxSlotSize);

            float headerH = Mathf.Clamp(slotSize * 0.3f, 26f, 44f);
            _headerStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(headerH * 0.85f), 23, 36);
            _slotStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(slotSize * 0.18f), 18, 28);
            _labelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(slotSize * 0.18f), 18, 28);

            float labelH = slotSize * 0.22f;
            int backpackRows = Mathf.CeilToInt((float)InventoryState.BackpackSize / BackpackColumns);
            float contentH = padding
                + headerH + slotGap
                + labelH + 1f + slotSize + slotGap * 2f + headerH * 0.4f
                + headerH + slotGap
                + backpackRows * (slotSize + slotGap)
                + padding;

            var contentRect = new Rect(0f, 0f, panelRect.width - scrollBarW, contentH);
            scrollPos = GUI.BeginScrollView(panelRect, scrollPos, contentRect);

            float curX = padding;
            float curY = padding;

            GUI.Label(new Rect(curX, curY, availableW, headerH), title, _headerStyle);
            curY += headerH + slotGap;

            float equipSpacing = (availableW - slotSize) / Mathf.Max(1, equipCount - 1);
            DrawEquipSlot(inventory, curX, curY, "W1", InventorySlotRef.Weapon(0), slotSize, slotGap, isLoot);
            DrawEquipSlot(inventory, curX + equipSpacing, curY, "W2", InventorySlotRef.Weapon(1), slotSize, slotGap, isLoot);
            DrawEquipSlot(inventory, curX + 2 * equipSpacing, curY, "Helm", InventorySlotRef.Helmet(), slotSize, slotGap, isLoot);
            DrawEquipSlot(inventory, curX + 3 * equipSpacing, curY, "Armor", InventorySlotRef.BodyArmor(), slotSize, slotGap, isLoot);

            curY += slotSize + slotGap * 2f + headerH * 0.4f;

            GUI.Label(new Rect(curX, curY, availableW, headerH), "BACKPACK", _headerStyle);
            curY += headerH + slotGap;

            for (int i = 0; i < InventoryState.BackpackSize; i++)
            {
                int col = i % BackpackColumns;
                int row = i / BackpackColumns;
                float x = curX + col * (slotSize + slotGap);
                float y = curY + row * (slotSize + slotGap);
                var slotRef = InventorySlotRef.BackpackSlot(i);
                var item = inventory.Backpack[i];

                DrawSlot(new Rect(x, y, slotSize, slotSize), slotRef, item, inventory, isLoot);
            }

            GUI.EndScrollView();
        }

        void DrawEquipSlot(InventoryState inventory, float x, float y, string label,
            InventorySlotRef slotRef, float slotSize, float slotGap, bool isLoot)
        {
            float labelH = slotSize * 0.22f;
            GUI.Label(new Rect(x, y - labelH - 1f, slotSize + slotSize * 0.4f, labelH), label, _labelStyle);
            var item = inventory.GetSlot(slotRef);
            var rect = new Rect(x, y, slotSize, slotSize);

            _slotStyle.normal.background = _equipBg;
            DrawSlot(rect, slotRef, item, inventory, isLoot);
        }

        void DrawSlot(Rect rect, InventorySlotRef slotRef, ItemState item,
            InventoryState inventory, bool isLoot)
        {
            bool isDragOver = _dragSource.HasValue && rect.Contains(Event.current.mousePosition);
            bool isDragSource = _dragSource.HasValue && _dragSource.Value.Equals(slotRef) && _dragFromLoot == isLoot;

            if (isDragSource || isDragOver)
                _slotStyle.normal.background = _slotHighlight;
            else
                _slotStyle.normal.background = _slotBg;

            string text = item != null
                ? (item.StackCount > 1 ? $"{item.DisplayName}\nx{item.StackCount}" : item.DisplayName)
                : "";
            GUI.Box(rect, text, _slotStyle);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
            {
                if (item != null)
                {
                    _dragSource = slotRef;
                    _dragFromLoot = isLoot;
                    _dragLabel = item.DisplayName;
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0
                && rect.Contains(Event.current.mousePosition))
            {
                if (_dragSource.HasValue && !(_dragSource.Value.Equals(slotRef) && _dragFromLoot == isLoot))
                {
                    var session = App.App.Instance?.RaidSession;
                    if (session != null)
                    {
                        var state = session.RaidState;
                        var playerInv = state.Inventory;
                        LootableContainerState lootTarget = null;
                        if (state.PlayerEntity.LootTargetId != EId.None)
                            lootTarget = LootSystem.GetLootable(state, state.PlayerEntity.LootTargetId);

                        var fromInv = _dragFromLoot && lootTarget != null ? lootTarget.Inventory : playerInv;
                        var toInv = isLoot && lootTarget != null ? lootTarget.Inventory : playerInv;

                        if (fromInv == toInv)
                            InventorySystem.TryMove(fromInv, _dragSource.Value, slotRef);
                        else
                            LootSystem.TryTransfer(fromInv, _dragSource.Value, toInv, slotRef);
                    }
                    _dragSource = null;
                    _dragLabel = null;
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1
                && rect.Contains(Event.current.mousePosition))
            {
                if (item != null)
                {
                    _showContextMenu = true;
                    _contextMenuSlot = slotRef;
                    _contextMenuFromLoot = isLoot;
                    _contextMenuPos = Event.current.mousePosition;
                    Event.current.Use();
                }
            }
        }

        void HandleDrag(Session.RaidSession session, RaidState state,
            LootableContainerState lootTarget)
        {
            if (!_dragSource.HasValue) return;

            if (_dragLabel != null)
            {
                var mousePos = Event.current.mousePosition;
                float dragW = 100f;
                float dragH = 24f;
                var dragRect = new Rect(mousePos.x + 10f, mousePos.y + 10f, dragW, dragH);

                var style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = Mathf.RoundToInt(dragH * 0.55f),
                    alignment = TextAnchor.MiddleCenter,
                };
                style.normal.background = _dragBg;
                style.normal.textColor = Color.white;
                GUI.Box(dragRect, _dragLabel, style);
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                bool insideAnyPanel = _playerPanelRect.Contains(Event.current.mousePosition)
                    || (_lootPanelRect.width > 0 && _lootPanelRect.Contains(Event.current.mousePosition));

                if (!insideAnyPanel)
                {
                    var playerInv = state.Inventory;
                    InventoryState fromInv;
                    if (_dragFromLoot && lootTarget != null)
                        fromInv = lootTarget.Inventory;
                    else
                        fromInv = playerInv;

                    var dropPos = state.PlayerEntity != null
                        ? state.PlayerEntity.Position + state.PlayerEntity.FacingDirection * 1.5f
                        : Vector3.zero;

                    var item = fromInv.GetSlot(_dragSource.Value);
                    if (item != null)
                    {
                        fromInv.SetSlot(_dragSource.Value, null);
                        var groundItem = GroundItemState.Create(item.Id, item.DefinitionId, dropPos, item.StackCount);
                        state.GroundItems.Add(groundItem);
                        session.ConsumeEvents().GroundItemSpawned(groundItem.Id, groundItem.Position, groundItem.DefinitionId);
                    }
                }

                _dragSource = null;
                _dragLabel = null;
            }
        }

        void DrawContextMenu(Session.RaidSession session, RaidState state,
            LootableContainerState lootTarget)
        {
            if (!_showContextMenu) return;

            float menuW = 80f;
            float menuItemH = 22f;
            float menuH = menuItemH + 4f;
            var menuRect = new Rect(_contextMenuPos.x, _contextMenuPos.y, menuW, menuH);

            GUI.Box(menuRect, "", GUI.skin.box);

            _dropBtnStyle.fontSize = Mathf.RoundToInt(menuItemH * 0.55f);
            if (GUI.Button(new Rect(menuRect.x + 2f, menuRect.y + 2f, menuW - 4f, menuItemH), "Drop", _dropBtnStyle))
            {
                var playerInv = state.Inventory;
                InventoryState fromInv;
                if (_contextMenuFromLoot && lootTarget != null)
                    fromInv = lootTarget.Inventory;
                else
                    fromInv = playerInv;

                var dropPos = state.PlayerEntity != null
                    ? state.PlayerEntity.Position + state.PlayerEntity.FacingDirection * 1.5f
                    : Vector3.zero;

                var item = fromInv.GetSlot(_contextMenuSlot);
                if (item != null)
                {
                    fromInv.SetSlot(_contextMenuSlot, null);
                    var groundItem = GroundItemState.Create(item.Id, item.DefinitionId, dropPos, item.StackCount);
                    state.GroundItems.Add(groundItem);
                    session.ConsumeEvents().GroundItemSpawned(groundItem.Id, groundItem.Position, groundItem.DefinitionId);
                }

                _showContextMenu = false;
            }

            if (Event.current.type == EventType.MouseDown && !menuRect.Contains(Event.current.mousePosition))
            {
                _showContextMenu = false;
                Event.current.Use();
            }
        }

        void DrawLootPrompt(RaidState state, PlayerEntityState player)
        {
            if (_isOpen) return;
            if (player.LootTargetId != EId.None) return;

            var nearest = LootSystem.FindNearestLootable(state, player.Position);
            if (!nearest.IsValid) return;

            EnsureStyles();

            string text = "Press F to loot";
            float w = 200f;
            float h = 32f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height * 0.65f;

            var rect = new Rect(x, y, w, h);
            GUI.DrawTexture(rect, _promptBg);

            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
                _promptStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
            }
            GUI.Label(rect, text, _promptStyle);
        }

        void EnsureStyles()
        {
            if (_slotStyle != null) return;

            _slotStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            _slotStyle.normal.textColor = Color.white;
            _slotStyle.normal.background = _slotBg;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.LowerLeft,
            };
            _labelStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            _headerStyle.normal.textColor = Color.white;

            _dropBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
            };
        }

        static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            if (_slotBg != null) Destroy(_slotBg);
            if (_slotHighlight != null) Destroy(_slotHighlight);
            if (_equipBg != null) Destroy(_equipBg);
            if (_dragBg != null) Destroy(_dragBg);
            if (_panelBg != null) Destroy(_panelBg);
            if (_promptBg != null) Destroy(_promptBg);
        }
    }
}
