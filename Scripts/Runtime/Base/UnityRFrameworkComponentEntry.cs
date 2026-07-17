
using RFramework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 游戏入口，管理所有 UnityRFrameworkComponent 的注册与查询。
    /// </summary>
    /// <remarks>
    /// 使用 C# 标准 LinkedList 管理 UnityRFrameworkComponent，
    /// 不再封装自定义 LinkedList 包装类——标准库已足够。
    /// </remarks>
    public static class UnityRFrameworkComponentEntry
    {
        /// <summary>
        /// 已注册的游戏框架组件链表。
        /// </summary>
        private static readonly LinkedList<UnityRFrameworkComponent> unityRFrameworkComponents = new LinkedList<UnityRFrameworkComponent>();

        /// <summary>
        /// 是否正在等待重启后的新框架完成启动。
        /// </summary>
        private static bool restartPending;

        /// <summary>
        /// 游戏框架所在的场景编号。
        /// </summary>
        internal const int UnityRFrameworkSceneId = 0;

        /// <summary>
        /// 获取游戏框架组件。
        /// </summary>
        /// <typeparam name="T">要获取的游戏框架组件类型。</typeparam>
        /// <returns>要获取的游戏框架组件。</returns>
        public static T GetComponent<T>() where T : UnityRFrameworkComponent
        {
            return (T)GetComponent(typeof(T));
        }

        /// <summary>
        /// 获取游戏框架组件。
        /// </summary>
        /// <param name="type">要获取的游戏框架组件类型。</param>
        /// <returns>要获取的游戏框架组件。</returns>
        public static UnityRFrameworkComponent GetComponent(Type type)
        {
            LinkedListNode<UnityRFrameworkComponent> current = unityRFrameworkComponents.First;
            while (current != null)
            {
                if (current.Value.GetType() == type)
                {
                    return current.Value;
                }

                current = current.Next;
            }

            return null;
        }

        /// <summary>
        /// 获取游戏框架组件。
        /// </summary>
        /// <param name="typeName">要获取的游戏框架组件类型名称。</param>
        /// <returns>要获取的游戏框架组件。</returns>
        public static UnityRFrameworkComponent GetComponent(string typeName)
        {
            LinkedListNode<UnityRFrameworkComponent> current = unityRFrameworkComponents.First;
            while (current != null)
            {
                Type type = current.Value.GetType();
                if (type.FullName == typeName || type.Name == typeName)
                {
                    return current.Value;
                }

                current = current.Next;
            }

            return null;
        }

        /// <summary>
        /// 关闭游戏框架。
        /// </summary>
        /// <param name="shutdownType">关闭游戏框架类型。</param>
        public static void Shutdown(ShutdownType shutdownType)
        {
            if (RFrameworkLog.IsInitialized)
            {
                switch (shutdownType)
                {
                    case ShutdownType.Restart:
                        Log.Info("[UnityRFramework] Framework restart requested.");
                        break;
                    case ShutdownType.Quit:
                        Log.Info("[UnityRFramework] Framework quit requested.");
                        break;
                    default:
                        Log.Info("[UnityRFramework] Framework shutdown requested.");
                        break;
                }
            }

            if (shutdownType == ShutdownType.Restart)
            {
                restartPending = true;
            }

            BaseComponent baseComponent = GetComponent<BaseComponent>();
            if (baseComponent != null)
            {
                // 先同步关闭模块，再销毁根节点，避免新启动场景与旧模块交叉运行。
                baseComponent.Shutdown(shutdownType);
                baseComponent = null;
            }

            unityRFrameworkComponents.Clear();
            GameEntry.ClearCachedComponents();

            if (shutdownType == ShutdownType.None)
            {
                return;
            }

            if (shutdownType == ShutdownType.Restart)
            {
                SceneManager.LoadScene(UnityRFrameworkSceneId);
                return;
            }

            if (shutdownType == ShutdownType.Quit)
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
                return;
            }
        }

        /// <summary>
        /// 报告框架启动完成，并闭合可能存在的软重启生命周期日志。
        /// </summary>
        internal static void NotifyStartupCompleted()
        {
            Log.Info("[UnityRFramework] Framework startup completed. Registered components: {0}.", unityRFrameworkComponents.Count);
            if (!restartPending)
            {
                return;
            }

            restartPending = false;
            Log.Info("[UnityRFramework] Framework restart completed.");
        }

        /// <summary>
        /// 注册游戏框架组件。
        /// </summary>
        /// <param name="gameFrameworkComponent">要注册的游戏框架组件。</param>
        internal static void RegisterComponent(UnityRFrameworkComponent gameFrameworkComponent)
        {
            if (gameFrameworkComponent == null)
            {
                Log.Error("Game Framework component is invalid.");
                return;
            }

            Type type = gameFrameworkComponent.GetType();

            LinkedListNode<UnityRFrameworkComponent> current = unityRFrameworkComponents.First;
            while (current != null)
            {
                if (current.Value.GetType() == type)
                {
                    Log.Error("Game Framework component type '{0}' is already exist.", type.FullName);
                    return;
                }

                current = current.Next;
            }

            unityRFrameworkComponents.AddLast(gameFrameworkComponent);
        }
    }
}
