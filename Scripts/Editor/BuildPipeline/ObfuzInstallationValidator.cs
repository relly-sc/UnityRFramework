using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace UnityRFramework.Editor
{
    /// <summary>插件未导入框架扩展时也能提前诊断；不引用第三方程序集或修改其配置。</summary>
    public sealed class ObfuzInstallationValidator : IBuildValidator
    {
        public string Id => "core.obfuz-installation";

        public void Validate(BuildValidationContext context, ICollection<BuildValidationIssue> issues)
        {
            if (context == null || issues == null) return;
            Type settingsType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Obfuz.Settings.ObfuzSettings", false)).FirstOrDefault(t => t != null);
            ValidateInstallation(context, issues, settingsType,
                BuildPipelineStepRegistry.GetAll().Any(s => s.Id == "obfuz"));
        }

        internal static void ValidateInstallation(BuildValidationContext context,
            ICollection<BuildValidationIssue> issues, Type settingsType, bool extensionAvailable)
        {
            if (settingsType == null || (context.Recipe != BuildRecipe.Player && context.Recipe != BuildRecipe.Release)) return;
            const string group = "第三方接入";
            string guidance = "请在 Edit > Project Settings > Obfuz 完成插件配置初始化并保存。需要框架管理混淆时，通过 UnityRFramework/安装扩展与示例 导入 Expansion.Obfuz；暂不接入时在 Obfuz 设置中关闭构建混淆。预检不会自动创建配置或改动开关。";
            if (!File.Exists(Path.Combine(context.ProjectRoot, "ProjectSettings", "Obfuz.asset")))
            {
                issues.Add(BuildValidationIssue.Error("OBFUZ_INIT", "已安装 Obfuz，但缺少 ProjectSettings/Obfuz.asset。即使 Profile 未启用混淆，插件仍有全局构建回调。" + guidance, group));
                return;
            }

            try
            {
                object settings = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                string[] required = { "buildPipelineSettings", "assemblySettings", "encryptionVMSettings", "symbolObfusSettings" };
                foreach (string name in required)
                    if (settings == null || settingsType.GetField(name)?.GetValue(settings) == null)
                    {
                        issues.Add(BuildValidationIssue.Error("OBFUZ_INIT", "Obfuz 配置子对象未初始化或当前插件 API 不受支持：" + name + "。" + guidance, group));
                        return;
                    }
                object pipeline = settingsType.GetField("buildPipelineSettings").GetValue(settings);
                object enabled = pipeline.GetType().GetField("enable")?.GetValue(pipeline);
                if (!(enabled is bool))
                    throw new InvalidOperationException("无法读取 Obfuz buildPipelineSettings.enable。");
                if (!extensionAvailable)
                {
                    string message = "仅安装了 Obfuz 插件，尚未导入 Expansion.Obfuz；框架无法隔离插件的全局构建回调。";
                    issues.Add((bool)enabled || BuildStepConfigLocator.HasEnabledEntry(context.Profile, "obfuz")
                        ? BuildValidationIssue.Error("OBFUZ_EXTENSION", message + guidance, group)
                        : BuildValidationIssue.Warning("OBFUZ_EXTENSION", message + "当前插件构建混淆已关闭，可构建不混淆的基础包。", group));
                }
            }
            catch (Exception error)
            {
                issues.Add(BuildValidationIssue.Error("OBFUZ_INIT", "无法安全检查 Obfuz 配置：" + error.GetBaseException().Message + "。" + guidance, group));
            }
        }
    }
}
