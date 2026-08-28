using System;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建命令进程退出码约定，供 CI 与脚本判断命令结果。
    /// </summary>
    public static class BuildCommandExitCodes
    {
        /// <summary>成功：全部步骤执行完毕。</summary>
        public const int Success = 0;

        /// <summary>参数错误：命令行参数缺失、非法或包含密码类参数。</summary>
        public const int ArgumentError = 1;

        /// <summary>校验失败：Profile 或工程状态未通过构建前校验。</summary>
        public const int ValidationFailure = 2;

        /// <summary>构建失败：流水线步骤失败或存在无法安全恢复的残留任务。</summary>
        public const int BuildFailure = 3;

        /// <summary>已取消：任务被取消，未完成的步骤不再执行。</summary>
        public const int Cancelled = 4;
    }

    /// <summary>
    /// BatchMode 构建命令入口，与构建工具窗口共用同一套
    /// Validator、Runner、步骤与报告实现，仅入口与退出方式不同。
    /// 命令行用法示例：
    /// Unity.exe -batchmode -projectPath &lt;工程路径&gt;
    ///   -executeMethod UnityRFramework.Editor.UnityRFrameworkBuildCommand.ExecuteFromCommandLine
    ///   -urfProfile WindowsRelease -urfCleanBuild true
    /// 密码与密钥只允许通过环境变量提供（Profile 中保存环境变量名），
    /// 出现在命令行中的密码类参数会被直接拒绝并以参数错误退出。
    /// Domain Reload 语义：同会话残留任务同步续跑（不等待人工确认）；
    /// 跨进程残留任务在 BatchMode 下直接作废并按构建失败退出。
    /// </summary>
    public static class UnityRFrameworkBuildCommand
    {
        /// <summary>
        /// -executeMethod 入口：解析命令行、执行构建并按结果退出进程。
        /// 仅在 BatchMode 下调用 EditorApplication.Exit 结束进程；
        /// 非批处理环境（误触发）只记录错误，不关闭编辑器。
        /// </summary>
        public static void ExecuteFromCommandLine()
        {
            BuildCommandLineArguments arguments = BuildCommandLineArguments.Parse(
                Environment.GetCommandLineArgs());
            int exitCode = Run(arguments);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else
            {
                Debug.Log(
                    $"构建命令在非批处理环境中触发，已执行并返回退出码 {exitCode}，"
                    + "不会关闭编辑器。");
            }
        }

        /// <summary>
        /// 执行一次完整构建：参数校验 → 残留任务守卫 → Profile 解析 →
        /// 应用覆盖项 → 构建前校验 → 流水线执行 → 恢复覆盖项。
        /// 覆盖项只在本次命令的运行期生效，结束后恢复 Profile 原值；
        /// 构建号未被覆盖时保留成功后的自动递增结果。
        /// </summary>
        /// <param name="arguments">已解析的命令行参数。</param>
        /// <returns>进程退出码，见 <see cref="BuildCommandExitCodes"/>。</returns>
        public static int Run(BuildCommandLineArguments arguments)
        {
            if (arguments == null || !arguments.IsValid)
            {
                LogErrors(arguments == null
                    ? new[] { "构建命令参数为空。" }
                    : arguments.Errors);
                Debug.LogError(
                    "命令行用法：-urfProfile &lt;GUID|资产路径|唯一名称&gt; "
                    + "[-urfOutputRoot 路径] [-urfVersion 版本] "
                    + "[-urfBuildNumber 正整数] [-urfCleanBuild true|false]");
                return BuildCommandExitCodes.ArgumentError;
            }

            // 活动任务守卫：当前进程已有任务在执行时不允许重复启动。
            if (BuildPipelineRunner.HasActiveTask)
            {
                Debug.LogError("当前进程已存在活动构建任务，不能重复启动。");
                return BuildCommandExitCodes.BuildFailure;
            }

            // 残留任务守卫：处理上次中断的任务（恢复 / 作废 / 清理）。
            // 同会话残留任务的续跑结果已在内部直接返回，不再启动新构建。
            int leftoverResult = HandleLeftoverTask();
            if (leftoverResult != BuildCommandExitCodes.Success)
            {
                return leftoverResult;
            }

            string profileError;
            UnityRFrameworkBuildProfile profile =
                ResolveProfile(arguments.ProfileReference, out profileError);
            if (profile == null)
            {
                Debug.LogError(profileError);
                return BuildCommandExitCodes.ArgumentError;
            }

            profile.Migrate();

            // 覆盖项只作用于运行期，结束后恢复 Profile 原值。
            BuildProfileOverrideScope overrideScope =
                new BuildProfileOverrideScope(profile, arguments);
            try
            {
                BuildValidationResult validation =
                    BuildProfileValidator.Validate(profile);
                if (!validation.CanBuild)
                {
                    LogValidationIssues(validation);
                    return BuildCommandExitCodes.ValidationFailure;
                }

                BuildPipelineRunner runner = BuildPipelineRunner.StartNew(profile);
                BuildRunResult result = runner.Execute();
                return MapResultToExitCode(result);
            }
            finally
            {
                overrideScope.Restore();
            }
        }

        /// <summary>
        /// 按 GUID、资产路径或唯一名称定位 Profile 资产。
        /// 解析顺序：32 位十六进制 GUID → Assets/ 开头的资产路径 → 唯一名称。
        /// </summary>
        /// <param name="reference">定位引用。</param>
        /// <param name="error">定位失败时的错误描述。</param>
        /// <returns>找到的 Profile；失败时返回 null 并填充 error。</returns>
        public static UnityRFrameworkBuildProfile ResolveProfile(
            string reference,
            out string error)
        {
            error = string.Empty;
            string trimmed = (reference ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                error = "Profile 定位引用为空，请通过 -urfProfile 提供 GUID、资产路径或唯一名称。";
                return null;
            }

            if (IsUnityGuid(trimmed))
            {
                UnityRFrameworkBuildProfile byGuid =
                    BuildProfileEditorUtility.LoadByGuid(trimmed);
                if (byGuid != null)
                {
                    return byGuid;
                }

                error = $"未找到 GUID 为 '{trimmed}' 的 Profile 资产。";
                return null;
            }

            if (trimmed.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                UnityRFrameworkBuildProfile byPath =
                    AssetDatabase.LoadAssetAtPath<UnityRFrameworkBuildProfile>(trimmed);
                if (byPath != null)
                {
                    return byPath;
                }

                error = $"未找到资产路径 '{trimmed}' 对应的 Profile。";
                return null;
            }

            System.Collections.Generic.List<UnityRFrameworkBuildProfile> profiles =
                BuildProfileEditorUtility.FindAllProfiles();
            for (int i = 0; i < profiles.Count; i++)
            {
                if (string.Equals(
                        profiles[i].name,
                        trimmed,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return profiles[i];
                }
            }

            error = $"未找到名称为 '{trimmed}' 的 Profile，"
                + "请确认资产位于默认目录且名称唯一。";
            return null;
        }

        /// <summary>
        /// 将流水线执行结果映射为进程退出码。
        /// </summary>
        /// <param name="result">流水线执行结果。</param>
        /// <returns>进程退出码。</returns>
        public static int MapResultToExitCode(BuildRunResult result)
        {
            if (result == null)
            {
                return BuildCommandExitCodes.BuildFailure;
            }

            if (result.Succeeded)
            {
                return BuildCommandExitCodes.Success;
            }

            if (result.Cancelled)
            {
                return BuildCommandExitCodes.Cancelled;
            }

            return BuildCommandExitCodes.BuildFailure;
        }

        /// <summary>
        /// 判断字符串是否为 Unity 资产 GUID（32 位十六进制）。
        /// </summary>
        /// <param name="value">待判断字符串。</param>
        /// <returns>符合 GUID 格式时返回 true。</returns>
        private static bool IsUnityGuid(string value)
        {
            if (value.Length != 32)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                bool isHex = Uri.IsHexDigit(value[i]);
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 处理上次中断的残留任务：
        /// - 已完成任务：清理残留状态与锁后继续；
        /// - 同会话未完成任务（Domain Reload）：同步续跑并返回其结果；
        /// - 跨进程未完成任务：BatchMode 直接作废按构建失败退出，
        ///   GUI 模式不弹窗等待，同样按构建失败退出（请通过窗口处理）。
        /// </summary>
        /// <returns>退出码；Success 表示可以继续启动新构建。</returns>
        private static int HandleLeftoverTask()
        {
            BuildPipelinePersistence persistence = new BuildPipelinePersistence();
            BuildPipelineState leftover;
            try
            {
                leftover = persistence.LoadState();
            }
            catch (Exception exception)
            {
                Debug.LogError($"构建任务状态读取失败：{exception.Message}");
                return BuildCommandExitCodes.BuildFailure;
            }

            if (leftover == null)
            {
                return BuildCommandExitCodes.Success;
            }

            BuildRecoveryAction action = BuildPipelineRecovery.Evaluate(
                leftover,
                BuildPipelineRecovery.IsActiveInSession(),
                Application.isBatchMode);
            switch (action)
            {
                case BuildRecoveryAction.Cleanup:
                    CleanupLeftover(persistence);
                    return BuildCommandExitCodes.Success;

                case BuildRecoveryAction.Resume:
                    // 同会话残留（Domain Reload 中断）：同步续跑，不等待人工确认。
                    Debug.Log(
                        $"检测到同会话未完成构建任务（{leftover.TaskId}），"
                        + "将直接从检查点续跑。");
                    try
                    {
                        BuildPipelineRunner runner = BuildPipelineRunner.Resume(leftover);
                        BuildRunResult result = runner.Execute();
                        return MapResultToExitCode(result);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"残留任务恢复失败：{exception.Message}");
                        CleanupLeftover(persistence);
                        return BuildCommandExitCodes.BuildFailure;
                    }

                case BuildRecoveryAction.Abandon:
                    Debug.LogError(
                        $"检测到跨进程残留构建任务（{leftover.TaskId}），"
                        + "BatchMode 下不等待人工确认，任务作废，本次命令按构建失败退出。");
                    CleanupLeftover(persistence);
                    return BuildCommandExitCodes.BuildFailure;

                case BuildRecoveryAction.Prompt:
                    Debug.LogError(
                        $"检测到跨进程残留构建任务（{leftover.TaskId}），"
                        + "命令入口不弹窗等待人工确认，本次命令按构建失败退出，"
                        + "请打开构建工具窗口处理。");
                    return BuildCommandExitCodes.BuildFailure;

                default:
                    return BuildCommandExitCodes.BuildFailure;
            }
        }

        /// <summary>
        /// 清理残留任务的状态文件与锁。
        /// </summary>
        /// <param name="persistence">持久化实例。</param>
        private static void CleanupLeftover(BuildPipelinePersistence persistence)
        {
            persistence.ReleaseLock();
            persistence.DeleteState();
            BuildPipelineRunner.ClearActive();
        }

        /// <summary>
        /// 输出参数错误列表。
        /// </summary>
        /// <param name="errors">错误列表。</param>
        private static void LogErrors(System.Collections.Generic.IEnumerable<string> errors)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }
        }

        /// <summary>
        /// 输出校验问题（错误与警告）。
        /// </summary>
        /// <param name="validation">校验结果。</param>
        private static void LogValidationIssues(BuildValidationResult validation)
        {
            for (int i = 0; i < validation.Issues.Count; i++)
            {
                BuildValidationIssue issue = validation.Issues[i];
                string line = $"[{issue.Group}] {issue.Code}：{issue.Message}";
                if (issue.Level == BuildValidationLevel.Error)
                {
                    Debug.LogError(line);
                }
                else
                {
                    Debug.Log(line);
                }
            }
        }
    }

    /// <summary>
    /// 命令行覆盖项的运行期作用域：构造时把覆盖值写入 Profile 实例，
    /// <see cref="Restore"/> 时恢复原值。
    /// 构建号未被覆盖时保留流水线成功后的自动递增结果；
    /// 构建号被覆盖时恢复原值（由 CI 自行管理构建号，不消耗 Profile 计数器）。
    /// 恢复后对已保存资产执行 SetDirty 与 SaveAssets，防止覆盖值被流水线收尾持久化。
    /// </summary>
    public sealed class BuildProfileOverrideScope
    {
        /// <summary>被覆盖的 Profile 实例。</summary>
        private readonly UnityRFrameworkBuildProfile profile;

        /// <summary>覆盖前的输出根目录原值。</summary>
        private readonly string originalOutputRoot;

        /// <summary>覆盖前的公共版本号原值。</summary>
        private readonly string originalPublicVersion;

        /// <summary>覆盖前的平台构建号原值。</summary>
        private readonly int originalBuildNumber;

        /// <summary>覆盖前的 Clean Build 原值。</summary>
        private readonly bool originalCleanBeforeBuild;

        /// <summary>是否应用了任意覆盖项；无覆盖时 Restore 为空操作。</summary>
        private readonly bool hasAnyOverride;

        /// <summary>构建号是否被覆盖；被覆盖时恢复原值，否则保留自动递增结果。</summary>
        private readonly bool buildNumberOverridden;

        /// <summary>
        /// 创建覆盖作用域并立即应用覆盖项。
        /// </summary>
        /// <param name="profile">目标 Profile，不能为空。</param>
        /// <param name="arguments">已解析的命令行参数，不能为空。</param>
        public BuildProfileOverrideScope(
            UnityRFrameworkBuildProfile profile,
            BuildCommandLineArguments arguments)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            this.profile = profile;
            originalOutputRoot = profile.Output.OutputRoot;
            originalPublicVersion = profile.Platform.PublicVersion;
            originalBuildNumber = profile.Platform.BuildNumber;
            originalCleanBeforeBuild = profile.Output.CleanBeforeBuild;

            buildNumberOverridden = arguments.BuildNumberOverride.HasValue;
            hasAnyOverride = !string.IsNullOrEmpty(arguments.OutputRootOverride)
                || !string.IsNullOrEmpty(arguments.VersionOverride)
                || buildNumberOverridden
                || arguments.CleanBuildOverride.HasValue;

            if (!string.IsNullOrEmpty(arguments.OutputRootOverride))
            {
                profile.Output.OutputRoot = arguments.OutputRootOverride;
            }

            if (!string.IsNullOrEmpty(arguments.VersionOverride))
            {
                profile.Platform.PublicVersion = arguments.VersionOverride;
            }

            if (buildNumberOverridden)
            {
                profile.Platform.BuildNumber = arguments.BuildNumberOverride.Value;
            }

            if (arguments.CleanBuildOverride.HasValue)
            {
                profile.Output.CleanBeforeBuild = arguments.CleanBuildOverride.Value;
            }
        }

        /// <summary>
        /// 恢复 Profile 原值并持久化。
        /// 构建号未被覆盖时保留当前值（可能是流水线成功后的递增结果）。
        /// </summary>
        public void Restore()
        {
            if (!hasAnyOverride)
            {
                return;
            }

            profile.Output.OutputRoot = originalOutputRoot;
            profile.Platform.PublicVersion = originalPublicVersion;
            profile.Output.CleanBeforeBuild = originalCleanBeforeBuild;
            if (buildNumberOverridden)
            {
                profile.Platform.BuildNumber = originalBuildNumber;
            }

            string assetPath = AssetDatabase.GetAssetPath(profile);
            if (!string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
