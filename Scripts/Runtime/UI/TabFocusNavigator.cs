using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityRFramework.Runtime
{
    /// <summary>在当前根节点下用 Tab 和 Shift+Tab 切换可交互控件。</summary>
    [AddComponentMenu("UnityRFramework/UI/Tab Focus Navigator")]
    public sealed class TabFocusNavigator : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private bool refreshEveryFrame;

        private readonly List<Selectable> selectables = new List<Selectable>();
        private bool dirty = true;

        public void Refresh()
        {
            selectables.Clear();
            RectTransform searchRoot = root != null ? root : transform as RectTransform;
            if (searchRoot != null)
            {
                Selectable[] found = searchRoot.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i].IsActive() && found[i].IsInteractable())
                    {
                        selectables.Add(found[i]);
                    }
                }
            }

            dirty = false;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        private void Update()
        {
            if (refreshEveryFrame || dirty)
            {
                Refresh();
            }

            if (EventSystem.current == null
                || selectables.Count == 0
                || !Input.GetKeyDown(KeyCode.Tab))
            {
                return;
            }

            GameObject current = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            int index = current != null
                ? selectables.FindIndex(item => item.gameObject == current)
                : -1;
            int direction = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                ? -1
                : 1;
            int next = (index + direction + selectables.Count) % selectables.Count;
            EventSystem.current.SetSelectedGameObject(selectables[next].gameObject);
        }
    }
}
