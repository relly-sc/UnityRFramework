using System.Text;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 默认字符辅助器。
    /// </summary>
    public class DefaultTextHelper : Utility.Text.ITextHelper
    {
        /// <summary>
        /// StringBuilder 默认容量。
        /// </summary>
        private const int StringBuilderCapacity = 1024;

        /// <summary>
        /// 线程本地缓存的 StringBuilder 实例。
        /// </summary>
        [System.ThreadStatic]
        private static StringBuilder s_CachedStringBuilder = null;

        /// <inheritdoc/>
        public string Format(string format, params object[] args)
        {
            if (format == null)
            {
                throw new RFrameworkException("Format is invalid.");
            }

            if (args == null || args.Length == 0)
            {
                return format;
            }

            if (s_CachedStringBuilder == null)
            {
                s_CachedStringBuilder = new StringBuilder(StringBuilderCapacity);
            }

            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, args);
            return s_CachedStringBuilder.ToString();
        }
    }
}
