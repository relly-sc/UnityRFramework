

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
    public sealed class BaseComponent : UnityRFrameworkComponent
    {
        private const int DefaultDpi = 96;  // default windows dpi

        private float gameSpeedBeforePause = 1f;

        [SerializeField]
        private bool editorResourceMode = true;

        [SerializeField]
        private string textHelperTypeName = "UnityRFramework.Runtime.DefaultTextHelper";

        [SerializeField]
        private string versionHelperTypeName = "UnityRFramework.Runtime.DefaultVersionHelper";

        [SerializeField]
        private string logHelperTypeName = "UnityRFramework.Runtime.DefaultLogHelper";

        [SerializeField]
        private string compressionHelperTypeName = "UnityRFramework.Runtime.DefaultCompressionHelper";

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
        /// 获取或设置是否使用编辑器资源模式（仅编辑器内有效）。
        /// </summary>
        public bool EditorResourceMode
        {
            get
            {
                return editorResourceMode;
            }
            set
            {
                editorResourceMode = value;
            }
        }



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
        protected override void Awake()
        {
            base.Awake();

            InitTextHelper();
            InitVersionHelper();
            InitLogHelper();

            Log.Info("Unity Version: {0}", Application.unityVersion);

#if UNITY_5_3_OR_NEWER || UNITY_5_3
            InitCompressionHelper();
            InitJsonHelper();

            Utility.Converter.ScreenDpi = Screen.dpi;
            if (Utility.Converter.ScreenDpi <= 0)
            {
                Utility.Converter.ScreenDpi = DefaultDpi;
            }

            editorResourceMode &= Application.isEditor;
            if (editorResourceMode)
            {
                Log.Info("During this run, Game Framework will use editor resource files, which you should validate first.");
            }

            Application.targetFrameRate = frameRate;
            Time.timeScale = gameSpeed;
            Application.runInBackground = runInBackground;
            Screen.sleepTimeout = neverSleep ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
#else
            Log.Error("Game Framework only applies with Unity 5.3 and above, but current Unity version is {0}.", Application.unityVersion);
            GameEntry.Shutdown(ShutdownType.Quit);
#endif
#if UNITY_5_6_OR_NEWER
            Application.lowMemory += OnLowMemory;
#endif
        }

        private void Start()
        {
        }

        private void Update()
        {
            RFrameworkModuleEntry.Update(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
#if UNITY_5_6_OR_NEWER
            Application.lowMemory -= OnLowMemory;
#endif
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            RFrameworkModuleEntry.Shutdown();
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

        private void InitVersionHelper()
        {

        }

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

        private void InitCompressionHelper()
        {

        }

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

        private void OnLowMemory()
        {
            Log.Info("Low memory reported...");

            // ObjectPoolComponent objectPoolComponent = ComponentEntry.GetComponent<ObjectPoolComponent>();
            // if (objectPoolComponent != null)
            // {
            //     objectPoolComponent.ReleaseAllUnused();
            // }

            // ResourceComponent resourceCompoent = ComponentEntry.GetComponent<ResourceComponent>();
            // if (resourceCompoent != null)
            // {
            //     resourceCompoent.ForceUnloadUnusedAssets(true);
            // }
        }
    }
}
