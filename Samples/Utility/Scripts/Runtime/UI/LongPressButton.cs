using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityRFramework.Samples
{
    /// <summary>
    /// 长按达到指定时长后触发的 UGUI 事件。
    /// </summary>
    [Serializable]
    public sealed class LongPressEvent : UnityEvent
    {
    }

    /// <summary>
    /// 支持普通点击与长按的 UGUI Button。
    /// 达到阈值时立即触发长按，并抑制同一次按压产生的普通点击。
    /// </summary>
    [AddComponentMenu("UnityRFramework/UI/Long Press Button")]
    [DisallowMultipleComponent]
    public sealed class LongPressButton : Button, ICancelHandler
    {
        private const int InvalidPointerId = int.MinValue;

        [SerializeField]
        [Min(0f)]
        [Tooltip("持续按下多少秒后触发长按。")]
        private float longPressDuration = 0.6f;

        [SerializeField]
        [Tooltip("是否使用不受 Time.timeScale 影响的时间。UI 按钮通常建议开启。")]
        private bool useUnscaledTime = true;

        [SerializeField]
        [Tooltip("达到长按阈值时触发。")]
        private LongPressEvent onLongPress = new LongPressEvent();

        private int activePointerId = InvalidPointerId;
        private int suppressedPointerId = InvalidPointerId;
        private float pressStartTime;
        private bool isTracking;
        private bool longPressTriggered;

        /// <summary>
        /// 获取或设置长按触发时长，单位为秒。
        /// 小于零的值会被限制为零。
        /// </summary>
        public float LongPressDuration
        {
            get { return longPressDuration; }
            set { longPressDuration = Mathf.Max(0f, value); }
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
        /// 获取长按事件，可在运行时添加或移除监听。
        /// </summary>
        public LongPressEvent OnLongPress
        {
            get { return onLongPress; }
        }

        /// <summary>
        /// 获取当前是否正在跟踪一次有效按压。
        /// </summary>
        public bool IsPressing
        {
            get { return isTracking; }
        }

        /// <inheritdoc />
        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null
                || eventData.button != PointerEventData.InputButton.Left
                || isTracking
                || !IsActive()
                || !IsInteractable())
            {
                return;
            }

            base.OnPointerDown(eventData);
            activePointerId = eventData.pointerId;
            suppressedPointerId = InvalidPointerId;
            pressStartTime = GetCurrentTime();
            isTracking = true;
            longPressTriggered = false;

            if (longPressDuration <= 0f)
            {
                TriggerLongPress();
            }
        }

        /// <inheritdoc />
        public override void OnPointerUp(PointerEventData eventData)
        {
            if (!IsActivePointer(eventData))
            {
                return;
            }

            bool restoreNormalVisual = longPressTriggered;
            base.OnPointerUp(eventData);
            StopTracking(false);
            if (restoreNormalVisual)
            {
                RestoreNormalVisual();
            }
        }

        /// <inheritdoc />
        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (IsActivePointer(eventData))
            {
                StopTracking(true);
                RestoreNormalVisual();
            }
        }

        /// <inheritdoc />
        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null
                && eventData.button == PointerEventData.InputButton.Left
                && eventData.pointerId == suppressedPointerId)
            {
                suppressedPointerId = InvalidPointerId;
                return;
            }

            base.OnPointerClick(eventData);
        }

        /// <inheritdoc />
        public void OnCancel(BaseEventData eventData)
        {
            bool restoreNormalVisual = isTracking;
            StopTracking(true);
            if (restoreNormalVisual)
            {
                RestoreNormalVisual();
            }
        }

        protected override void OnDisable()
        {
            StopTracking(true);
            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            longPressDuration = Mathf.Max(0f, longPressDuration);
        }
#endif

        private void Update()
        {
            if (!isTracking || longPressTriggered)
            {
                return;
            }

            if (!IsActive() || !IsInteractable())
            {
                StopTracking(true);
                return;
            }

            if (GetCurrentTime() - pressStartTime >= longPressDuration)
            {
                TriggerLongPress();
            }
        }

        private void TriggerLongPress()
        {
            if (!isTracking || longPressTriggered)
            {
                return;
            }

            longPressTriggered = true;
            suppressedPointerId = activePointerId;
            onLongPress?.Invoke();
        }

        private bool IsActivePointer(PointerEventData eventData)
        {
            return isTracking
                && eventData != null
                && eventData.pointerId == activePointerId;
        }

        private void StopTracking(bool clearClickSuppression)
        {
            isTracking = false;
            longPressTriggered = false;
            activePointerId = InvalidPointerId;
            if (clearClickSuppression)
            {
                suppressedPointerId = InvalidPointerId;
            }
        }

        private void RestoreNormalVisual()
        {
            if (IsActive() && IsInteractable())
            {
                DoStateTransition(SelectionState.Normal, true);
            }
        }

        private float GetCurrentTime()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }
    }
}
