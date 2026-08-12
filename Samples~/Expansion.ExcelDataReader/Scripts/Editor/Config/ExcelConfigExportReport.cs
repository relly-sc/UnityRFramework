using System.Collections.Generic;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// Excel 配置表校验或导出结果。
    /// </summary>
    public sealed class ExcelConfigExportReport
    {
        private readonly List<string> messages = new List<string>();

        /// <summary>获取已处理工作簿数量。</summary>
        public int WorkbookCount { get; internal set; }

        /// <summary>获取已处理 Sheet 数量。</summary>
        public int TableCount { get; internal set; }

        /// <summary>获取实际发生写入的文件数量。</summary>
        public int WrittenFileCount { get; internal set; }

        /// <summary>获取结果消息只读列表。</summary>
        public IReadOnlyList<string> Messages => messages;

        /// <summary>
        /// 添加一条结果消息。
        /// </summary>
        /// <param name="message">消息内容。</param>
        public void AddMessage(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                messages.Add(message);
            }
        }
    }
}
