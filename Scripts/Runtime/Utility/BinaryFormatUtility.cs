using System;
using System.IO;
using System.Text;
using RFramework;

namespace UnityRFramework.Runtime
{
    /// <summary>
    /// 框架二进制数据协议的公共基础函数。
    /// 所有数值由 BinaryReader/BinaryWriter 按 little-endian 编码。
    /// 字符串使用 Int32 长度加 UTF-8 数据。
    /// </summary>
    public static class BinaryFormatUtility
    {
        /// <summary>URFC 反射兼容协议版本。</summary>
        public const ushort ConfigReflectionVersion = 1;

        /// <summary>URFC 生成代码协议版本。</summary>
        public const ushort ConfigGeneratedVersion = 2;

        /// <summary>URFL 协议版本。</summary>
        public const ushort LocalizationVersion = 1;

        /// <summary>协议允许的最大集合元素数量。</summary>
        public const int MaxCollectionCount = 1_000_000;

        /// <summary>协议允许的最大单个字符串或字节块长度。</summary>
        public const int MaxBlobBytes = 16 * 1024 * 1024;

        private const uint Fnv32Offset = 2166136261;
        private const uint Fnv32Prime = 16777619;
        private const ulong Fnv64Offset = 14695981039346656037;
        private const ulong Fnv64Prime = 1099511628211;
        private const uint Crc32Polynomial = 0xedb88320;

        private static readonly uint[] Crc32Table = BuildCrc32Table();

        /// <summary>
        /// 读取 Int32 长度前缀的 UTF-8 字符串。
        /// </summary>
        /// <param name="reader">二进制读取器。</param>
        /// <param name="allowNull">是否允许使用 -1 表示 null。</param>
        /// <returns>读取的字符串。</returns>
        public static string ReadUtf8String(BinaryReader reader, bool allowNull)
        {
            if (reader == null)
            {
                throw new RFrameworkException("Binary reader is invalid.");
            }

            int byteCount = reader.ReadInt32();
            if (allowNull && byteCount == -1)
            {
                return null;
            }

            if (byteCount < 0 || byteCount > MaxBlobBytes)
            {
                throw new RFrameworkException($"Binary string byte length '{byteCount}' is invalid.");
            }

            byte[] bytes = reader.ReadBytes(byteCount);
            if (bytes.Length != byteCount)
            {
                throw new EndOfStreamException();
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 写入 Int32 长度前缀的 UTF-8 字符串。
        /// </summary>
        /// <param name="writer">二进制写入器。</param>
        /// <param name="value">字符串值。</param>
        /// <param name="allowNull">是否允许使用 -1 表示 null。</param>
        public static void WriteUtf8String(BinaryWriter writer, string value, bool allowNull)
        {
            if (writer == null)
            {
                throw new RFrameworkException("Binary writer is invalid.");
            }

            if (value == null)
            {
                if (!allowNull)
                {
                    throw new RFrameworkException("Binary string can not be null.");
                }

                writer.Write(-1);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > MaxBlobBytes)
            {
                throw new RFrameworkException(
                    $"Binary string byte length '{bytes.Length}' exceeds '{MaxBlobBytes}'.");
            }

            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// 计算字节数组的 CRC32 校验值。
        /// </summary>
        /// <param name="bytes">待校验字节。</param>
        /// <returns>CRC32 校验值。</returns>
        public static uint ComputeCrc32(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new RFrameworkException("CRC32 source bytes are invalid.");
            }

            uint crc = 0xffffffff;
            for (int i = 0; i < bytes.Length; i++)
            {
                crc = (crc >> 8) ^ Crc32Table[(crc ^ bytes[i]) & 0xff];
            }

            return ~crc;
        }

        /// <summary>
        /// 计算 UTF-8 字符串的 FNV-1a 32 位哈希。
        /// </summary>
        /// <param name="value">源字符串。</param>
        /// <returns>32 位哈希。</returns>
        public static uint ComputeFnv1A32(string value)
        {
            if (value == null)
            {
                throw new RFrameworkException("FNV32 source string is invalid.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            uint hash = Fnv32Offset;
            unchecked
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= Fnv32Prime;
                }
            }

            return hash;
        }

        /// <summary>
        /// 计算 UTF-8 字符串的 FNV-1a 64 位哈希。
        /// </summary>
        /// <param name="value">源字符串。</param>
        /// <returns>64 位哈希。</returns>
        public static ulong ComputeFnv1A64(string value)
        {
            if (value == null)
            {
                throw new RFrameworkException("FNV64 source string is invalid.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            ulong hash = Fnv64Offset;
            unchecked
            {
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    hash *= Fnv64Prime;
                }
            }

            return hash;
        }

        /// <summary>
        /// 校验集合数量是否位于协议允许范围内。
        /// </summary>
        /// <param name="count">集合数量。</param>
        /// <param name="maximum">最大允许数量。</param>
        /// <param name="name">错误信息使用的集合名称。</param>
        public static void EnsureCountInRange(int count, int maximum, string name)
        {
            if (count < 0 || count > maximum)
            {
                throw new RFrameworkException(
                    $"Binary {name} count '{count}' is outside the supported range 0..{maximum}.");
            }
        }

        private static uint[] BuildCrc32Table()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0
                        ? (value >> 1) ^ Crc32Polynomial
                        : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }
    }
}
