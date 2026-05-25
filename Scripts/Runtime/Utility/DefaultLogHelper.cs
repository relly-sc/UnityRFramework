
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认游戏框架日志辅助器。
    /// </summary>
    public class DefaultLogHelper : RFrameworkLog.ILogHelper
    {
        /// <summary>
        /// 记录日志。
        /// </summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志内容。</param>
        public void Log(RFrameworkLogLevel level, object message)
        {
            switch (level)
            {
                case RFrameworkLogLevel.Debug:
                    Debug.Log(Utility.Text.Format("<color=#888888>{0}</color>", message));
                    break;

                case RFrameworkLogLevel.Info:
                    Debug.Log(message.ToString());
                    break;

                case RFrameworkLogLevel.Warning:
                    Debug.LogWarning(message.ToString());
                    break;

                case RFrameworkLogLevel.Error:
                    Debug.LogError(message.ToString());
                    break;

                default:
                    throw new RFrameworkException(message.ToString());
            }
        }
    }
}
