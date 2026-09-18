using System;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Runtime
{
    /// <summary>连接可选平台密钥仓与 Storage 核心，不产生平台程序集强依赖。</summary>
    public static class InstallSaveKeyStoreRegistry
    {
        private static readonly object SyncRoot = new object();
        private static Func<string, IKeyStore> platformFactory;

        /// <summary>注册当前平台的安装级密钥仓工厂。</summary>
        public static void RegisterPlatformFactory(Func<string, IKeyStore> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            lock (SyncRoot)
            {
                platformFactory = factory;
            }
        }

        /// <summary>清除平台工厂，恢复到基础文件密钥仓。</summary>
        public static void ResetPlatformFactory()
        {
            lock (SyncRoot)
            {
                platformFactory = null;
            }
        }

        internal static IKeyStore Create(string rootDirectory)
        {
            Func<string, IKeyStore> factory;
            lock (SyncRoot)
            {
                factory = platformFactory;
            }

            return factory == null
                ? new FileKeyStore(rootDirectory)
                : factory(rootDirectory) ?? throw new RFrameworkException(
                    "The registered platform key store factory returned null.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration()
        {
            ResetPlatformFactory();
        }
    }
}
