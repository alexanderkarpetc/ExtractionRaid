using UnityEngine;
using UnityEngine.UI;

namespace View
{
    public class WorldProgressBar : MonoBehaviour
    {
        const float BarWidth = 1f;
        const float BarHeight = 0.08f;
        const float VerticalOffset = 2.45f;

        Image _fill;
        CanvasGroup _canvasGroup;

        public static WorldProgressBar Create(Transform parent)
        {
            var go = new GameObject("ProgressBar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, VerticalOffset, 0f);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 101;

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
            fillImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            var bar = go.AddComponent<WorldProgressBar>();
            bar._fill = fillImage;
            bar._canvasGroup = canvasGroup;

            return bar;
        }

        public void SetProgress(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            _fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
            _canvasGroup.alpha = 1f;
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            transform.rotation = cam.transform.rotation;
        }
    }
}
