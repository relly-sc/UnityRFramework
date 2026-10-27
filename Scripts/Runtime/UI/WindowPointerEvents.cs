using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Collections.Generic;

namespace UnityRFramework.Runtime
{
    /// <summary>为窗口提供点击内、点击外和指针按下事件。</summary>
    [AddComponentMenu("UnityRFramework/UI/Window Pointer Events")]
    public sealed class WindowPointerEvents : MonoBehaviour
    {
        [SerializeField] private UnityEvent pointerInside;
        [SerializeField] private UnityEvent pointerOutside;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        public UnityEvent PointerInside => pointerInside;
        public UnityEvent PointerOutside => pointerOutside;

        private void Update()
        {
            if (EventSystem.current == null || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };
            raycastResults.Clear();
            EventSystem.current.RaycastAll(eventData, raycastResults);
            bool inside = false;
            for (int i = 0; i < raycastResults.Count; i++)
            {
                Transform hit = raycastResults[i].gameObject.transform;
                if (hit == transform || hit.IsChildOf(transform))
                {
                    inside = true;
                    break;
                }
            }

            if (inside)
            {
                pointerInside?.Invoke();
            }
            else
            {
                pointerOutside?.Invoke();
            }
        }
    }
}
