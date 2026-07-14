using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RFramework;
using UnityRFramework.Runtime;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 将经过校验的 Config 表导出为 URFC 二进制。
    /// </summary>
    public static class ConfigBinaryExporter
    {
        private static readonly byte[] ConfigMagic = Encoding.ASCII.GetBytes("URFC");

        /// <summary>
        /// 构建一张 URFC v2 配置表。
        /// </summary>
        /// <param name="schema">配置表定义和数据行。</param>
        /// <returns>完整 URFC v2 文件字节。</returns>
        public static byte[] BuildV2(ConfigTableSchema schema)
        {
            if (schema == null)
            {
                throw new RFrameworkException("Config schema is invalid.");
            }

            byte[] body;
            using (MemoryStream bodyStream = new MemoryStream())
            using (BinaryWriter bodyWriter = new BinaryWriter(bodyStream, Encoding.UTF8, true))
            {
                for (int rowIndex = 0; rowIndex < schema.Rows.Count; rowIndex++)
                {
                    CsvRow row = schema.Rows[rowIndex];
                    for (int fieldIndex = 0; fieldIndex < schema.Fields.Count; fieldIndex++)
                    {
                        ConfigValueParser.Write(
                            bodyWriter,
                            schema.Fields[fieldIndex],
                            row.Values[fieldIndex],
                            schema.SourcePath,
                            row.LineNumber);
                    }
                }

                bodyWriter.Flush();
                body = bodyStream.ToArray();
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(ConfigMagic);
                writer.Write(BinaryFormatUtility.ConfigGeneratedVersion);
                writer.Write(schema.TableId);
                writer.Write(schema.SchemaHash);
                writer.Write(schema.Rows.Count);
                writer.Write(body.Length);
                writer.Write(BinaryFormatUtility.ComputeCrc32(body));
                writer.Write(body);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 仅在内容变化时写入二进制文件。
        /// </summary>
        /// <param name="path">输出路径。</param>
        /// <param name="bytes">文件字节。</param>
        /// <returns>发生实际写入时返回 true。</returns>
        public static bool WriteBytesIfChanged(string path, byte[] bytes)
        {
            if (string.IsNullOrWhiteSpace(path) || bytes == null)
            {
                throw new RFrameworkException("Binary output path or bytes are invalid.");
            }

            if (File.Exists(path))
            {
                byte[] old = File.ReadAllBytes(path);
                if (AreEqual(old, bytes))
                {
                    return false;
                }
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, bytes);
            return true;
        }

        private static bool AreEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
