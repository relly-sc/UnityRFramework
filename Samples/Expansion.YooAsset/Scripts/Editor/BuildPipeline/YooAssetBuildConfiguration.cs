using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// YooAsset 构建步骤私有配置资产。
    /// </summary>
    public sealed class YooAssetBuildConfiguration : ScriptableObject
    {
        [Tooltip("要构建的 YooAsset Package 名称。")]
        public string PackageName = string.Empty;

        [Tooltip("资源包版本号；留空时使用 Builder 默认版本。")]
        public string PackageVersion = string.Empty;

        [Tooltip("发布到工程 Bundles 目录下的子目录名。")]
        public string ServerDirectoryName = string.Empty;

        [Tooltip("构建管线名称；留空时读取 YooAsset Builder 设置。")]
        public string BuildPipelineName = string.Empty;

        [Tooltip("构建前是否清理 Build Cache。")]
        public bool ClearBuildCache;

        public static string GetDefaultBuilderVersion()
        {
            int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
        }

        public static string[] GetBuildPipelineOptions()
        {
            List<string> names = new List<string>();
            foreach (Type type in TypeCache.GetTypesWithAttribute<BuildPipelineAttribute>())
            {
                BuildPipelineAttribute attribute =
                    Attribute.GetCustomAttribute(
                        type,
                        typeof(BuildPipelineAttribute))
                        as BuildPipelineAttribute;
                if (attribute != null
                    && !string.IsNullOrWhiteSpace(attribute.PipelineName)
                    && !names.Contains(attribute.PipelineName))
                {
                    names.Add(attribute.PipelineName);
                }
            }

            return names.OrderBy(GetPipelineOrder)
                .ThenBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>获取 Bundle Collector Setting 中已配置的 Package 名称。</summary>
        public static string[] GetPackageOptions()
        {
            if (BundleCollectorSettingData.Setting == null
                || BundleCollectorSettingData.Setting.Packages == null)
            {
                return Array.Empty<string>();
            }

            return BundleCollectorSettingData.Setting.Packages
                .Select(package => package.PackageName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>判断当前构建步骤是否实现指定 YooAsset 构建管线。</summary>
        public static bool IsSupportedBuildPipeline(string pipelineName)
        {
            return string.Equals(
                       pipelineName,
                       nameof(EBuildPipeline.ScriptableBuildPipeline),
                       StringComparison.Ordinal)
                || string.Equals(
                       pipelineName,
                       nameof(EBuildPipeline.LegacyBuildPipeline),
                       StringComparison.Ordinal)
                || string.Equals(
                       pipelineName,
                       nameof(EBuildPipeline.RawFileBuildPipeline),
                       StringComparison.Ordinal);
        }

        /// <summary>按 YooAsset 内置枚举顺序排列构建管线，自定义管线排在末尾。</summary>
        private static int GetPipelineOrder(string pipelineName)
        {
            return Enum.TryParse(pipelineName, out EBuildPipeline pipeline)
                ? (int)pipeline
                : int.MaxValue;
        }
    }
}
