using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// ButtonState 选中项变化事件。
    /// 参数为新的选中项；允许无选中且已清空时为 null。
    /// </summary>
    [Serializable]
    public sealed class ButtonStateSelectionEvent : UnityEvent<ButtonState>
    {
    }

    /// <summary>
    /// 维护一组 ButtonState 的单选关系。
    /// 成员通过各自的 Group 引用注册，点击时不会扫描 Transform 层级。
    /// </summary>
    [AddComponentMenu("UnityRFramework/UI/Button State Group")]
    [DisallowMultipleComponent]
    public sealed class ButtonStateGroup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("是否允许再次点击当前选中项后清空选择。")]
        private bool allowNoSelection;

        [SerializeField]
        [Tooltip("显式初始选中项。为空时使用成员中第一个 Selected On Start 项；仍为空且不允许无选择时使用第一个可用成员。")]
        private ButtonState initialSelection;

        [SerializeField]
        [Tooltip("选中项变化后触发。允许无选择时参数可能为 null。")]
        private ButtonStateSelectionEvent onSelectionChanged =
            new ButtonStateSelectionEvent();

        private readonly List<ButtonState> members = new List<ButtonState>();

        private ButtonState selected;
        private bool initialized;
        private bool isChanging;

        /// <summary>
        /// 选中项变化后触发。允许无选择时参数可能为 null。
        /// </summary>
        public event Action<ButtonState> SelectionChanged;

        /// <summary>
        /// 获取当前选中项；无选中时为 null。
        /// </summary>
        public ButtonState Selected
        {
            get { return selected; }
        }

        /// <summary>
        /// 获取当前已注册且启用的成员数量。
        /// </summary>
        public int MemberCount
        {
            get { return members.Count; }
        }

        /// <summary>
        /// 获取或设置是否允许无选中项。
        /// 关闭该选项不会立即创建选择；下一次初始化、注册或点击时恢复单选约束。
        /// </summary>
        public bool AllowNoSelection
        {
            get { return allowNoSelection; }
            set
            {
                allowNoSelection = value;
                if (!allowNoSelection && selected == null && initialized)
                {
                    ButtonState fallback = FindFirstAvailableMember();
                    if (fallback != null)
                    {
                        ChangeSelection(fallback, false);
                    }
                }
            }
        }

        private void Start()
        {
            InitializeSelection();
        }

        /// <summary>
        /// 选中指定成员。
        /// 切换时先取消旧项，再选中新项，最后触发组事件。
        /// </summary>
        /// <param name="member">要选中的已注册成员。</param>
        /// <param name="notify">是否触发成员事件和组事件。</param>
        /// <returns>选中项实际发生变化时返回 true。</returns>
        public bool Select(ButtonState member, bool notify = true)
        {
            if (!IsRegisteredMember(member) || ReferenceEquals(selected, member))
            {
                return false;
            }

            return ChangeSelection(member, notify);
        }

        /// <summary>
        /// 取消指定成员。
        /// 仅当该成员当前已选中且允许无选中项时生效。
        /// </summary>
        /// <param name="member">要取消的成员。</param>
        /// <param name="notify">是否触发成员事件和组事件。</param>
        /// <returns>选中项实际发生变化时返回 true。</returns>
        public bool Deselect(ButtonState member, bool notify = true)
        {
            if (!allowNoSelection || !ReferenceEquals(selected, member))
            {
                return false;
            }

            return ChangeSelection(null, notify);
        }

        /// <summary>
        /// 清空当前选中项。
        /// 仅在允许无选中项时生效。
        /// </summary>
        /// <param name="notify">是否触发成员事件和组事件。</param>
        /// <returns>选中项实际发生变化时返回 true。</returns>
        public bool ClearSelection(bool notify = true)
        {
            if (!allowNoSelection || selected == null)
            {
                return false;
            }

            return ChangeSelection(null, notify);
        }

        /// <summary>
        /// 注册启用的状态成员。
        /// </summary>
        /// <param name="member">要注册的成员。</param>
        internal void Register(ButtonState member)
        {
            if (member == null || members.Contains(member))
            {
                return;
            }

            members.Add(member);
            if (!initialized)
            {
                member.SetSelectedFromGroup(false, false);
                return;
            }

            if (selected == null && !allowNoSelection)
            {
                ChangeSelection(member, false);
            }
            else
            {
                member.SetSelectedFromGroup(false, false);
            }
        }

        /// <summary>
        /// 注销禁用或改组的状态成员。
        /// </summary>
        /// <param name="member">要注销的成员。</param>
        internal void Unregister(ButtonState member)
        {
            if (member == null || !members.Remove(member))
            {
                return;
            }

            if (!ReferenceEquals(selected, member))
            {
                return;
            }

            selected = null;
            if (isActiveAndEnabled && initialized && !allowNoSelection)
            {
                ButtonState fallback = FindFirstAvailableMember();
                if (fallback != null)
                {
                    ChangeSelection(fallback, false);
                }
            }
        }

        /// <summary>
        /// 处理成员 Button 的点击。
        /// </summary>
        /// <param name="member">被点击的成员。</param>
        internal void HandleMemberClick(ButtonState member)
        {
            if (!IsRegisteredMember(member))
            {
                return;
            }

            if (ReferenceEquals(selected, member))
            {
                if (allowNoSelection)
                {
                    ChangeSelection(null, true);
                }

                return;
            }

            ChangeSelection(member, true);
        }

        private void InitializeSelection()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            ButtonState candidate = IsRegisteredMember(initialSelection)
                ? initialSelection
                : FindStartSelectedMember();
            if (candidate == null && !allowNoSelection)
            {
                candidate = FindFirstAvailableMember();
            }

            for (int index = 0; index < members.Count; index++)
            {
                members[index].SetSelectedFromGroup(false, false);
            }

            if (candidate != null)
            {
                ChangeSelection(candidate, candidate.InvokeEventOnStart);
            }
        }

        private bool ChangeSelection(ButtonState next, bool notify)
        {
            if (isChanging || ReferenceEquals(selected, next))
            {
                return false;
            }

            initialized = true;
            ButtonState previous = selected;
            selected = next;
            isChanging = true;
            try
            {
                previous?.SetSelectedFromGroup(false, notify);
                next?.SetSelectedFromGroup(true, notify);
                if (notify)
                {
                    onSelectionChanged?.Invoke(next);
                    SelectionChanged?.Invoke(next);
                }

                return true;
            }
            finally
            {
                isChanging = false;
            }
        }

        private bool IsRegisteredMember(ButtonState member)
        {
            return member != null
                && ReferenceEquals(member.Group, this)
                && members.Contains(member);
        }

        private ButtonState FindStartSelectedMember()
        {
            for (int index = 0; index < members.Count; index++)
            {
                ButtonState member = members[index];
                if (member != null
                    && member.isActiveAndEnabled
                    && member.SelectedOnStart)
                {
                    return member;
                }
            }

            return null;
        }

        private ButtonState FindFirstAvailableMember()
        {
            for (int index = 0; index < members.Count; index++)
            {
                ButtonState member = members[index];
                if (member != null && member.isActiveAndEnabled)
                {
                    return member;
                }
            }

            return null;
        }
    }
}
