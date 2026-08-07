using System;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 自定义 Excel 配置导出器生成的一个文件。
    /// </summary>
    public sealed class ExcelConfigOutputFile
    {
        /// <summary>
        /// 初始化导出文件。
        /// </summary>
        /// <param name="relativePath">相对于工具输出根目录的文件路径。</param>
        /// <param name="data">完整文件字节。</param>
        public ExcelConfigOutputFile(string relativePath, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new ArgumentException(
                    "Excel output relative path is invalid.", nameof(relativePath));
            }

            RelativePath = relativePath.Replace('\\', '/');
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>获取相对于输出根目录的文件路径。</summary>
        public string RelativePath { get; }

        /// <summary>获取完整文件字节。</summary>
        public byte[] Data { get; }
    }
}
