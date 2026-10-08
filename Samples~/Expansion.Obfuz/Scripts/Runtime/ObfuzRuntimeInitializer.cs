using System;
using Obfuz;
using Obfuz.EncryptionVM;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 在任何混淆代码执行前初始化 Obfuz 静态加密服务。
    /// 该脚本位于 AOT 程序集侧，供混淆后的热更新程序集使用。
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
