using System;
using System.Collections.Generic;

namespace UnityRFramework.Sample.Messenger
{
    /// <summary>
    /// 小型项目使用的字符串消息总线。正式框架业务事件请使用 EventModule。
    /// </summary>
    public static class Messenger
    {
        private static readonly Dictionary<string, Delegate> Handlers = new Dictionary<string, Delegate>();

        public static void Subscribe(string message, Action handler)
        {
            Add(message, handler);
        }

        public static void Subscribe<T>(string message, Action<T> handler)
        {
            Add(message, handler);
        }

        public static void Unsubscribe(string message, Action handler)
        {
            Remove(message, handler);
        }

        public static void Unsubscribe<T>(string message, Action<T> handler)
        {
            Remove(message, handler);
        }

        public static void Publish(string message)
        {
            if (Handlers.TryGetValue(message, out Delegate handler))
            {
                if (handler is Action callback)
                {
                    callback.Invoke();
                    return;
                }

                throw new InvalidOperationException($"消息 '{message}' 的参数类型与发布方式不匹配。");
            }
        }

        public static void Publish<T>(string message, T value)
        {
            if (Handlers.TryGetValue(message, out Delegate handler))
            {
                if (handler is Action<T> callback)
                {
                    callback.Invoke(value);
                    return;
                }

                throw new InvalidOperationException($"消息 '{message}' 的参数类型与发布方式不匹配。");
            }
        }

        public static void Clear(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Handlers.Remove(message);
            }
        }

        public static void ClearAll()
        {
            Handlers.Clear();
        }

        private static void Add(string message, Delegate handler)
        {
            if (string.IsNullOrWhiteSpace(message) || handler == null)
            {
                throw new ArgumentException("消息名和处理器不能为空。");
            }

            if (Handlers.TryGetValue(message, out Delegate current))
            {
                if (current.GetType() != handler.GetType())
                {
                    throw new InvalidOperationException($"消息 '{message}' 已使用其他参数类型注册。");
                }

                Handlers[message] = Delegate.Combine(current, handler);
                return;
            }

            Handlers.Add(message, handler);
        }

        private static void Remove(string message, Delegate handler)
        {
            if (!Handlers.TryGetValue(message, out Delegate current))
            {
                return;
            }

            if (current.GetType() != handler.GetType())
            {
                throw new InvalidOperationException($"消息 '{message}' 的参数类型与取消订阅方式不匹配。");
            }

            Delegate remaining = Delegate.Remove(current, handler);
            if (remaining == null)
            {
                Handlers.Remove(message);
            }
            else
            {
                Handlers[message] = remaining;
            }
        }
    }
}
