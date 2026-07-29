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
        private static readonly byte[] LocalizationBundleMagic = Encoding.ASCII.GetBytes("URLM");

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

            byte[] body = BuildBody(localization);

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(LocalizationMagic);
                writer.Write(BinaryFormatUtility.LocalizationVersion);
                WritePayload(writer, localization, body);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>构建 URLM v1 多语言容器。</summary>
        public static byte[] BuildBundle(IReadOnlyList<LocalizationTable> localizations)
        {
            if (localizations == null || localizations.Count == 0)
            {
                throw new RFrameworkException("Localization bundle sources are empty.");
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(LocalizationBundleMagic);
                writer.Write(BinaryFormatUtility.LocalizationBundleVersion);
                writer.Write(localizations.Count);
                for (int i = 0; i < localizations.Count; i++)
                {
                    LocalizationTable localization = localizations[i]
                        ?? throw new RFrameworkException(
                            "Localization bundle contains an invalid source.");
                    BinaryFormatUtility.WriteUtf8String(
                        writer, localization.Language, false);
                    WritePayload(writer, localization, BuildBody(localization));
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static byte[] BuildBody(LocalizationTable localization)
        {
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

            return body;
        }

        private static void WritePayload(
            BinaryWriter writer, LocalizationTable localization, byte[] body)
        {
            writer.Write(localization.Entries.Count);
            writer.Write(body.Length);
            writer.Write(BinaryFormatUtility.ComputeCrc32(body));
            writer.Write(body);
        }
    }
}
