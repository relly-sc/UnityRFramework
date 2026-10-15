using System.Collections.Generic;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>构建步骤使用的配置表源工具。</summary>
    public enum ConfigExportTool
    {
        Csv = 0,
        Excel = 1
    }

    /// <summary>
    /// Config 与 Localization 导出步骤配置资产。
    /// </summary>
    public sealed class ConfigExportBuildConfiguration : ScriptableObject
    {
        /// <summary>配置表导出路径和格式。</summary>
        [Tooltip("配置表导出路径和格式。")]
        public ConfigPipelineOptions Options = new ConfigPipelineOptions();

        /// <summary>Config 使用 CSV 核心工具或可选 Excel 扩展工具。</summary>
        public ConfigExportTool ExportTool = ConfigExportTool.Csv;

        [Tooltip("导出 Config 配置表；关闭后不检查或读取 Config 源目录及密钥。")]
        public bool ExportConfig = true;

        [Tooltip("导出 Localization 多语言表；没有多语言表的项目可关闭，不检查或读取该源目录。")]
        public bool ExportLocalization = true;

        /// <summary>是否执行正式发布配置泄漏检查。</summary>
        [Tooltip("检查被 Player、Resources、StreamingAssets 或 YooAsset 收集的开发配置和密钥文件。")]
        public bool ReleaseLeakCheck = true;

        /// <summary>正式构建发现泄漏时是否阻止构建。</summary>
        [Tooltip("正式构建发现泄漏时阻止构建；Development Build 始终只告警。")]
        public bool BlockReleaseBuildOnLeak = true;

        /// <summary>允许进入正式产物的文件或目录路径。</summary>
        [Tooltip("允许进入正式产物的工程内文件或目录。仅填写经过人工确认的路径。")]
        public List<string> LeakCheckAllowedPaths = new List<string>();

    }
}
