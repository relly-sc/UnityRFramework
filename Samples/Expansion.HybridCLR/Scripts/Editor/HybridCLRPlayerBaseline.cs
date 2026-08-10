using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HybridCLR.Editor;
using HybridCLR.Editor.Settings;
using RFramework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 记录成功 Player Build 对应的裁剪后 AOT 程序集，并在发布前校验其未被替换。
    /// </summary>
    internal static class HybridCLRPlayerBaseline
    {
        private const string BaselineRoot =
            "HybridCLRData/UnityRFramework/PlayerBaselines";

        /// <summary>
        /// 保存指定 Player Build 的 AOT 元数据基线。
        /// </summary>
        /// <param name="report">成功完成的 Player Build 报告。</param>
        public static void Capture(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            string sourceRoot = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            string[] assemblyNames = HybridCLRSettings.Instance.patchAOTAssemblies?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            if (assemblyNames.Length == 0)
            {
                return;
            }

            HybridCLRPlayerBaselineData data = new HybridCLRPlayerBaselineData
            {
                buildTarget = target.ToString(),
                playerOutputPath = report.summary.outputPath,
                createdUtc = DateTime.UtcNow.ToString("O"),
                assemblies = CreateAssemblyRecords(sourceRoot, assemblyNames)
            };

            string markerPath = GetMarkerPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, JsonUtility.ToJson(data, true));
            Debug.Log(
                $"[HybridCLR] Captured Player AOT baseline for '{target}' at "
                + $"'{markerPath}'.");
        }

        /// <summary>
        /// 校验当前 AOT 裁剪产物与最近一次成功 Player Build 完全一致。
        /// </summary>
        /// <param name="target">目标构建平台。</param>
        /// <param name="assemblyNames">准备发布的 AOT 程序集名称。</param>
        public static void Validate(BuildTarget target, string[] assemblyNames)
        {
            string markerPath = GetMarkerPath(target);
            if (!File.Exists(markerPath))
            {
                throw new RFrameworkException(
                    $"HybridCLR Player baseline is missing for '{target}'. "
                    + "Build the IL2CPP Player before publishing the Host Package.");
            }

            HybridCLRPlayerBaselineData data = JsonUtility.FromJson<
                HybridCLRPlayerBaselineData>(File.ReadAllText(markerPath));
            if (data == null
                || !string.Equals(
                    data.buildTarget,
                    target.ToString(),
                    StringComparison.Ordinal)
                || data.assemblies == null)
            {
                throw new RFrameworkException(
                    $"HybridCLR Player baseline is invalid: '{markerPath}'.");
            }

            Dictionary<string, HybridCLRPlayerBaselineAssembly> records =
                data.assemblies.ToDictionary(
                    item => item.assemblyName,
                    StringComparer.Ordinal);
            string sourceRoot = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            foreach (string assemblyName in assemblyNames)
            {
                if (!records.TryGetValue(
                        assemblyName,
                        out HybridCLRPlayerBaselineAssembly record))
                {
                    throw new RFrameworkException(
                        $"HybridCLR Player baseline does not contain "
                        + $"'{assemblyName}'. Rebuild the Player.");
                }

                string sourcePath = Path.Combine(sourceRoot, assemblyName + ".dll");
                if (!File.Exists(sourcePath)
                    || new FileInfo(sourcePath).Length != record.length
                    || !string.Equals(
                        ComputeSha256(sourcePath),
                        record.sha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new RFrameworkException(
                        $"HybridCLR AOT metadata '{assemblyName}' no longer "
                        + "matches the latest Player Build. Rebuild the Player "
                        + "before publishing the Host Package.");
                }
            }
        }

        private static HybridCLRPlayerBaselineAssembly[] CreateAssemblyRecords(
            string sourceRoot,
            string[] assemblyNames)
        {
            List<HybridCLRPlayerBaselineAssembly> result =
                new List<HybridCLRPlayerBaselineAssembly>();
            foreach (string assemblyName in assemblyNames)
            {
                string sourcePath = Path.Combine(sourceRoot, assemblyName + ".dll");
                if (!File.Exists(sourcePath))
                {
                    throw new RFrameworkException(
                        $"HybridCLR stripped AOT DLL is missing after Player "
                        + $"Build: '{sourcePath}'.");
                }

                FileInfo file = new FileInfo(sourcePath);
                result.Add(new HybridCLRPlayerBaselineAssembly
                {
                    assemblyName = assemblyName,
                    length = file.Length,
                    sha256 = ComputeSha256(sourcePath)
                });
            }

            return result.ToArray();
        }

        private static string ComputeSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty);
        }

        private static string GetMarkerPath(BuildTarget target)
        {
            return Path.Combine(BaselineRoot, target + ".json");
        }
    }

    /// <summary>
    /// 在成功构建 Player 后捕获 HybridCLR AOT 元数据基线。
    /// </summary>
    public sealed class HybridCLRPlayerBaselinePostprocessor :
        IPostprocessBuildWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => int.MaxValue;

        /// <inheritdoc />
        public void OnPostprocessBuild(BuildReport report)
        {
            BuildResult result = report.summary.result;
            if (HybridCLRSettings.Instance.enable
                && result != BuildResult.Failed
                && result != BuildResult.Cancelled)
            {
                HybridCLRPlayerBaseline.Capture(report);
            }
        }
    }

    [Serializable]
    internal sealed class HybridCLRPlayerBaselineData
    {
        public string buildTarget;
        public string playerOutputPath;
        public string createdUtc;
        public HybridCLRPlayerBaselineAssembly[] assemblies;
    }

    [Serializable]
    internal sealed class HybridCLRPlayerBaselineAssembly
    {
        public string assemblyName;
        public long length;
        public string sha256;
    }
}
