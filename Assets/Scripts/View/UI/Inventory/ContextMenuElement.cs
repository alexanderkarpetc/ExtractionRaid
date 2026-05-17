using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace View.UI.Inventory
{
    /// <summary>
    /// Floating right-click context menu for inventory slots. Lives inside
    /// <see cref="InventoryWindow"/>'s root (absolute-positioned), so it
    /// auto-hides when the window closes. Mirrors the legacy uGUI
    /// <c>ContextMenuView</c> — a vertical list of clickable rows з опційним
    /// hot-key labels справа.
    /// </summary>
    public class ContextMenuElement : VisualElement
    {
        public struct Option
        {
            public string Label;
            public string Hotkey;
            public Action OnClick;
        }

        readonly VisualElement _list;

        public bool IsVisible => style.display == DisplayStyle.Flex;

        public ContextMenuElement()
        {
            AddToClassList("inv-ctx-menu");
            style.display = DisplayStyle.None;
            _list = new VisualElement();
            _list.AddToClassList("inv-ctx-menu__list");
            Add(_list);
        }

        public void Show(Vector2 panelPos, IReadOnlyList<Option> options)
        {
            _list.Clear();
            if (options == null || options.Count == 0) { Hide(); return; }

            foreach (var opt in options)
            {
                var localOpt = opt;
                var btn = new Button(() =>
                {
                    Hide();
                    localOpt.OnClick?.Invoke();
                }) { text = string.Empty };
                btn.AddToClassList("inv-ctx-menu__btn");

                var label = new Label(localOpt.Label);
                label.AddToClassList("inv-ctx-menu__label");
                label.pickingMode = PickingMode.Ignore;
                btn.Add(label);

                if (!string.IsNullOrEmpty(localOpt.Hotkey))
                {
                    var hk = new Label(localOpt.Hotkey);
                    hk.AddToClassList("inv-ctx-menu__hotkey");
                    hk.pickingMode = PickingMode.Ignore;
                    btn.Add(hk);
                }

                _list.Add(btn);
            }

            style.left = panelPos.x;
            style.top  = panelPos.y;
            style.display = DisplayStyle.Flex;

            // Clamp to parent bounds next frame — resolvedStyle.width/height
            // are 0 until layout runs once. parent.resolvedStyle.height у
            // panel coords corresponds to the inventory's root viewport.
            schedule.Execute(() =>
            {
                if (parent == null) return;
                float parentW = parent.resolvedStyle.width;
                float parentH = parent.resolvedStyle.height;
                float w = resolvedStyle.width;
                float h = resolvedStyle.height;
                if (parentW <= 0f || parentH <= 0f || w <= 0f || h <= 0f) return;

                float left = style.left.value.value;
                float top  = style.top.value.value;
                if (left + w > parentW) style.left = parentW - w;
                if (top  + h > parentH) style.top  = parentH - h;
                if (style.left.value.value < 0f) style.left = 0f;
                if (style.top.value.value  < 0f) style.top  = 0f;
            }).StartingIn(0);
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
            _list.Clear();
        }
    }
}
