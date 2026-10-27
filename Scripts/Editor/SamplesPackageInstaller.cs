using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityRFramework.Editor
{
    /// <summary>从官方发布仓库安装可选 Samples 包。</summary>
    [InitializeOnLoad]
    internal static class SamplesPackageInstaller
    {
        private const string CorePackage = "com.relly-sc.unityrframework";
        private const string SamplesPackage = "com.relly-sc.unityrframework.samples";
        private const string PendingVersionKey = "UnityRFramework.Samples.PendingVersion";
        private static AddRequest request;
        private static ListRequest resumeListing;
        private static string samplesVersion;
        internal static bool IsBusy => !string.IsNullOrEmpty(samplesVersion);

        static SamplesPackageInstaller()
        {
            samplesVersion = SessionState.GetString(PendingVersionKey, string.Empty);
            if (IsBusy) EditorApplication.update += CheckResult;
        }

        internal static string GetSamplesUrl(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Samples 版本不能为空。", nameof(version));
            return "https://github.com/relly-sc/UnityRFramework-Samples.git#" + Uri.EscapeDataString(version);
        }

        internal static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog("安装 Samples", "请退出 Play Mode，并等待编译或导入完成。", "确定");
                return;
            }
            if (IsBusy || FrameworkSamplesInstaller.IsBusy)
            {
                EditorUtility.DisplayDialog("安装 Samples", "安装任务正在进行，请等待 Package Manager 完成。", "确定");
                return;
            }

            PackageInfo core = PackageInfo.FindForPackageName(CorePackage);
            if (core == null)
            {
                EditorUtility.DisplayDialog(
                    "安装 Samples",
                    "请先通过 Package Manager 安装 UnityRFramework 核心包。",
                    "确定");
                return;
            }

            var catalog = FrameworkSampleCatalog.Load(Path.Combine(core.resolvedPath, "Scripts", "Editor", "FrameworkSamples.json"));
            if (core.version != catalog.frameworkVersion)
            {
                EditorUtility.DisplayDialog("安装 Samples", "核心包版本与自身扩展清单不一致，请先刷新 Package Manager。", "确定");
                return;
            }

            PackageInfo installed = PackageInfo.FindForPackageName(SamplesPackage);
            if (installed != null && installed.version == catalog.samplesVersion)
            {
                FrameworkSamplesWindow.Open();
                return;
            }
            if (installed != null && FrameworkPackageDependency.IsProtected(installed.source))
            {
                EditorUtility.DisplayDialog("安装 Samples", "Samples 是本地或嵌入式包，请自行同步到兼容当前核心的版本。安装器不会覆盖本地源码。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "安装 Samples",
                    $"将安装与当前核心兼容的 Samples {catalog.samplesVersion}。\n"
                    + $"Samples 最低支持核心版本：{catalog.samplesMinimumFrameworkVersion}。\n"
                    + (installed == null ? string.Empty : $"当前 Samples：{installed.version}。\n")
                    + "不会自动导入全部示例或安装全部第三方插件。",
                    "安装",
                    "取消"))
            {
                return;
            }

            try
            {
                samplesVersion = catalog.samplesVersion;
                SessionState.SetString(PendingVersionKey, samplesVersion);
                request = Client.Add(GetSamplesUrl(samplesVersion));
                EditorApplication.update += CheckResult;
            }
            catch (Exception exception)
            {
                request = null;
                samplesVersion = null;
                SessionState.EraseString(PendingVersionKey);
                Debug.LogError($"Samples 安装未启动：{exception.Message}");
            }
        }

        private static void CheckResult()
        {
            try { CheckRequestResult(); }
            catch (Exception exception) { Stop("Samples 安装已停止：" + exception.Message); }
        }

        private static void CheckRequestResult()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            PackageInfo samples;
            if (request != null)
            {
                if (!request.IsCompleted) return;
                if (request.Status != StatusCode.Success)
                {
                    Stop("Samples 安装失败：" + request.Error?.message);
                    return;
                }
                samples = request.Result;
            }
            else
            {
                // UPM 导入可能先触发 Domain Reload；通过重新查询确认结果，不重复发起安装。
                if (resumeListing == null) { resumeListing = Client.List(true, true); return; }
                if (!resumeListing.IsCompleted) return;
                if (resumeListing.Status != StatusCode.Success)
                {
                    Stop("无法恢复 Samples 安装结果：" + resumeListing.Error?.message);
                    return;
                }
                samples = null;
                foreach (var package in resumeListing.Result)
                    if (package.name == SamplesPackage) { samples = package; break; }
            }
            string expected = samplesVersion;
            Stop(null);
            if (samples == null || !string.Equals(samples.version, expected, StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"Samples 安装结果版本不正确：要求 {expected}，实际 {samples?.version ?? "未安装"}。"
                    + "请重新打开安装工具检查。");
                return;
            }

            Debug.Log($"Samples {samples.version} 安装成功。");
            FrameworkSamplesWindow.Open();
        }

        private static void Stop(string error)
        {
            EditorApplication.update -= CheckResult;
            SessionState.EraseString(PendingVersionKey);
            request = null;
            resumeListing = null;
            samplesVersion = null;
            if (error != null) Debug.LogError(error);
        }
    }
}
