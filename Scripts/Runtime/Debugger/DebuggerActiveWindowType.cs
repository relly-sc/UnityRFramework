namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 调试器窗口激活模式。
    /// </summary>
    public enum DebuggerActiveWindowType : byte
    {
        /// <summary>
        /// 始终开启。
        /// </summary>
        AlwaysOpen = 0,

        /// <summary>
        /// 仅在 Development Build 时开启。
        /// </summary>
        OnlyOpenWhenDevelopment,

        /// <summary>
        /// 仅在 Editor 中开启。
        /// </summary>
        OnlyOpenInEditor,

        /// <summary>
        /// 始终关闭。
        /// </summary>
        AlwaysClose,
    }
}
