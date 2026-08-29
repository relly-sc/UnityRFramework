using System;
using System.IO;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建收尾步骤：验证构建产物真实存在并输出产物信息。
    /// 构建命令使用临时设置事务，任务结束后由运行器按快照恢复"应用参数"
    /// 写入的临时设置（活动构建平台按契约保留）；本步骤不修改 Profile 资产，
    /// 天然幂等，恢复重跑不会产生副作用。
    /// </summary>
    public sealed class FinalizeBuildStep : BuildPipelineStepBase
    {
        /// <summary>步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "core.finalize";
            }
        }

        /// <summary>步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "构建收尾";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.Finalize;

        /// <summary>步骤排序值，最后执行。</summary>
        public override int Order
        {
            get
            {
                return 40;
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
        /// 验证产物存在性并输出产物信息；iOS 校验 Xcode 工程目录，
        /// Windows/Android 校验产物文件。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>产物存在返回成功，缺失返回失败。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return BuildStepResult.Cancelled(
                    "构建收尾已取消。");
            }

            string productPath;
            try
            {
                productPath = BuildPlayerOptionsFactory.ResolveLocationPath(
                    context);
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"解析产物路径失败：{exception.Message}",
                    exception);
            }

            if (context.Target == UnityEditor.BuildTarget.iOS)
            {
                if (!Directory.Exists(productPath))
                {
                    return BuildStepResult.Failed(
                        $"iOS Xcode 工程目录不存在：{productPath}",
                        null);
                }

                return BuildStepResult.Succeeded(
                    $"构建收尾：iOS Xcode 工程目录已就绪：{productPath}。"
                    + "最终签名请在 macOS/Xcode 中完成。"
                    + "临时构建设置将在任务结束后按快照恢复（活动平台保留）。");
            }

            if (!File.Exists(productPath))
            {
                return BuildStepResult.Failed(
                    $"构建产物不存在：{productPath}",
                    null);
            }

            FileInfo info = new FileInfo(productPath);
            return BuildStepResult.Succeeded(
                $"构建收尾：产物已就绪：{productPath}"
                + $"（{FormatSize(info.Length)}）。"
                + "临时构建设置将在任务结束后按快照恢复（活动平台保留）。");
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
    }
}
