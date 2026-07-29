using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UI 组件。作为 UIModule 的运行时包装层，绑定 Unity 生命周期，
    /// 转发所有 UI 操作到 UIModule。
    /// </summary>
    [AddComponentMenu("UnityRFramework/UI")]
    [DisallowMultipleComponent]
    public sealed class UIComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// UI 模块引用。
        /// </summary>
        private IUIModule uiModule;

        /// <summary>
        /// UI 辅助器类型名称。
        /// </summary>
        [SerializeField] private string uiHelperTypeName = "UnityRFramework.Runtime.DefaultUIHelper";

        /// <summary>
        /// 获取当前打开的 UI 数量。
        /// </summary>
        public int UIFormCount
        {
            get { return uiModule != null ? uiModule.UIFormCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();

            uiModule = RFrameworkModuleEntry.GetModule<IUIModule>();
            if (uiModule == null)
            {
                Log.Error("Can not find module '{0}'.", nameof(IUIModule));
                return;
            }

            // 注入依赖模块
            IResourceModule resourceModule = RFrameworkModuleEntry.GetModule<IResourceModule>();
            IEventModule eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
            IPoolModule poolModule = RFrameworkModuleEntry.GetModule<IPoolModule>();
            uiModule.SetDependencies(resourceModule, eventModule, poolModule);

            // 创建并注入 UI 辅助器
            UIHelperBase uiHelper = Helper.CreateHelper<UIHelperBase>(uiHelperTypeName, null);
            if (uiHelper != null)
            {
                uiModule.SetHelper(uiHelper);
                uiHelper.transform.SetParent(transform);
            }
        }

        /// <summary>
        /// 运行时替换 UI 辅助器。
        /// </summary>
        public void SetHelper(IUIHelper helper)
        {
            uiModule.SetHelper(helper);
        }

        /// <inheritdoc cref="IUIModule.OpenUIFormAsync"/>
        public Task<IUIForm> OpenUIFormAsync(string assetName, int windowLayer = 0,
            bool fullScreen = false, uint priority = 0, object userData = null,
            CancellationToken ct = default)
        {
            return uiModule.OpenUIFormAsync(assetName, windowLayer, fullScreen, priority, userData, ct);
        }

        /// <summary>
        /// 将场景中已有的 UI 对象登记到 UI 模块。
        /// 场景 UI 参与窗口栈和生命周期管理，但模块不会销毁对象或卸载资源。
        /// </summary>
        /// <param name="uiInstance">场景中的 UI 对象。</param>
        /// <param name="formName">UI 表单唯一名称。</param>
        /// <param name="windowLayer">窗口层级。</param>
        /// <param name="fullScreen">是否为全屏窗口。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>登记后的 UI 表单。</returns>
        public IUIForm RegisterSceneUIForm(GameObject uiInstance, string formName, int windowLayer = 0,
            bool fullScreen = false, object userData = null)
        {
            if (uiInstance == null)
            {
                throw new RFrameworkException("Scene UI instance is invalid.");
            }

            UIForm uiForm = uiInstance.GetOrAddComponent<UIForm>();
            uiForm.Init(formName, windowLayer, fullScreen);
            uiModule.RegisterUIForm(formName, uiForm, userData);
            return uiForm;
        }

        /// <summary>
        /// 从 UI 模块注销场景 UI，不销毁对象或卸载资源。
        /// </summary>
        /// <param name="formName">UI 表单唯一名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void UnregisterSceneUIForm(string formName, object userData = null)
        {
            uiModule.UnregisterUIForm(formName, userData);
        }

        /// <inheritdoc cref="IUIModule.CloseUIForm"/>
        public void CloseUIForm(string assetName, object userData = null)
        {
            uiModule.CloseUIForm(assetName, userData);
        }

        /// <inheritdoc cref="IUIModule.CloseAllUIForms"/>
        public void CloseAllUIForms(object userData = null)
        {
            uiModule.CloseAllUIForms(userData);
        }

        /// <inheritdoc cref="IUIModule.HasUIForm"/>
        public bool HasUIForm(string assetName)
        {
            return uiModule.HasUIForm(assetName);
        }

        /// <inheritdoc cref="IUIModule.GetUIForm"/>
        public IUIForm GetUIForm(string assetName)
        {
            return uiModule.GetUIForm(assetName);
        }
    }
}
