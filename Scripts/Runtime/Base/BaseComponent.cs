

using RFramework;
using System;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 基础组件。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UnityRFramework/Base")]
    [DefaultExecutionOrder(-100)]
    public sealed class BaseComponent : UnityRFrameworkComponent
    {
        /// <summary>
        /// 默认 DPI（Windows 标准 96）。
        /// 当 Unity 无法获取屏幕 DPI 时使用此值。
        /// </summary>
        private const int DefaultDpi = 96;  // default windows dpi

        /// <summary>
        /// 暂停前的游戏速度缓存，用于 ResumeGame 时恢复原速度。
        /// </summary>
        private float gameSpeedBeforePause = 1f;



        [SerializeField]
        private string textHelperTypeName = "UnityRFramework.Runtime.DefaultTextHelper";

        [SerializeField]
        private string logHelperTypeName = "UnityRFramework.Runtime.DefaultLogHelper";

        [SerializeField]
        private string jsonHelperTypeName = "UnityRFramework.Runtime.DefaultJsonHelper";

        [SerializeField]
        private int frameRate = 30;

        [SerializeField]
        private float gameSpeed = 1f;

        [SerializeField]
        private bool runInBackground = true;

        [SerializeField]
        private bool neverSleep = true;




        /// <summary>
        /// 获取或设置游戏帧率。
        /// </summary>
        public int FrameRate
        {
            get
            {
                return frameRate;
            }
            set
            {
                Application.targetFrameRate = frameRate = value;
            }
        }

        /// <summary>
        /// 获取或设置游戏速度。
        /// </summary>
        public float GameSpeed
        {
            get
            {
                return gameSpeed;
            }
            set
            {
                Time.timeScale = gameSpeed = value >= 0f ? value : 0f;
            }
        }

        /// <summary>
        /// 获取游戏是否暂停。
        /// </summary>
        public bool IsGamePaused
        {
            get
            {
                return gameSpeed <= 0f;
            }
        }

        /// <summary>
        /// 获取是否正常游戏速度。
        /// </summary>
        public bool IsNormalGameSpeed
        {
            get
            {
                return gameSpeed == 1f;
            }
        }

        /// <summary>
        /// 获取或设置是否允许后台运行。
        /// </summary>
        public bool RunInBackground
        {
            get
            {
                return runInBackground;
            }
            set
            {
                Application.runInBackground = runInBackground = value;
            }
        }

        /// <summary>
        /// 获取或设置是否禁止休眠。
        /// </summary>
        public bool NeverSleep
        {
            get
            {
                return neverSleep;
            }
            set
            {
                neverSleep = value;
                Screen.sleepTimeout = value ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            }
        }

        /// <summary>
        /// 游戏框架组件初始化。
        /// </summary>
        /// <summary>
        /// 生命周期：唤醒并初始化所有 Helper 和框架设置。
        /// 由 [DefaultExecutionOrder(-100)] 确保最先执行。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            InitLogHelper();
            InitTextHelper();

            Log.Info("Unity Version: {0}", Application.unityVersion);

#if UNITY_5_3_OR_NEWER || UNITY_5_3
            InitJsonHelper();

            Utility.Converter.ScreenDpi = Screen.dpi;
            if (Utility.Converter.ScreenDpi <= 0)
            {
                Utility.Converter.ScreenDpi = DefaultDpi;
            }



            Application.targetFrameRate = frameRate;
            Time.timeScale = gameSpeed;
            Application.runInBackground = runInBackground;
            Screen.sleepTimeout = neverSleep ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
#else
            Log.Error(" UnityRFramework only applies with Unity 5.3 and above, but current Unity version is {0}.", Application.unityVersion);
            UnityRFrameworkComponentEntry.Shutdown(ShutdownType.Quit);
#endif
#if UNITY_5_6_OR_NEWER
            Application.lowMemory += OnLowMemory;
#endif
        }

        /// <summary>
        /// 生命周期：Start（空实现，供未来扩展）。
        /// </summary>
        private void Start()
        {
        }

        /// <summary>
        /// 生命周期：轮询。驱动 RFrameworkModuleEntry.Update。
        /// </summary>
        private void Update()
        {
            try
            {
                RFrameworkModuleEntry.Update(Time.deltaTime, Time.unscaledDeltaTime);
            }
            catch (RFrameworkException e)
            {
                // 模块轮询（含异步事件派发）抛出的框架异常在此转日志，避免整帧崩溃
                Log.Error(e.ToString());
            }
        }

        /// <summary>
        /// 生命周期：应用退出。注销低内存回调，停止所有协程。
        /// </summary>
        private void OnApplicationQuit()
        {
#if UNITY_5_6_OR_NEWER
            Application.lowMemory -= OnLowMemory;
#endif
            StopAllCoroutines();
        }

        /// <summary>
        /// 生命周期：销毁。关闭所有框架模块并释放资源。
        /// </summary>
        private void OnDestroy()
        {
            try
            {
                RFrameworkModuleEntry.Shutdown(false);
            }
            catch (RFrameworkException ex)
            {
                if (RFrameworkLog.IsInitialized)
                {
                    try
                    {
                        Log.Error(ex.ToString());
                    }
                    catch
                    {
                        // 关闭阶段已无可用日志后端时，优先保证框架完成清理。
                    }
                }
            }
            finally
            {
                RFrameworkLog.SetLogHelper(null);
            }
        }

        /// <summary>
        /// 暂停游戏。
        /// </summary>
        public void PauseGame()
        {
            if (IsGamePaused)
            {
                return;
            }

            gameSpeedBeforePause = GameSpeed;
            GameSpeed = 0f;
        }

        /// <summary>
        /// 恢复游戏。
        /// </summary>
        public void ResumeGame()
        {
            if (!IsGamePaused)
            {
                return;
            }

            GameSpeed = gameSpeedBeforePause;
        }

        /// <summary>
        /// 重置为正常游戏速度。
        /// </summary>
        public void ResetNormalGameSpeed()
        {
            if (IsNormalGameSpeed)
            {
                return;
            }

            GameSpeed = 1f;
        }

        internal void Shutdown()
        {
            Destroy(gameObject);
        }

        private void InitTextHelper()
        {
            if (string.IsNullOrEmpty(textHelperTypeName))
            {
                return;
            }

            Type textHelperType = Utility.Assembly.GetType(textHelperTypeName);
            if (textHelperType == null)
            {
                Log.Error("Can not find text helper type '{0}'.", textHelperTypeName);
                return;
            }

            Utility.Text.ITextHelper textHelper = (Utility.Text.ITextHelper)Activator.CreateInstance(textHelperType);
            if (textHelper == null)
            {
                Log.Error("Can not create text helper instance '{0}'.", textHelperTypeName);
                return;
            }

            Utility.Text.SetTextHelper(textHelper);
        }

        /// <summary>
        /// 初始化日志辅助器，通过反射创建 ILogHelper 实例并注入到 RFrameworkLog。
        /// 必须在所有其他 InitXxxHelper 之前调用（后续代码依赖 Log 输出）。
        /// </summary>
        private void InitLogHelper()
        {
            if (string.IsNullOrEmpty(logHelperTypeName))
            {
                return;
            }

            Type logHelperType = Utility.Assembly.GetType(logHelperTypeName);
            if (logHelperType == null)
            {
                throw new RFrameworkException(Utility.Text.Format("Can not find log helper type '{0}'.", logHelperTypeName));
            }

            RFrameworkLog.ILogHelper logHelper = (RFrameworkLog.ILogHelper)Activator.CreateInstance(logHelperType);
            if (logHelper == null)
            {
                throw new RFrameworkException(Utility.Text.Format("Can not create log helper instance '{0}'.", logHelperTypeName));
            }

            RFrameworkLog.SetLogHelper(logHelper);
        }

        /// <summary>
        /// 初始化 JSON 辅助器，通过反射创建 IJsonHelper 实例并注入到 Utility.Json。
        /// </summary>
        private void InitJsonHelper()
        {
            if (string.IsNullOrEmpty(jsonHelperTypeName))
            {
                return;
            }

            Type jsonHelperType = Utility.Assembly.GetType(jsonHelperTypeName);
            if (jsonHelperType == null)
            {
                Log.Error("Can not find JSON helper type '{0}'.", jsonHelperTypeName);
                return;
            }

            Utility.Json.IJsonHelper jsonHelper = (Utility.Json.IJsonHelper)Activator.CreateInstance(jsonHelperType);
            if (jsonHelper == null)
            {
                Log.Error("Can not create JSON helper instance '{0}'.", jsonHelperTypeName);
                return;
            }

            Utility.Json.SetJsonHelper(jsonHelper);
        }

        /// <summary>
        /// 低内存回调（仅 Unity 5.6+ 有效）。
        /// 当系统报告内存不足时触发，释放对象池空闲对象和未使用的资源。
        /// </summary>
        private void OnLowMemory()
        {
            Log.Info("Low memory reported, releasing unused resources...");

            PoolComponent poolComponent = UnityRFrameworkComponentEntry.GetComponent<PoolComponent>();
            if (poolComponent != null)
            {
                poolComponent.ReleaseAllUnused();
            }

            ResourceComponent resourceComponent = UnityRFrameworkComponentEntry.GetComponent<ResourceComponent>();
            if (resourceComponent != null)
            {
                resourceComponent.UnloadUnusedAssets();
            }
        }
    }
}
