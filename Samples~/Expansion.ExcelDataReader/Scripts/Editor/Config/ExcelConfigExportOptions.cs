using System;
using System.Collections.Generic;
using RFramework;
using UnityRFramework.Editor;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// Excel 配置表解析、代码生成和数据导出设置。
    /// </summary>
    [Serializable]
    public sealed class ExcelConfigExportOptions
    {
        /// <summary>Excel 文件目录，可使用工程相对路径或绝对路径。</summary>
        public string SourceDirectory = "Assets/ConfigSource/Config";

        /// <summary>配置数据输出根目录。</summary>
        public string OutputDirectory = ExcelConfigExportDefaults.OutputDirectory;

        /// <summary>是否生成配置行类型和 URFC Codec。</summary>
        public bool GenerateCode = true;

        /// <summary>生成代码目录。</summary>
        public string GeneratedCodeDirectory = ExcelConfigExportDefaults.GeneratedCodeDirectory;

        /// <summary>生成代码使用的命名空间，留空时使用全局命名空间。</summary>
        public string GeneratedNamespace = ExcelConfigExportDefaults.GeneratedNamespace;

        /// <summary>正式 .bytes 文件解密后的数据格式。</summary>
        public ConfigReleaseDataFormat ConfigReleaseFormat =
            ConfigReleaseDataFormat.FrameworkBinary;

        /// <summary>正式二进制保护模式。</summary>
        public ConfigProtectionMode ConfigBinaryProtection = ConfigProtectionMode.None;

        /// <summary>加密产物中的密钥编号。</summary>
        public string ConfigProtectionKeyId = "config-v1";

        /// <summary>偏移混淆密钥文件。</summary>
        public string ConfigProtectionKeyFile = "Assets/ConfigSource/ConfigKey.bytes";

        /// <summary>运行时配置加载路径前缀。</summary>
        public string ConfigProtectionSourceRoot = "Config/Binary";

        /// <summary>已选导出器标识。</summary>
        public List<string> SelectedExporterIds = new List<string>
        {
            ExcelConfigExporterIds.Json,
            ExcelConfigExporterIds.Binary
        };
    }

    /// <summary>
    /// Project 右键导出的固定路径与默认代码设置。
    /// </summary>
    public static class ExcelConfigExportDefaults
    {
        /// <summary>固定配置数据输出根目录。</summary>
        public const string OutputDirectory = "Assets/Resources/Config";

        /// <summary>固定生成代码目录。</summary>
        public const string GeneratedCodeDirectory =
            "Assets/Generated/UnityRFramework/Config";

        /// <summary>默认生成代码命名空间；留空时使用全局命名空间。</summary>
        public const string GeneratedNamespace = "";
    }
}
