using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// UGUI 模态遮罩。所在 Image 阻断后方射线，可选把空白区域点击转为关闭请求。
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class ModalBackdrop : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        [Tooltip("是否在点击遮罩空白区域时触发关闭请求。")]
        private bool closeOnBackdropClick = true;

        [SerializeField]
        [Tooltip("点击遮罩空白区域时触发。")]
        private UnityEvent onBackdropClick = new UnityEvent();

        /// <summary>获取或设置是否响应遮罩点击。</summary>
        public bool CloseOnBackdropClick
        {
            get { return closeOnBackdropClick; }
            set { closeOnBackdropClick = value; }
        }

        /// <summary>获取遮罩点击事件。</summary>
        public UnityEvent OnBackdropClick => onBackdropClick;

        private void Awake()
        {
            GetComponent<Image>().raycastTarget = true;
        }

        /// <inheritdoc />
        public void OnPointerClick(PointerEventData eventData)
        {
            if (closeOnBackdropClick
                && (eventData == null || eventData.pointerCurrentRaycast.gameObject == gameObject))
            {
                onBackdropClick?.Invoke();
            }
        }
    }
}
