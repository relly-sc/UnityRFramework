using System;
using System.IO;
using System.Text;
using RFramework;
using UnityRFramework.Runtime;
using YooAsset;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 配置 UnityRFramework YooAsset Bundle 解密所需的密钥提供器。
    /// 使用加密 Bundle 的项目必须在初始化 ResourceModule 前完成配置。
    /// </summary>
    public static class YooAssetBundleProtection
    {
        /// <summary>Builder 读取 Base64 编码 32 字节密钥的环境变量名。</summary>
        public const string BuildKeyEnvironmentVariable =
            "UNITY_RFRAMEWORK_YOOASSET_KEY";

        /// <summary>Builder 读取密钥标识的可选环境变量名。</summary>
        public const string BuildKeyIdEnvironmentVariable =
            "UNITY_RFRAMEWORK_YOOASSET_KEY_ID";

        /// <summary>未设置密钥标识环境变量时使用的默认标识。</summary>
        public const string DefaultKeyId = "YooAssetBundle";

        private static readonly byte[] BundleContext =
            Encoding.UTF8.GetBytes("UnityRFramework.YooAsset.Bundle.v1");
        private static readonly object SyncRoot = new object();

        private static IKeyProvider keyProvider;

        /// <summary>获取运行时 Bundle 解密是否已经配置。</summary>
        public static bool IsConfigured
        {
            get
            {
                lock (SyncRoot)
                {
                    return keyProvider != null
                        || RuntimeKeyProviderRegistry.TryGetContentKeys(out _);
                }
            }
        }

        /// <summary>
        /// 设置运行时密钥提供器。提供器应能按加密数据头中的 KeyId 返回密钥副本。
        /// </summary>
        /// <param name="provider">项目自己的密钥提供器。</param>
        public static void Configure(IKeyProvider provider)
        {
            if (provider == null)
            {
                throw new RFrameworkException(
                    "YooAsset bundle key provider is invalid.");
            }

            lock (SyncRoot)
            {
                keyProvider = provider;
            }
        }

        /// <summary>清除运行时密钥提供器，后续初始化不再挂载框架解密器。</summary>
        public static void Reset()
        {
            lock (SyncRoot)
            {
                keyProvider = null;
            }
        }

        /// <summary>使用与运行时解密一致的格式保护 Bundle 数据。</summary>
        /// <param name="data">待保护的 Bundle 数据。</param>
        /// <param name="keyId">写入保护头的密钥标识。</param>
        /// <param name="provider">提供构建密钥的密钥提供器。</param>
        /// <returns>加密并认证后的数据。</returns>
        public static byte[] Protect(
            byte[] data,
            string keyId,
            IKeyProvider provider)
        {
            return new DefaultDataProtector(provider).Protect(
                data,
                ProtectedDataPayloadKind.Custom,
                keyId,
                BundleContext);
        }

        internal static byte[] Unprotect(byte[] data)
        {
            IKeyProvider provider;
            lock (SyncRoot)
            {
                provider = keyProvider;
            }

            if (provider == null)
            {
                RuntimeKeyProviderRegistry.TryGetContentKeys(out provider);
            }

            if (provider == null)
            {
                throw new RFrameworkException(
                    "YooAsset bundle decryption is not configured. Call "
                    + "YooAssetBundleProtection.Configure before initializing resources.");
            }

            return new DefaultDataProtector(provider).Unprotect(
                data,
                ProtectedDataPayloadKind.Custom,
                BundleContext);
        }
    }

    /// <summary>
    /// YooAsset v3 内存 Bundle 解密器。支持 AssetBundle、RawBundle 和 ArchiveBundle。
    /// </summary>
    public sealed class UnityRFrameworkBundleDecryptor : IBundleMemoryDecryptor
    {
        /// <inheritdoc />
        public byte[] GetDecryptedData(BundleDecryptArgs args)
        {
            byte[] protectedData = args.FileData;
            if (protectedData == null)
            {
                if (string.IsNullOrWhiteSpace(args.FilePath))
                {
                    throw new RFrameworkException(
                        "YooAsset encrypted bundle path is invalid.");
                }

                protectedData = File.ReadAllBytes(args.FilePath);
            }

            return YooAssetBundleProtection.Unprotect(protectedData);
        }
    }
}
