using System;
using Obfuz;
using Obfuz.EncryptionVM;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 在任何混淆代码执行前初始化 Obfuz 静态加密服务。
    /// 适用于普通 Player；有热更新时该脚本须保留在 AOT 程序集侧。
    /// </summary>
    internal static class ObfuzRuntimeInitializer
    {
        /// <summary>
        /// 加载 Obfuz 静态密钥并注册生成的加密虚拟机。
        /// </summary>
        [ObfuzIgnore]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            TextAsset keyAsset = Resources.Load<TextAsset>(
                "Obfuz/defaultStaticSecretKey");
            if (keyAsset == null || keyAsset.bytes == null || keyAsset.bytes.Length == 0)
            {
                throw new InvalidOperationException(
                    "Obfuz 静态密钥缺失：Resources/Obfuz/defaultStaticSecretKey");
            }

            EncryptionService<DefaultStaticEncryptionScope>.Encryptor =
                new GeneratedEncryptionVirtualMachine(keyAsset.bytes);
        }
    }
}
