using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// CSV 中的一行及其源文件行号。
    /// </summary>
    public sealed class CsvRow
    {
        /// <summary>
        /// 初始化 CSV 行。
        /// </summary>
        /// <param name="lineNumber">该行在源文件中的起始行号。</param>
        /// <param name="values">字段值集合。</param>
        public CsvRow(int lineNumber, IReadOnlyList<string> values)
        {
            LineNumber = lineNumber;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>
        /// 获取该行在源文件中的起始行号。
        /// </summary>
        public int LineNumber { get; }

        /// <summary>
        /// 获取该行的字段值。
        /// </summary>
        public IReadOnlyList<string> Values { get; }
    }
}
