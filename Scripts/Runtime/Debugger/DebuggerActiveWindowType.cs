namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 调试器窗口激活模式。
    /// </summary>
    public enum DebuggerActiveWindowType : byte
    {
        /// <summary>
        /// 在所有构建中启用。
        /// </summary>
        Enabled = 0,

        /// <summary>
        /// 仅在 Development Build 中启用。
        /// </summary>
        DevelopmentBuildOnly = 1,

        /// <summary>
        /// 仅在 Unity Editor 中启用。
        /// </summary>
        EditorOnly = 2,

        /// <summary>
        /// 始终禁用。
        /// </summary>
        Disabled = 3
    }
}
