using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 与 Localization 导出步骤配置资产。
    /// </summary>
    public sealed class ConfigExportBuildConfiguration : ScriptableObject
    {
        /// <summary>配置表导出路径和格式。</summary>
        [Tooltip("配置表导出路径和格式。")]
        public ConfigPipelineOptions Options = new ConfigPipelineOptions();

        /// <summary>是否保留 JSON 产物。</summary>
        [Tooltip("是否保留 JSON 产物。")]
        public bool ExportJson = true;

        /// <summary>正式档是否检查开发 JSON 残留。</summary>
        [Tooltip("正式档是否检查开发 JSON 残留。")]
        public bool JsonLeakCheck = true;
    }
}
