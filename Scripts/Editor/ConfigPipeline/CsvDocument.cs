using System;
using System.Collections.Generic;
using System.IO;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 完整 CSV 文档。
    /// </summary>
    public sealed class CsvDocument
    {
        /// <summary>
        /// 初始化 CSV 文档。
        /// </summary>
        /// <param name="sourcePath">源文件路径或逻辑名称。</param>
        /// <param name="rows">CSV 行集合。</param>
        public CsvDocument(string sourcePath, IReadOnlyList<CsvRow> rows)
            : this(sourcePath, Path.GetFileNameWithoutExtension(sourcePath), rows)
        {
        }

        /// <summary>
        /// 使用独立逻辑名称初始化 CSV 文档。
        /// </summary>
        /// <param name="sourcePath">用于错误定位的源文件路径。</param>
        /// <param name="documentName">不含扩展名的逻辑表名或语言名称。</param>
        /// <param name="rows">CSV 行集合。</param>
        public CsvDocument(
            string sourcePath, string documentName, IReadOnlyList<CsvRow> rows)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("CSV source path is invalid.", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(documentName))
            {
                throw new ArgumentException(
                    "CSV document name is invalid.", nameof(documentName));
            }

            SourcePath = sourcePath;
            DocumentName = documentName.Trim();
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        /// <summary>
        /// 获取源文件路径或逻辑名称。
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// 获取不依赖物理文件名的逻辑表名或语言名称。
        /// </summary>
        public string DocumentName { get; }

        /// <summary>
        /// 获取 CSV 行集合。
        /// </summary>
        public IReadOnlyList<CsvRow> Rows { get; }
    }
}
