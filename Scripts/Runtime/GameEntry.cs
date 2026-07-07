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

        /// <summary>
        /// 基础组件缓存。
        /// </summary>
        private static BaseComponent baseCache;

        /// <summary>
        /// 对象池组件缓存。
        /// </summary>
        private static PoolComponent poolCache;

        /// <summary>
        /// 事件组件缓存。
        /// </summary>
        private static EventComponent eventCache;

        /// <summary>
        /// 计时器组件缓存。
        /// </summary>
        private static TimerComponent timerCache;

        /// <summary>
        /// 资源组件缓存。
        /// </summary>
        private static ResourceComponent resourceCache;

        /// <summary>
        /// 配置组件缓存。
        /// </summary>
        private static ConfigComponent configCache;

        /// <summary>
        /// 有限状态机组件缓存。
        /// </summary>
        private static FsmComponent fsmCache;

        /// <summary>
        /// 流程组件缓存。
        /// </summary>
        private static ProcedureComponent procedureCache;

        /// <summary>
        /// WebRequest 组件缓存。
        /// </summary>
        private static WebRequestComponent webRequestCache;

        /// <summary>
        /// 实体组件缓存。
        /// </summary>
        private static EntityComponent entityCache;

        /// <summary>
        /// 场景组件缓存。
        /// </summary>
        private static SceneComponent sceneCache;

        /// <summary>
        /// UI 组件缓存。
        /// </summary>
        private static UIComponent uiCache;

        /// <summary>
        /// 音频组件缓存。
        /// </summary>
        private static AudioComponent audioCache;

        /// <summary>
        /// 本地化组件缓存。
        /// </summary>
        private static LocalizationComponent localizationCache;

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

        /// <summary>
        /// 获取计时器组件。
        /// </summary>
        public static TimerComponent Timer
        {
            get
            {
                if (timerCache == null)
                {
                    timerCache = Get<TimerComponent>();
                }
                return timerCache;
            }
        }

        /// <summary>
        /// 获取资源组件。
        /// </summary>
        public static ResourceComponent Resource
        {
            get
            {
                if (resourceCache == null)
                {
                    resourceCache = Get<ResourceComponent>();
                }
                return resourceCache;
            }
        }

        /// <summary>
        /// 获取配置组件。
        /// </summary>
        public static ConfigComponent Config
        {
            get
            {
                if (configCache == null)
                {
                    configCache = Get<ConfigComponent>();
                }
                return configCache;
            }
        }

        /// <summary>
        /// 获取有限状态机组件。
        /// </summary>
        public static FsmComponent Fsm
        {
            get
            {
                if (fsmCache == null)
                {
                    fsmCache = Get<FsmComponent>();
                }
                return fsmCache;
            }
        }

        /// <summary>
        /// 获取流程组件。
        /// </summary>
        public static ProcedureComponent Procedure
        {
            get
            {
                if (procedureCache == null)
                {
                    procedureCache = Get<ProcedureComponent>();
                }
                return procedureCache;
            }
        }

        /// <summary>
        /// 获取 WebRequest 组件。
        /// </summary>
        public static WebRequestComponent WebRequest
        {
            get
            {
                if (webRequestCache == null)
                {
                    webRequestCache = Get<WebRequestComponent>();
                }
                return webRequestCache;
            }
        }

        /// <summary>
        /// 获取场景组件。
        /// </summary>
        public static SceneComponent Scene
        {
            get
            {
                if (sceneCache == null)
                {
                    sceneCache = Get<SceneComponent>();
                }
                return sceneCache;
            }
        }

        /// <summary>
        /// 获取 UI 组件。
        /// </summary>
        public static UIComponent UI
        {
            get
            {
                if (uiCache == null)
                {
                    uiCache = Get<UIComponent>();
                }
                return uiCache;
            }
        }

        /// <summary>
        /// 获取音频组件。
        /// </summary>
        public static AudioComponent Audio
        {
            get
            {
                if (audioCache == null)
                {
                    audioCache = Get<AudioComponent>();
                }
                return audioCache;
            }
        }

        /// <summary>
        /// 获取本地化组件。
        /// </summary>
        public static LocalizationComponent Localization
        {
            get
            {
                if (localizationCache == null)
                {
                    localizationCache = Get<LocalizationComponent>();
                }
                return localizationCache;
            }
        }

        /// <summary>
        /// 获取实体组件。
        /// </summary>
        public static EntityComponent Entity
        {
            get
            {
                if (entityCache == null)
                {
                    entityCache = Get<EntityComponent>();
                }
                return entityCache;
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

        /// <summary>
        /// 生命周期：唤醒。设置 DontDestroyOnLoad，确保框架节点跨场景存活。
        /// </summary>
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}