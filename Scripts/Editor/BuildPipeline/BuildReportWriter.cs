using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建报告写入器：将执行报告序列化为 JSON 落盘供自动化读取，
    /// 并提供窗口可展示的中文摘要文本。
    /// 输出解析失败时报告回退写入工程 Library 目录，保证失败构建
    /// 仍有可定位的步骤与原因。
    /// </summary>
    public static class BuildReportWriter
    {
        /// <summary>报告文件名（ASCII 文件名，避免跨平台编码问题）。</summary>
        public const string ReportFileName = "build-report.json";

        /// <summary>统一构建报告目录名。</summary>
        public const string ReportDirectoryName = "BuildReports";

        /// <summary>输出解析失败时的回退目录名。</summary>
        public const string FallbackDirectoryName = ReportDirectoryName;

        /// <summary>
        /// 获取 Assets/HotUpdate Recipe 的报告目录。
        /// 使用任务创建时间与 Recipe 命名，便于人工按时间和构建类型定位。
        /// </summary>
        /// <param name="state">构建任务状态。</param>
        /// <returns>相对于 Bundles 的报告目录。</returns>
        public static string GetAssetOnlyReportDirectory(BuildPipelineState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (DateTimeOffset.TryParse(state.CreatedAt, out DateTimeOffset createdAt))
            {
                return Path.Combine(
                    ReportDirectoryName,
                    $"{createdAt:yyyy-MM-dd_HH-mm-ss-fff}_{state.Recipe}");
            }

            string taskId = string.IsNullOrEmpty(state.TaskId)
                ? "unknown"
                : state.TaskId.Substring(0, Math.Min(8, state.TaskId.Length));
            return Path.Combine(
                ReportDirectoryName,
                $"unknown_{state.Recipe}_{taskId}");
        }

        /// <summary>
        /// 将执行报告写入目标目录；目录不存在时自动创建。
        /// 输出目录解析成功时写入输出目录，否则写入工程 Library 回退目录。
        /// </summary>
        /// <param name="report">执行报告，不能为空。</param>
        /// <param name="outputRootAbsolute">输出根目录绝对路径，可为空。</param>
        /// <param name="outputDirectory">输出目录（相对输出根），可为空。</param>
        /// <param name="taskId">任务 Id，用于回退目录命名。</param>
        /// <returns>报告文件完整路径。</returns>
        public static string Write(
            BuildExecutionReport report,
            string outputRootAbsolute,
            string outputDirectory,
            string taskId)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            string targetDirectory = ResolveTargetDirectory(
                outputRootAbsolute,
                outputDirectory,
                taskId);
            Directory.CreateDirectory(targetDirectory);

            string filePath = Path.Combine(targetDirectory, ReportFileName);
            File.WriteAllText(
                filePath,
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));
            return filePath;
        }

        /// <summary>
        /// 生成报告的中文摘要文本，供窗口与日志展示。
        /// </summary>
        /// <param name="report">执行报告，可为空。</param>
        /// <returns>中文摘要文本。</returns>
        public static string BuildTextSummary(BuildExecutionReport report)
        {
            if (report == null)
            {
                return "构建报告为空。";
            }

            StringBuilder builder = new StringBuilder();
            string resultText = report.Succeeded
                ? "成功"
                : report.Cancelled ? "已取消" : "失败";
            builder.AppendLine(
                $"构建报告：{report.ProfileName}（{report.TaskId}）");
            builder.AppendLine($"结果：{resultText}");
            builder.AppendLine($"平台：{report.TargetPlatform}（{report.ScriptBackend}）");
            builder.AppendLine($"版本：{report.PublicVersion}（Build {report.BuildNumber}）");
            builder.AppendLine($"Unity：{report.UnityVersion}");
            builder.AppendLine(
                $"输出：{report.OutputRoot}/{report.OutputDirectory}");
            builder.AppendLine(
                $"耗时：{report.DurationSeconds:0.0} 秒");

            if (report.Steps.Count > 0)
            {
                builder.AppendLine("步骤：");
                for (int i = 0; i < report.Steps.Count; i++)
                {
                    BuildExecutionReportStep step = report.Steps[i];
                    builder.AppendLine(
                        $"  {i + 1}. [{step.Status}] {step.DisplayName}"
                        + $"（{step.StepId}）— {step.DurationSeconds:0.0} 秒");
                    if (!string.IsNullOrEmpty(step.OutputPath))
                    {
                        builder.AppendLine($"     输出：{step.OutputPath}");
                    }

                    if (step.Status == "Failed"
                        && !string.IsNullOrEmpty(step.Error))
                    {
                        builder.AppendLine($"     错误：{step.Error}");
                    }
                }
            }

            if (!report.Succeeded
                && !string.IsNullOrEmpty(report.ErrorMessage))
            {
                builder.AppendLine($"失败原因：{report.ErrorMessage}");
            }

            return builder.ToString();
        }

        /// <summary>
        /// 解析报告目标目录：输出目录可用时使用输出目录，否则使用
        /// 工程 Library 下的回退目录（按任务 Id 隔离）。
        /// </summary>
        /// <param name="outputRootAbsolute">输出根目录绝对路径。</param>
        /// <param name="outputDirectory">输出目录（相对输出根）。</param>
        /// <param name="taskId">任务 Id。</param>
        /// <returns>报告目标目录绝对路径。</returns>
        private static string ResolveTargetDirectory(
            string outputRootAbsolute,
            string outputDirectory,
            string taskId)
        {
            if (!string.IsNullOrEmpty(outputRootAbsolute)
                && !string.IsNullOrEmpty(outputDirectory))
            {
                return Path.Combine(outputRootAbsolute, outputDirectory);
            }

            string fallbackRoot = Path.Combine(
                "Library",
                "UnityRFramework",
                FallbackDirectoryName);
            string safeTaskId = string.IsNullOrEmpty(taskId)
                ? "unknown"
                : taskId;
            return Path.Combine(fallbackRoot, safeTaskId);
        }
    }
}
