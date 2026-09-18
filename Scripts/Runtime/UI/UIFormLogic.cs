using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// UI 逻辑基类。用户继承此类编写 UI 行为。
    /// 生命周期方法使用 protected internal virtual：
    /// - protected：子类可重写
    /// - internal：UIForm（同程序集）可调用
    /// - 外部不可直接调用
    /// </summary>
    public class UIFormLogic : MonoBehaviour
    {
        /// <summary>
        /// 所属的 UIForm 包装器引用。
        /// </summary>
        public UIForm Owner { get; private set; }

        /// <summary>
        /// 初始化回调。资源实例化后首次创建时调用。
        /// </summary>
        protected internal virtual void OnInit(UIForm owner, object userData)
        {
            Owner = owner;
        }

        /// <summary>
        /// 打开回调。每次显示时调用。
        /// </summary>
        protected internal virtual void OnOpen(object userData)
        {
        }

        /// <summary>
        /// 暂停回调。被其他全屏 UI 覆盖时调用。
        /// </summary>
        protected internal virtual void OnPause()
        {
        }

        /// <summary>
        /// 恢复回调。覆盖的 UI 关闭后恢复时调用。
        /// </summary>
        protected internal virtual void OnResume()
        {
        }

        /// <summary>
        /// 关闭回调。关闭时调用。
        /// </summary>
        protected internal virtual void OnClose(object userData)
        {
        }

        /// <summary>
        /// 轮询回调。每帧调用（仅在 UI 打开状态时）。
        /// </summary>
        protected internal virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }
    }
}
