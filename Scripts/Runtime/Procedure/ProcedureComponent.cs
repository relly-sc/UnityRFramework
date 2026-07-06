using RFramework;
using RFramework.Procedure;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 流程组件。作为 ProcedureModule 的运行时包装层，
    /// 绑定 Unity 生命周期，暴露 Inspector 统计信息，转发所有流程操作到 ProcedureModule。
    /// </summary>
    /// <remarks>
    /// 使用方式：在游戏启动脚本中调用 Initialize() 注册所有流程状态，再调用 StartProcedure&lt;T&gt;() 启动。
    /// <code>
    /// // 在启动脚本中：
    /// GameEntry.Procedure.Initialize(new ProcedureLaunch(), new ProcedureCheckVersion(), new ProcedureLogin());
    /// GameEntry.Procedure.StartProcedure&lt;ProcedureLaunch&gt;();
    /// </code>
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Procedure")]
    [DisallowMultipleComponent]
    public sealed class ProcedureComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 流程模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IProcedureModule procedureModule;

        /// <summary>
        /// 获取跨状态数据黑板。
        /// </summary>
        public ProcedureBlackboard Blackboard
        {
            get { return procedureModule != null ? procedureModule.Blackboard : null; }
        }

        /// <summary>
        /// 获取当前运行中的流程状态实例（运行时只读）。
        /// </summary>
        public ProcedureStateBase CurrentProcedure
        {
            get { return procedureModule != null ? procedureModule.CurrentProcedure : null; }
        }

        /// <summary>
        /// 获取当前流程状态已运行的持续时间（秒）（运行时只读）。
        /// </summary>
        public float CurrentProcedureTime
        {
            get { return procedureModule != null ? procedureModule.CurrentProcedureTime : 0f; }
        }

        protected override void Awake()
        {
            base.Awake();
            procedureModule = RFrameworkModuleEntry.GetModule<IProcedureModule>();
        }

        /// <inheritdoc cref="IProcedureModule.Initialize"/>
        public void Initialize(params ProcedureStateBase[] procedures)
        {
            procedureModule.Initialize(procedures);
        }

        /// <inheritdoc cref="IProcedureModule.StartProcedure{T}"/>
        public void StartProcedure<T>() where T : ProcedureStateBase
        {
            procedureModule.StartProcedure<T>();
        }

        /// <inheritdoc cref="IProcedureModule.ChangeProcedure{T}"/>
        public void ChangeProcedure<T>() where T : ProcedureStateBase
        {
            procedureModule.ChangeProcedure<T>();
        }

        /// <inheritdoc cref="IProcedureModule.GetProcedure{T}"/>
        public T GetProcedure<T>() where T : ProcedureStateBase
        {
            return procedureModule.GetProcedure<T>();
        }

        /// <inheritdoc cref="IProcedureModule.HasProcedure{T}"/>
        public bool HasProcedure<T>() where T : ProcedureStateBase
        {
            return procedureModule.HasProcedure<T>();
        }
    }
}
