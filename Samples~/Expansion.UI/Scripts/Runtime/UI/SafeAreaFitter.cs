using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 将当前 RectTransform 约束到 Screen.safeArea，并在分辨率或安全区域变化时自动刷新。
    /// 父节点应覆盖完整屏幕。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;

        private void OnEnable()
        {
            rectTransform = (RectTransform)transform;
            ApplySafeArea();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea
                || lastScreenSize.x != Screen.width
                || lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        /// <summary>
        /// 立即按当前屏幕安全区域刷新锚点。
        /// </summary>
        public void ApplySafeArea()
        {
            if (rectTransform == null)
            {
                rectTransform = (RectTransform)transform;
            }

            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            rectTransform.anchorMin = new Vector2(
                safeArea.xMin / width,
                safeArea.yMin / height);
            rectTransform.anchorMax = new Vector2(
                safeArea.xMax / width,
                safeArea.yMax / height);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(width, height);
        }
    }
}
