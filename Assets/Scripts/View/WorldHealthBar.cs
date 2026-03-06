using UnityEngine;
using UnityEngine.UI;

namespace View
{
    public class WorldHealthBar : MonoBehaviour
    {
        const float BarWidth = 1f;
        const float BarHeight = 0.12f;
        const float VerticalOffset = 2.2f;

        Image _fill;
        CanvasGroup _canvasGroup;

        public static WorldHealthBar Create(Transform parent)
        {
            var go = new GameObject("HealthBar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BarWidth, BarHeight);
            rt.localScale = Vector3.one;

            var canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = new Color(0.8f, 0.2f, 0.15f, 1f);

            var bar = go.AddComponent<WorldHealthBar>();
            bar._fill = fillImage;
            bar._canvasGroup = canvasGroup;

            return bar;
        }

        public void UpdateHealth(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            var fillRect = _fill.rectTransform;
            fillRect.anchorMax = new Vector2(ratio, 1f);

            _canvasGroup.alpha = ratio < 1f ? 1f : 0f;

            if (ratio > 0.5f)
                _fill.color = Color.Lerp(new Color(1f, 0.8f, 0f), new Color(0.2f, 0.85f, 0.2f), (ratio - 0.5f) * 2f);
            else
                _fill.color = Color.Lerp(new Color(0.8f, 0.2f, 0.15f), new Color(1f, 0.8f, 0f), ratio * 2f);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            transform.rotation = cam.transform.rotation;
        }
    }
}
