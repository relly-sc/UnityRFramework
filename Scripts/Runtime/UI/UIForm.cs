using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UI 表单 MonoBehaviour 实现，桥接 IUIForm 接口与 Unity GameObject。
    /// 由 IUIHelper.CreateUIForm 创建，挂载 UIFormLogic 子组件转发生命周期回调。
    /// 生命周期方法（OnInit/OnOpen/OnPause/OnResume/OnClose/OnUpdate）使用显式接口实现，
    /// 仅框架内部通过 IUIForm 接口调用，外部不可直接调用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIForm : MonoBehaviour, IUIForm
    {
        /// <summary>
        /// UI 资源路径。
        /// </summary>
        public string AssetName { get; private set; }

        /// <summary>
        /// UI 实例对象。
        /// 注意：框架关闭（模块 Shutdown / 场景卸载）时，Unity 可能已先行销毁本组件所在 GameObject。
        /// 此时访问 gameObject 会抛 MissingReferenceException，故显式判空短路。
        /// </summary>
        public object Handle => this != null ? gameObject : null;

        /// <summary>
        /// 窗口层级。
        /// </summary>
        public int WindowLayer { get; private set; }

        /// <summary>
        /// 是否全屏。
        /// </summary>
        public bool FullScreen { get; private set; }

        /// <summary>
        /// 是否已打开（用于暂停/恢复判定，只读）。
        /// </summary>
        public bool IsOpened { get; private set; }

        /// <summary>
        /// 用户逻辑组件引用。
        /// </summary>
        private UIFormLogic uiFormLogic;

        /// <summary>
        /// 设置 UI 属性（由 Helper 调用，在 OnInit 之前）。
        /// </summary>
        public void Init(string assetName, int windowLayer, bool fullScreen)
        {
            AssetName = assetName;
            WindowLayer = windowLayer;
            FullScreen = fullScreen;
        }

        /// <summary>
        /// 生命周期：初始化。由 UIModule 通过 IUIForm 接口调用。
        /// </summary>
        void IUIForm.OnInit(object userData)
        {
            if (this == null)
            {
                return;
            }

            uiFormLogic = GetComponent<UIFormLogic>();
            if (uiFormLogic == null)
            {
                uiFormLogic = gameObject.AddComponent<UIFormLogic>();
            }

            uiFormLogic.OnInit(this, userData);
        }

        /// <summary>
        /// 生命周期：打开。由 UIModule 通过 IUIForm 接口调用。
        /// </summary>
        void IUIForm.OnOpen(object userData)
        {
            if (this == null)
            {
                return;
            }

            IsOpened = true;
            gameObject.SetActive(true);

            if (uiFormLogic != null)
            {
                uiFormLogic.OnOpen(userData);
            }
        }

        /// <summary>
        /// 生命周期：暂停。由 UIModule 通过 IUIForm 接口调用。
        /// </summary>
        void IUIForm.OnPause()
        {
            if (this == null)
            {
                return;
            }

            if (uiFormLogic != null)
            {
                uiFormLogic.OnPause();
            }
        }

        /// <summary>
        /// 生命周期：恢复。由 UIModule 通过 IUIForm 接口调用。
        /// </summary>
        void IUIForm.OnResume()
        {
            if (this == null)
            {
                return;
            }

            if (uiFormLogic != null)
            {
                uiFormLogic.OnResume();
            }
        }

        /// <summary>
        /// 生命周期：关闭。由 UIModule 通过 IUIForm 接口调用。
        /// </summary>
        void IUIForm.OnClose(object userData)
        {
            if (this == null)
            {
                return;
            }

            IsOpened = false;

            if (uiFormLogic != null)
            {
                uiFormLogic.OnClose(userData);
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 生命周期：每帧更新。由 UIModule 通过 IUIForm 接口调用。
        /// </summary>
        void IUIForm.OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (this == null)
            {
                return;
            }

            if (uiFormLogic != null && IsOpened)
            {
                uiFormLogic.OnUpdate(elapseSeconds, realElapseSeconds);
            }
        }
    }
}
