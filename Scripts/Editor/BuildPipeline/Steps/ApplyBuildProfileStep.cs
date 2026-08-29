using System.Text;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 应用构建参数步骤：将 Profile 平台参数写入 PlayerSettings 与
    /// EditorBuildSettings。复用阶段 3 应用器，应用前自动执行构建前校验，
    /// 校验不过时短路失败且不改变任何设置。
    /// </summary>
    public sealed class ApplyBuildProfileStep : BuildPipelineStepBase
    {
        /// <summary>步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "core.apply-profile";
            }
        }

        /// <summary>步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "应用构建参数";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.ApplySettings;

        /// <summary>步骤排序值，位于平台切换之后。</summary>
        public override int Order
        {
            get
            {
                return 20;
            }
        }

        /// <summary>应用器内部可能发生平台切换（与当前平台不一致时）。</summary>
        public override bool SwitchesTarget
        {
            get
            {
                return true;
            }
        }

        /// <summary>脚本宏变更会触发脚本编译。</summary>
        public override bool TriggersCompilation
        {
            get
            {
                return true;
            }
        }

        /// <summary>宏与编译设置变更可能触发 Domain Reload。</summary>
        public override bool TriggersDomainReload
        {
            get
            {
                return true;
            }
        }

        /// <summary>本步骤仅在校验配置存在时可用。</summary>
        public override bool CanRun(BuildPipelineContext context)
        {
            return context != null && context.Profile != null;
        }

        /// <summary>
        /// 应用 Profile 全部参数到编辑器设置。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>应用成功返回成功并附脱敏摘要，失败返回全部错误描述。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return BuildStepResult.Cancelled(
                    "应用构建参数已取消。");
            }

            // 构建命令使用临时设置事务：标记事务生效，
            // 任务结束（成功、失败、取消）时由运行器按快照恢复。
            // "应用到项目"永久生效路径不经过本步骤。
            context.SettingsTransaction?.MarkApplied();

            BuildApplyResult result =
                BuildProfileApplier.Apply(context.Profile);
            if (result.Succeeded)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(
                    $"已应用构建参数（{result.ReportLines.Count} 条，"
                    + $"平台切换：{(result.PlatformSwitched ? "是" : "否")}）。");
                for (int i = 0; i < result.ReportLines.Count; i++)
                {
                    builder.AppendLine();
                    builder.Append("- ");
                    builder.Append(result.ReportLines[i]);
                }

                return BuildStepResult.Succeeded(builder.ToString());
            }

            StringBuilder errorBuilder = new StringBuilder();
            errorBuilder.Append("应用构建参数失败：");
            for (int i = 0; i < result.Errors.Count; i++)
            {
                errorBuilder.AppendLine();
                errorBuilder.Append("- ");
                errorBuilder.Append(result.Errors[i]);
            }

            return BuildStepResult.Failed(errorBuilder.ToString(), null);
        }
    }
}
