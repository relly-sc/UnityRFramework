using System.Collections.Generic;
using System.IO;
using System.Text;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将单语言键值表导出为 URFL 二进制。
    /// </summary>
    public static class LocalizationBinaryExporter
    {
        private static readonly byte[] LocalizationMagic = Encoding.ASCII.GetBytes("URFL");

        /// <summary>
        /// 构建一个 URFL v1 语言文件，仅用于兼容性测试和旧项目迁移。
        /// </summary>
        /// <param name="localization">经过校验的本地化键值表。</param>
        /// <returns>完整 URFL v1 文件字节。</returns>
        public static byte[] BuildV1(LocalizationTable localization)
        {
            if (localization == null || localization.Entries == null)
            {
                throw new RFrameworkException("Localization source is invalid.");
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(LocalizationMagic);
                writer.Write(BinaryFormatUtility.LocalizationLegacyVersion);
                writer.Write(localization.Entries.Count);
                for (int i = 0; i < localization.Entries.Count; i++)
                {
                    KeyValuePair<string, string> entry = localization.Entries[i];
                    BinaryFormatUtility.WriteUtf8String(writer, entry.Key, false);
                    BinaryFormatUtility.WriteUtf8String(writer, entry.Value ?? string.Empty, false);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 构建一个带 CRC32 完整性校验的 URFL v2 语言文件。
        /// </summary>
        /// <param name="localization">经过校验的本地化键值表。</param>
        /// <returns>完整 URFL v2 文件字节。</returns>
        public static byte[] BuildV2(LocalizationTable localization)
        {
            if (localization == null || localization.Entries == null)
            {
                throw new RFrameworkException("Localization source is invalid.");
            }

            byte[] body;
            using (MemoryStream bodyStream = new MemoryStream())
            using (BinaryWriter bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8, true))
            {
                for (int i = 0; i < localization.Entries.Count; i++)
                {
                    KeyValuePair<string, string> entry = localization.Entries[i];
                    BinaryFormatUtility.WriteUtf8String(bodyWriter, entry.Key, false);
                    BinaryFormatUtility.WriteUtf8String(
                        bodyWriter, entry.Value ?? string.Empty, false);
                }

                bodyWriter.Flush();
                body = bodyStream.ToArray();
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(LocalizationMagic);
                writer.Write(BinaryFormatUtility.LocalizationVersion);
                writer.Write(localization.Entries.Count);
                writer.Write(body.Length);
                writer.Write(BinaryFormatUtility.ComputeCrc32(body));
                writer.Write(body);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
