using System;
using System.Collections.Generic;
using RFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 保存当前框架组件，并协调停止、重启和退出流程。
    /// </summary>
    public static class UnityRFrameworkRuntime
    {
        private const int StartupSceneIndex = 0;

        private static readonly Dictionary<Type, UnityRFrameworkComponent> Components =
            new Dictionary<Type, UnityRFrameworkComponent>();

        private static bool restartPending;
        private static bool isShuttingDown;

        /// <summary>
        /// 获取已注册组件数量。
        /// </summary>
        public static int ComponentCount => Components.Count;

        /// <summary>
        /// 获取指定类型的框架组件。
        /// </summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <returns>组件实例；未注册时返回 null。</returns>
        public static T Get<T>() where T : UnityRFrameworkComponent
        {
            return Components.TryGetValue(typeof(T), out UnityRFrameworkComponent component)
                ? (T)component
                : null;
        }

        /// <summary>
        /// 获取指定类型的框架组件。
        /// </summary>
        /// <param name="componentType">组件类型。</param>
        /// <returns>组件实例；未注册时返回 null。</returns>
        public static UnityRFrameworkComponent Get(Type componentType)
        {
            if (componentType == null)
            {
                throw new RFrameworkException("Framework component type cannot be null.");
            }

            Components.TryGetValue(componentType, out UnityRFrameworkComponent component);
            return component;
        }

        /// <summary>
        /// 按完整类型名或短类型名查找框架组件。
        /// </summary>
        /// <param name="typeName">组件类型名。</param>
        /// <returns>组件实例；未注册时返回 null。</returns>
        public static UnityRFrameworkComponent Get(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            foreach (KeyValuePair<Type, UnityRFrameworkComponent> pair in Components)
            {
                if (string.Equals(pair.Key.FullName, typeName, StringComparison.Ordinal)
                    || string.Equals(pair.Key.Name, typeName, StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 停止当前框架，并执行指定的应用行为。
        /// </summary>
        /// <param name="mode">停止后的行为。</param>
        public static void Shutdown(UnityRFrameworkShutdownMode mode)
        {
            if (isShuttingDown)
            {
                return;
            }

            isShuttingDown = true;
            restartPending = mode == UnityRFrameworkShutdownMode.Restart;
            Log.Info("[UnityRFramework] Framework shutdown requested. Mode: {0}.", mode);

            UnityRFrameworkController controller = Get<UnityRFrameworkController>();
            if (controller != null)
            {
                controller.StopFramework(mode);
            }
            else
            {
                ResetComponents();
            }

            switch (mode)
            {
                case UnityRFrameworkShutdownMode.Destroy:
                    isShuttingDown = false;
                    break;

                case UnityRFrameworkShutdownMode.Restart:
                    SceneManager.LoadScene(StartupSceneIndex);
                    break;

                case UnityRFrameworkShutdownMode.Quit:
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                    break;
            }
        }

        /// <summary>
        /// 登记框架组件，并拒绝同类型重复实例。
        /// </summary>
        /// <param name="component">待登记组件。</param>
        internal static void Register(UnityRFrameworkComponent component)
        {
            if (component == null)
            {
                throw new RFrameworkException("Cannot register a null framework component.");
            }

            Type componentType = component.GetType();
            if (Components.ContainsKey(componentType))
            {
                throw new RFrameworkException(
                    $"Framework component '{componentType.FullName}' is already registered.");
            }

            Components.Add(componentType, component);
        }

        /// <summary>
        /// 标记框架启动完成并通知订阅者。
        /// </summary>
        internal static void NotifyStarted()
        {
            isShuttingDown = false;
            Log.Info(
                "[UnityRFramework] Framework startup completed. Registered components: {0}.",
                Components.Count);

            if (restartPending)
            {
                restartPending = false;
                Log.Info("[UnityRFramework] Framework restart completed.");
            }
        }

        /// <summary>
        /// 清空 Runtime 组件缓存并恢复未启动状态。
        /// </summary>
        internal static void ResetComponents()
        {
            Components.Clear();
            GameEntry.ClearCachedComponents();
        }
    }
}
