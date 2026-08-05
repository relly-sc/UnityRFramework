using System;
using RFramework;
using UnityEngine;
using UnityEngine.Serialization;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 初始化全局服务、应用运行设置，并逐帧驱动所有 Library 模块。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UnityRFramework/Controller")]
    [DefaultExecutionOrder(-10000)]
    public sealed class UnityRFrameworkController : UnityRFrameworkComponent
    {
        [FormerlySerializedAs("logHelperTypeName")]
        [SerializeField]
        private string logSinkTypeName = "UnityRFramework.Runtime.DefaultLogSink";

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

        private float speedBeforePause = 1f;
        private bool hasStopped;

        /// <summary>获取或设置目标帧率。</summary>
        public int FrameRate
        {
            get => frameRate;
            set
            {
                frameRate = value;
                Application.targetFrameRate = value;
            }
        }

        /// <summary>获取或设置全局时间缩放。</summary>
        public float GameSpeed
        {
            get => gameSpeed;
            set
            {
                gameSpeed = Mathf.Max(0f, value);
                Time.timeScale = gameSpeed;
            }
        }

        /// <summary>获取游戏时间是否已暂停。</summary>
        public bool IsGamePaused => gameSpeed <= 0f;

        /// <summary>获取游戏是否使用正常时间缩放。</summary>
        public bool IsNormalGameSpeed => Mathf.Approximately(gameSpeed, 1f);

        /// <summary>获取或设置应用是否在后台继续运行。</summary>
        public bool RunInBackground
        {
            get => runInBackground;
            set
            {
                runInBackground = value;
                Application.runInBackground = value;
            }
        }

        /// <summary>获取或设置应用是否禁止系统休眠。</summary>
        public bool NeverSleep
        {
            get => neverSleep;
            set
            {
                neverSleep = value;
                Screen.sleepTimeout = value
                    ? SleepTimeout.NeverSleep
                    : SleepTimeout.SystemSetting;
            }
        }

        /// <inheritdoc/>
        protected override void Awake()
        {
            base.Awake();
            InstallLogSink();
            Log.Info("[UnityRFramework] Framework startup started.");
            Log.Info("Unity Version: {0}", Application.unityVersion);

            InstallJsonHelper();
            ApplyRuntimeSettings();
            Application.lowMemory += HandleLowMemory;
        }

        private void Start()
        {
            UnityRFrameworkRuntime.NotifyStarted();
        }

        private void Update()
        {
            try
            {
                RFrameworkModuleHost.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            }
            catch (RFrameworkException ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private void OnApplicationQuit()
        {
            StopModules(UnityRFrameworkShutdownMode.Quit);
        }

        private void OnDestroy()
        {
            StopModules(UnityRFrameworkShutdownMode.Destroy);
        }

        /// <summary>暂停游戏时间。</summary>
        public void PauseGame()
        {
            if (IsGamePaused)
            {
                return;
            }

            speedBeforePause = GameSpeed;
            GameSpeed = 0f;
        }

        /// <summary>恢复暂停前的游戏速度。</summary>
        public void ResumeGame()
        {
            if (IsGamePaused)
            {
                GameSpeed = speedBeforePause;
            }
        }

        /// <summary>恢复正常游戏速度。</summary>
        public void ResetNormalGameSpeed()
        {
            GameSpeed = 1f;
        }

        /// <summary>
        /// 停止框架并销毁根对象。
        /// </summary>
        /// <param name="mode">关闭方式。</param>
        internal void StopFramework(UnityRFrameworkShutdownMode mode)
        {
            gameObject.SetActive(false);
            StopModules(mode);
            Destroy(gameObject);
        }

        private void StopModules(UnityRFrameworkShutdownMode mode)
        {
            if (hasStopped)
            {
                return;
            }

            hasStopped = true;
            Log.Info("[UnityRFramework] Framework shutdown started. Mode: {0}.", mode);
            Application.lowMemory -= HandleLowMemory;
            StopAllCoroutines();

            bool failed = false;
            try
            {
                RFrameworkModuleHost.StopAll();
            }
            catch (RFrameworkException ex)
            {
                failed = true;
                Log.Error(ex.ToString());
            }
            finally
            {
                Log.Info(
                    failed
                        ? "[UnityRFramework] Framework shutdown completed with module errors. Mode: {0}."
                        : "[UnityRFramework] Framework shutdown completed. Mode: {0}.",
                    mode);
                try
                {
                    RFrameworkLog.Clear();
                }
                finally
                {
                    UnityRFrameworkRuntime.ResetComponents();
                }
            }
        }

        private void InstallLogSink()
        {
            Type sinkType = Utility.Assembly.GetType(logSinkTypeName);
            if (sinkType == null || !typeof(ILogSink).IsAssignableFrom(sinkType))
            {
                throw new RFrameworkException(
                    $"Log sink '{logSinkTypeName}' is missing or does not implement ILogSink.");
            }

            try
            {
                RFrameworkLog.SetSink((ILogSink)Activator.CreateInstance(sinkType));
            }
            catch (Exception ex) when (!(ex is RFrameworkException))
            {
                throw new RFrameworkException(
                    $"Log sink '{logSinkTypeName}' could not be created.", ex);
            }
        }

        private void InstallJsonHelper()
        {
            Type helperType = Utility.Assembly.GetType(jsonHelperTypeName);
            if (helperType == null
                || !typeof(Utility.Json.IJsonHelper).IsAssignableFrom(helperType))
            {
                throw new RFrameworkException(
                    $"JSON helper '{jsonHelperTypeName}' is missing or invalid.");
            }

            try
            {
                Utility.Json.SetJsonHelper(
                    (Utility.Json.IJsonHelper)Activator.CreateInstance(helperType));
            }
            catch (Exception ex) when (!(ex is RFrameworkException))
            {
                throw new RFrameworkException(
                    $"JSON helper '{jsonHelperTypeName}' could not be created.", ex);
            }
        }

        private void ApplyRuntimeSettings()
        {
            Application.targetFrameRate = frameRate;
            Time.timeScale = gameSpeed;
            Application.runInBackground = runInBackground;
            Screen.sleepTimeout = neverSleep
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;
        }

        private void HandleLowMemory()
        {
            Log.Warning("Low memory reported. Releasing unused framework resources.");
            UnityRFrameworkRuntime.Get<PoolComponent>()?.ReleaseAllUnused();
            UnityRFrameworkRuntime.Get<ResourceComponent>()?.UnloadUnusedAssets();
        }
    }
}
