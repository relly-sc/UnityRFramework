using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 管理单个 UGUI Button 的普通、悬停预览和选中显示状态。
    /// 未加入 ButtonStateGroup 时点击会切换自身状态；加入组后由组统一维护单选关系。
    /// </summary>
    [AddComponentMenu("UnityRFramework/UI/Button State")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        [Tooltip("接收点击事件的 Button。未指定时使用当前 GameObject 上的 Button。")]
        private Button button;

        [SerializeField]
        [Tooltip("可选状态组。指定后由组维护单选关系。")]
        private ButtonStateGroup group;

        [Header("State Visuals")]
        [SerializeField]
        [Tooltip("普通状态背景，可为空。")]
        private GameObject normalBackground;

        [SerializeField]
        [Tooltip("选中状态背景，可为空。")]
        private GameObject selectedBackground;

        [SerializeField]
        [Tooltip("切换 Sprite 的目标 Image。未指定时使用当前 GameObject 上的 Image。")]
        private Image targetImage;

        [SerializeField]
        [Tooltip("普通状态 Sprite。为空时在 Awake 中捕获目标 Image 当前 Sprite。")]
        private Sprite normalSprite;

        [SerializeField]
        [Tooltip("选中状态 Sprite。为空时继续使用普通状态 Sprite。")]
        private Sprite selectedSprite;

        [SerializeField]
        [Tooltip("切换文字内容或颜色的 UGUI Text，可为空。")]
        private Text targetText;

        [SerializeField]
        [Tooltip("是否由本组件切换文字内容。")]
        private bool overrideTextContent;

        [SerializeField]
        [Tooltip("普通状态文字。")]
        private string normalText = string.Empty;

        [SerializeField]
        [Tooltip("选中状态文字。")]
        private string selectedText = string.Empty;

        [SerializeField]
        [Tooltip("是否由本组件切换文字颜色。")]
        private bool overrideTextColor;

        [SerializeField]
        [Tooltip("普通状态文字颜色。")]
        private Color normalTextColor = Color.black;

        [SerializeField]
        [Tooltip("选中状态文字颜色。")]
        private Color selectedTextColor = Color.white;

        [Header("Interaction")]
        [SerializeField]
        [Tooltip("鼠标进入时是否只预览选中外观，不改变持久选中状态。")]
        private bool enableHoverPreview;

        [SerializeField]
        [Tooltip("无状态组时是否在启动时选中；有状态组时作为组的候选初始项。")]
        private bool selectedOnStart;

        [SerializeField]
        [Tooltip("应用启动选中状态时是否触发选中事件。")]
        private bool invokeEventOnStart;

        [Header("Events")]
        [SerializeField]
        [Tooltip("状态从未选中变为选中后触发。")]
        private UnityEvent onSelected = new UnityEvent();

        [SerializeField]
        [Tooltip("状态从选中变为未选中后触发。")]
        private UnityEvent onDeselected = new UnityEvent();

        private bool isSelected;
        private bool pointerInside;

        /// <summary>
        /// 状态发生持久变更时触发。参数依次为当前组件和新的选中值。
        /// </summary>
        public event Action<ButtonState, bool> StateChanged;

        /// <summary>
        /// 获取当前是否处于持久选中状态。
        /// 悬停预览不会改变此值。
        /// </summary>
        public bool IsSelected
        {
            get { return isSelected; }
        }

        /// <summary>
        /// 获取当前所属状态组；未分组时为 null。
        /// </summary>
        public ButtonStateGroup Group
        {
            get { return group; }
        }

        /// <summary>
        /// 获取是否配置为启动选中候选项。
        /// </summary>
        internal bool SelectedOnStart
        {
            get { return selectedOnStart; }
        }

        /// <summary>
        /// 获取应用启动选中时是否触发事件。
        /// </summary>
        internal bool InvokeEventOnStart
        {
            get { return invokeEventOnStart; }
        }

        private void Reset()
        {
            button = GetComponent<Button>();
            targetImage = GetComponent<Image>();
            targetText = GetComponentInChildren<Text>(true);
            group = GetComponentInParent<ButtonStateGroup>();
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (targetImage == null)
            {
                targetImage = GetComponent<Image>();
            }

            if (normalSprite == null && targetImage != null)
            {
                normalSprite = targetImage.sprite;
            }

            isSelected = group == null && selectedOnStart;
            ApplyVisual(isSelected);
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }

            group?.Register(this);
        }

        private void Start()
        {
            if (group == null && selectedOnStart && invokeEventOnStart)
            {
                InvokeStateChanged(true);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }

            group?.Unregister(this);
            pointerInside = false;
        }

        /// <summary>
        /// 设置持久选中状态。
        /// 已加入状态组时会遵循组的单选和允许取消规则。
        /// </summary>
        /// <param name="selected">目标选中状态。</param>
        /// <param name="notify">状态变化时是否触发事件。</param>
        /// <returns>状态实际发生变化时返回 true。</returns>
        public bool SetSelected(bool selected, bool notify = true)
        {
            if (group != null)
            {
                return selected
                    ? group.Select(this, notify)
                    : group.Deselect(this, notify);
            }

            return SetSelectedFromGroup(selected, notify);
        }

        /// <summary>
        /// 动态设置状态组，并维护旧组和新组的注册关系。
        /// </summary>
        /// <param name="newGroup">新的状态组，可为 null。</param>
        public void SetGroup(ButtonStateGroup newGroup)
        {
            if (ReferenceEquals(group, newGroup))
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                group?.Unregister(this);
            }

            group = newGroup;
            if (isActiveAndEnabled)
            {
                group?.Register(this);
            }
        }

        /// <inheritdoc />
        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            if (enableHoverPreview && !isSelected)
            {
                ApplyVisual(true);
            }
        }

        /// <inheritdoc />
        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            if (enableHoverPreview)
            {
                ApplyVisual(isSelected);
            }
        }

        /// <summary>
        /// 由状态组直接提交状态，避免再次进入组协调逻辑。
        /// </summary>
        /// <param name="selected">目标选中状态。</param>
        /// <param name="notify">是否触发事件。</param>
        /// <returns>状态实际发生变化时返回 true。</returns>
        internal bool SetSelectedFromGroup(bool selected, bool notify)
        {
            if (isSelected == selected)
            {
                ApplyVisual(selected || (enableHoverPreview && pointerInside));
                return false;
            }

            isSelected = selected;
            ApplyVisual(selected || (enableHoverPreview && pointerInside));
            if (notify)
            {
                InvokeStateChanged(selected);
            }

            return true;
        }

        private void HandleClick()
        {
            if (group != null)
            {
                group.HandleMemberClick(this);
                return;
            }

            SetSelectedFromGroup(!isSelected, true);
        }

        private void ApplyVisual(bool selectedVisual)
        {
            if (normalBackground != null)
            {
                normalBackground.SetActive(!selectedVisual);
            }

            if (selectedBackground != null)
            {
                selectedBackground.SetActive(selectedVisual);
            }

            if (targetImage != null)
            {
                targetImage.sprite = selectedVisual && selectedSprite != null
                    ? selectedSprite
                    : normalSprite;
            }

            if (targetText == null)
            {
                return;
            }

            if (overrideTextContent)
            {
                targetText.text = selectedVisual ? selectedText : normalText;
            }

            if (overrideTextColor)
            {
                targetText.color = selectedVisual
                    ? selectedTextColor
                    : normalTextColor;
            }
        }

        private void InvokeStateChanged(bool selected)
        {
            if (selected)
            {
                onSelected?.Invoke();
            }
            else
            {
                onDeselected?.Invoke();
            }

            StateChanged?.Invoke(this, selected);
        }
    }
}
