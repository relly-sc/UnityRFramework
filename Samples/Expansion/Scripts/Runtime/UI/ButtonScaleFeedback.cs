using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 按钮缩放反馈组件：支持两种触发方式，在 Inspector 中切换。
    /// - Hover：鼠标移入放大，移出复原（适合 PC）。
    /// - Click：鼠标按下放大，松开 / 移出复原（适合触屏或无悬停场景）。
    /// 仅处理表现层缩放，与按钮点击逻辑 (OnClick) 完全解耦。
    /// 注意：所在按钮的 Transition 请设为 None 或 Color Tint，避免与缩放动画互相覆盖。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonScaleFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        // 触发模式枚举：决定用悬停还是按下驱动缩放
        private enum ScaleMode
        {
            Hover, // 悬停触发
            Click  // 按下触发
        }

        [Header("缩放设置")]
        [Tooltip("选择触发方式：Hover=悬停放大移出复原；Click=按下放大松开复原")]
        [SerializeField] private ScaleMode scaleMode = ScaleMode.Hover;

        [Tooltip("相对原始大小的放大倍数，例如 1.2 表示放大到 1.2 倍")]
        [SerializeField] private float hoverScale = 1.2f;

        [Tooltip("放大 / 复原的过渡时长（秒）")]
        [SerializeField] private float duration = 0.15f;

        // 按钮原始局部缩放，作为复原目标与放大基准
        private Vector3 originalScale;
        // 当前动画要插值到的目标缩放
        private Vector3 targetScale;
        // 正在播放的缩放协程引用，用于切换时停掉旧协程避免叠加
        private Coroutine scaleRoutine;

        private void Awake()
        {
            // 记录初始缩放，防止被其它逻辑改动后无法复原
            originalScale = transform.localScale;
            targetScale = originalScale;
        }

        /// <summary>
        /// 指针进入按钮区域：悬停模式下放大
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            // 仅悬停模式在移入时放大
            if (scaleMode == ScaleMode.Hover)
            {
                BeginScale(originalScale * hoverScale);
            }
        }

        /// <summary>
        /// 指针离开按钮区域：悬停模式移出复原；点击模式拖出也复原，避免卡在放大态
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            // 两种模式离开都复原，点击模式防止按下后拖出松开导致卡住
            if (scaleMode == ScaleMode.Hover || scaleMode == ScaleMode.Click)
            {
                BeginScale(originalScale);
            }
        }

        /// <summary>
        /// 指针在按钮上按下：点击模式下放大
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            // 仅点击模式在按下时放大
            if (scaleMode == ScaleMode.Click)
            {
                BeginScale(originalScale * hoverScale);
            }
        }

        /// <summary>
        /// 指针在按钮上抬起：点击模式下复原
        /// </summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            // 仅点击模式在抬起时复原
            if (scaleMode == ScaleMode.Click)
            {
                BeginScale(originalScale);
            }
        }

        // 启动一次缩放补间；若已有动画在播则先停掉再重开，避免叠加
        private void BeginScale(Vector3 target)
        {
            targetScale = target;
            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
            }
            scaleRoutine = StartCoroutine(ScaleTween());
        }

        // 按帧插值完成缩放，配合 SmoothStep 缓动让手感更顺滑
        private System.Collections.IEnumerator ScaleTween()
        {
            Vector3 from = transform.localScale;
            float progress = 0f;
            while (progress < 1f)
            {
                progress += Time.deltaTime / duration;
                float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
                transform.localScale = Vector3.Lerp(from, targetScale, ease);
                yield return null;
            }
            transform.localScale = targetScale;
            scaleRoutine = null;
        }
    }
}
