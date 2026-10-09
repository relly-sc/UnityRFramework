using System;
using System.Collections.Generic;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// Excel Localization 解析和多格式导出设置。
    /// </summary>
    [Serializable]
    public sealed class ExcelLocalizationExportOptions
    {
        /// <summary>Excel 文件目录，可使用工程相对路径或绝对路径。</summary>
        public string SourceDirectory =
            "Assets/ConfigSource/Localization";

        /// <summary>检查本地化键时扫描固定键调用的 C# 源目录。</summary>
        public string CodeDirectory = "Assets";

        /// <summary>本地化数据输出根目录。</summary>
        public string OutputDirectory =
            ExcelLocalizationExportDefaults.OutputDirectory;

        /// <summary>是否生成 JSON 与二进制多语言容器。</summary>
        public bool ExportBundle = true;

        /// <summary>不含扩展名的多语言容器文件名。</summary>
        public string BundleName =
            ExcelLocalizationExportDefaults.BundleName;

        /// <summary>已选导出器标识。</summary>
        public List<string> SelectedExporterIds = new List<string>
        {
            ExcelLocalizationExporterIds.Json,
            ExcelLocalizationExporterIds.Binary
        };
    }

    /// <summary>
    /// Project 右键导出的 Localization 固定设置。
    /// </summary>
    public static class ExcelLocalizationExportDefaults
    {
        /// <summary>固定本地化数据输出根目录。</summary>
        public const string OutputDirectory =
            "Assets/Resources/Localization";

        /// <summary>默认多语言容器文件名。</summary>
        public const string BundleName = "LocalizationBundle";
    }
}
