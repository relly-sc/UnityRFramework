
using System.Diagnostics;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 日志工具集（Runtime 层专用，内部委托给 RFrameworkLog）。
    /// 分层约定：Library 层业务代码不直接调用 RFrameworkLog，而是以 RFrameworkException 上报错误；
    /// Runtime 层在捕获 RFrameworkException 后调用本类输出日志。
    /// 需要在 BaseComponent.Awake() 中调用 RFrameworkLog.SetLogHelper() 初始化后才可用。
    /// </summary>
    public static class Log
    {
        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Info(object message)
        {
            RFrameworkLog.Info(message);
        }

        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Info(string message)
        {
            RFrameworkLog.Info(message);
        }

        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="format">日志格式。</param>
        /// <param name="args">日志参数。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Info(string format, params object[] args)
        {
            RFrameworkLog.Info(format, args);
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Warning(object message)
        {
            RFrameworkLog.Warning(message);
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Warning(string message)
        {
            RFrameworkLog.Warning(message);
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="format">日志格式。</param>
        /// <param name="args">日志参数。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Warning(string format, params object[] args)
        {
            RFrameworkLog.Warning(format, args);
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Error(object message)
        {
            RFrameworkLog.Error(message);
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Error(string message)
        {
            RFrameworkLog.Error(message);
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="format">日志格式。</param>
        /// <param name="args">日志参数。</param>
        /// <remarks>仅在带有 ENABLE_LOG 预编译选项时生效。</remarks>
        [Conditional("ENABLE_LOG")]
        public static void Error(string format, params object[] args)
        {
            RFrameworkLog.Error(format, args);
        }

    }
}
