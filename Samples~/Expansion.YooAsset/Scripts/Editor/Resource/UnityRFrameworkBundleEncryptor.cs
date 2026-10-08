using System;
using System.IO;
using RFramework;
using YooAsset;

namespace UnityRFramework.Expansion.Editor
{
    /// <summary>
    /// YooAsset Builder 可选的认证 Bundle 加密器。
    /// 密钥只从环境变量读取，不写入 Unity 资产或构建报告。
    /// </summary>
    public sealed class UnityRFrameworkBundleEncryptor : IBundleEncryptor
    {
        /// <inheritdoc />
        public BundleEncryptResult Encrypt(BundleEncryptArgs args)
        {
            byte[] source = File.ReadAllBytes(args.FilePath);
            EnvironmentKeyProvider provider = CreateEnvironmentKeyProvider(
                out string keyId);
            byte[] protectedData = YooAssetBundleProtection.Protect(
                source,
                keyId,
                provider);
            return new BundleEncryptResult(true, protectedData);
        }

        /// <summary>校验当前 Builder 进程是否具备有效的加密密钥。</summary>
        internal static bool TryValidateEnvironment(out string error)
        {
            try
            {
                EnvironmentKeyProvider provider = CreateEnvironmentKeyProvider(
                    out string keyId);
                if (!provider.TryGetKey(keyId, out byte[] key) || key == null)
                {
                    error = $"环境变量 {YooAssetBundleProtection.BuildKeyEnvironmentVariable} "
                        + "未提供可用密钥。";
                    return false;
                }

                try
                {
                    if (key.Length != 32)
                    {
                        throw new RFrameworkException(
                            "YooAsset bundle encryption key must contain exactly 32 bytes.");
                    }
                }
                finally
                {
                    Array.Clear(key, 0, key.Length);
                }

                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static EnvironmentKeyProvider CreateEnvironmentKeyProvider(
            out string keyId)
        {
            keyId = Environment.GetEnvironmentVariable(
                YooAssetBundleProtection.BuildKeyIdEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(keyId))
            {
                keyId = YooAssetBundleProtection.DefaultKeyId;
            }
            else
            {
                keyId = keyId.Trim();
            }

            return new EnvironmentKeyProvider(keyId);
        }

        private sealed class EnvironmentKeyProvider : IKeyProvider
        {
            private readonly string expectedKeyId;

            internal EnvironmentKeyProvider(string expectedKeyId)
            {
                this.expectedKeyId = expectedKeyId;
            }

            public bool TryGetKey(string keyId, out byte[] key)
            {
                key = null;
                if (!string.Equals(
                        keyId,
                        expectedKeyId,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                string encoded = Environment.GetEnvironmentVariable(
                    YooAssetBundleProtection.BuildKeyEnvironmentVariable);
                if (string.IsNullOrWhiteSpace(encoded))
                {
                    return false;
                }

                try
                {
                    key = Convert.FromBase64String(encoded.Trim());
                    return true;
                }
                catch (FormatException exception)
                {
                    throw new RFrameworkException(
                        $"Environment variable "
                        + $"'{YooAssetBundleProtection.BuildKeyEnvironmentVariable}' "
                        + "must be a Base64 encoded 32-byte key.",
                        exception);
                }
            }
        }
    }
}
