using System;
using R3;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>将框架事件接入 R3；每次 Subscribe 独立订阅，Dispose 自动退订。</summary>
    public static class EventObservableExtensions
    {
        /// <summary>观察当前事件组件。组件关闭前应释放订阅；软重启后从新组件重新订阅。</summary>
        public static Observable<T> Observe<T>(this EventComponent events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            return Observable.FromEvent<T>(events.Subscribe<T>, events.Unsubscribe<T>);
        }

        /// <summary>观察指定事件模块，不创建新事件总线，也不改变原有派发线程。</summary>
        public static Observable<T> Observe<T>(this IEventModule events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            return Observable.FromEvent<T>(events.Subscribe<T>, events.Unsubscribe<T>);
        }
    }
}
