using State;
using Systems;
using UnityEngine;

namespace View
{
    public class HotbarDebugOverlay : MonoBehaviour
    {
        Texture2D _selectedTex;
        Texture2D _pendingTex;
        Texture2D _occupiedTex;
        Texture2D _emptyTex;
        Texture2D _reloadTex;
        Texture2D _quickSlotBoundTex;
        Texture2D _quickSlotActiveTex;
        GUIStyle _slotStyle;

        const int TotalSlots = PlayerEntityState.HotbarSize + InventoryState.QuickSlotCount;

        void Awake()
        {
            _selectedTex = MakeTex(Color.green);
            _pendingTex = MakeTex(new Color(1f, 1f, 0f, 0.7f));
            _occupiedTex = MakeTex(new Color(0.3f, 0.3f, 0.3f, 0.9f));
            _emptyTex = MakeTex(new Color(0.15f, 0.15f, 0.15f, 0.8f));
            _reloadTex = MakeTex(new Color(0.9f, 0.5f, 0.1f, 0.7f));
            _quickSlotBoundTex = MakeTex(new Color(0.35f, 0.3f, 0.2f, 0.9f));
            _quickSlotActiveTex = MakeTex(new Color(0.8f, 0.6f, 0.1f, 0.9f));
        }

        void OnGUI()
        {
            var session = App.App.Instance?.RaidSession;
            if (session == null) return;

            var player = session.RaidState?.PlayerEntity;
            if (player == null) return;

            var inventory = session.RaidState.Inventory;

            if (_slotStyle == null)
            {
                _slotStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 25,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                };
                _slotStyle.normal.textColor = Color.white;
            }

            const float slotW = 200f;
            const float slotH = 140f;
            const float gap = 6f;
            float totalW = TotalSlots * slotW + (TotalSlots - 1) * gap;
            float startX = (Screen.width - totalW) / 2f;
            float startY = Screen.height - slotH - 16f;

            for (int i = 0; i < PlayerEntityState.HotbarSize; i++)
            {
                var rect = new Rect(startX + i * (slotW + gap), startY, slotW, slotH);
                DrawWeaponSlot(rect, i, player, session);
            }

            for (int qi = 0; qi < InventoryState.QuickSlotCount; qi++)
            {
                int visualIndex = PlayerEntityState.HotbarSize + qi;
                var rect = new Rect(startX + visualIndex * (slotW + gap), startY, slotW, slotH);
                DrawQuickSlot(rect, qi, player, inventory);
            }
        }

        void DrawWeaponSlot(Rect rect, int i, PlayerEntityState player, Session.RaidSession session)
        {
            bool isSelected = i == player.SelectedHotbarSlot;
            bool isPending = i == player.PendingHotbarSlot;
            var weapon = player.Hotbar[i];

            _slotStyle.normal.background = isSelected
                ? _selectedTex
                : isPending
                    ? _pendingTex
                    : weapon != null
                        ? _occupiedTex
                        : _emptyTex;

            _slotStyle.normal.textColor = isSelected || isPending ? Color.black : Color.white;

            string label;
            if (weapon != null)
            {
                string ammoInfo = "";
                if (!string.IsNullOrEmpty(weapon.AmmoType))
                {
                    int reserve = AmmoSystem.CountReserve(
                        session.RaidState.Inventory, weapon.AmmoType);
                    ammoInfo = $"\n{weapon.AmmoInMagazine}/{reserve}";
                }
                label = $"[{i + 1}]\n{weapon.PrefabId}{ammoInfo}";
            }
            else
            {
                label = $"[{i + 1}]";
            }

            GUI.Box(rect, label, _slotStyle);

            if (weapon != null && weapon.Phase == WeaponPhase.Reloading && weapon.ReloadTime > 0f)
            {
                float elapsed = session.RaidState.ElapsedTime - weapon.PhaseStartTime;
                float remaining = 1f - Mathf.Clamp01(elapsed / weapon.ReloadTime);
                if (remaining > 0.001f)
                {
                    var reloadRect = new Rect(rect.x, rect.y, rect.width, rect.height * remaining);
                    GUI.DrawTexture(reloadRect, _reloadTex);
                }
            }
        }

        void DrawQuickSlot(Rect rect, int qi, PlayerEntityState player, InventoryState inventory)
        {
            int boundSlot = inventory.QuickSlotBindings[qi];
            bool hasBind = boundSlot >= 0;
            var boundItem = hasBind ? inventory.Backpack[boundSlot] : null;
            bool isActive = player.ActiveQuickSlot == qi;
            int keyNum = qi + InventoryState.QuickSlotKeyOffset;

            _slotStyle.normal.background = isActive
                ? _quickSlotActiveTex
                : hasBind
                    ? _quickSlotBoundTex
                    : _emptyTex;

            _slotStyle.normal.textColor = isActive ? Color.black : Color.white;

            string label = boundItem != null
                ? $"[{keyNum}]\n{boundItem.DisplayName}"
                : $"[{keyNum}]";

            GUI.Box(rect, label, _slotStyle);
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
            if (_selectedTex != null) Destroy(_selectedTex);
            if (_pendingTex != null) Destroy(_pendingTex);
            if (_occupiedTex != null) Destroy(_occupiedTex);
            if (_emptyTex != null) Destroy(_emptyTex);
            if (_reloadTex != null) Destroy(_reloadTex);
            if (_quickSlotBoundTex != null) Destroy(_quickSlotBoundTex);
            if (_quickSlotActiveTex != null) Destroy(_quickSlotActiveTex);
        }
    }
}
