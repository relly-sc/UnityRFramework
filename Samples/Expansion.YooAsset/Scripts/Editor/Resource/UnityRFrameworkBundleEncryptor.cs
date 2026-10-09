using System;
using System.IO;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;
using YooAsset;

namespace UnityRFramework.Expansion.Editor
{
    /// <summary>
    /// YooAsset Builder 可选的认证 Bundle 加密器。
    /// 读取工具生成的 YooAssetKey.bytes，与 Player 使用同一份密钥。
    /// </summary>
    public sealed class UnityRFrameworkBundleEncryptor : IBundleEncryptor
    {
        /// <inheritdoc />
        public BundleEncryptResult Encrypt(BundleEncryptArgs args)
        {
            byte[] source = File.ReadAllBytes(args.FilePath);
            IKeyProvider provider = CreateKeyProvider(
                out string keyId);
            byte[] protectedData = YooAssetBundleProtection.Protect(
                source,
                keyId,
                provider);
            return new BundleEncryptResult(true, protectedData);
        }

        /// <summary>校验当前 Builder 是否具备有效的密钥文件。</summary>
        internal static bool TryValidateKey(out string error)
        {
            try
            {
                IKeyProvider provider = CreateKeyProvider(out string keyId);
                if (!provider.TryGetKey(keyId, out byte[] key) || key == null)
                {
                    error = $"未找到 YooAsset Bundle 密钥，请生成 "
                        + $"{YooAssetKeyFileGenerator.DefaultPath}。";
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

        private static IKeyProvider CreateKeyProvider(out string keyId)
        {
            TextAsset keyFile = Resources.Load<TextAsset>(
                YooAssetBundleProtection.DefaultKeyResourcePath);
            if (keyFile != null)
            {
                keyId = YooAssetBundleProtection.DefaultKeyId;
                return new KeyFileProvider(keyId, keyFile.bytes);
            }

            throw new RFrameworkException(
                $"YooAsset Bundle 密钥文件不存在：{YooAssetKeyFileGenerator.DefaultPath}");
        }

        private sealed class KeyFileProvider : IKeyProvider
        {
            private readonly string expectedKeyId;
            private readonly byte[] key;

            internal KeyFileProvider(string expectedKeyId, byte[] fileBytes)
            {
                this.expectedKeyId = expectedKeyId;
                try
                {
                    key = ConfigKeyFile.Decode(fileBytes);
                }
                catch (RFrameworkException exception)
                {
                    throw new RFrameworkException(
                        "YooAsset bundle key file is invalid.",
                        exception);
                }
            }

            public bool TryGetKey(string keyId, out byte[] result)
            {
                if (!string.Equals(
                        keyId,
                        expectedKeyId,
                        StringComparison.Ordinal))
                {
                    result = null;
                    return false;
                }

                result = (byte[])key.Clone();
                return true;
            }
        }
    }
}
