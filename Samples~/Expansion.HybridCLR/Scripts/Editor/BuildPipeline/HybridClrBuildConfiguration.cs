using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// HybridCLR 构建步骤私有配置资产。
    /// </summary>
    public sealed class HybridClrBuildConfiguration : ScriptableObject
    {
        private const string DemoOutputSuffix =
            "/Expansion.HybridCLR.Demo/GameAssets/HotUpdate";
        private const string DemoBuilderSuffix =
            "/Expansion.HybridCLR.Demo/Scripts/Editor/ExpansionHybridCLRDemoBuilder.cs";

        [Tooltip("热更产物输出根目录。")]
        public string OutputAssetRoot = "Assets/GameAssets/HotUpdate";

        [Tooltip("必填。热更新程序集中实现 IHotUpdateEntry 接口的入口类型全名，格式为命名空间+类名，例如 UnityRFramework.Sample.HotUpdateEntry。")]
        public string EntryTypeName = string.Empty;

        [Tooltip("业务代码版本号；留空时自动生成。")]
        public string CodeVersion = string.Empty;

        [Tooltip("是否包含 Portable PDB。")]
        public bool IncludePdb;

        private void OnEnable()
        {
            TryMigrateOfficialDemoPath();
        }

        internal bool TryMigrateOfficialDemoPath()
        {
            string current = NormalizePath(OutputAssetRoot);
            if (AssetDatabase.IsValidFolder(current)
                || !current.EndsWith(
                    DemoOutputSuffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] candidates = AssetDatabase
                .FindAssets("ExpansionHybridCLRDemoBuilder t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(
                    DemoBuilderSuffix,
                    StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Substring(
                    0,
                    path.Length - DemoBuilderSuffix.Length) + DemoOutputSuffix)
                .Where(AssetDatabase.IsValidFolder)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (candidates.Length != 1)
            {
                return false;
            }

            OutputAssetRoot = candidates[0];
            EditorUtility.SetDirty(this);
            return true;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').TrimEnd('/');
        }
    }
}
