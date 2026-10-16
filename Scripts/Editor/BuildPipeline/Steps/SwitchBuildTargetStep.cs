using UnityEditor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 切换目标平台步骤：将活动构建目标切换到 Profile 指定平台。
    /// 目标平台已是当前平台时直接成功（幂等）；切换可能触发脚本编译与
    /// Domain Reload，恢复机制会在此步骤完成后从下一检查点继续。
    /// </summary>
    public sealed class SwitchBuildTargetStep : BuildPipelineStepBase
    {
        /// <summary>步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "core.switch-target";
            }
        }

        /// <summary>步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "切换目标平台";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.SwitchTarget;

        /// <summary>步骤排序值，位于校验之后、应用参数之前。</summary>
        public override int Order
        {
            get
            {
                return 10;
            }
        }

        /// <summary>切换平台会修改活动构建目标。</summary>
        public override bool SwitchesTarget
        {
            get
            {
                return true;
            }
        }

        /// <summary>平台切换可能触发脚本编译。</summary>
        public override bool TriggersCompilation
        {
            get
            {
                return true;
            }
        }

        /// <summary>平台切换可能触发 Domain Reload。</summary>
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
        /// 执行平台切换；目标平台与当前一致时直接成功。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>切换成功或无需切换返回成功，切换失败返回失败。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return BuildStepResult.Cancelled(
                    "平台切换已取消。");
            }

            BuildTarget target = context.Target;
            BuildTarget current = EditorUserBuildSettings.activeBuildTarget;
            if (current == target)
            {
                return BuildStepResult.Succeeded(
                    $"目标平台已是 {target}，无需切换。");
            }

            BuildTargetGroup targetGroup =
                UnityEditor.BuildPipeline.GetBuildTargetGroup(target);
            bool switched =
                EditorUserBuildSettings.SwitchActiveBuildTarget(
                    targetGroup,
                    target);
            if (!switched)
            {
                return BuildStepResult.Failed(
                    $"切换到目标平台 '{target}' 失败，请检查平台模块是否已安装。",
                    null);
            }

            return BuildStepResult.Succeeded(
                $"目标平台：{current} → {target}。");
        }
    }
}
