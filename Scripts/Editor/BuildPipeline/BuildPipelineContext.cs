using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建流水线上下文，承载 Profile、目标平台、输出解析结果与步骤实例字典。
    /// 输出解析失败时输出字段为空字符串，步骤不应假设输出字段非空。
    /// </summary>
    public sealed class BuildPipelineContext
    {
        /// <summary>构建配置；恢复时从资产路径重新加载，加载失败时为空。</summary>
        public UnityRFrameworkBuildProfile Profile { get; }

        /// <summary>目标平台。</summary>
        public BuildTarget Target { get; }

        /// <summary>目标平台对应的组。</summary>
        public BuildTargetGroup Group
        {
            get
            {
                return BuildPipeline.GetBuildTargetGroup(Target);
            }
        }

        /// <summary>Unity 工程根目录绝对路径。</summary>
        public string ProjectRoot { get; }

        /// <summary>输出根目录绝对路径；解析失败时为空字符串。</summary>
        public string OutputRootAbsolute { get; }

        /// <summary>解析后的输出目录（相对输出根）；失败时为空字符串。</summary>
        public string OutputDirectory { get; }

        /// <summary>解析后的输出文件名（不含扩展名）；失败时为空字符串。</summary>
        public string OutputFileName { get; }

        /// <summary>步骤实例字典，键为步骤唯一 Id。</summary>
        public IReadOnlyDictionary<string, IBuildPipelineStep> Steps { get; }

        /// <summary>取消令牌；步骤在执行中应定期检查。</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// 设置事务；承载"临时应用、结束恢复"的快照。
        /// "应用构建参数"步骤在写入设置前调用 <see cref="BuildSettingsTransaction.MarkApplied"/>。
        /// </summary>
        public BuildSettingsTransaction SettingsTransaction { get; }

        /// <summary>任务创建时刻。</summary>
        public DateTime StartedAt { get; }

        /// <summary>输出解析失败原因；未失败时为空字符串。</summary>
        public string OutputError { get; }

        /// <summary>
        /// 创建构建上下文。
        /// </summary>
        /// <param name="profile">构建配置，可为空。</param>
        /// <param name="target">目标平台。</param>
        /// <param name="projectRoot">工程根目录绝对路径。</param>
        /// <param name="outputRootAbsolute">输出根目录绝对路径。</param>
        /// <param name="outputDirectory">输出目录（相对输出根）。</param>
        /// <param name="outputFileName">输出文件名（不含扩展名）。</param>
        /// <param name="steps">步骤实例字典。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="startedAt">任务创建时刻。</param>
        /// <param name="outputError">输出解析失败原因，可为空。</param>
        /// <param name="settingsTransaction">设置事务，可为空（测试场景）。</param>
        public BuildPipelineContext(
            UnityRFrameworkBuildProfile profile,
            BuildTarget target,
            string projectRoot,
            string outputRootAbsolute,
            string outputDirectory,
            string outputFileName,
            IReadOnlyDictionary<string, IBuildPipelineStep> steps,
            CancellationToken cancellationToken,
            DateTime startedAt,
            string outputError,
            BuildSettingsTransaction settingsTransaction = null)
        {
            Profile = profile;
            Target = target;
            ProjectRoot = projectRoot ?? string.Empty;
            OutputRootAbsolute = outputRootAbsolute ?? string.Empty;
            OutputDirectory = outputDirectory ?? string.Empty;
            OutputFileName = outputFileName ?? string.Empty;
            Steps = steps ?? new Dictionary<string, IBuildPipelineStep>(
                StringComparer.Ordinal);
            CancellationToken = cancellationToken;
            SettingsTransaction = settingsTransaction;
            StartedAt = startedAt;
            OutputError = outputError ?? string.Empty;
        }

        /// <summary>
        /// 创建构建上下文：解析工程根目录与输出路径，并捕获设置事务快照。
        /// 输出解析失败时记录到 <see cref="OutputError"/>，输出字段保持为空字符串。
        /// </summary>
        /// <param name="profile">构建配置，可为空。</param>
        /// <param name="steps">步骤实例字典。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="taskId">任务 Id，用于快照归属；为空时使用"未命名任务"。</param>
        /// <param name="persistenceRoot">任务状态目录；为空时使用工程默认目录。</param>
        /// <returns>构建上下文。</returns>
        public static BuildPipelineContext Create(
            UnityRFrameworkBuildProfile profile,
            IReadOnlyDictionary<string, IBuildPipelineStep> steps,
            CancellationToken cancellationToken,
            string taskId = null,
            string persistenceRoot = null)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputRoot = string.Empty;
            string directory = string.Empty;
            string fileName = string.Empty;
            string outputError = string.Empty;

            if (profile != null)
            {
                BuildOutputToken token = CreateOutputToken(profile);
                try
                {
                    outputRoot = profile.Output.ResolveRootAbsolute(projectRoot);
                }
                catch (Exception exception)
                {
                    outputError = $"输出根目录解析失败：{exception.Message}";
                }

                if (outputError.Length == 0)
                {
                    try
                    {
                        directory = profile.Output.ResolveDirectory(token);
                        fileName = profile.Output.ResolveFileName(token);
                    }
                    catch (Exception exception)
                    {
                        outputError = $"输出模板解析失败：{exception.Message}";
                    }
                }
            }

            BuildTarget target = profile != null
                ? profile.Platform.Target
                : EditorUserBuildSettings.activeBuildTarget;

            BuildSettingsTransaction transaction = null;
            try
            {
                transaction = BuildSettingsTransaction.Capture(
                    new BuildPipelinePersistence(persistenceRoot),
                    taskId ?? "未命名任务");
            }
            catch (Exception exception)
            {
                outputError = string.IsNullOrEmpty(outputError)
                    ? $"设置事务快照捕获失败：{exception.Message}"
                    : outputError;
                transaction = null;
            }

            return new BuildPipelineContext(
                profile,
                target,
                projectRoot,
                outputRoot,
                directory,
                fileName,
                steps,
                cancellationToken,
                DateTime.Now,
                outputError,
                transaction);
        }

        /// <summary>
        /// 将本上下文转换为阶段 3 的校验上下文，供步骤校验与窗口复用。
        /// </summary>
        /// <returns>校验上下文。</returns>
        public BuildValidationContext ToValidationContext()
        {
            return new BuildValidationContext(
                Profile,
                Target,
                ProjectRoot,
                OutputRootAbsolute,
                OutputDirectory,
                OutputFileName);
        }

        /// <summary>
        /// 构造输出令牌，字段来源与构建前校验保持一致。
        /// </summary>
        /// <param name="profile">构建配置。</param>
        /// <returns>输出令牌。</returns>
        private static BuildOutputToken CreateOutputToken(
            UnityRFrameworkBuildProfile profile)
        {
            return new BuildOutputToken(
                profile.name,
                profile.Platform.ProductName,
                profile.Platform.Target.ToString(),
                profile.Platform.PublicVersion,
                profile.Platform.BuildNumber,
                profile.Platform.ScriptingBackend.ToString(),
                DateTime.Now);
        }
    }
}
