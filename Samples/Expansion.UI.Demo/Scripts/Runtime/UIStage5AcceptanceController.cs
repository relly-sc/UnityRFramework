using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion.UI.Demo
{
    /// <summary>
    /// UI 交互与列表验收场景控制器，仅负责把按钮操作映射到待验收组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIStage5AcceptanceController : MonoBehaviour
    {
        [SerializeField] private ConfirmationDialogQueue confirmationQueue;
        [SerializeField] private ToastQueue toastQueue;
        [SerializeField] private VirtualizedVerticalList virtualList;
        [SerializeField] private Button requestConfirmationButton;
        [SerializeField] private Button cancelCurrentButton;
        [SerializeField] private Button cancelAllButton;
        [SerializeField] private Button enqueueToastsButton;
        [SerializeField] private Button showThousandItemsButton;
        [SerializeField] private Button showTenItemsButton;
        [SerializeField] private Button scrollToLastButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Text statusText;

        private readonly Queue<string> statusLines = new Queue<string>();
        private int confirmationSequence;

        private void Start()
        {
            requestConfirmationButton.onClick.AddListener(RequestConfirmation);
            cancelCurrentButton.onClick.AddListener(CancelCurrent);
            cancelAllButton.onClick.AddListener(CancelAll);
            enqueueToastsButton.onClick.AddListener(EnqueueToasts);
            showThousandItemsButton.onClick.AddListener(ShowThousandItems);
            showTenItemsButton.onClick.AddListener(ShowTenItems);
            scrollToLastButton.onClick.AddListener(ScrollToLast);
            restartButton.onClick.AddListener(GameEntry.Restart);
            SetListCount(1000);
            AddStatus("准备完成，请连续点击三次确认按钮验证队列顺序。");
        }

        private void OnDestroy()
        {
            requestConfirmationButton.onClick.RemoveListener(RequestConfirmation);
            cancelCurrentButton.onClick.RemoveListener(CancelCurrent);
            cancelAllButton.onClick.RemoveListener(CancelAll);
            enqueueToastsButton.onClick.RemoveListener(EnqueueToasts);
            showThousandItemsButton.onClick.RemoveListener(ShowThousandItems);
            showTenItemsButton.onClick.RemoveListener(ShowTenItems);
            scrollToLastButton.onClick.RemoveListener(ScrollToLast);
            restartButton.onClick.RemoveListener(GameEntry.Restart);
        }

        private async void RequestConfirmation()
        {
            int id = ++confirmationSequence;
            AddStatus("确认请求 #" + id + " 已加入队列");
            try
            {
                bool result = await confirmationQueue.ShowAsync("确认请求 #" + id);
                AddStatus("确认请求 #" + id + "：" + (result ? "已接受" : "已拒绝"));
            }
            catch (OperationCanceledException)
            {
                AddStatus("确认请求 #" + id + "：已随生命周期取消");
            }
        }

        private void CancelCurrent()
        {
            confirmationQueue.CancelCurrent();
            AddStatus("已拒绝当前确认请求");
        }

        private void CancelAll()
        {
            confirmationQueue.CancelAll();
            AddStatus("已取消全部确认请求");
        }

        private void EnqueueToasts()
        {
            toastQueue.Show("提示 1 / 3", 0.7f);
            toastQueue.Show("提示 2 / 3", 0.7f);
            toastQueue.Show("提示 3 / 3", 0.7f);
            AddStatus("已加入三条提示消息");
        }

        private void SetListCount(int count)
        {
            virtualList.SetItemCount(count, BindListItem);
            AddStatus("列表：" + count + " 条数据 / "
                + virtualList.PooledItemCount + " 个视图");
        }

        private void ShowThousandItems()
        {
            SetListCount(1000);
        }

        private void ShowTenItems()
        {
            SetListCount(10);
        }

        private void ScrollToLast()
        {
            int count = virtualList.ItemCount;
            if (count == 0)
            {
                return;
            }

            virtualList.ScrollToIndex(count - 1);
            AddStatus("已滚动到第 " + (count - 1) + " 项");
        }

        private static void BindListItem(int index, GameObject item)
        {
            Text label = item.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "虚拟列表项 " + index;
            }
        }

        private void AddStatus(string message)
        {
            if (statusText == null)
            {
                return;
            }

            statusLines.Enqueue(message);
            while (statusLines.Count > 5)
            {
                statusLines.Dequeue();
            }

            statusText.text = string.Join("\n", statusLines);
        }

    }
}
