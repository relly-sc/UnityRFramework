using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 阶段 5 队列和固定高度虚拟列表的轻量回归检查。
    /// </summary>
    internal static class UIStage5SelfCheck
    {
        [MenuItem("GameObject/UnityRFramework/UI 交互与列表自检", false, 23)]
        private static void Run()
        {
            CheckModalBackdrop();
            CheckConfirmationQueue();
            CheckVirtualizedList();
            Debug.Log("[UI 交互与列表自检] 通过。");
        }

        private static void CheckModalBackdrop()
        {
            var node = new GameObject("Backdrop", typeof(RectTransform), typeof(Image),
                typeof(ModalBackdrop));
            try
            {
                int clickCount = 0;
                ModalBackdrop backdrop = node.GetComponent<ModalBackdrop>();
                backdrop.OnBackdropClick.AddListener(() => clickCount++);
                backdrop.OnPointerClick(null);
                Require(clickCount == 1, "模态遮罩未转发空白区域点击。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(node);
            }
        }

        private static void CheckConfirmationQueue()
        {
            var root = new GameObject("ConfirmationQueue");
            try
            {
                ConfirmationDialogQueue queue = root.AddComponent<ConfirmationDialogQueue>();
                GameObject view = new GameObject("View", typeof(RectTransform));
                view.transform.SetParent(root.transform, false);
                Text message = CreateText("Message", view.transform);
                Button confirm = CreateButton("Confirm", view.transform);
                Button cancel = CreateButton("Cancel", view.transform);

                var serialized = new SerializedObject(queue);
                serialized.FindProperty("viewRoot").objectReferenceValue = view;
                serialized.FindProperty("messageText").objectReferenceValue = message;
                serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
                serialized.FindProperty("cancelButton").objectReferenceValue = cancel;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Task<bool> first = queue.ShowAsync("First");
                Task<bool> second = queue.ShowAsync("Second");
                Require(view.activeSelf && message.text == "First" && queue.PendingCount == 2,
                    "确认弹窗未按顺序显示首项。");

                queue.ConfirmCurrent();
                Require(first.IsCompleted && first.Result && message.text == "Second",
                    "确认后未切换到下一项。");

                queue.CancelCurrent();
                Require(second.IsCompleted && !second.Result && !view.activeSelf
                    && queue.PendingCount == 0, "取消后队列未正确清空。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckVirtualizedList()
        {
            var root = new GameObject("VirtualList", typeof(RectTransform), typeof(ScrollRect));
            try
            {
                RectTransform viewport = CreateRect("Viewport", root.transform, 200f);
                RectTransform content = CreateRect("Content", viewport, 200f);
                RectTransform template = CreateRect("ItemTemplate", content, 40f);
                ScrollRect scrollRect = root.GetComponent<ScrollRect>();
                scrollRect.viewport = viewport;
                scrollRect.content = content;

                VirtualizedVerticalList list = root.AddComponent<VirtualizedVerticalList>();
                var serialized = new SerializedObject(list);
                serialized.FindProperty("itemTemplate").objectReferenceValue = template;
                serialized.FindProperty("itemHeight").floatValue = 40f;
                serialized.FindProperty("extraVisibleItems").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                int lastBoundIndex = -1;
                list.SetItemCount(1000, (index, _) => lastBoundIndex = index);
                Require(list.PooledItemCount == 6,
                    "千项列表创建的对象数量不等于视口所需数量。");

                list.ScrollToIndex(999);
                Require(lastBoundIndex == 999 && list.PooledItemCount == 6,
                    "滚动定位未复用现有可见项。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Text CreateText(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Text));
            node.transform.SetParent(parent, false);
            return node.GetComponent<Text>();
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            node.transform.SetParent(parent, false);
            return node.GetComponent<Button>();
        }

        private static RectTransform CreateRect(string name, Transform parent, float height)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, height);
            return rect;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("[UI 交互与列表自检] " + message);
            }
        }
    }
}
