using System;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>读取和生成经过简单偏移混淆的 Config 密钥文件。</summary>
    public static class ConfigKeyFile
    {
        private static readonly byte[] Magic = { (byte)'U', (byte)'R', (byte)'F', (byte)'K' };
        private const byte Version = 1;
        private const int KeySize = 32;
        private const int FileSize = 42;

        /// <summary>生成密钥文件内容。</summary>
        public static byte[] Encode(byte[] key, byte offset)
        {
            if (key == null || key.Length != KeySize)
            {
                throw new RFrameworkException("Config key must contain exactly 32 bytes.");
            }

            byte[] result = new byte[FileSize];
            Buffer.BlockCopy(Magic, 0, result, 0, Magic.Length);
            result[4] = Version;
            result[5] = offset;
            for (int i = 0; i < KeySize; i++)
            {
                result[6 + i] = unchecked((byte)(key[i] + offset));
            }

            byte[] checksum = Utility.Verifier.GetCrc32Bytes(
                Utility.Verifier.GetCrc32(key));
            Buffer.BlockCopy(checksum, 0, result, 38, checksum.Length);
            return result;
        }

        /// <summary>读取并校验密钥文件。</summary>
        public static byte[] Decode(byte[] fileBytes)
        {
            if (fileBytes == null || fileBytes.Length != FileSize)
            {
                throw new RFrameworkException("Config key file format is invalid.");
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (fileBytes[i] != Magic[i])
                {
                    throw new RFrameworkException("Config key file magic is invalid.");
                }
            }

            if (fileBytes[4] != Version)
            {
                throw new RFrameworkException("Config key file version is unsupported.");
            }

            byte offset = fileBytes[5];
            byte[] key = new byte[KeySize];
            for (int i = 0; i < KeySize; i++)
            {
                key[i] = unchecked((byte)(fileBytes[6 + i] - offset));
            }

            byte[] checksum = Utility.Verifier.GetCrc32Bytes(
                Utility.Verifier.GetCrc32(key));
            for (int i = 0; i < checksum.Length; i++)
            {
                if (fileBytes[38 + i] != checksum[i])
                {
                    Array.Clear(key, 0, key.Length);
                    throw new RFrameworkException("Config key file checksum is invalid.");
                }
            }

            return key;
        }
    }
}
