using System;
using System.Collections.Generic;
using System.Text;
using RFramework;
using UnityEngine;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 描述一个需要补充元数据的 AOT 程序集资源。
    /// </summary>
    [Serializable]
    public sealed class HybridCLRAotMetadataInfo
    {
        /// <summary>AOT 程序集名称，不包含 .dll 后缀。</summary>
        public string assemblyName;

        /// <summary>裁剪后 AOT DLL 的资源位置。</summary>
        public string location;
    }

    /// <summary>
    /// 描述一个按依赖顺序加载的热更新程序集资源。
    /// </summary>
    [Serializable]
    public sealed class HybridCLRHotUpdateAssemblyInfo
    {
        /// <summary>程序集名称，不包含 .dll 后缀。</summary>
        public string assemblyName;

        /// <summary>热更新 DLL 的资源位置。</summary>
        public string dllLocation;

        /// <summary>可选 PDB 的资源位置；为空时不加载调试符号。</summary>
        public string pdbLocation;
    }

    /// <summary>
    /// HybridCLR 热更新资源清单。
    /// </summary>
    [Serializable]
    public sealed class HybridCLRHotUpdateManifest
    {
        /// <summary>当前清单结构版本。</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>清单结构版本。</summary>
        public int schemaVersion = CurrentSchemaVersion;

        /// <summary>业务代码版本。</summary>
        public string codeVersion;

        /// <summary>生成该清单的 Unity BuildTarget 名称。</summary>
        public string buildTarget;

        /// <summary>热更新入口类型全名。</summary>
        public string entryTypeName;

        /// <summary>需要补充元数据的 AOT 程序集。</summary>
        public HybridCLRAotMetadataInfo[] aotMetadata = Array.Empty<HybridCLRAotMetadataInfo>();

        /// <summary>按依赖顺序排列的热更新程序集。</summary>
        public HybridCLRHotUpdateAssemblyInfo[] hotUpdateAssemblies =
            Array.Empty<HybridCLRHotUpdateAssemblyInfo>();

        /// <summary>
        /// 从 UTF-8 JSON 字节解析并校验清单。
        /// </summary>
        /// <param name="bytes">清单字节。</param>
        /// <param name="expectedBuildTarget">期望的 BuildTarget；为空时不校验平台。</param>
        /// <returns>有效的清单实例。</returns>
        public static HybridCLRHotUpdateManifest Parse(
            byte[] bytes,
            string expectedBuildTarget = null)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException("HybridCLR manifest bytes are empty.");
            }

            HybridCLRHotUpdateManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<HybridCLRHotUpdateManifest>(
                    Encoding.UTF8.GetString(bytes));
            }
            catch (Exception exception)
            {
                throw new RFrameworkException(
                    "HybridCLR manifest JSON is invalid.",
                    exception);
            }

            if (manifest == null)
            {
                throw new RFrameworkException("HybridCLR manifest is invalid.");
            }

            manifest.Validate(expectedBuildTarget);
            return manifest;
        }

        /// <summary>
        /// 校验清单结构和程序集唯一性。
        /// </summary>
        /// <param name="expectedBuildTarget">期望的 BuildTarget；为空时不校验平台。</param>
        public void Validate(string expectedBuildTarget = null)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new RFrameworkException(
                    $"Unsupported HybridCLR manifest schema '{schemaVersion}'. "
                    + $"Expected '{CurrentSchemaVersion}'.");
            }

            if (string.IsNullOrWhiteSpace(codeVersion)
                || string.IsNullOrWhiteSpace(buildTarget)
                || string.IsNullOrWhiteSpace(entryTypeName))
            {
                throw new RFrameworkException(
                    "HybridCLR manifest version, build target or entry type is empty.");
            }

            if (!string.IsNullOrWhiteSpace(expectedBuildTarget)
                && !string.Equals(
                    buildTarget,
                    expectedBuildTarget,
                    StringComparison.Ordinal))
            {
                throw new RFrameworkException(
                    $"HybridCLR manifest target '{buildTarget}' does not match "
                    + $"runtime target '{expectedBuildTarget}'.");
            }

            ValidateAotMetadata();
            ValidateHotUpdateAssemblies();
        }

        private void ValidateAotMetadata()
        {
            aotMetadata = aotMetadata ?? Array.Empty<HybridCLRAotMetadataInfo>();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < aotMetadata.Length; i++)
            {
                HybridCLRAotMetadataInfo item = aotMetadata[i];
                if (item == null || string.IsNullOrWhiteSpace(item.assemblyName)
                    || string.IsNullOrWhiteSpace(item.location))
                {
                    throw new RFrameworkException(
                        $"HybridCLR AOT metadata entry at index '{i}' is invalid.");
                }

                if (!names.Add(item.assemblyName))
                {
                    throw new RFrameworkException(
                        $"Duplicate HybridCLR AOT metadata assembly '{item.assemblyName}'.");
                }
            }
        }

        private void ValidateHotUpdateAssemblies()
        {
            hotUpdateAssemblies = hotUpdateAssemblies
                ?? Array.Empty<HybridCLRHotUpdateAssemblyInfo>();
            if (hotUpdateAssemblies.Length == 0)
            {
                throw new RFrameworkException(
                    "HybridCLR manifest contains no hot update assemblies.");
            }

            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < hotUpdateAssemblies.Length; i++)
            {
                HybridCLRHotUpdateAssemblyInfo item = hotUpdateAssemblies[i];
                if (item == null || string.IsNullOrWhiteSpace(item.assemblyName)
                    || string.IsNullOrWhiteSpace(item.dllLocation))
                {
                    throw new RFrameworkException(
                        $"HybridCLR assembly entry at index '{i}' is invalid.");
                }

                if (!names.Add(item.assemblyName))
                {
                    throw new RFrameworkException(
                        $"Duplicate HybridCLR hot update assembly '{item.assemblyName}'.");
                }
            }
        }
    }
}
