
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
        private static readonly LinkedList<UnityRFrameworkComponent> unityRFrameworkComponents = new LinkedList<UnityRFrameworkComponent>();

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
            Log.Info("Shutdown Game Framework ({0})...", shutdownType);
            BaseComponent baseComponent = GetComponent<BaseComponent>();
            if (baseComponent != null)
            {
                baseComponent.Shutdown();
                baseComponent = null;
            }

            unityRFrameworkComponents.Clear();

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
