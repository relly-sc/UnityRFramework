using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 第三方集成摘要：记录名称、是否安装与版本，用于报告记录
    /// YooAsset、HybridCLR 与 Obfuz 的集成状态（若存在）。
    /// 程序集探测方式保证第三方包未安装时报告仍可生成。
    /// </summary>
    [Serializable]
    public sealed class BuildThirdPartySummary
    {
        /// <summary>集成名称。</summary>
        public string Name = string.Empty;

        /// <summary>是否已安装（对应程序集存在）。</summary>
        public bool Installed;

        /// <summary>包版本；程序集来自本地代码时为空字符串。</summary>
        public string Version = string.Empty;
    }

    /// <summary>
    /// 单步执行记录，供 JSON 构建报告输出。
    /// 全部字段为可序列化基元，JsonUtility 可直接往返。
    /// </summary>
    [Serializable]
    public sealed class BuildExecutionReportStep
    {
        /// <summary>步骤唯一 Id。</summary>
        public string StepId = string.Empty;

        /// <summary>步骤显示名称。</summary>
        public string DisplayName = string.Empty;

        /// <summary>步骤执行结果状态名称。</summary>
        public string Status = string.Empty;

        /// <summary>步骤开始时刻（ISO 8601 字符串）。</summary>
        public string StartedAt = string.Empty;

        /// <summary>步骤结束时刻（ISO 8601 字符串）。</summary>
        public string FinishedAt = string.Empty;

        /// <summary>步骤耗时（秒）。</summary>
        public double DurationSeconds;

        /// <summary>步骤返回的用户可见消息。</summary>
        public string Message = string.Empty;

        /// <summary>步骤输出路径（脱敏）；未提供时为空字符串。</summary>
        public string OutputPath = string.Empty;

        /// <summary>步骤失败时的脱敏异常文本；成功时为空字符串。</summary>
        public string Error = string.Empty;
    }

    /// <summary>
    /// 单次构建执行报告数据模型，序列化为 JSON 供自动化读取。
    /// 报告内容经过脱敏：不包含密码、私钥、令牌与完整本机敏感路径。
    /// </summary>
    [Serializable]
    public sealed class BuildExecutionReport
    {
        /// <summary>报告模型版本号，供读取方判断字段结构。</summary>
        public const string CurrentSchemaVersion = "1.0";

        /// <summary>单条异常文本的最大保留长度，超长部分截断。</summary>
        private const int MaxErrorLength = 2000;

        /// <summary>报告模型版本号。</summary>
        public string SchemaVersion = CurrentSchemaVersion;

        /// <summary>构建任务唯一 Id。</summary>
        public string TaskId = string.Empty;

        /// <summary>Unity 编辑器版本。</summary>
        public string UnityVersion = string.Empty;

        /// <summary>构建配置资产名称。</summary>
        public string ProfileName = string.Empty;

        /// <summary>构建用途分档名称。</summary>
        public string ProfileFlavor = string.Empty;

        /// <summary>本任务实际执行的 Recipe。</summary>
        public string Recipe = string.Empty;

        /// <summary>构建配置资产路径（工程相对路径）。</summary>
        public string ProfileAssetPath = string.Empty;

        /// <summary>目标平台名称。</summary>
        public string TargetPlatform = string.Empty;

        /// <summary>脚本后端名称。</summary>
        public string ScriptBackend = string.Empty;

        /// <summary>公共版本号。</summary>
        public string PublicVersion = string.Empty;

        /// <summary>平台构建号（本次构建使用的值）。</summary>
        public int BuildNumber;

        /// <summary>参与构建的场景路径列表。</summary>
        public List<string> Scenes = new List<string>();

        /// <summary>构建时生效的脚本宏定义列表。</summary>
        public List<string> DefineSymbols = new List<string>();

        /// <summary>输出根目录绝对路径（脱敏）。</summary>
        public string OutputRoot = string.Empty;

        /// <summary>输出目录（相对输出根）。</summary>
        public string OutputDirectory = string.Empty;

        /// <summary>输出文件名（不含扩展名）。</summary>
        public string OutputFileName = string.Empty;

        /// <summary>Player 产物完整路径（脱敏）。</summary>
        public string PlayerOutputPath = string.Empty;

        /// <summary>任务创建时刻（ISO 8601 字符串）。</summary>
        public string StartedAt = string.Empty;

        /// <summary>任务结束时刻（ISO 8601 字符串）。</summary>
        public string FinishedAt = string.Empty;

        /// <summary>任务总耗时（秒）。</summary>
        public double DurationSeconds;

        /// <summary>任务是否成功完成。</summary>
        public bool Succeeded;

        /// <summary>任务是否被取消。</summary>
        public bool Cancelled;

        /// <summary>任务失败原因的用户可见描述。</summary>
        public string ErrorMessage = string.Empty;

        /// <summary>步骤执行记录，按执行顺序排列。</summary>
        public List<BuildExecutionReportStep> Steps =
            new List<BuildExecutionReportStep>();

        /// <summary>第三方集成摘要列表。</summary>
        public List<BuildThirdPartySummary> ThirdParty =
            new List<BuildThirdPartySummary>();

        /// <summary>
        /// 从构建上下文、任务状态与执行结果组装执行报告。
        /// 组装过程只读，不修改 Profile、PlayerSettings 或输出目录。
        /// </summary>
        /// <param name="context">构建上下文，可为空。</param>
        /// <param name="state">任务状态，可为空。</param>
        /// <param name="succeeded">任务是否成功完成。</param>
        /// <param name="cancelled">任务是否被取消。</param>
        /// <returns>组装完成的执行报告。</returns>
        public static BuildExecutionReport Create(
            BuildPipelineContext context,
            BuildPipelineState state,
            bool succeeded,
            bool cancelled)
        {
            BuildExecutionReport report = new BuildExecutionReport
            {
                UnityVersion = Application.unityVersion,
                Succeeded = succeeded,
                Cancelled = cancelled
            };

            if (context != null && context.Profile != null)
            {
                FillProfile(report, context);
            }

            if (state != null)
            {
                FillState(report, state, context);
            }

            FillThirdParty(report);
            return report;
        }

        /// <summary>
        /// 脱敏路径：将用户主目录替换为波浪号，避免完整本机敏感路径进入报告。
        /// 路径分隔符统一为斜杠，保证跨平台可读。
        /// </summary>
        /// <param name="path">原始路径。</param>
        /// <returns>脱敏后的路径；空值返回空字符串。</returns>
        public static string RedactPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', '/');
            string userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
            {
                return normalized;
            }

            string normalizedProfile = userProfile.Replace('\\', '/');
            if (normalized.StartsWith(
                    normalizedProfile,
                    StringComparison.OrdinalIgnoreCase))
            {
                return "~" + normalized.Substring(normalizedProfile.Length);
            }

            return normalized;
        }

        /// <summary>
        /// 脱敏文本：将文本中出现的用户主目录（正斜杠与反斜杠两种形式）
        /// 替换为波浪号。使用正则实现大小写不敏感替换，兼容 .NET Standard 2.0。
        /// </summary>
        /// <param name="text">原始文本。</param>
        /// <returns>脱敏后的文本；空值返回空字符串。</returns>
        public static string RedactText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string userProfile = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
            {
                return text;
            }

            string pattern = Regex.Escape(userProfile)
                + "|"
                + Regex.Escape(userProfile.Replace('\\', '/'));
            return Regex.Replace(text, pattern, "~", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 填充 Profile 相关字段：名称、分档、路径、平台、版本、场景与宏。
        /// </summary>
        /// <param name="report">待填充的报告。</param>
        /// <param name="context">构建上下文。</param>
        private static void FillProfile(
            BuildExecutionReport report,
            BuildPipelineContext context)
        {
            UnityRFrameworkBuildProfile profile = context.Profile;
            report.ProfileName = profile.name;
            report.ProfileFlavor = profile.Flavor.ToString();
            report.Recipe = context.Recipe.ToString();
            report.ProfileAssetPath = AssetDatabase.GetAssetPath(profile);
            report.TargetPlatform = profile.Platform.Target.ToString();
            report.ScriptBackend = profile.Platform.ScriptingBackend.ToString();
            report.PublicVersion = profile.Platform.PublicVersion;
            report.BuildNumber = profile.Platform.BuildNumber;

            if (context.Recipe == BuildRecipe.Player
                || context.Recipe == BuildRecipe.Release)
            {
                string[] scenes = BuildPlayerOptionsFactory.ResolveScenes(profile);
                for (int i = 0; i < scenes.Length; i++)
                {
                    report.Scenes.Add(scenes[i]);
                }
            }

            FillDefineSymbols(report, profile);
            FillOutput(report, context);
        }

        /// <summary>
        /// 填充 Profile 脚本宏定义列表并去重。
        /// </summary>
        /// <param name="report">待填充的报告。</param>
        /// <param name="profile">构建配置。</param>
        private static void FillDefineSymbols(
            BuildExecutionReport report,
            UnityRFrameworkBuildProfile profile)
        {
            HashSet<string> symbols =
                new HashSet<string>(StringComparer.Ordinal);
            AppendSymbols(symbols, profile.Platform.DefineSymbols);
            report.DefineSymbols.AddRange(symbols);
        }

        /// <summary>
        /// 将宏列表追加到去重集合。
        /// </summary>
        /// <param name="target">去重集合。</param>
        /// <param name="symbols">宏列表。</param>
        private static void AppendSymbols(
            HashSet<string> target,
            IReadOnlyList<string> symbols)
        {
            if (symbols == null)
            {
                return;
            }

            for (int i = 0; i < symbols.Count; i++)
            {
                string symbol = symbols[i];
                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    target.Add(symbol);
                }
            }
        }

        /// <summary>
        /// 填充输出相关字段，全部路径经脱敏处理。
        /// </summary>
        /// <param name="report">待填充的报告。</param>
        /// <param name="context">构建上下文。</param>
        private static void FillOutput(
            BuildExecutionReport report,
            BuildPipelineContext context)
        {
            report.OutputRoot = RedactPath(context.OutputRootAbsolute);
            report.OutputDirectory = context.OutputDirectory;
            report.OutputFileName = context.OutputFileName;

            try
            {
                report.PlayerOutputPath = RedactPath(
                    BuildPlayerOptionsFactory.ResolveLocationPath(context));
            }
            catch (Exception)
            {
                // 输出解析失败时产物路径留空，报告仍可生成。
                report.PlayerOutputPath = string.Empty;
            }
        }

        /// <summary>
        /// 填充任务状态字段：任务 Id、时刻、耗时、步骤记录与失败原因。
        /// </summary>
        /// <param name="report">待填充的报告。</param>
        /// <param name="state">任务状态。</param>
        /// <param name="context">构建上下文，用于步骤显示名称查询。</param>
        private static void FillState(
            BuildExecutionReport report,
            BuildPipelineState state,
            BuildPipelineContext context)
        {
            report.TaskId = state.TaskId;
            report.StartedAt = state.CreatedAt;
            report.FinishedAt = state.UpdatedAt;
            report.PublicVersion = state.PublicVersion;
            report.BuildNumber = state.BuildNumber;
            report.Recipe = state.Recipe.ToString();
            report.DurationSeconds = CalculateDuration(
                state.CreatedAt,
                state.UpdatedAt);
            report.ErrorMessage = RedactText(state.ErrorMessage);

            FillSteps(report, state, context);
        }

        /// <summary>
        /// 填充步骤记录：已完成步骤按顺序输出，失败步骤追加在末尾。
        /// </summary>
        /// <param name="report">待填充的报告。</param>
        /// <param name="state">任务状态。</param>
        /// <param name="context">构建上下文，用于步骤显示名称查询。</param>
        private static void FillSteps(
            BuildExecutionReport report,
            BuildPipelineState state,
            BuildPipelineContext context)
        {
            for (int i = 0; i < state.CompletedSteps.Count; i++)
            {
                report.Steps.Add(ToReportStep(
                    state.CompletedSteps[i],
                    context));
            }

            if (state.FailedStep != null)
            {
                report.Steps.Add(ToReportStep(state.FailedStep, context));
            }
        }

        /// <summary>
        /// 将检查点步骤记录转换为报告步骤模型，补充显示名称、耗时与脱敏字段。
        /// </summary>
        /// <param name="record">检查点步骤记录。</param>
        /// <param name="context">构建上下文，用于步骤显示名称查询。</param>
        /// <returns>报告步骤模型。</returns>
        private static BuildExecutionReportStep ToReportStep(
            BuildStepRecord record,
            BuildPipelineContext context)
        {
            BuildExecutionReportStep step = new BuildExecutionReportStep
            {
                StepId = record.StepId,
                DisplayName = ResolveDisplayName(record.StepId, context),
                Status = record.Status,
                StartedAt = record.StartedAt,
                FinishedAt = record.FinishedAt,
                DurationSeconds = CalculateDuration(
                    record.StartedAt,
                    record.FinishedAt),
                Message = RedactText(record.Message),
                OutputPath = RedactPath(record.OutputPath),
                Error = Truncate(RedactText(record.ExceptionText))
            };
            return step;
        }

        /// <summary>
        /// 解析步骤显示名称；注册表缺失时回退为步骤 Id。
        /// </summary>
        /// <param name="stepId">步骤唯一 Id。</param>
        /// <param name="context">构建上下文。</param>
        /// <returns>步骤显示名称。</returns>
        private static string ResolveDisplayName(
            string stepId,
            BuildPipelineContext context)
        {
            IBuildPipelineStep step;
            if (context != null
                && context.Steps.TryGetValue(stepId, out step))
            {
                return step.DisplayName;
            }

            return stepId;
        }

        /// <summary>
        /// 计算两个 ISO 时刻之间的耗时（秒）；任一时刻解析失败时返回 0。
        /// </summary>
        /// <param name="startedAt">开始时刻。</param>
        /// <param name="finishedAt">结束时刻。</param>
        /// <returns>耗时秒数。</returns>
        private static double CalculateDuration(
            string startedAt,
            string finishedAt)
        {
            DateTime start;
            DateTime finish;
            if (!DateTime.TryParse(startedAt, out start)
                || !DateTime.TryParse(finishedAt, out finish))
            {
                return 0.0;
            }

            double seconds = (finish - start).TotalSeconds;
            return seconds > 0.0 ? seconds : 0.0;
        }

        /// <summary>
        /// 截断超长文本，避免报告体积失控。
        /// </summary>
        /// <param name="text">原始文本。</param>
        /// <returns>截断后的文本。</returns>
        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxErrorLength)
            {
                return text;
            }

            return text.Substring(0, MaxErrorLength) + "...（截断）";
        }

        /// <summary>
        /// 填充第三方集成摘要：按程序集名探测 YooAsset、HybridCLR 与 Obfuz。
        /// 程序集探测不产生对第三方类型的硬引用，未安装时对应条目 Installed 为 false。
        /// </summary>
        /// <param name="report">待填充的报告。</param>
        private static void FillThirdParty(BuildExecutionReport report)
        {
            report.ThirdParty.Add(CreateSummary(
                "YooAsset",
                new[] { "YooAsset", "YooAsset.Editor" }));
            report.ThirdParty.Add(CreateSummary(
                "HybridCLR",
                new[] { "HybridCLR", "HybridCLR.Editor" }));
            report.ThirdParty.Add(CreateSummary(
                "Obfuz",
                new[] { "Obfuz.Editor" }));
            report.ThirdParty.Add(CreateSummary(
                "Obfuz4HybridCLR",
                new[] { "Obfuz4HybridCLR.Editor" }));
        }

        /// <summary>
        /// 创建单个第三方摘要：查找已加载程序集中第一个匹配名称的程序集。
        /// </summary>
        /// <param name="displayName">集成显示名称。</param>
        /// <param name="assemblyNames">候选程序集名称。</param>
        /// <returns>第三方集成摘要。</returns>
        private static BuildThirdPartySummary CreateSummary(
            string displayName,
            string[] assemblyNames)
        {
            BuildThirdPartySummary summary = new BuildThirdPartySummary
            {
                Name = displayName,
                Installed = false,
                Version = string.Empty
            };

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string assemblyName = assemblies[i].GetName().Name;
                for (int j = 0; j < assemblyNames.Length; j++)
                {
                    if (string.Equals(
                            assemblyName,
                            assemblyNames[j],
                            StringComparison.Ordinal))
                    {
                        summary.Installed = true;
                        summary.Version = ResolvePackageVersion(assemblies[i]);
                        return summary;
                    }
                }
            }

            return summary;
        }

        /// <summary>
        /// 解析程序集对应的包版本；程序集来自本地代码时返回空字符串。
        /// 使用全限定类型名，避免与 UnityEditor 命名空间下的旧 PackageInfo 类型歧义。
        /// </summary>
        /// <param name="assembly">目标程序集。</param>
        /// <returns>包版本号；无法解析时返回空字符串。</returns>
        private static string ResolvePackageVersion(Assembly assembly)
        {
            try
            {
                UnityEditor.PackageManager.PackageInfo info =
                    UnityEditor.PackageManager.PackageInfo
                        .FindForAssembly(assembly);
                if (info != null
                    && !string.IsNullOrEmpty(info.version))
                {
                    return info.version;
                }
            }
            catch (Exception)
            {
                // 包信息解析失败时版本留空，不影响报告生成。
            }

            return string.Empty;
        }
    }
}
