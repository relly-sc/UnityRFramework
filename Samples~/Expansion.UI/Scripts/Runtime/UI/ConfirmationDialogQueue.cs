using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 串行确认弹窗队列。视觉由引用的 UGUI 子视图维护，队列组件自身应保持激活。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConfirmationDialogQueue : MonoBehaviour
    {
        private sealed class Request
        {
            public string Message;
            public CancellationToken Token;
            public CancellationTokenRegistration Registration;
            public TaskCompletionSource<bool> Completion;
            public bool Canceled;
        }

        [SerializeField] private GameObject viewRoot;
        [SerializeField] private Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private ModalBackdrop backdrop;

        private readonly Queue<Request> requests = new Queue<Request>();
        private SynchronizationContext unityContext;
        private Request current;
        private bool initialized;

        /// <summary>获取当前显示及等待处理的请求数量。</summary>
        public int PendingCount => requests.Count + (current == null ? 0 : 1);

        private void Awake()
        {
            Initialize();
            SetViewVisible(false);
        }

        private void OnDisable()
        {
            CancelAll();
        }

        private void OnDestroy()
        {
            RemoveListeners();
            CancelAll();
        }

        /// <summary>将确认请求加入队列，并异步返回用户选择。</summary>
        public Task<bool> ShowAsync(string message, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(ct);
            }

            Initialize();
            ValidateReferences();

            Request request = new Request
            {
                Message = message ?? string.Empty,
                Token = ct,
                Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
            };

            if (ct.CanBeCanceled)
            {
                request.Registration = ct.Register(() => PostCancellation(request));
            }

            requests.Enqueue(request);
            ShowNext();
            return request.Completion.Task;
        }

        /// <summary>确认当前请求。</summary>
        public void ConfirmCurrent()
        {
            CompleteCurrent(true);
        }

        /// <summary>取消当前请求。</summary>
        public void CancelCurrent()
        {
            CompleteCurrent(false);
        }

        /// <summary>取消并清空当前及等待中的全部请求。</summary>
        public void CancelAll()
        {
            if (current != null)
            {
                CancelRequest(current);
                current = null;
            }

            while (requests.Count > 0)
            {
                CancelRequest(requests.Dequeue());
            }

            SetViewVisible(false);
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            unityContext = SynchronizationContext.Current;
            confirmButton?.onClick.AddListener(ConfirmCurrent);
            cancelButton?.onClick.AddListener(CancelCurrent);
            backdrop?.OnBackdropClick.AddListener(CancelCurrent);
            initialized = true;
        }

        private void RemoveListeners()
        {
            if (!initialized)
            {
                return;
            }

            confirmButton?.onClick.RemoveListener(ConfirmCurrent);
            cancelButton?.onClick.RemoveListener(CancelCurrent);
            backdrop?.OnBackdropClick.RemoveListener(CancelCurrent);
            initialized = false;
        }

        private void ShowNext()
        {
            if (current != null)
            {
                return;
            }

            while (requests.Count > 0)
            {
                Request next = requests.Dequeue();
                if (next.Canceled || next.Completion.Task.IsCompleted)
                {
                    next.Registration.Dispose();
                    continue;
                }

                current = next;
                messageText.text = next.Message;
                SetViewVisible(true);
                return;
            }

            SetViewVisible(false);
        }

        private void CompleteCurrent(bool result)
        {
            if (current == null)
            {
                return;
            }

            Request completed = current;
            current = null;
            completed.Registration.Dispose();
            completed.Completion.TrySetResult(result);
            ShowNext();
        }

        private void PostCancellation(Request request)
        {
            if (unityContext != null)
            {
                unityContext.Post(_ => HandleCancellation(request), null);
                return;
            }

            request.Canceled = true;
            request.Completion.TrySetCanceled(request.Token);
        }

        private void HandleCancellation(Request request)
        {
            if (request.Completion.Task.IsCompleted)
            {
                return;
            }

            CancelRequest(request);
            if (ReferenceEquals(current, request))
            {
                current = null;
                ShowNext();
            }
        }

        private static void CancelRequest(Request request)
        {
            request.Canceled = true;
            request.Registration.Dispose();
            request.Completion.TrySetCanceled(request.Token);
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
            if (viewRoot == null || viewRoot == gameObject || messageText == null
                || confirmButton == null || cancelButton == null)
            {
                throw new InvalidOperationException(
                    "ConfirmationDialogQueue requires a child View Root, Text, Confirm Button and Cancel Button.");
            }
        }
    }
}
