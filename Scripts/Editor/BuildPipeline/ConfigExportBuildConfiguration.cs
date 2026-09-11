using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

        /// <summary>是否执行正式发布配置泄漏检查。</summary>
        [FormerlySerializedAs("JsonLeakCheck")]
        [Tooltip("检查被 Player、Resources、StreamingAssets 或 YooAsset 收集的开发配置和密钥文件。")]
        public bool ReleaseLeakCheck = true;

        /// <summary>正式构建发现泄漏时是否阻止构建。</summary>
        [Tooltip("正式构建发现泄漏时阻止构建；Development Build 始终只告警。")]
        public bool BlockReleaseBuildOnLeak = true;

        /// <summary>允许进入正式产物的文件或目录路径。</summary>
        [Tooltip("允许进入正式产物的工程内文件或目录。仅填写经过人工确认的路径。")]
        public List<string> LeakCheckAllowedPaths = new List<string>();

        /// <summary>旧版 JSON 泄漏检查开关，保留源码兼容。</summary>
        [Obsolete("请改用 ReleaseLeakCheck。")]
        public bool JsonLeakCheck
        {
            get { return ReleaseLeakCheck; }
            set { ReleaseLeakCheck = value; }
        }
    }
}
