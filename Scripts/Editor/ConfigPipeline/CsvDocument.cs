using System;
using System.Collections.Generic;

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
        {
            SourcePath = sourcePath;
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        /// <summary>
        /// 获取源文件路径或逻辑名称。
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// 获取 CSV 行集合。
        /// </summary>
        public IReadOnlyList<CsvRow> Rows { get; }
    }
}
