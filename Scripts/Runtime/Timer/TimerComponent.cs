using UnityEngine;

using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 计时器组件。作为 TimerModule 的运行时包装层，绑定 Unity 生命周期，转发所有计时器操作到 TimerModule。
    /// </summary>
    /// <remarks>
    /// 设计约束：不包含业务逻辑（逻辑在 TimerModule 中），保持 50 行以内。
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Timer")]
    [DisallowMultipleComponent]
    public sealed class TimerComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 计时器模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private ITimerModule timerModule;

        /// <summary>
        /// 获取当前活跃的计时器数量。
        /// </summary>
        public int TimerCount
        {
            get { return timerModule != null ? timerModule.TimerCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();
            timerModule = RFrameworkModuleEntry.GetModule<ITimerModule>();
        }

        /// <inheritdoc cref="ITimerModule.RegisterTimer"/>
        public void RegisterTimer(Timer timer)
        {
            timerModule.RegisterTimer(timer);
        }

        /// <inheritdoc cref="ITimerModule.CancelTimer"/>
        public void CancelTimer(Timer timer)
        {
            timerModule.CancelTimer(timer);
        }

        /// <inheritdoc cref="ITimerModule.CancelAllTimers"/>
        public void CancelAllTimers()
        {
            timerModule.CancelAllTimers();
        }
    }
}
