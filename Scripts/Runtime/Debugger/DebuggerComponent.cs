#if DEVELOPMENT_BUILD || UNITY_EDITOR

using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 调试器组件。挂载到 UnityRFramework 预制体上，在 Inspector 中配置
    /// 开关快捷键、日志缓存上限、日志过滤等参数，由 DebuggerOverlay 读取。
    /// Release 包通过条件编译完全移除，零开销。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("UnityRFramework/Debugger")]
    public class DebuggerComponent : UnityRFrameworkComponent
    {
        [Header("Window")]
        [SerializeField]
        [Tooltip("调试器窗口的激活模式：始终开启、仅开发版、仅编辑器、始终关闭。")]
        private DebuggerActiveWindowType activeWindowType = DebuggerActiveWindowType.AlwaysOpen;

        [SerializeField]
        [Tooltip("开关调试器窗口的快捷键。")]
        private KeyCode toggleKey = KeyCode.F3;

        [SerializeField]
        [Tooltip("调试器窗口缩放比例（1.0 = 100%，2.0 = 200%）。移动端建议 1.5~2.0。")]
        private float windowScale = 1f;

        [SerializeField]
        [Tooltip("启用后自动根据屏幕 DPI 缩放，移动端文字不会太小。")]
        private bool autoDpiScale = true;

        [Header("Console")]
        [SerializeField]
        [Tooltip("日志缓存最大条数，超过后自动移除旧条目。")]
        private int maxLogEntries = 200;

        [SerializeField]
        [Tooltip("切换 Overview / Log 页签的快捷键。")]
        private KeyCode switchTabKey = KeyCode.Tab;

        [SerializeField]
        [Tooltip("是否显示 Info 级别日志。")]
        private bool infoFilter = true;

        [SerializeField]
        [Tooltip("是否显示 Warning 级别日志。")]
        private bool warningFilter = true;

        [SerializeField]
        [Tooltip("是否显示 Error 级别日志。")]
        private bool errorFilter = true;

        /// <summary>
        /// 获取调试器窗口激活模式。
        /// </summary>
        public DebuggerActiveWindowType ActiveWindowType
        {
            get { return activeWindowType; }
        }

        /// <summary>
        /// 获取或设置调试器窗口是否激活。
        /// </summary>
        public bool ActiveWindow
        {
            get { return DebuggerOverlay.ActiveWindow; }
            set { DebuggerOverlay.ActiveWindow = value; }
        }

        /// <summary>
        /// 获取开关快捷键。
        /// </summary>
        public KeyCode ToggleKey
        {
            get { return toggleKey; }
        }

        /// <summary>
        /// 获取窗口缩放比例。
        /// </summary>
        public float WindowScale
        {
            get { return windowScale; }
        }

        /// <summary>
        /// 获取是否启用自动 DPI 缩放。
        /// </summary>
        public bool AutoDpiScale
        {
            get { return autoDpiScale; }
        }

        /// <summary>
        /// 获取日志缓存最大条数。
        /// </summary>
        public int MaxLogEntries
        {
            get { return maxLogEntries; }
        }

        /// <summary>
        /// 获取切换页签快捷键。
        /// </summary>
        public KeyCode SwitchTabKey
        {
            get { return switchTabKey; }
        }

        /// <summary>
        /// 获取是否显示 Info 日志。
        /// </summary>
        public bool InfoFilter
        {
            get { return infoFilter; }
        }

        /// <summary>
        /// 获取是否显示 Warning 日志。
        /// </summary>
        public bool WarningFilter
        {
            get { return warningFilter; }
        }

        /// <summary>
        /// 获取是否显示 Error 日志。
        /// </summary>
        public bool ErrorFilter
        {
            get { return errorFilter; }
        }

        /// <summary>
        /// 初始化。注册到 ComponentEntry 并启动 DebuggerOverlay。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            DebuggerOverlay.Initialize(this);

            // 根据激活模式设置初始状态
            switch (activeWindowType)
            {
                case DebuggerActiveWindowType.AlwaysOpen:
                    ActiveWindow = true;
                    break;

                case DebuggerActiveWindowType.OnlyOpenWhenDevelopment:
                    ActiveWindow = Debug.isDebugBuild;
                    break;

                case DebuggerActiveWindowType.OnlyOpenInEditor:
                    ActiveWindow = Application.isEditor;
                    break;

                case DebuggerActiveWindowType.AlwaysClose:
                    ActiveWindow = false;
                    break;
            }
        }
    }
}

#endif
