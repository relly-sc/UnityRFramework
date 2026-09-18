using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>为可选资源扩展提供独立的发布内容解密密钥入口。</summary>
    public static class RuntimeKeyProviderRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IKeyProvider contentKeyProvider;

        /// <summary>
        /// 注册资源扩展使用的密钥解析入口。
        /// ConfigComponent 使用自己的 Config 密钥，不通过此注册表。
        /// </summary>
        public static void ConfigureContentKeys(IKeyProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (provider is InstallSaveKeyProvider)
            {
                throw new RFrameworkException(
                    "InstallSaveKeyProvider cannot be reused for published content keys.");
            }

            lock (SyncRoot)
            {
                contentKeyProvider = provider;
            }
        }

        /// <summary>尝试获取当前发布内容密钥提供器。</summary>
        public static bool TryGetContentKeys(out IKeyProvider provider)
        {
            lock (SyncRoot)
            {
                provider = contentKeyProvider;
                return provider != null;
            }
        }

        /// <summary>清除发布内容密钥提供器。</summary>
        public static void ResetContentKeys()
        {
            lock (SyncRoot)
            {
                contentKeyProvider = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            ResetContentKeys();
        }
    }
}
