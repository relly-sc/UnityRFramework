using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 固定高度纵向 UGUI 虚拟列表。只创建视口所需项，并在滚动时复用可见对象。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public sealed class VirtualizedVerticalList : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform itemTemplate;
        [SerializeField, Min(0f)] private float itemHeight;
        [SerializeField, Min(0f)] private float spacing;
        [SerializeField, Min(0f)] private float paddingTop;
        [SerializeField, Min(0f)] private float paddingBottom;
        [SerializeField, Min(0)] private int extraVisibleItems = 1;

        private readonly List<RectTransform> pooledItems = new List<RectTransform>();
        private readonly List<int> pooledIndices = new List<int>();
        private Action<int, GameObject> bindItem;
        private int itemCount;
        private int firstVisibleIndex = -1;
        private bool listening;

        /// <summary>获取当前数据项总数。</summary>
        public int ItemCount => itemCount;

        /// <summary>获取当前创建并复用的视图数量。</summary>
        public int PooledItemCount => pooledItems.Count;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (!HasRequiredReferences())
            {
                return;
            }

            AddListener();
            Refresh();
        }

        private void OnDisable()
        {
            RemoveListener();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && scrollRect != null && content != null && itemTemplate != null)
            {
                Refresh();
            }
        }

        /// <summary>设置数据数量和可见项绑定回调，并立即刷新列表。</summary>
        public void SetItemCount(int count, Action<int, GameObject> binder)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count > 0 && binder == null)
            {
                throw new ArgumentNullException(nameof(binder));
            }

            itemCount = count;
            bindItem = binder;
            Refresh();
        }

        /// <summary>重新绑定当前可见项，适用于数据内容或数量变化。</summary>
        public void Refresh()
        {
            EnsureReferences();
            AddListener();

            float height = ResolveItemHeight();
            float stride = height + spacing;
            int required = Mathf.Max(1,
                Mathf.CeilToInt(viewport.rect.height / stride) + extraVisibleItems);
            EnsurePool(required, height);

            float itemsHeight = itemCount > 0 ? itemCount * stride - spacing : 0f;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(viewport.rect.height, paddingTop + itemsHeight + paddingBottom));
            firstVisibleIndex = -1;
            UpdateVisible(true);
        }

        /// <summary>将指定数据项滚动到视口顶部附近。</summary>
        public void ScrollToIndex(int index)
        {
            if (index < 0 || index >= itemCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            EnsureReferences();
            float targetY = paddingTop + index * (ResolveItemHeight() + spacing);
            float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);
            Vector2 position = content.anchoredPosition;
            position.y = Mathf.Clamp(targetY, 0f, maxY);
            content.anchoredPosition = position;
            UpdateVisible(true);
        }

        private void OnScroll(Vector2 _)
        {
            UpdateVisible(false);
        }

        private void UpdateVisible(bool force)
        {
            if (pooledItems.Count == 0)
            {
                return;
            }

            float stride = ResolveItemHeight() + spacing;
            int first = itemCount == 0
                ? 0
                : Mathf.Clamp(Mathf.FloorToInt((content.anchoredPosition.y - paddingTop) / stride),
                    0, itemCount - 1);
            if (!force && first == firstVisibleIndex)
            {
                return;
            }

            firstVisibleIndex = first;
            for (int i = 0; i < pooledItems.Count; i++)
            {
                int dataIndex = first + i;
                RectTransform item = pooledItems[i];
                bool visible = dataIndex < itemCount;
                item.gameObject.SetActive(visible);
                if (!visible)
                {
                    pooledIndices[i] = -1;
                    continue;
                }

                item.anchoredPosition = new Vector2(0f,
                    -(paddingTop + dataIndex * stride));
                if (force || pooledIndices[i] != dataIndex)
                {
                    pooledIndices[i] = dataIndex;
                    bindItem?.Invoke(dataIndex, item.gameObject);
                }
            }
        }

        private void EnsurePool(int required, float height)
        {
            itemTemplate.gameObject.SetActive(false);
            while (pooledItems.Count < required)
            {
                RectTransform item = Instantiate(itemTemplate, content, false);
                item.name = itemTemplate.name + " (Virtualized)";
                item.anchorMin = new Vector2(0f, 1f);
                item.anchorMax = new Vector2(1f, 1f);
                item.pivot = new Vector2(0.5f, 1f);
                item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                pooledItems.Add(item);
                pooledIndices.Add(-1);
            }
        }

        private float ResolveItemHeight()
        {
            float height = itemHeight > 0f ? itemHeight : itemTemplate.rect.height;
            if (height <= 0f)
            {
                throw new InvalidOperationException(
                    "VirtualizedVerticalList requires Item Height or a template with positive height.");
            }

            return height;
        }

        private void EnsureReferences()
        {
            ResolveReferences();
            if (!HasRequiredReferences())
            {
                throw new InvalidOperationException(
                    "VirtualizedVerticalList requires Scroll Rect, Viewport, Content and Item Template.");
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            scrollRect.horizontal = false;
        }

        private void ResolveReferences()
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponent<ScrollRect>();
            }

            if (viewport == null && scrollRect != null)
            {
                viewport = scrollRect.viewport;
            }

            if (content == null && scrollRect != null)
            {
                content = scrollRect.content;
            }
        }

        private bool HasRequiredReferences()
        {
            return scrollRect != null && viewport != null && content != null && itemTemplate != null;
        }

        private void AddListener()
        {
            if (!listening && scrollRect != null)
            {
                scrollRect.onValueChanged.AddListener(OnScroll);
                listening = true;
            }
        }

        private void RemoveListener()
        {
            if (listening && scrollRect != null)
            {
                scrollRect.onValueChanged.RemoveListener(OnScroll);
                listening = false;
            }
        }
    }
}
