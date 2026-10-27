using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>用于动态 UI 的最小 RectTransform 布局辅助。</summary>
    public static class RectTransformExtensions
    {
        public static void SetSize(this RectTransform rect, Vector2 size)
        {
            rect.sizeDelta = size;
        }

        public static void SetAnchors(this RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
        }

        public static void SetStretch(this RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static void SetPivot(this RectTransform rect, Vector2 pivot)
        {
            rect.pivot = pivot;
        }
    }
}
