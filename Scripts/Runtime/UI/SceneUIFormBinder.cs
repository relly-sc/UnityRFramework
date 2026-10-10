using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 场景 UI 绑定组件。将场景中预先放置的 UI 登记到 UI 模块，并在场景销毁时自动注销。
    /// </summary>
    [AddComponentMenu("UnityRFramework/Scene UI Form Binder")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIForm))]
    public sealed class SceneUIFormBinder : MonoBehaviour
    {
        /// <summary>
        /// UI 表单唯一名称。留空时使用当前 GameObject 名称。
        /// </summary>
        [SerializeField]
        [Tooltip("UI 表单唯一名称。留空时使用当前 GameObject 名称。")]
        private string formName = string.Empty;

        /// <summary>
        /// 窗口层级，数值越大越靠前。
        /// </summary>
        [SerializeField]
        [Tooltip("窗口层级，数值越大越靠前。")]
        private int windowLayer = UILayer.HUD;

        /// <summary>
        /// 是否为全屏窗口。全屏窗口会暂停被覆盖的下层 UI。
        /// </summary>
        [SerializeField]
        [Tooltip("是否为全屏窗口。全屏窗口会暂停被覆盖的下层 UI。")]
        private bool fullScreen;

        /// <summary>
        /// 是否在 Start 阶段自动登记。
        /// </summary>
        [SerializeField]
        [Tooltip("是否在 Start 阶段自动登记到 UI 模块。")]
        private bool registerOnStart = true;

        /// <summary>
        /// 已使用的 UI 组件引用。
        /// </summary>
        private UIComponent uiComponent;

        /// <summary>
        /// 当前登记使用的表单名称。
        /// </summary>
        private string registeredFormName = string.Empty;

        /// <summary>
        /// 获取当前是否已登记到 UI 模块。
        /// </summary>
        public bool IsRegistered
        {
            get
            {
                return uiComponent != null && !string.IsNullOrEmpty(registeredFormName) &&
                    uiComponent.HasUIForm(registeredFormName);
            }
        }

        private void Start()
        {
            if (!registerOnStart)
            {
                return;
            }

            try
            {
                Register();
            }
            catch (Exception ex)
            {
                Log.Error("Register scene UI form '{0}' failed: {1}", ResolveFormName(), ex);
            }
        }

        /// <summary>
        /// 将当前场景 UI 登记到 UI 模块。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>登记后的 UI 表单。</returns>
        public IUIForm Register(object userData = null)
        {
            if (IsRegistered)
            {
                return uiComponent.GetUIForm(registeredFormName);
            }

            uiComponent = GameEntry.UI;
            if (uiComponent == null)
            {
                throw new RFrameworkException("Can not find UIComponent for scene UI registration.");
            }

            registeredFormName = ResolveFormName();
            return uiComponent.RegisterSceneUIForm(gameObject, registeredFormName, windowLayer,
                fullScreen, userData);
        }

        /// <summary>
        /// 从 UI 模块注销当前场景 UI，不销毁当前 GameObject。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public void Unregister(object userData = null)
        {
            if (!IsRegistered)
            {
                registeredFormName = string.Empty;
                return;
            }

            string currentFormName = registeredFormName;
            registeredFormName = string.Empty;
            uiComponent.UnregisterSceneUIForm(currentFormName, userData);
        }

        private void OnDestroy()
        {
            if (!IsRegistered)
            {
                return;
            }

            string currentFormName = registeredFormName;
            try
            {
                Unregister();
            }
            catch (Exception ex)
            {
                Log.Error("Unregister scene UI form '{0}' failed: {1}", currentFormName, ex);
            }
        }

        /// <summary>
        /// 解析当前使用的 UI 表单名称。
        /// </summary>
        /// <returns>非空的 UI 表单名称。</returns>
        private string ResolveFormName()
        {
            return string.IsNullOrWhiteSpace(formName) ? gameObject.name : formName.Trim();
        }
    }
}
