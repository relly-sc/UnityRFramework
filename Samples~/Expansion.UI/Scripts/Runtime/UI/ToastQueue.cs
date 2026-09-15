using System;
using System.Collections.Generic;
using RFramework;
using UnityEngine;
using UnityEngine.UI;
using Timer = RFramework.Timer;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 使用框架 Timer 串行显示轻量文本提示。视觉与动画由引用的子视图维护。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToastQueue : MonoBehaviour
    {
        private readonly struct Entry
        {
            public Entry(string message, float duration)
            {
                Message = message;
                Duration = duration;
            }

            public string Message { get; }
            public float Duration { get; }
        }

        [SerializeField] private GameObject viewRoot;
        [SerializeField] private Text messageText;
        [SerializeField, Min(0.05f)] private float defaultDuration = 2f;

        private readonly Queue<Entry> entries = new Queue<Entry>();
        private Timer timer;
        private bool showing;

        /// <summary>获取当前显示及等待显示的消息数量。</summary>
        public int PendingCount => entries.Count + (showing ? 1 : 0);

        private void Awake()
        {
            SetViewVisible(false);
        }

        private void OnDisable()
        {
            Clear();
        }

        /// <summary>将消息加入显示队列。duration 小于等于零时使用 Inspector 默认时长。</summary>
        public void Show(string message, float duration = -1f)
        {
            ValidateReferences();
            entries.Enqueue(new Entry(message ?? string.Empty,
                duration > 0f ? duration : defaultDuration));
            ShowNext();
        }

        /// <summary>清空当前及等待中的全部消息。</summary>
        public void Clear()
        {
            timer?.Cancel();
            timer = null;
            showing = false;
            entries.Clear();
            SetViewVisible(false);
        }

        private void ShowNext()
        {
            if (showing || entries.Count == 0)
            {
                return;
            }

            if (UnityRFramework.Runtime.GameEntry.Timer == null)
            {
                entries.Clear();
                throw new InvalidOperationException("ToastQueue requires an active TimerComponent.");
            }

            Entry entry = entries.Dequeue();
            showing = true;
            messageText.text = entry.Message;
            SetViewVisible(true);
            timer = Timer.CreateOnce(entry.Duration, CompleteCurrent);
            UnityRFramework.Runtime.GameEntry.Timer.RegisterTimer(timer);
        }

        private void CompleteCurrent()
        {
            timer = null;
            showing = false;
            if (entries.Count == 0)
            {
                SetViewVisible(false);
                return;
            }

            ShowNext();
        }

        private void SetViewVisible(bool visible)
        {
            if (viewRoot != null && viewRoot != gameObject)
            {
                viewRoot.SetActive(visible);
            }
        }

        private void ValidateReferences()
        {
            if (viewRoot == null || viewRoot == gameObject || messageText == null)
            {
                throw new InvalidOperationException(
                    "ToastQueue requires a child View Root and Text.");
            }
        }
    }
}
