using UnityEngine;
using RFramework;
using RFramework.Event;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 事件组件。作为 EventModule 的运行时包装层，绑定 Unity 生命周期，
    /// 暴露 Inspector 统计信息，转发所有事件操作到 EventModule。
    /// </summary>
    /// <remarks>
    /// 设计约束：不包含业务逻辑（逻辑在 EventModule 中），保持 80 行以内。
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Event")]
    [DisallowMultipleComponent]
    public sealed class EventComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 事件模块引用，由 Awake 从 RFrameworkModuleEntry 获取并缓存。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 获取已注册的事件处理函数总数（运行时只读）。
        /// </summary>
        public int HandlerCount
        {
            get { return eventModule != null ? eventModule.HandlerCount : 0; }
        }

        /// <summary>
        /// 获取异步队列中待处理的事件数（运行时只读）。
        /// </summary>
        public int AsyncEventCount
        {
            get { return eventModule != null ? eventModule.AsyncEventCount : 0; }
        }

        protected override void Awake()
        {
            base.Awake();
            eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
        }

        /// <inheritdoc cref="IEventModule.Count{T}"/>
        public int Count<T>()
        {
            return eventModule.Count<T>();
        }

        /// <inheritdoc cref="IEventModule.Subscribe{T}"/>
        public void Subscribe<T>(System.Action<T> handler)
        {
            eventModule.Subscribe(handler);
        }

        /// <inheritdoc cref="IEventModule.Unsubscribe{T}"/>
        public void Unsubscribe<T>(System.Action<T> handler)
        {
            eventModule.Unsubscribe(handler);
        }

        /// <inheritdoc cref="IEventModule.Fire{T}"/>
        public void Fire<T>(T args)
        {
            try
            {
                eventModule.Fire(args);
            }
            catch (RFrameworkException e)
            {
                // Library 层以 RFrameworkException 上报派发异常，Runtime 层在此转日志
                Log.Error(e.ToString());
            }
        }

        /// <inheritdoc cref="IEventModule.FireAsync{T}"/>
        public void FireAsync<T>(T args)
        {
            eventModule.FireAsync(args);
        }

        /// <inheritdoc cref="IEventModule.CreateGroup"/>
        public EventGroup CreateGroup()
        {
            return eventModule.CreateGroup();
        }
    }
}
