using System;
using System.Collections.Generic;
using System.IO;
using HybridCLR.Editor;
using Obfuz.Settings;
using Obfuz4HybridCLR;
using Obfuz.ObfusPasses;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Obfuz 热更混淆步骤：对 HybridCLR 步骤编译出的热更程序集执行代码混淆，
    /// 并基于混淆后的程序集重生成 MethodBridge 与 AOT 泛型引用，最后将混淆产物
    /// 同名覆盖 HybridCLR 步骤暂存的热更资源目录，供 YooAsset 收集打包。
    /// 复用 Obfuz4HybridCLR 扩展包的 ObfuscateUtil 与 PrebuildCommandExt，
    /// 不复制第三方逻辑，也不重复编译热更程序集（编译由 hybridclr 步骤完成）。
    /// 门控：仅当 Profile 启用了 obfuz 步骤条目，且 hybridclr 步骤条目已启用并
    /// 配置输出目录时参与构建，避免导入扩展包但未配置 Profile 时意外执行。
    /// 自动跳过：HybridCLR 步骤以 GenerateOnly（首次基线）模式运行时未暂存热更
    /// 程序集，本步骤检测到暂存目录无热更 DLL 时直接成功跳过，不报错。
    /// 混淆清单在 Obfuz Settings（第三方窗口）中手动配置，须同时包含 AOT 与
    /// 热更程序集名称；本步骤不侵入第三方设置。
    /// </summary>
    public sealed class ObfuzBuildStep : BuildPipelineStepBase, IBuildStepInspector
    {
        /// <summary>错误码：Obfuz 热更混淆。</summary>
        private const string StepCode = "OBFUZ";

        /// <summary>校验分组：构建步骤。</summary>
        private const string StepGroup = "构建步骤";

        /// <summary>依赖的 HybridCLR 步骤 Id。</summary>
        private const string HybridClrStepId = "hybridclr";

        /// <summary>获取步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "obfuz";
            }
        }

        /// <summary>获取步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "Obfuz 热更混淆";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.PrepareCode;

        public override IReadOnlyList<string> Dependencies =>
            new[] { HybridClrStepId };

        public override Type ConfigurationType =>
            typeof(ObfuzBuildConfiguration);

        /// <summary>获取步骤排序值：位于 HybridCLR 之后、YooAsset 打包之前。</summary>
        public override int Order
        {
            get
            {
                return 26;
            }
        }

        /// <summary>获取是否触发脚本编译：重生成 AOTGenericReferences 等代码会触发编译。</summary>
        public override bool TriggersCompilation
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 判断步骤是否可用于当前构建上下文：Profile 启用了 obfuz 条目，
        /// 且依赖的 hybridclr 条目已启用并配置输出目录时返回 true。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>门控条件全部满足时返回 true。</returns>
        public override bool CanRun(BuildPipelineContext context)
        {
            if (context == null || context.Profile == null)
            {
                return false;
            }

            if (!BuildStepConfigLocator.HasEnabledEntry(context.Profile, Id))
            {
                return false;
            }

            if (!BuildStepConfigLocator.HasEnabledEntry(
                context.Profile,
                HybridClrStepId))
            {
                return false;
            }

            HybridClrBuildConfiguration hybridSettings =
                BuildStepConfigLocator.GetConfiguration<HybridClrBuildConfiguration>(
                    context.Profile,
                    HybridClrStepId);
            return hybridSettings != null
                && !string.IsNullOrWhiteSpace(hybridSettings.OutputAssetRoot);
        }

        /// <summary>
        /// 执行前置校验：只读检查 Obfuz 设置与依赖配置完整性，不执行任何混淆。
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

            if (!BuildStepConfigLocator.HasEnabledEntry(context.Profile, Id))
            {
                return;
            }

            HybridClrBuildConfiguration hybridSettings =
                BuildStepConfigLocator.GetConfiguration<HybridClrBuildConfiguration>(
                    context.Profile,
                    HybridClrStepId);
            if (hybridSettings == null
                || string.IsNullOrWhiteSpace(hybridSettings.OutputAssetRoot))
            {
                issues.Add(BuildValidationIssue.Warning(
                    StepCode,
                    "Profile 启用了 obfuz 步骤但 hybridclr 步骤未配置输出目录，"
                    + "热更混淆不会执行。",
                    StepGroup));
                return;
            }

            ObfuzSettings obfuzSettings = ObfuzSettings.Instance;
            if (obfuzSettings == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "ObfuzSettings 实例缺失，请确认 Obfuz 包已正确导入。",
                    StepGroup));
                return;
            }

            ObfuzBuildConfiguration configuration =
                BuildStepConfigLocator.GetConfiguration<ObfuzBuildConfiguration>(
                    context.Profile,
                    Id);
            if (configuration == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "Obfuz 步骤未绑定 ObfuzBuildConfiguration 配置资产。",
                    StepGroup));
                return;
            }

            if (!configuration.Enable)
            {
                return;
            }

            string encryptionVmPath =
                obfuzSettings.encryptionVMSettings.codeOutputPath;
            if (string.IsNullOrWhiteSpace(encryptionVmPath)
                || !File.Exists(encryptionVmPath))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    $"Obfuz 加密虚拟机源码不存在：{encryptionVmPath}。"
                    + "请先执行 Obfuz/GenerateVm，等待 Unity 编译完成后再构建 Player。",
                    StepGroup));
            }

            if (!obfuzSettings.buildPipelineSettings.enable)
            {
                issues.Add(BuildValidationIssue.Warning(
                    StepCode,
                    "Obfuz 构建设置已关闭（buildPipelineSettings.enable=false），"
                    + "AOT 自动混淆与热更混淆都不会执行。",
                    StepGroup));
            }

            if (!ContainsHotUpdateAssembly(obfuzSettings))
            {
                issues.Add(BuildValidationIssue.Warning(
                    StepCode,
                    "Obfuz 混淆清单（assembliesToObfuscate）不包含任何热更程序集，"
                    + "热更混淆不会生效，请将热更程序集名称加入清单。",
                    StepGroup));
            }

            if (!HasAotBaseline(context.Target))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "AOT 基线缺失（AssembliesPostIl2CppStrip 为空），请先运行 "
                    + "HybridCLR/Generate/All 建立基线后再执行热更混淆。",
                    StepGroup));
            }
        }

        /// <summary>
        /// 执行热更混淆：混淆 HybridCLR 步骤编译出的热更程序集，基于混淆后程序集
        /// 重生成 MethodBridge 与 AOT 泛型引用，并将混淆产物同名覆盖暂存目录。
        /// 暂存目录无热更 DLL（GenerateOnly 基线场景）时直接成功跳过。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>成功返回混淆结果；异常返回失败结果。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            ObfuzBuildConfiguration configuration =
                BuildStepConfigLocator.GetConfiguration<ObfuzBuildConfiguration>(
                    context.Profile,
                    Id);
            if (configuration == null)
            {
                return BuildStepResult.Failed(
                    "Obfuz 步骤未绑定 ObfuzBuildConfiguration 配置资产。",
                    null);
            }

            SyncProfileToObfuzSettings(configuration);
            if (!configuration.Enable)
            {
                return BuildStepResult.Succeeded(
                    "Obfuz 已按 Profile 配置关闭，本次跳过混淆。");
            }

            HybridClrBuildConfiguration hybridSettings =
                BuildStepConfigLocator.GetConfiguration<HybridClrBuildConfiguration>(
                    context.Profile,
                    HybridClrStepId);
            if (hybridSettings == null)
            {
                return BuildStepResult.Failed(
                    "Obfuz 依赖的 HybridCLR 配置资产缺失。",
                    null);
            }
            string stagedDir = GetStagedAssembliesDir(context, hybridSettings);
            if (!Directory.Exists(stagedDir)
                || Directory.GetFiles(stagedDir, "*.dll.bytes").Length == 0)
            {
                return BuildStepResult.Succeeded(
                    $"Obfuz 热更混淆跳过：暂存目录 {stagedDir} 无热更 DLL，"
                    + "当前为 GenerateOnly 首次基线或 HybridCLR 步骤尚未执行。");
            }

            try
            {
                string obfuscatedDir = GetObfuscatedOutputDir(context);
                if (Directory.Exists(obfuscatedDir))
                {
                    Directory.Delete(obfuscatedDir, true);
                }

                Directory.CreateDirectory(obfuscatedDir);

                ObfuscateUtil.ObfuscateHotUpdateAssemblies(
                    context.Target,
                    obfuscatedDir);

                PrebuildCommandExt.GenerateMethodBridgeAndReversePInvokeWrapper(
                    context.Target,
                    obfuscatedDir);

                PrebuildCommandExt.GenerateAOTGenericReference(
                    context.Target,
                    obfuscatedDir);

                int deployed = DeployObfuscatedAssemblies(
                    obfuscatedDir,
                    stagedDir);

                AssetDatabase.Refresh();
                return BuildStepResult.Succeeded(
                    $"Obfuz 热更混淆完成：{deployed} 个程序集已覆盖到 {stagedDir}，"
                    + "MethodBridge 与 AOT 泛型引用已基于混淆后程序集重生成。");
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"Obfuz 热更混淆失败：{exception.Message}",
                    exception);
            }
        }

        /// <summary>
        /// 计算 HybridCLR 步骤暂存的热更程序集磁盘目录。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <param name="hybridSettings">HybridCLR 内嵌配置。</param>
        /// <returns>暂存目录绝对路径。</returns>
        private static string GetStagedAssembliesDir(
            BuildPipelineContext context,
            HybridClrBuildConfiguration hybridSettings)
        {
            return Path.Combine(
                context.ProjectRoot,
                hybridSettings.OutputAssetRoot,
                context.Target.ToString(),
                "Assemblies");
        }

        /// <summary>
        /// 计算 Obfuz 热更混淆输出目录（工程根/Library/Obfuz/目标/...）。
        /// 与 HybridCLR 热更输入目录（HybridCLRData/HotUpdateDlls）分处不同目录，
        /// 满足 ObfuscateUtil 的禁同目录约束。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>混淆输出目录绝对路径。</returns>
        private static string GetObfuscatedOutputDir(BuildPipelineContext context)
        {
            return Path.Combine(
                context.ProjectRoot,
                "Library",
                "Obfuz",
                context.Target.ToString(),
                "ObfuscatedHotUpdateAssemblies");
        }

        /// <summary>
        /// 判断 Obfuz 混淆清单是否包含任意热更程序集。
        /// 混淆集合为全局清单（含自动追加的 Obfuz.Runtime），热更程序集取
        /// HybridCLR 配置的热更程序集名称；两者存在交集时热更混淆才会生效。
        /// </summary>
        /// <param name="obfuzSettings">Obfuz 设置。</param>
        /// <returns>清单包含热更程序集时返回 true。</returns>
        private static bool ContainsHotUpdateAssembly(
            ObfuzSettings obfuzSettings)
        {
            List<string> obfuscated = obfuzSettings.assemblySettings
                .GetAssembliesToObfuscate();
            if (obfuscated == null || obfuscated.Count == 0)
            {
                return false;
            }

            List<string> hotUpdate = SettingsUtil
                .HotUpdateAssemblyNamesExcludePreserved;
            if (hotUpdate == null || hotUpdate.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < obfuscated.Count; i++)
            {
                for (int j = 0; j < hotUpdate.Count; j++)
                {
                    if (string.Equals(
                        obfuscated[i],
                        hotUpdate[j],
                        StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 判断 AOT 基线目录是否存在且非空。
        /// MethodBridge 重生成依赖 AOT 程序集列表，基线缺失时无法继续。
        /// </summary>
        /// <param name="target">目标平台。</param>
        /// <returns>目录存在且含 DLL 时返回 true。</returns>
        private static bool HasAotBaseline(BuildTarget target)
        {
            string dir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            return Directory.Exists(dir)
                && Directory.GetFiles(dir, "*.dll").Length > 0;
        }

        /// <summary>
        /// 将混淆输出目录中的 DLL 以 .dll.bytes 命名覆盖到暂存目录。
        /// PDB 不混淆，保持 HybridCLR 步骤暂存的原样。
        /// </summary>
        /// <param name="obfuscatedDir">混淆输出目录。</param>
        /// <param name="stagedDir">暂存目录。</param>
        /// <returns>部署的程序集数量。</returns>
        private static int DeployObfuscatedAssemblies(
            string obfuscatedDir,
            string stagedDir)
        {
            Directory.CreateDirectory(stagedDir);
            string[] dlls = Directory.GetFiles(
                obfuscatedDir,
                "*.dll",
                SearchOption.TopDirectoryOnly);
            int count = 0;
            for (int i = 0; i < dlls.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(dlls[i]);
                File.Copy(
                    dlls[i],
                    Path.Combine(stagedDir, name + ".dll.bytes"),
                    true);
                count++;
            }

            return count;
        }

        /// <summary>
        /// 将 Profile 中 Obfuz 内嵌配置同步到 ObfuzSettings.Instance，
        /// 使构建参数以 Profile 为唯一事实源，无需手动打开插件设置窗口。
        /// 仅在 Obfuz 包已导入时执行；同步失败或插件缺失时静默跳过，
        /// 不阻塞构建流程。
        /// </summary>
        /// <param name="obfuzSettings">Profile 中的 Obfuz 内嵌配置。</param>
        private static void SyncProfileToObfuzSettings(
            ObfuzBuildConfiguration obfuzSettings)
        {
            if (obfuzSettings == null)
            {
                return;
            }

            ObfuzSettings pluginSettings = ObfuzSettings.Instance;
            if (pluginSettings == null)
            {
                return;
            }

            // 同步启用开关
            pluginSettings.buildPipelineSettings.enable = obfuzSettings.Enable;

            // 同步混淆 Pass 类型；支持逗号分隔的枚举名称与"All"/"None"关键字
            if (!string.IsNullOrWhiteSpace(obfuzSettings.EnabledPasses))
            {
                ObfuscationPassType passes = ParseObfuscationPasses(obfuzSettings.EnabledPasses);
                pluginSettings.obfuscationPassSettings.enabledPasses = passes;
            }

            // 持久化变更，确保后续插件逻辑读取到最新值
            ObfuzSettings.Save();
        }

        /// <summary>
        /// 解析逗号分隔的混淆 Pass 枚举名称字符串为 <see cref="ObfuscationPassType"/> 位掩码。
        /// 支持关键字 "All"（全 Pass）与 "None"（无 Pass），也支持逗号分隔的单个枚举名。
        /// </summary>
        /// <param name="value">枚举名称字符串。</param>
        /// <returns>解析后的混淆 Pass 位掩码。</returns>
        private static ObfuscationPassType ParseObfuscationPasses(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ObfuscationPassType.None;
            }

            string trimmed = value.Trim();
            if (string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
            {
                return ObfuscationPassType.All;
            }
            if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            {
                return ObfuscationPassType.None;
            }

            ObfuscationPassType result = ObfuscationPassType.None;
            // 注意：StringSplitOptions.TrimEntries 在 .NET Standard 2.0（Unity 2022.3）中不存在，
            // 因此单独对每个分段做 Trim，等价于 RemoveEmptyEntries | TrimEntries 的行为。
            string[] parts = trimmed.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (string.IsNullOrEmpty(part))
                {
                    continue;
                }

                if (Enum.TryParse<ObfuscationPassType>(part, ignoreCase: true, out var pass))
                {
                    result |= pass;
                }
                else
                {
                    Debug.LogWarning($"[Obfuz] 未知的混淆 Pass 类型 '{part}'，已忽略。");
                }
            }

            return result;
        }

        /// <summary>
        /// 绘制 Obfuz 步骤编辑器区：Profile 内嵌可编辑字段（启用开关 + 混淆 Pass）
        /// 与 Obfuz Settings 只读展示区，底部附「打开 Obfuz 设置」按钮跳转到插件设置面板。
        /// </summary>
        /// <param name="profileSO">当前 Profile 的 SerializedObject。</param>
        /// <param name="stepId">步骤唯一 Id。</param>
        public void DrawInspector(SerializedObject profileSO, string stepId)
        {
            try
            {
                // 绘制 Profile 内嵌可编辑字段
                SerializedProperty obfuzProp = profileSO.FindProperty("Obfuz");
                if (obfuzProp != null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label("Obfuz 步骤配置（可编辑）", EditorStyles.boldLabel);
                        EditorGUILayout.PropertyField(
                            obfuzProp.FindPropertyRelative("Enable"),
                            new GUIContent("启用混淆", "关闭后整个混淆步骤跳过。"));
                        EditorGUILayout.PropertyField(
                            obfuzProp.FindPropertyRelative("EnabledPasses"),
                            new GUIContent("混淆 Pass", "逗号分隔枚举名，如 \"SymbolObfus,CallObfus\"；留空 \"All\" 表示全部。"));
                    }
                }

                EditorGUILayout.Space(8f);

                // 只读展示：从 ObfuzSettings.Instance 读取真实配置供参考
                ObfuzSettings obfuzSettings = ObfuzSettings.Instance;
                if (obfuzSettings != null)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        GUILayout.Label("Obfuz Settings（只读参考）", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(
                            "构建管线启用",
                            obfuzSettings.buildPipelineSettings?.enable.ToString() ?? "null");
                        EditorGUILayout.LabelField(
                            "混淆清单程序集数",
                            obfuzSettings.assemblySettings?.GetAssembliesToObfuscate()?.Count.ToString() ?? "0");
                        EditorGUILayout.LabelField(
                            "混淆 Pass 类型",
                            obfuzSettings.obfuscationPassSettings?.enabledPasses.ToString() ?? "null");
                        EditorGUILayout.LabelField(
                            "Obfuz 根目录",
                            obfuzSettings.ObfuzRootDir);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Obfuz 未导入或 Settings 实例缺失，请确认 Obfuz 包已正确安装。", MessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                EditorGUILayout.HelpBox($"绘制 Obfuz 步骤区失败：{ex.Message}", MessageType.Error);
            }

            EditorGUILayout.Space(8f);

            if (GUILayout.Button("打开 Obfuz 设置", GUILayout.Height(24f)))
            {
                SettingsService.OpenProjectSettings("Project/Obfuz Settings");
            }
        }
    }
}
