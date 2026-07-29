using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 转换工具单次操作结果。
    /// </summary>
    public sealed class ConfigPipelineReport
    {
        private readonly List<string> messages = new List<string>();

        /// <summary>获取已处理的源文件数量。</summary>
        public int ProcessedFileCount { get; private set; }

        /// <summary>获取实际写入的输出文件数量。</summary>
        public int WrittenFileCount { get; private set; }

        /// <summary>获取操作消息。</summary>
        public IReadOnlyList<string> Messages => messages;

        /// <summary>记录一个已处理的源文件。</summary>
        public void FileProcessed()
        {
            ProcessedFileCount++;
        }

        /// <summary>
        /// 记录一个实际写入的输出文件。
        /// </summary>
        /// <param name="path">输出文件路径。</param>
        public void FileWritten(string path)
        {
            WrittenFileCount++;
            messages.Add("Written: " + path);
        }

        /// <summary>
        /// 增加操作消息。
        /// </summary>
        /// <param name="message">消息内容。</param>
        public void AddMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                messages.Add(message);
            }
        }
    }
}
