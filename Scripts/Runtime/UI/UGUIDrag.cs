using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityRFramework.Runtime
{
    /// <summary>UGUI RectTransform 拖拽组件。</summary>
    [AddComponentMenu("UnityRFramework/UI/UGUI Drag")]
    public sealed class UGUIDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private bool keepInsideParent = true;

        private RectTransform parentRect;
        private Canvas canvas;
        private Camera eventCamera;
        private Vector2 pointerOffset;

        public RectTransform Target => target != null ? target : transform as RectTransform;

        private void Awake()
        {
            target = Target;
            parentRect = target != null ? target.parent as RectTransform : null;
            canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null)
            {
                return;
            }

            eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? eventData.pressEventCamera
                : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, eventCamera, out Vector2 localPoint);
            pointerOffset = target.anchoredPosition - localPoint;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null || parentRect == null
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, eventData.position, eventCamera, out Vector2 localPoint))
            {
                return;
            }

            target.anchoredPosition = localPoint + pointerOffset;
            if (keepInsideParent)
            {
                ClampInsideParent();
            }
        }

        private void ClampInsideParent()
        {
            Rect parent = parentRect.rect;
            Rect targetRect = target.rect;
            Vector2 min = parent.min - targetRect.min;
            Vector2 max = parent.max - targetRect.max;
            target.anchoredPosition = new Vector2(
                Mathf.Clamp(target.anchoredPosition.x, min.x, max.x),
                Mathf.Clamp(target.anchoredPosition.y, min.y, max.y));
        }
    }
}
