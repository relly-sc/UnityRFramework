using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Obfuz.Settings;
using Obfuz.Unity;
using UnityEditor;

namespace UnityRFramework.Editor
{
    /// <summary>普通 Player 的 Obfuz 接入入口；热更联合构建由可选扩展提供。</summary>
    public sealed class ObfuzBuildStep : BuildPipelineStepBase, IBuildIntegrationActivationController
    {
        private const string ResultKey = "UnityRFramework.Obfuz.PlayerResult";
        public override string Id => "obfuz";
        public override string DisplayName => "Obfuz 代码混淆";
        public override BuildPipelineStage Stage => BuildPipelineStage.PrepareCode;
        public override Type ConfigurationType => typeof(ObfuzBuildConfiguration);
        public override int Order => 26;
        string IBuildIntegrationActivationController.StepId => Id;
        bool IBuildIntegrationActivationController.IsEnabled => ObfuzSettings.Instance.buildPipelineSettings.enable;

        static ObfuzBuildStep()
        {
            ObfuscationProcess.OnObfuscationBegin += _ => ResetPlayerResult();
            ObfuscationProcess.OnObfuscationEnd += result => RecordPlayerResult(result.success);
        }

        void IBuildIntegrationActivationController.SetEnabled(bool enabled)
        {
            if (ObfuzSettings.Instance.buildPipelineSettings.enable == enabled) return;
            ObfuzSettings.Instance.buildPipelineSettings.enable = enabled;
            ObfuzSettings.Save();
        }

        internal static void ResetPlayerResult() => SessionState.SetString(ResultKey, "Pending");

        internal static void RecordPlayerResult(bool success)
        {
            SessionState.SetString(ResultKey, success ? "Succeeded" : "Failed");
        }

        internal static bool PlayerSucceeded => SessionState.GetString(ResultKey, string.Empty) == "Succeeded";

        public override bool CanRun(BuildPipelineContext context) =>
            context != null && BuildStepConfigLocator.HasEnabledEntry(context.Profile, Id);

        public override void Validate(BuildPipelineContext context, ICollection<BuildValidationIssue> issues)
        {
            if (context == null || issues == null || !BuildStepConfigLocator.HasEnabledEntry(context.Profile, Id)) return;
            if (BuildStepConfigLocator.GetConfiguration<ObfuzBuildConfiguration>(context.Profile, Id) == null)
                issues.Add(BuildValidationIssue.Error("OBFUZ", "Obfuz 步骤未绑定配置资产。", "构建步骤"));
            var settings = ObfuzSettings.Instance;
            if (!settings.buildPipelineSettings.enable)
                issues.Add(BuildValidationIssue.Error("OBFUZ", "已启用 Obfuz 步骤，但 Obfuz Settings 中的构建混淆开关未开启。", "构建步骤"));
            if (string.IsNullOrWhiteSpace(settings.encryptionVMSettings.codeOutputPath)
                || !File.Exists(settings.encryptionVMSettings.codeOutputPath))
                issues.Add(BuildValidationIssue.Error("OBFUZ", "请先通过 Obfuz/GenerateVm 生成加密虚拟机并等待编译完成。", "构建步骤"));
            if (!File.Exists("Assets/Resources/Obfuz/defaultStaticSecretKey.bytes"))
                issues.Add(BuildValidationIssue.Error("OBFUZ", "运行时静态密钥缺失，请生成 Resources/Obfuz/defaultStaticSecretKey.bytes。", "构建步骤"));

            bool hybrid = BuildStepConfigLocator.HasEnabledEntry(context.Profile, "hybridclr");
            if (hybrid && !BuildPipelineStepRegistry.GetAll().Any(step => step.Id == "obfuz.hotupdate"))
                issues.Add(BuildValidationIssue.Error("OBFUZ", "HybridCLR 联合混淆还需导入 Expansion.Obfuz.HybridCLR。", "构建步骤"));
            if (!hybrid && context.Recipe == BuildRecipe.HotUpdate)
                issues.Add(BuildValidationIssue.Error("OBFUZ", "普通 Player 混淆不适用 HotUpdate，请使用 Player 或 Release。", "构建步骤"));
        }

        public override BuildStepResult Execute(BuildPipelineContext context) =>
            BuildStepResult.Succeeded("Obfuz 构建回调已接入；普通 Player 在 Unity 构建期间混淆，热更程序集由可选联合扩展处理。");
    }

    /// <summary>Player 构建前检查 Obfuz，并为本次任务清空混淆结果。</summary>
    public sealed class ObfuzPlayerValidationStep : BuildPipelineStepBase, IAutomaticBuildPipelineStep
    {
        public override string Id => "obfuz.validate-player";
        public override string DisplayName => "检查 Player 混淆";
        public override BuildPipelineStage Stage => BuildPipelineStage.PreparePlayer;
        public override int Order => 24;
        public bool ShouldInclude(UnityRFrameworkBuildProfile profile, BuildRecipe recipe) =>
            (recipe == BuildRecipe.Player || recipe == BuildRecipe.Release)
            && BuildStepConfigLocator.HasEnabledEntry(profile, "obfuz");
        public override bool CanRun(BuildPipelineContext context) => context != null && ShouldInclude(context.Profile, context.Recipe);
        public override void Validate(BuildPipelineContext context, ICollection<BuildValidationIssue> issues) =>
            new ObfuzBuildStep().Validate(context, issues);
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            ObfuzBuildStep.ResetPlayerResult();
            return BuildStepResult.Succeeded("等待 Unity Player 构建回调执行 Obfuz 混淆。");
        }
    }

    /// <summary>原生构建结束后确认混淆回调成功，避免插件内部捕获异常后误报成功。</summary>
    public sealed class ObfuzPlayerFinalizeStep : BuildPipelineStepBase, IAutomaticBuildPipelineStep
    {
        public override string Id => "obfuz.finalize-player";
        public override string DisplayName => "确认 Player 混淆结果";
        public override BuildPipelineStage Stage => BuildPipelineStage.Finalize;
        public override int Order => 35;
        public override IReadOnlyList<string> Dependencies => new[] { "core.build-player" };
        public bool ShouldInclude(UnityRFrameworkBuildProfile profile, BuildRecipe recipe) =>
            (recipe == BuildRecipe.Player || recipe == BuildRecipe.Release)
            && BuildStepConfigLocator.HasEnabledEntry(profile, "obfuz");
        public override bool CanRun(BuildPipelineContext context) => context != null && ShouldInclude(context.Profile, context.Recipe);
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            if (!ObfuzBuildStep.PlayerSucceeded)
                return BuildStepResult.Failed("未收到本次 Player 的 Obfuz 混淆成功结果，请检查 Obfuz 构建日志。", null);
            try
            {
                string mapping = ObfuzMappingArchive.Archive(context);
                return BuildStepResult.Succeeded("Player 混淆已确认成功。" + (mapping == null ? string.Empty : " 映射已归档到 " + mapping));
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed("Obfuz 符号映射归档失败：" + exception.Message, exception);
            }
        }
    }

    /// <summary>
    /// 将 Obfuz 符号映射归档到 Bundles 外部目录，避免跟随 Player 或 UPM 包发布。
    /// </summary>
    internal static class ObfuzMappingArchive
    {
        private const string ArchiveRoot = "Bundles/ObfuzMappings";

        /// <summary>
        /// 归档当前 Obfuz 符号映射；未启用符号混淆或文件尚未生成时返回 null。
        /// </summary>
        /// <param name="context">当前构建上下文。</param>
        /// <returns>归档后的项目相对路径；无映射文件时返回 null。</returns>
        internal static string Archive(BuildPipelineContext context)
        {
            if (context == null || ObfuzSettings.Instance == null
                || ObfuzSettings.Instance.symbolObfusSettings == null)
            {
                return null;
            }

            string configuredPath = ObfuzSettings.Instance.symbolObfusSettings
                .GetSymbolMappingFile();
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return null;
            }

            string sourcePath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(context.ProjectRoot, configuredPath);
            sourcePath = Path.GetFullPath(sourcePath);
            if (!File.Exists(sourcePath))
            {
                return null;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            string folder = Path.Combine(
                context.ProjectRoot,
                ArchiveRoot,
                context.Target.ToString(),
                context.Profile.Platform.PublicVersion + "-"
                    + context.Profile.Platform.BuildNumber,
                stamp);
            Directory.CreateDirectory(folder);
            string destination = Path.Combine(folder, "symbol-mapping.xml");
            File.Copy(sourcePath, destination, false);
            return NormalizeProjectPath(
                Path.Combine(
                    ArchiveRoot,
                    context.Target.ToString(),
                    context.Profile.Platform.PublicVersion + "-"
                        + context.Profile.Platform.BuildNumber,
                    stamp,
                    "symbol-mapping.xml"));
        }

        private static string NormalizeProjectPath(string path)
        {
            return path.Replace('\\', '/');
        }
    }

}
