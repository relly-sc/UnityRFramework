using System.Diagnostics;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// Runtime 日志入口。框架未启动或已经关闭时安全忽略晚到日志。
    /// </summary>
    public static class Log
    {
        /// <summary>输出普通运行信息。</summary>
        [Conditional("ENABLE_LOG")]
        public static void Info(object message) =>
            RFrameworkLog.TryWrite(LogLevel.Info, message);

        /// <summary>格式化并输出普通运行信息。</summary>
        [Conditional("ENABLE_LOG")]
        public static void Info(string format, params object[] args) =>
            RFrameworkLog.TryWrite(LogLevel.Info, format, args);

        /// <summary>输出可恢复问题或降级提示。</summary>
        [Conditional("ENABLE_LOG")]
        public static void Warning(object message) =>
            RFrameworkLog.TryWrite(LogLevel.Warning, message);

        /// <summary>格式化并输出可恢复问题或降级提示。</summary>
        [Conditional("ENABLE_LOG")]
        public static void Warning(string format, params object[] args) =>
            RFrameworkLog.TryWrite(LogLevel.Warning, format, args);

        /// <summary>输出功能错误。</summary>
        [Conditional("ENABLE_LOG")]
        public static void Error(object message) =>
            RFrameworkLog.TryWrite(LogLevel.Error, message);

        /// <summary>格式化并输出功能错误。</summary>
        [Conditional("ENABLE_LOG")]
        public static void Error(string format, params object[] args) =>
            RFrameworkLog.TryWrite(LogLevel.Error, format, args);
    }
}
