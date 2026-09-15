namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架停止后的应用行为。
    /// </summary>
    public enum UnityRFrameworkShutdownMode : byte
    {
        /// <summary>仅停止并销毁当前框架实例。</summary>
        Destroy = 0,

        /// <summary>重新加载启动场景。</summary>
        Restart = 1,

        /// <summary>退出应用。</summary>
        Quit = 2
    }
}
