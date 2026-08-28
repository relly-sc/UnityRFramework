using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建用途分档：正式、测试与开发。分档用于校验和报告；
    /// 推荐调试参数只有在用户显式确认后才写入 Profile。
    /// </summary>
    public enum BuildProfileFlavor
    {
        /// <summary>正式发布档：关闭开发构建与调试能力。</summary>
        Release,

        /// <summary>测试验收档：Development Build 与脚本调试，便于抓取日志。</summary>
        Qa,

        /// <summary>开发迭代档：启用常用调试能力。</summary>
        Development
    }

    /// <summary>
    /// 构建配置资产，承载平台参数、输出目录、场景与步骤配置。
    /// 资产仅由构建工具窗口统一创建，项目根 Assets/BuildProfiles/ 下管理，不写入框架包默认资源。
    /// 敏感密码字段只保存环境变量名，密码本体只存在于环境变量中。
    /// </summary>
    public sealed class UnityRFrameworkBuildProfile : ScriptableObject
    {
        /// <summary>Profile 资产默认存放目录（项目根，与框架代码分离）。</summary>
        public const string DefaultAssetDirectory =
            "Assets/BuildProfiles";

        /// <summary>当前数据模型版本号，用于序列化升级迁移。</summary>
        public const int CurrentSerializedVersion = 3;

        /// <summary>Profile 说明，用于窗口与报告展示。</summary>
        [Tooltip("Profile 说明。")]
        public string Description = string.Empty;

        /// <summary>Profile 启用状态；关闭的 Profile 不会出现在构建候选列表中。</summary>
        [Tooltip("Profile 启用状态。")]
        public bool Enabled = true;

        /// <summary>构建用途分档，用于校验、报告与推荐参数选择。</summary>
        [Tooltip("构建用途分档；切换不会自动覆盖参数，可显式应用推荐参数。")]
        public BuildProfileFlavor Flavor = BuildProfileFlavor.Release;

        /// <summary>本 Profile 默认执行的构建方案。</summary>
        [Tooltip("本 Profile 默认执行的构建方案。")]
        public BuildRecipe Recipe = BuildRecipe.Release;

        /// <summary>平台构建参数。</summary>
        [Tooltip("平台构建参数。")]
        public BuildPlatformSettings Platform = new BuildPlatformSettings();

        /// <summary>输出目录与文件名配置。</summary>
        [Tooltip("输出目录与文件名配置。")]
        public BuildOutputSettings Output = new BuildOutputSettings();

        /// <summary>参与构建的场景列表，至少保留一个启用场景。</summary>
        [Tooltip("参与构建的场景列表。")]
        public List<BuildSceneEntry> Scenes = new List<BuildSceneEntry>();

        /// <summary>构建步骤配置列表，与步骤实现的唯一 Id 对应。</summary>
        [Tooltip("构建步骤配置列表。")]
        public List<BuildStepSettings> Steps = new List<BuildStepSettings>();

        /// <summary>版本 1 Config 内联配置，仅作为迁移源。</summary>
        [HideInInspector]
        public ConfigExportBuildSettings ConfigExport = new ConfigExportBuildSettings();

        /// <summary>版本 1 HybridCLR 内联配置，仅作为迁移源。</summary>
        [HideInInspector]
        public HybridClrBuildSettings HybridClr = new HybridClrBuildSettings();

        /// <summary>版本 1 YooAsset 内联配置，仅作为迁移源。</summary>
        [HideInInspector]
        public YooAssetBuildSettings YooAsset = new YooAssetBuildSettings();

        /// <summary>版本 1 Obfuz 内联配置，仅作为迁移源。</summary>
        [HideInInspector]
        public ObfuzBuildSettings Obfuz = new ObfuzBuildSettings();

        /// <summary>序列化版本号，升级时用于迁移旧资产。</summary>
        [HideInInspector]
        public int SerializedVersion = CurrentSerializedVersion;

        /// <summary>执行不涉及资产写入的数据模型升级迁移。</summary>
        public void Migrate()
        {
            if (SerializedVersion >= CurrentSerializedVersion)
            {
                return;
            }

            if (SerializedVersion < 1)
            {
                // 版本 0 至 1 为初版落地，无旧数据需要处理。
            }

            if (SerializedVersion < 2)
            {
                Recipe = BuildRecipe.Release;
            }

            Platform ??= new BuildPlatformSettings();
            if (SerializedVersion < 3)
            {
                Platform.DefineSymbols ??= new List<string>();

                if (Platform.ApiCompatibilityLevel
                    == ApiCompatibilityLevel.NET_Standard_2_0)
                {
                    Platform.ApiCompatibilityLevel =
                        ApiCompatibilityLevel.NET_Standard;
                }

                if (Platform.LegacyAdditionalDefineSymbols != null)
                {
                    for (int i = 0;
                         i < Platform.LegacyAdditionalDefineSymbols.Count;
                         i++)
                    {
                        string symbol = Platform.LegacyAdditionalDefineSymbols[i];
                        if (!Platform.DefineSymbols.Contains(symbol))
                        {
                            Platform.DefineSymbols.Add(symbol);
                        }
                    }

                    Platform.LegacyAdditionalDefineSymbols.Clear();
                }
            }

            Output ??= new BuildOutputSettings();
            Scenes ??= new List<BuildSceneEntry>();
            Steps ??= new List<BuildStepSettings>();

            SerializedVersion = CurrentSerializedVersion;
        }

        /// <summary>
        /// 获取版本 1 内联步骤配置的 JSON，仅供配置资产迁移使用。
        /// </summary>
        public string GetLegacyStepConfigurationJson(string stepId)
        {
            switch (stepId)
            {
                case "config":
                    return JsonUtility.ToJson(ConfigExport);
                case "hybridclr":
                    return JsonUtility.ToJson(HybridClr);
                case "yooasset":
                    return JsonUtility.ToJson(YooAsset);
                case "obfuz":
                    return JsonUtility.ToJson(Obfuz);
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 汇总校验 Profile 全部配置。
        /// </summary>
        /// <returns>错误描述列表；无错误时返回空列表。</returns>
        public List<string> Validate()
        {
            List<string> errors = new List<string>();

            errors.AddRange(Platform.Validate());

            ValidateFlavor(errors);
            ValidateScenes(errors);
            ValidateSteps(errors);

            return errors;
        }

        /// <summary>
        /// 校验构建用途分档与调试参数的一致性。
        /// </summary>
        /// <param name="errors">追加错误描述的目标列表。</param>
        private void ValidateFlavor(List<string> errors)
        {
            if (Flavor != BuildProfileFlavor.Release)
            {
                return;
            }

            if (Platform.DevelopmentBuild)
            {
                errors.Add(
                    "正式 Profile 不允许启用 Development Build，请使用测试或开发档。");
            }

            if (Platform.ScriptDebugging)
            {
                errors.Add(
                    "正式 Profile 不允许启用脚本调试，请使用测试或开发档。");
            }

            if (Platform.AutoconnectProfiler || Platform.DeepProfiling)
            {
                errors.Add(
                    "正式 Profile 不允许启用 Profiler 相关选项，请使用开发档。");
            }
        }

        /// <summary>
        /// 校验场景列表至少存在一个启用的场景。
        /// </summary>
        /// <param name="errors">追加错误描述的目标列表。</param>
        private void ValidateScenes(List<string> errors)
        {
            if (Scenes == null || Scenes.Count == 0)
            {
                errors.Add("场景列表为空，至少需要一个启用场景。");
                return;
            }

            bool hasEnabledScene = false;
            for (int i = 0; i < Scenes.Count; i++)
            {
                BuildSceneEntry entry = Scenes[i];
                if (entry == null)
                {
                    errors.Add($"场景条目 {i} 为 null。");
                    continue;
                }

                if (!entry.Enabled)
                {
                    continue;
                }

                if (entry.Scene == null)
                {
                    errors.Add($"启用场景条目 {i} 的场景引用缺失。");
                    continue;
                }

                hasEnabledScene = true;
            }

            if (!hasEnabledScene)
            {
                errors.Add("场景列表中没有可用的启用场景。");
            }
        }

        /// <summary>
        /// 校验步骤配置条目的基本合法性。
        /// </summary>
        /// <param name="errors">追加错误描述的目标列表。</param>
        private void ValidateSteps(List<string> errors)
        {
            if (Steps == null)
            {
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Steps.Count; i++)
            {
                BuildStepSettings step = Steps[i];
                if (step == null)
                {
                    errors.Add($"步骤条目 {i} 为 null。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.StepId))
                {
                    errors.Add($"步骤条目 {i} 的步骤 Id 为空。");
                    continue;
                }

                if (!ids.Add(step.StepId))
                {
                    errors.Add($"步骤 Id '{step.StepId}' 重复。");
                }
            }
        }
    }
}
