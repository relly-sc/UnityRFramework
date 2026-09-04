using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建 Player 步骤：调用 Unity BuildPipeline 执行 Player 构建。
    /// 构建前按 Profile 配置执行可选的 Clean Build（清理只允许在输出根目录
    /// 边界内执行）；构建期间展示进度条；构建结果严格读取 BuildReport，
    /// 失败、取消与未知结果都不会显示为成功。
    /// </summary>
    public sealed class BuildPlayerStep : BuildPipelineStepBase
    {
        /// <summary>步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "core.build-player";
            }
        }

        /// <summary>步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "构建 Player";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.BuildPlayer;

        /// <summary>步骤排序值，位于参数应用之后、收尾之前。</summary>
        public override int Order
        {
            get
            {
                return 30;
            }
        }

        /// <summary>本步骤调用 Unity BuildPipeline。</summary>
        public override bool CallsBuildPipeline
        {
            get
            {
                return true;
            }
        }

        /// <summary>构建失败后允许修复重试。</summary>
        public override bool CanRetry
        {
            get
            {
                return true;
            }
        }

        /// <summary>失败时可能残留输出目录，需要人工清理确认。</summary>
        public override bool RequiresManualCleanup
        {
            get
            {
                return true;
            }
        }

        /// <summary>本步骤仅在配置存在且输出解析成功时可用。</summary>
        public override bool CanRun(BuildPipelineContext context)
        {
            return context != null
                && context.Profile != null
                && context.OutputError.Length == 0;
        }

        /// <summary>
        /// 执行 Player 构建；取消只检查构建开始前与结束后，构建过程不可中断。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>依据 BuildReport 状态返回成功、失败或取消。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return BuildStepResult.Cancelled(
                    "Player 构建已取消。");
            }

            CleanOutputIfRequested(context);

            BuildPlayerOptions options;
            try
            {
                options = BuildPlayerOptionsFactory.Create(context);
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"组装构建选项失败：{exception.Message}",
                    exception);
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    "构建 Player",
                    $"正在构建 {context.Target}，请勿关闭编辑器……",
                    0.5f);

                BuildReport report = BuildPipeline.BuildPlayer(options);
                return InterpretReport(report, options.locationPathName);
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"BuildPlayer 抛出异常：{exception.Message}",
                    exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 按 Profile 配置清理目标输出目录；清理前解析绝对路径并校验
        /// 其位于输出根目录边界内，防止模板穿越目录删除工程文件。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        private static void CleanOutputIfRequested(
            BuildPipelineContext context)
        {
            if (context.Profile == null
                || !context.Profile.Output.CleanBeforeBuild)
            {
                return;
            }

            string targetDirectory = Path.Combine(
                context.OutputRootAbsolute,
                context.OutputDirectory);
            if (!Directory.Exists(targetDirectory))
            {
                return;
            }

            string normalizedTarget = Path.GetFullPath(targetDirectory);
            string normalizedRoot = Path.GetFullPath(
                context.OutputRootAbsolute);
            if (!IsWithin(normalizedTarget, normalizedRoot))
            {
                throw new InvalidOperationException(
                    $"拒绝清理：输出目录 '{targetDirectory}' 不在输出根目录"
                    + $" '{context.OutputRootAbsolute}' 边界内。");
            }

            Directory.Delete(targetDirectory, true);
        }

        /// <summary>
        /// 解读 BuildReport 结果：仅 Succeeded 视为成功，
        /// 失败、取消与未知结果均不会显示为成功。
        /// </summary>
        /// <param name="report">Unity 构建报告。</param>
        /// <param name="locationPath">产物预期路径。</param>
        /// <returns>对应的步骤结果。</returns>
        private static BuildStepResult InterpretReport(
            BuildReport report,
            string locationPath)
        {
            if (report == null)
            {
                return BuildStepResult.Failed(
                    "BuildPipeline.BuildPlayer 返回空报告，构建结果未知。",
                    null);
            }

            BuildSummary summary = report.summary;
            string sizeText = FormatSize((long)summary.totalSize);
            string durationText = FormatDuration(summary.totalTime);
            switch (summary.result)
            {
                case BuildResult.Succeeded:
                    if (!ProductExists(summary.outputPath))
                    {
                        // 强制中断 Player 编译后，Unity/Bee 增量状态可能损坏：
                        // 后续构建跳过原生编译却报告成功。立即拦截并给出实测有效的修复路径。
                        return BuildStepResult.Failed(
                            "BuildReport 报告成功，但产物未生成在预期路径："
                            + $"{summary.outputPath}。\n"
                            + "原因：上次构建在 Player 编译阶段被强制中断，Unity 增量状态损坏，"
                            + "本次构建跳过了原生编译（Unity 侧假成功）。\n"
                            + "修复：用官方 Build Settings 完整构建一次（成功产出 exe 即修复），"
                            + "再用本工具重新构建；清理输出目录或删除缓存无效。\n"
                            + "请避免在构建 Player 阶段强制关闭 Unity。",
                            null);
                    }

                    return BuildStepResult.Succeeded(
                        $"Player 构建成功：{summary.outputPath}"
                        + $"（{sizeText}，耗时 {durationText}）。");
                case BuildResult.Cancelled:
                    return BuildStepResult.Cancelled(
                        $"Player 构建被取消：{summary.outputPath}。");
                case BuildResult.Failed:
                    return BuildStepResult.Failed(
                        $"Player 构建失败：{summary.outputPath}"
                        + FormatErrors(report),
                        null);
                default:
                    return BuildStepResult.Failed(
                        $"Player 构建结果未知（{summary.result}），不能视为成功。"
                        + $"产物路径：{locationPath}",
                        null);
            }
        }

        /// <summary>
        /// 校验产物是否真实落盘：Windows/Android 为文件，
        /// iOS/WebGL 等目录产物为目录。
        /// </summary>
        /// <param name="outputPath">构建报告给出的产物路径。</param>
        /// <returns>文件或目录存在时返回 true。</returns>
        private static bool ProductExists(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            return File.Exists(outputPath) || Directory.Exists(outputPath);
        }

        /// <summary>
        /// 从构建报告中提取错误详情文本：遍历各构建步骤的消息，
        /// 收集 Error/Assert/Exception 级别消息，最多展示前 5 条，避免刷屏。
        /// </summary>
        /// <param name="report">构建报告。</param>
        /// <returns>错误详情文本；无错误时返回空字符串。</returns>
        private static string FormatErrors(BuildReport report)
        {
            if (report.steps == null)
            {
                return string.Empty;
            }

            const int maxErrorCount = 5;
            StringBuilder builder = new StringBuilder();
            int errorCount = 0;
            for (int stepIndex = 0;
                 stepIndex < report.steps.Length;
                 stepIndex++)
            {
                BuildStepMessage[] messages = report.steps[stepIndex].messages;
                if (messages == null)
                {
                    continue;
                }

                for (int messageIndex = 0;
                     messageIndex < messages.Length;
                     messageIndex++)
                {
                    BuildStepMessage message = messages[messageIndex];
                    if (!IsErrorLogType(message.type))
                    {
                        continue;
                    }

                    if (errorCount < maxErrorCount)
                    {
                        builder.Append("\n  - ");
                        builder.Append(message.content);
                    }

                    errorCount++;
                }
            }

            if (errorCount > maxErrorCount)
            {
                builder.Append("\n  - ……（共 ");
                builder.Append(errorCount);
                builder.Append(" 条错误）");
            }

            return builder.ToString();
        }

        /// <summary>
        /// 判断日志级别是否属于构建错误（Error、Assert、Exception）。
        /// </summary>
        /// <param name="type">日志级别。</param>
        /// <returns>属于错误级别时返回 true。</returns>
        private static bool IsErrorLogType(LogType type)
        {
            return type == LogType.Error
                || type == LogType.Assert
                || type == LogType.Exception;
        }

        /// <summary>
        /// 判断子路径是否位于父路径内部（含相等）。
        /// </summary>
        /// <param name="child">子路径。</param>
        /// <param name="parent">父路径。</param>
        /// <returns>位于内部或相等时返回 true。</returns>
        private static bool IsWithin(string child, string parent)
        {
            return child.Equals(parent, StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(
                    parent + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(
                    parent + Path.AltDirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将字节数格式化为人类可读大小。
        /// </summary>
        /// <param name="bytes">字节数。</param>
        /// <returns>格式化后的大小文本。</returns>
        private static string FormatSize(long bytes)
        {
            const long oneKilo = 1024L;
            const long oneMega = oneKilo * 1024L;
            const long oneGiga = oneMega * 1024L;

            if (bytes >= oneGiga)
            {
                return $"{(double)bytes / oneGiga:0.00} GB";
            }

            if (bytes >= oneMega)
            {
                return $"{(double)bytes / oneMega:0.0} MB";
            }

            if (bytes >= oneKilo)
            {
                return $"{(double)bytes / oneKilo:0.0} KB";
            }

            return $"{bytes} B";
        }

        /// <summary>
        /// 将构建耗时格式化为人类可读文本。
        /// </summary>
        /// <param name="duration">构建耗时。</param>
        /// <returns>格式化后的耗时文本。</returns>
        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes >= 1.0)
            {
                return $"{duration.TotalMinutes:0.0} 分钟";
            }

            return $"{duration.TotalSeconds:0} 秒";
        }
    }
}
