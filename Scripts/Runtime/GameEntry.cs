using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架入口组件。挂在启动场景的 "RFramework" 根节点上，
    /// 提供所有内置模块的强类型静态访问入口和自定义组件的泛型查询方法。
    /// </summary>
    /// <remarks>
    /// 内置模块属性采用 static 缓存 + 懒加载：首次访问查找并缓存，后续 O(1)。
    /// Unity 重载了 == 运算符，MonoBehaviour 销毁后缓存自动失效。不暴露 Instance 单例。
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Game Entry")]
    [DisallowMultipleComponent]
    public sealed class GameEntry : MonoBehaviour
    {
        // ====== 内置模块缓存（static，利用 Unity == 重载自动失效） ======
        private static BaseComponent baseCache;
        private static PoolComponent poolCache;
        private static EventComponent eventCache;

        // ====== 强类型静态快捷入口（内置模块） ======

        /// <summary>
        /// 获取框架基础组件（帧率、日志、Helpers 等全局设置）。
        /// </summary>
        public static BaseComponent Base
        {
            get
            {
                if (baseCache == null)
                {
                    baseCache = Get<BaseComponent>();
                }
                return baseCache;
            }
        }

        /// <summary>
        /// 获取对象池组件。
        /// </summary>
        public static PoolComponent Pool
        {
            get
            {
                if (poolCache == null)
                {
                    poolCache = Get<PoolComponent>();
                }
                return poolCache;
            }
        }

        /// <summary>
        /// 获取事件组件。
        /// </summary>
        public static EventComponent Event
        {
            get
            {
                if (eventCache == null)
                {
                    eventCache = Get<EventComponent>();
                }
                return eventCache;
            }
        }

        // ====== 泛型查询（自定义组件） ======

        /// <summary>
        /// 获取指定类型的框架组件。
        /// 用于用户自定义的 UnityRFrameworkComponent 子类，无需修改 GameEntry 代码。
        /// </summary>
        /// <typeparam name="T">框架组件类型，必须继承 UnityRFrameworkComponent。</typeparam>
        /// <returns>组件实例，未注册时返回 null。</returns>
        public static T Get<T>() where T : UnityRFrameworkComponent
        {
            return UnityRFrameworkComponentEntry.GetComponent<T>();
        }

        // ====== 生命周期 ======

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}