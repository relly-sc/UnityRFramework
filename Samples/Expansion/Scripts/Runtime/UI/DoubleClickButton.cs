using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 鼠标或触摸在指定时间内连续点击两次时触发的 UGUI 事件。
    /// </summary>
    [Serializable]
    public sealed class DoubleClickEvent : UnityEvent
    {
    }

    /// <summary>
    /// 区分单击与双击的 UGUI Button。
    /// 单击会延迟到双击判定结束后触发，双击只触发双击事件。
    /// </summary>
    [AddComponentMenu("UnityRFramework/UI/Double Click Button")]
    [DisallowMultipleComponent]
    public sealed class DoubleClickButton : Button, ICancelHandler
    {
        private const int InvalidPointerId = int.MinValue;

        [SerializeField]
        [Min(0f)]
        [Tooltip("两次点击可被判定为双击的最大时间间隔，单位为秒。")]
        private float doubleClickInterval = 0.3f;

        [SerializeField]
        [Tooltip("是否使用不受 Time.timeScale 影响的时间。UI 按钮通常建议开启。")]
        private bool useUnscaledTime = true;

        [SerializeField]
        [Tooltip("在指定时间内完成第二次点击时触发。")]
        private DoubleClickEvent onDoubleClick = new DoubleClickEvent();

        private int pendingPointerId = InvalidPointerId;
        private float firstClickTime;
        private bool hasPendingClick;

        /// <summary>
        /// 获取或设置双击判定的最大时间间隔，单位为秒。
        /// 小于零的值会被限制为零。
        /// </summary>
        public float DoubleClickInterval
        {
            get { return doubleClickInterval; }
            set { doubleClickInterval = Mathf.Max(0f, value); }
        }

        /// <summary>
        /// 获取或设置是否使用不受 Time.timeScale 影响的时间。
        /// </summary>
        public bool UseUnscaledTime
        {
            get { return useUnscaledTime; }
            set { useUnscaledTime = value; }
        }

        /// <summary>
        /// 获取双击事件，可在运行时添加或移除监听。
        /// </summary>
        public DoubleClickEvent OnDoubleClick
        {
            get { return onDoubleClick; }
        }

        /// <summary>
        /// 获取当前是否有等待双击判定的首次点击。
        /// </summary>
        public bool HasPendingClick
        {
            get { return hasPendingClick; }
        }

        /// <inheritdoc />
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null
                || eventData.button != PointerEventData.InputButton.Left
                || !IsActive()
                || !IsInteractable())
            {
                return;
            }

            float currentTime = GetCurrentTime();
            if (hasPendingClick
                && eventData.pointerId == pendingPointerId
                && currentTime - firstClickTime <= doubleClickInterval)
            {
                ClearPendingClick();
                onDoubleClick?.Invoke();
                return;
            }

            if (hasPendingClick)
            {
                InvokePendingSingleClick();
            }

            hasPendingClick = true;
            pendingPointerId = eventData.pointerId;
            firstClickTime = currentTime;
        }

        /// <inheritdoc />
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            ClearPendingClick();
        }

        /// <inheritdoc />
        public void OnCancel(BaseEventData eventData)
        {
            ClearPendingClick();
        }

        protected override void OnDisable()
        {
            ClearPendingClick();
            base.OnDisable();
        }

        private void Update()
        {
            if (!hasPendingClick)
            {
                return;
            }

            if (!IsActive() || !IsInteractable())
            {
                ClearPendingClick();
                return;
            }

            if (GetCurrentTime() - firstClickTime >= doubleClickInterval)
            {
                InvokePendingSingleClick();
            }
        }

        private void InvokePendingSingleClick()
        {
            if (!hasPendingClick)
            {
                return;
            }

            ClearPendingClick();
            if (IsActive() && IsInteractable())
            {
                onClick?.Invoke();
            }
        }

        private void ClearPendingClick()
        {
            hasPendingClick = false;
            pendingPointerId = InvalidPointerId;
            firstClickTime = 0f;
        }

        private float GetCurrentTime()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }
    }
}
