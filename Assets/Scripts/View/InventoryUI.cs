using State;
using Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace View
{
    public class InventoryUI : MonoBehaviour
    {
        const int BackpackColumns = 10;
        const float ScreenMargin = 30f;

        bool _isOpen;

        Texture2D _slotBg;
        Texture2D _slotHighlight;
        Texture2D _equipBg;
        Texture2D _dragBg;
        GUIStyle _slotStyle;
        GUIStyle _labelStyle;
        GUIStyle _headerStyle;
        GUIStyle _dropBtnStyle;

        InventorySlotRef? _dragSource;
        string _dragLabel;

        bool _showContextMenu;
        InventorySlotRef _contextMenuSlot;
        Vector2 _contextMenuPos;
        Rect _inventoryRect;

        void Awake()
        {
            _slotBg = MakeTex(new Color(0.2f, 0.2f, 0.2f, 0.9f));
            _slotHighlight = MakeTex(new Color(0.4f, 0.6f, 0.3f, 0.9f));
            _equipBg = MakeTex(new Color(0.25f, 0.25f, 0.35f, 0.9f));
            _dragBg = MakeTex(new Color(0.5f, 0.5f, 0.2f, 0.85f));
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Key.Tab].wasPressedThisFrame)
                _isOpen = !_isOpen;
        }

        void OnGUI()
        {
            if (!_isOpen) return;

            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var state = session.RaidState;
            if (state == null) return;

            var inventory = state.Inventory;
            if (inventory == null) return;

            EnsureStyles();

            float totalW = Screen.width - ScreenMargin * 2f;
            float totalH = Screen.height - ScreenMargin * 2f;
            float windowX = ScreenMargin;
            float windowY = ScreenMargin;

            float padding = totalW * 0.02f;
            float slotGap = Mathf.Max(4f, totalW * 0.005f);

            float availableW = totalW - padding * 2f;
            float slotSize = (availableW - (BackpackColumns - 1) * slotGap) / BackpackColumns;
            slotSize = Mathf.Floor(slotSize);

            int backpackRows = Mathf.CeilToInt((float)InventoryState.BackpackSize / BackpackColumns);

            _inventoryRect = new Rect(windowX, windowY, totalW, totalH);
            GUI.Box(_inventoryRect, "", GUI.skin.box);

            float curY = windowY + padding;
            float curX = windowX + padding;

            float headerH = Mathf.Max(22f, slotSize * 0.35f);
            _headerStyle.fontSize = Mathf.RoundToInt(headerH * 0.7f);
            _slotStyle.fontSize = Mathf.RoundToInt(slotSize * 0.18f);
            _labelStyle.fontSize = Mathf.RoundToInt(slotSize * 0.2f);

            GUI.Label(new Rect(curX, curY, availableW, headerH), "EQUIPMENT", _headerStyle);
            curY += headerH + slotGap;

            float equipSlotSpacing = slotSize + slotGap + slotSize * 0.4f;
            DrawEquipSlot(inventory, curX, curY, "Weapon 1", InventorySlotRef.Weapon(0), slotSize, slotGap);
            DrawEquipSlot(inventory, curX + equipSlotSpacing, curY, "Weapon 2", InventorySlotRef.Weapon(1), slotSize, slotGap);
            DrawEquipSlot(inventory, curX + 2 * equipSlotSpacing, curY, "Helmet", InventorySlotRef.Helmet(), slotSize, slotGap);
            DrawEquipSlot(inventory, curX + 3 * equipSlotSpacing, curY, "Armor", InventorySlotRef.BodyArmor(), slotSize, slotGap);

            curY += slotSize + slotGap * 2f + headerH * 0.5f;

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

                DrawSlot(new Rect(x, y, slotSize, slotSize), slotRef, item);
            }

            HandleDrag(slotSize, session, state);
            DrawContextMenu(session, state, slotSize);
        }

        void DrawEquipSlot(InventoryState inventory, float x, float y, string label,
            InventorySlotRef slotRef, float slotSize, float slotGap)
        {
            float labelH = slotSize * 0.25f;
            GUI.Label(new Rect(x, y - labelH - 2f, slotSize + slotSize * 0.6f, labelH), label, _labelStyle);
            var item = inventory.GetSlot(slotRef);
            var rect = new Rect(x, y, slotSize, slotSize);

            _slotStyle.normal.background = _equipBg;
            DrawSlot(rect, slotRef, item);
        }

        void DrawSlot(Rect rect, InventorySlotRef slotRef, ItemState item)
        {
            bool isDragOver = _dragSource.HasValue && rect.Contains(Event.current.mousePosition);
            bool isDragSource = _dragSource.HasValue && _dragSource.Value.Equals(slotRef);

            if (isDragSource)
                _slotStyle.normal.background = _slotHighlight;
            else if (isDragOver)
                _slotStyle.normal.background = _slotHighlight;
            else
                _slotStyle.normal.background = item != null ? _slotBg : _slotBg;

            string text = item != null ? item.DisplayName : "";
            GUI.Box(rect, text, _slotStyle);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                if (item != null)
                {
                    _dragSource = slotRef;
                    _dragLabel = item.DisplayName;
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                if (_dragSource.HasValue && !_dragSource.Value.Equals(slotRef))
                {
                    var session = App.App.Instance?.RaidSession;
                    if (session != null)
                    {
                        InventorySystem.TryMove(session.RaidState.Inventory, _dragSource.Value, slotRef);
                    }
                    _dragSource = null;
                    _dragLabel = null;
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                if (item != null)
                {
                    _showContextMenu = true;
                    _contextMenuSlot = slotRef;
                    _contextMenuPos = Event.current.mousePosition;
                    Event.current.Use();
                }
            }
        }

        void HandleDrag(float slotSize, Session.RaidSession session, RaidState state)
        {
            if (!_dragSource.HasValue) return;

            if (_dragLabel != null)
            {
                var mousePos = Event.current.mousePosition;
                float dragW = slotSize * 1.6f;
                float dragH = slotSize * 0.45f;
                var dragRect = new Rect(mousePos.x + 10f, mousePos.y + 10f, dragW, dragH);

                var style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = Mathf.RoundToInt(dragH * 0.5f),
                    alignment = TextAnchor.MiddleCenter,
                };
                style.normal.background = _dragBg;
                style.normal.textColor = Color.white;
                GUI.Box(dragRect, _dragLabel, style);
            }

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                if (!_inventoryRect.Contains(Event.current.mousePosition))
                {
                    DropItem(session, state, _dragSource.Value);
                }
                _dragSource = null;
                _dragLabel = null;
            }
        }

        void DrawContextMenu(Session.RaidSession session, RaidState state, float slotSize)
        {
            if (!_showContextMenu) return;

            float menuW = slotSize * 1.4f;
            float menuItemH = slotSize * 0.4f;
            float menuH = menuItemH + 4f;
            var menuRect = new Rect(_contextMenuPos.x, _contextMenuPos.y, menuW, menuH);

            GUI.Box(menuRect, "", GUI.skin.box);

            _dropBtnStyle.fontSize = Mathf.RoundToInt(menuItemH * 0.5f);
            if (GUI.Button(new Rect(menuRect.x + 2f, menuRect.y + 2f, menuW - 4f, menuItemH), "Drop", _dropBtnStyle))
            {
                DropItem(session, state, _contextMenuSlot);
                _showContextMenu = false;
            }

            if (Event.current.type == EventType.MouseDown && !menuRect.Contains(Event.current.mousePosition))
            {
                _showContextMenu = false;
                Event.current.Use();
            }
        }

        void DropItem(Session.RaidSession session, RaidState state, InventorySlotRef slot)
        {
            var dropPos = state.PlayerEntity != null
                ? state.PlayerEntity.Position + state.PlayerEntity.FacingDirection * 1.5f
                : Vector3.zero;
            session.RequestDrop(slot, dropPos);
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
        }
    }
}
