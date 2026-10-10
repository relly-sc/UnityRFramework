using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace UnityRFramework.Editor
{
    /// <summary>从官方发布仓库安装可选 Samples 包。</summary>
    internal static class SamplesPackageInstaller
    {
        private const string CorePackage = "com.relly-sc.unityrframework";
        private const string SamplesPackage = "com.relly-sc.unityrframework.samples";
        private const string SamplesUrl =
            "https://github.com/relly-sc/UnityRFramework-Samples.git#main";

        private static AddRequest request;
        private static string coreVersion;

        [MenuItem("UnityRFramework/安装 Samples")]
        private static void Install()
        {
            if (request != null)
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

            PackageInfo installed = PackageInfo.FindForPackageName(SamplesPackage);
            if (installed != null)
            {
                EditorUtility.DisplayDialog(
                    "安装 Samples",
                    $"Samples {installed.version} 已安装。如需更新，请在 Package Manager 中操作。",
                    "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "安装 Samples",
                    $"将从官方 Samples 仓库的 main 分支安装最新修订版。\n"
                    + $"当前核心版本：{core.version}。安装后会检查两个包的版本是否一致。",
                    "安装",
                    "取消"))
            {
                return;
            }

            try
            {
                coreVersion = core.version;
                request = Client.Add(SamplesUrl);
                EditorApplication.update += CheckResult;
            }
            catch (Exception exception)
            {
                request = null;
                Debug.LogError($"Samples 安装未启动：{exception.Message}");
            }
        }

        private static void CheckResult()
        {
            if (!request.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= CheckResult;
            AddRequest completed = request;
            request = null;
            if (completed.Status != StatusCode.Success)
            {
                Debug.LogError($"Samples 安装失败：{completed.Error?.message}");
                return;
            }

            PackageInfo samples = completed.Result;
            if (samples == null || !string.Equals(samples.version, coreVersion, StringComparison.Ordinal))
            {
                Debug.LogError(
                    $"Samples 与核心版本不一致：核心 {coreVersion}，Samples {samples?.version}。"
                    + "请更新核心包和 Samples 包至相同版本。");
                return;
            }

            Debug.Log($"Samples {samples.version} 安装成功。可在 Package Manager 中按需导入示例。");
        }
    }
}
