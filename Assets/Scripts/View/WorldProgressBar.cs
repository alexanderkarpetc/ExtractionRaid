using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>Compact medicine-use row below the player's health and status indicators.</summary>
    public class WorldProgressBar : MonoBehaviour
    {
        const float RowWidth = 1.25f;
        const float BarWidth = 1f;
        const float BarHeight = 0.12f;
        const float IconSize = 0.19f;
        const float VerticalOffset = 0.3f;

        static readonly Color MedkitColor = new(0.25f, 0.9f, 0.48f, 1f);
        static readonly Color BandageColor = new(0.95f, 0.72f, 0.38f, 1f);

        Image _fill;
        Image _crossHorizontal;
        Image _crossVertical;
        CanvasGroup _canvasGroup;

        public static WorldProgressBar Create(Transform healthBar)
        {
            var go = new GameObject("MedicineProgress");
            go.transform.SetParent(healthBar, false);
            go.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 102;

            var rowRect = go.GetComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(RowWidth, IconSize);

            var canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            CreateCross(go.transform, out var crossHorizontal, out var crossVertical);

            var barGo = new GameObject("Bar");
            barGo.transform.SetParent(go.transform, false);
            var barRect = barGo.AddComponent<RectTransform>();
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.pivot = new Vector2(0.5f, 0.5f);
            barRect.anchoredPosition = new Vector2(0.125f, 0f);
            barRect.sizeDelta = new Vector2(BarWidth, BarHeight);

            var background = barGo.AddComponent<Image>();
            background.color = new Color(0.06f, 0.07f, 0.08f, 0.9f);
            background.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barGo.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(0.012f, 0.012f);
            fillRect.offsetMax = new Vector2(-0.012f, -0.012f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            var fill = fillGo.AddComponent<Image>();
            fill.color = MedkitColor;
            fill.raycastTarget = false;

            var component = go.AddComponent<WorldProgressBar>();
            component._fill = fill;
            component._crossHorizontal = crossHorizontal;
            component._crossVertical = crossVertical;
            component._canvasGroup = canvasGroup;
            return component;
        }

        static void CreateCross(Transform parent, out Image horizontal, out Image vertical)
        {
            horizontal = CreateCrossPart(parent, "MedicalCrossHorizontal",
                new Vector2(-0.54f, 0f), new Vector2(IconSize, IconSize * 0.38f));
            vertical = CreateCrossPart(parent, "MedicalCrossVertical",
                new Vector2(-0.54f, 0f), new Vector2(IconSize * 0.38f, IconSize));
        }

        static Image CreateCrossPart(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public void SetProgress(float ratio, bool isBandage)
        {
            Color color = isBandage ? BandageColor : MedkitColor;
            _fill.color = color;
            _crossHorizontal.color = color;
            _crossVertical.color = color;
            _fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            _canvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
        }
    }
}
