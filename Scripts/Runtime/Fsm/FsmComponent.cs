using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 有限状态机组件。作为 FsmModule 的运行时包装层，
    /// 绑定 Unity 生命周期，暴露 Inspector 统计信息，管理 FSM 实例的创建与销毁。
    /// </summary>
    /// <remarks>
    /// 设计约束：不包含业务逻辑（逻辑在 FsmModule 中）。
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Fsm")]
    [DisallowMultipleComponent]
    public sealed class FsmComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 有限状态机模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IFsmModule fsmModule;

        /// <summary>
        /// 获取当前活跃的 FSM 实例总数。
        /// </summary>
        public int FsmCount
        {
            get { return fsmModule != null ? fsmModule.FsmCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();
            fsmModule = RFrameworkModuleEntry.GetModule<IFsmModule>();
        }

        /// <inheritdoc cref="IFsmModule.CreateFsm{TOwner}"/>
        public IFsm CreateFsm<TOwner>(TOwner owner, params IFsmState[] states) where TOwner : class
        {
            return fsmModule.CreateFsm(owner, states);
        }

        /// <inheritdoc cref="IFsmModule.DestroyFsm"/>
        public bool DestroyFsm(IFsm fsm)
        {
            return fsmModule.DestroyFsm(fsm);
        }
    }
}
