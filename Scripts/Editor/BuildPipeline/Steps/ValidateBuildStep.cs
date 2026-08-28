using System.Collections.Generic;
using System.Text;
using UnityEditor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建前校验步骤：执行阶段 3 主校验器并追加调试选项互斥检查。
    /// 排序值最小，作为流水线第一道闸门，在平台切换前阻止错误配置。
    /// 本步骤只读，不修改 Profile、PlayerSettings、场景或输出目录。
    /// </summary>
    public sealed class ValidateBuildStep : BuildPipelineStepBase
    {
        /// <summary>步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "core.validate";
            }
        }

        /// <summary>步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "构建前校验";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.Validate;

        /// <summary>步骤排序值，最先执行。</summary>
        public override int Order
        {
            get
            {
                return 5;
            }
        }

        /// <summary>本步骤仅在校验配置存在时可用。</summary>
        public override bool CanRun(BuildPipelineContext context)
        {
            return context != null && context.Profile != null;
        }

        /// <summary>
        /// 执行构建前校验；存在 Error 级问题时立即失败，不进入任何写入型步骤。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>校验通过返回成功，否则返回失败并携带全部错误描述。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return BuildStepResult.Cancelled(
                    "构建前校验已取消。");
            }

            BuildValidationResult validation =
                BuildProfileValidator.Validate(context.Profile);
            if (!validation.CanBuild)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(
                    $"构建前校验未通过（{validation.Errors.Count} 个错误）：");
                for (int i = 0; i < validation.Errors.Count; i++)
                {
                    builder.AppendLine();
                    builder.Append("- ");
                    builder.Append(validation.Errors[i].Message);
                }

                return BuildStepResult.Failed(builder.ToString(), null);
            }

            BuildValidationIssue? optionIssue =
                BuildPlayerOptionsFactory.ValidateDevelopmentOptions(
                    context.Profile);
            if (optionIssue.HasValue)
            {
                return BuildStepResult.Failed(
                    $"构建前校验未通过：{optionIssue.Value.Message}",
                    null);
            }

            return BuildStepResult.Succeeded(
                $"构建前校验通过（警告 {validation.Warnings.Count} 条）。");
        }
    }
}
