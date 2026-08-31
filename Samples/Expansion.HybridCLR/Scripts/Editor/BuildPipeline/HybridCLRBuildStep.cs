using System;
using System.Collections.Generic;
using HybridCLR.Editor;
using HybridCLR.Editor.Settings;
using RFramework;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// HybridCLR Player 构建准备步骤。启用 hybridclr 后由 Player 与 Release Recipe
    /// 自动加入，在 Player 构建前执行官方 Generate/All，不单独暴露步骤条目或配置。
    /// </summary>
    public sealed class HybridCLRPlayerPrepareStep : BuildPipelineStepBase,
        IAutomaticBuildPipelineStep
    {
        private const string HybridClrStepId = "hybridclr";

        public override string Id => "hybridclr.prepare-player";

        public override string DisplayName => "HybridCLR Player 准备";

        public override BuildPipelineStage Stage => BuildPipelineStage.PreparePlayer;

        public override int Order => 24;

        public override bool TriggersCompilation => true;

        public bool ShouldInclude(
            UnityRFrameworkBuildProfile profile,
            BuildRecipe recipe)
        {
            return profile != null
                && (recipe == BuildRecipe.Player
                    || recipe == BuildRecipe.Release)
                && BuildStepConfigLocator.HasEnabledEntry(profile, HybridClrStepId);
        }

        public override bool CanRun(BuildPipelineContext context)
        {
            return context != null
                && ShouldInclude(context.Profile, context.Recipe);
        }

        public override void Validate(
            BuildPipelineContext context,
            ICollection<BuildValidationIssue> issues)
        {
            // Release 还会选择热更发布步骤，由发布步骤统一报告插件配置问题，
            // 避免同一校验结果在窗口中重复出现。
            if (context != null && context.Recipe == BuildRecipe.Player)
            {
                HybridCLRBuildValidation.ValidatePluginSettings(issues);
            }
        }

        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            try
            {
                HybridCLRArtifactBuilder.ConfigureAndValidate();
                HybridCLRArtifactBuilder.GenerateCurrentTarget();
                return BuildStepResult.Succeeded(
                    $"HybridCLR Player 代码产物已生成，目标：{context.Target}。");
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"HybridCLR Player 准备失败：{exception.Message}",
                    exception);
            }
        }
    }

    /// <summary>
    /// HybridCLR 热更发布步骤：编译热更程序集并暂存到资源目录。
    /// 复用 <see cref="HybridCLRArtifactBuilder"/> 与 <see cref="HybridCLRPlayerBaseline"/>，
    /// 不复制第三方逻辑。仅当 Profile 配置并启用了 hybridclr 步骤条目时参与构建；
    /// 未配置条目时跳过，避免导入 Expansion.HybridCLR 但未配置 Profile 时意外执行。
    /// HotUpdate Recipe 使用最近一次 Player 基线；Release Recipe 则先由自动准备步骤生成代码
    /// 并构建 Player，再由本步骤基于新基线整理热更产物。
    /// </summary>
    public sealed class HybridCLRBuildStep : BuildPipelineStepBase
    {
        /// <summary>错误码：HybridCLR 热更准备。</summary>
        private const string StepCode = "HYBRIDCLR";

        /// <summary>校验分组：构建步骤。</summary>
        private const string StepGroup = "构建步骤";

        /// <summary>获取步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "hybridclr";
            }
        }

        /// <summary>获取步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "HybridCLR 热更准备";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.PrepareCode;

        public override Type ConfigurationType =>
            typeof(HybridClrBuildConfiguration);

        /// <summary>获取步骤排序值：位于应用参数之后、YooAsset 打包之前。</summary>
        public override int Order
        {
            get
            {
                return 24;
            }
        }

        /// <summary>获取是否触发脚本编译：生成桥接代码与编译热更程序集都会触发编译。</summary>
        public override bool TriggersCompilation
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 判断步骤是否可用于当前构建上下文：仅当 Profile 配置并启用了
        /// hybridclr 步骤条目时可用。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>条目已启用时返回 true。</returns>
        public override bool CanRun(BuildPipelineContext context)
        {
            if (context == null || context.Profile == null)
            {
                return false;
            }

            return BuildStepConfigLocator.HasEnabledEntry(
                context.Profile,
                Id);
        }

        /// <summary>
        /// 执行前置校验：只读检查步骤配置完整性，不生成任何代码。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        public override void Validate(
            BuildPipelineContext context,
            ICollection<BuildValidationIssue> issues)
        {
            if (context == null || context.Profile == null || issues == null)
            {
                return;
            }

            HybridClrBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<HybridClrBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 步骤未绑定 HybridClrBuildConfiguration 配置资产。",
                    StepGroup));
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.EntryTypeName))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 步骤配置缺少 EntryTypeName（热更新入口类型全名）。",
                    StepGroup));
            }

            if (string.IsNullOrWhiteSpace(settings.OutputAssetRoot)
                || (!settings.OutputAssetRoot.Equals(
                        "Assets",
                        StringComparison.Ordinal)
                    && !settings.OutputAssetRoot.StartsWith(
                        "Assets/",
                        StringComparison.Ordinal)))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 步骤配置的 OutputAssetRoot 必须以 Assets/ 开头，"
                    + "当前值：" + settings.OutputAssetRoot + "。",
                    StepGroup));
            }

            HybridCLRBuildValidation.ValidatePluginSettings(issues);
        }

        /// <summary>
        /// 执行 HybridCLR 热更发布：编译热更程序集并暂存产物，包含 AOT 基线校验。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>成功返回生成结果；配置缺失或执行失败返回失败结果。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            HybridClrBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<HybridClrBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                return BuildStepResult.Failed(
                    "HybridCLR 步骤未绑定 HybridClrBuildConfiguration 配置资产。",
                    null);
            }

            if (string.IsNullOrWhiteSpace(settings.EntryTypeName))
            {
                return BuildStepResult.Failed(
                    "HybridCLR 步骤配置缺少 EntryTypeName（热更新入口类型全名）。",
                    null);
            }

            try
            {
                // 校验并启用 HybridCLR 设置；要求已配置热更与 AOT 程序集。
                HybridCLRArtifactBuilder.ConfigureAndValidate();

                string codeVersion = string.IsNullOrWhiteSpace(settings.CodeVersion)
                    ? DateTime.Now.ToString("yyyy-MM-dd-HHmmss")
                    : settings.CodeVersion.Trim();

                // 程序集清单以 HybridCLRSettings 为唯一事实源，不在步骤配置中
                // 维护平行副本；缺失时直接失败并引导到插件设置窗口。
                string[] hotUpdateAssemblies = SettingsUtil
                    .HotUpdateAssemblyNamesExcludePreserved
                    .ToArray();
                if (hotUpdateAssemblies.Length == 0)
                {
                    throw new RFrameworkException(
                        "HybridCLR 未配置热更新程序集（hotUpdateAssemblyDefinitions），"
                        + "请在 HybridCLR Settings 中配置。");
                }

                string[] patchAotAssemblies =
                    HybridCLRSettings.Instance.patchAOTAssemblies;
                if (patchAotAssemblies == null || patchAotAssemblies.Length == 0)
                {
                    throw new RFrameworkException(
                        "HybridCLR 未配置 AOT 补充元数据程序集（patchAOTAssemblies），"
                        + "请在 HybridCLR Settings 中配置。");
                }

                string targetName = HybridCLRArtifactBuilder.CompileAndStage(
                    settings.OutputAssetRoot,
                    settings.EntryTypeName,
                    codeVersion,
                    hotUpdateAssemblies,
                    patchAotAssemblies,
                    settings.IncludePdb);
                return BuildStepResult.Succeeded(
                    $"HybridCLR 热更产物已生成：代码版本 {codeVersion}，"
                    + $"目标 {targetName}，输出 {settings.OutputAssetRoot}。");
            }
            catch (RFrameworkException exception)
            {
                return BuildStepResult.Failed(
                    $"HybridCLR 准备失败：{exception.Message}",
                    exception);
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"HybridCLR 准备失败：{exception.Message}",
                    exception);
            }
        }

    }

    internal static class HybridCLRBuildValidation
    {
        private const string StepCode = "HYBRIDCLR";
        private const string StepGroup = "构建步骤";

        public static void ValidatePluginSettings(
            ICollection<BuildValidationIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            if (SettingsUtil.HotUpdateAssemblyNamesExcludePreserved.Count == 0)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 未配置热更新程序集（hotUpdateAssemblyDefinitions），"
                    + "请在 HybridCLR Settings 中配置。",
                    StepGroup));
            }

            string[] patchAotAssemblies =
                HybridCLRSettings.Instance.patchAOTAssemblies;
            if (patchAotAssemblies == null || patchAotAssemblies.Length == 0)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "HybridCLR 未配置 AOT 补充元数据程序集（patchAOTAssemblies），"
                    + "请在 HybridCLR Settings 中配置。",
                    StepGroup));
            }
        }
    }
}
