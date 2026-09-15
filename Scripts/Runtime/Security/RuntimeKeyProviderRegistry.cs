using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>集中提供发布内容解密密钥，避免各模块重复注入。</summary>
    public static class RuntimeKeyProviderRegistry
    {
        private static readonly object SyncRoot = new object();
        private static IKeyProvider contentKeyProvider;

        /// <summary>
        /// 注册 Config、YooAsset Bundle 等发布内容共用的密钥解析入口。
        /// 提供器应按不同 KeyId 返回彼此独立的密钥材料。
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
