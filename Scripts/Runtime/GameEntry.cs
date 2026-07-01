using System.ComponentModel;
using Unity.Collections;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架入口组件。
    /// 挂在启动场景的 "RFramework" 根节点上，
    /// 提供所有内置模块的强类型静态访问入口，以及自定义组件的泛型查询方法。
    /// </summary>
    /// <remarks>
    /// 设计约束：
    /// 1. 内置模块属性采用 static 缓存 + 懒加载模式：
    ///    - 首次访问时从 ComponentEntry 查找并缓存。
    ///    - 后续访问直接返回缓存，避免每次 O(N) 遍历。
    ///    - Unity 重载了 == 运算符，MonoBehaviour 销毁后 cache == null 为 true，
    ///      因此框架重启后缓存自动失效，无需手动清理。
    /// 2. Inspector 字段仅用于编辑器下的可视化发现——OnValidate 自动填充。
    /// 3. 不暴露 Instance 单例——外部代码不需要访问 GameEntry 实例本身。
    /// </remarks>
    [AddComponentMenu("UnityRFramework/Game Entry")]
    [DisallowMultipleComponent]
    public sealed class GameEntry : MonoBehaviour
    {


        // ====== 内置模块缓存（static，利用 Unity == 重载自动失效） ======
        private static BaseComponent baseCache;
        private static PoolComponent poolCache;

        // ====== 强类型静态快捷入口（内置模块） ======

        /// <summary>
        /// 获取框架基础组件（帧率、日志、Helpers 等全局设置）。
        /// </summary>
        public static BaseComponent Base
        {
            get
            {
                if (baseCache == null)
                    baseCache = Get<BaseComponent>();
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
                    poolCache = Get<PoolComponent>();
                return poolCache;
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