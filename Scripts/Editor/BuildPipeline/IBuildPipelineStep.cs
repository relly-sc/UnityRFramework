using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建步骤执行结果状态。
    /// </summary>
    public enum BuildStepStatus
    {
        /// <summary>步骤成功完成。</summary>
        Succeeded,

        /// <summary>步骤失败，流水线立即停止。</summary>
        Failed,

        /// <summary>步骤被取消。</summary>
        Cancelled,

        /// <summary>步骤已发起编辑器操作，等待编译或 Domain Reload 后恢复。</summary>
        WaitingForEditor
    }

    /// <summary>
    /// 构建步骤执行结果。失败时携带完整异常用于检查点记录；
    /// 成功时可携带输出路径用于构建报告。
    /// </summary>
    public sealed class BuildStepResult
    {
        /// <summary>结果状态。</summary>
        public BuildStepStatus Status { get; }

        /// <summary>用户可见的结果消息。</summary>
        public string Message { get; }

        /// <summary>失败时的原始异常；成功或取消时为空。</summary>
        public Exception Exception { get; }

        /// <summary>步骤输出路径（相对或绝对）；未提供时为空字符串。</summary>
        public string OutputPath { get; }

        /// <summary>
        /// 创建执行结果。
        /// </summary>
        /// <param name="status">结果状态。</param>
        /// <param name="message">用户可见消息。</param>
        /// <param name="exception">失败异常，可为空。</param>
        /// <param name="outputPath">步骤输出路径，可为空。</param>
        private BuildStepResult(
            BuildStepStatus status,
            string message,
            Exception exception,
            string outputPath)
        {
            Status = status;
            Message = message ?? string.Empty;
            Exception = exception;
            OutputPath = outputPath ?? string.Empty;
        }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        /// <param name="message">成功消息。</param>
        /// <returns>成功结果。</returns>
        public static BuildStepResult Succeeded(string message)
        {
            return new BuildStepResult(
                BuildStepStatus.Succeeded,
                message,
                null,
                string.Empty);
        }

        /// <summary>
        /// 创建成功结果，并携带输出路径用于构建报告。
        /// </summary>
        /// <param name="message">成功消息。</param>
        /// <param name="outputPath">步骤输出路径，可为空。</param>
        /// <returns>成功结果。</returns>
        public static BuildStepResult Succeeded(
            string message,
            string outputPath)
        {
            return new BuildStepResult(
                BuildStepStatus.Succeeded,
                message,
                null,
                outputPath);
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        /// <param name="message">失败原因描述。</param>
        /// <param name="exception">原始异常，可为空。</param>
        /// <returns>失败结果。</returns>
        public static BuildStepResult Failed(string message, Exception exception)
        {
            return new BuildStepResult(
                BuildStepStatus.Failed,
                message,
                exception,
                string.Empty);
        }

        /// <summary>
        /// 创建取消结果。
        /// </summary>
        /// <param name="message">取消原因描述。</param>
        /// <returns>取消结果。</returns>
        public static BuildStepResult Cancelled(string message)
        {
            return new BuildStepResult(
                BuildStepStatus.Cancelled,
                message,
                null,
                string.Empty);
        }

        /// <summary>
        /// 创建等待编辑器恢复结果。
        /// </summary>
        public static BuildStepResult WaitingForEditor(string message)
        {
            return new BuildStepResult(
                BuildStepStatus.WaitingForEditor,
                message,
                null,
                string.Empty);
        }
    }

    /// <summary>
    /// 构建流水线步骤契约。核心与第三方 Expansion 通过实现本接口参与构建流程，
    /// 由 <see cref="BuildPipelineStepRegistry"/> 通过 TypeCache 自动发现。
    /// 实现要点：
    /// - 重跑需幂等：Domain Reload 后当前步骤可能被再次执行，重复执行不能产生错误副作用。
    /// - 步骤内部如需切换平台或触发编译，须保证动作前状态检查点已落盘（运行器已保证）。
    /// - 消息中不得包含密码、私钥或令牌；敏感信息只从环境变量读取并脱敏。
    /// </summary>
    public interface IBuildPipelineStep
    {
        /// <summary>获取步骤唯一 Id，与 Profile.Steps 中的步骤配置对应。</summary>
        string Id { get; }

        /// <summary>获取步骤显示名称，用于报告与进度展示。</summary>
        string DisplayName { get; }

        /// <summary>获取步骤所属固定阶段。</summary>
        BuildPipelineStage Stage { get; }

        /// <summary>获取步骤排序值，升序执行；相同 Order 按 Id 字典序确定顺序。</summary>
        int Order { get; }

        /// <summary>获取必须先成功完成的步骤 Id。</summary>
        IReadOnlyList<string> Dependencies { get; }

        /// <summary>获取步骤要求的配置资产类型；无私有配置时为空。</summary>
        Type ConfigurationType { get; }

        /// <summary>获取是否会在执行中切换活动构建目标。</summary>
        bool SwitchesTarget { get; }

        /// <summary>获取是否会在执行中触发脚本编译。</summary>
        bool TriggersCompilation { get; }

        /// <summary>获取是否会在执行中触发 Domain Reload。</summary>
        bool TriggersDomainReload { get; }

        /// <summary>获取是否会调用 Unity BuildPipeline 执行 Player 构建。</summary>
        bool CallsBuildPipeline { get; }

        /// <summary>获取失败后是否允许在修复后重试本步骤。</summary>
        bool CanRetry { get; }

        /// <summary>获取失败后是否需要人工清理残留（如未完成的输出目录）。</summary>
        bool RequiresManualCleanup { get; }

        /// <summary>
        /// 判断步骤是否可用于当前构建上下文。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>可用时返回 true。</returns>
        bool CanRun(BuildPipelineContext context);

        /// <summary>
        /// 执行前置校验，向集合追加问题条目。
        /// 实现必须保证只读，不修改 Profile、PlayerSettings、场景或输出目录。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        void Validate(
            BuildPipelineContext context,
            ICollection<BuildValidationIssue> issues);

        /// <summary>
        /// 执行步骤主体逻辑。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>步骤执行结果。</returns>
        BuildStepResult Execute(BuildPipelineContext context);
    }

    /// <summary>
    /// 无需用户单独创建步骤条目的自动步骤。实现根据 Profile 决定是否加入 Recipe，
    /// 用于承载由已启用扩展步骤隐式要求的准备动作。
    /// </summary>
    public interface IAutomaticBuildPipelineStep
    {
        /// <summary>判断当前 Profile 和实际 Recipe 是否需要自动加入该步骤。</summary>
        /// <param name="profile">构建配置。</param>
        /// <param name="recipe">本任务实际执行的 Recipe。</param>
        bool ShouldInclude(
            UnityRFrameworkBuildProfile profile,
            BuildRecipe recipe);
    }

    /// <summary>
    /// 构建步骤注册表：通过 TypeCache 自动发现全部 <see cref="IBuildPipelineStep"/> 实现。
    /// 带程序集级缓存；安装或卸载包后调用 <see cref="InvalidateCache"/> 重新扫描。
    /// 重复 Id 的步骤会被跳过并记录警告；列表按 Order 升序、同 Order 按 Id 字典序排序，
    /// 保证步骤顺序在多次扫描间确定不变。
    /// </summary>
    public static class BuildPipelineStepRegistry
    {
        /// <summary>缓存是否有效。</summary>
        private static bool cacheValid;

        /// <summary>步骤实例缓存。</summary>
        private static List<IBuildPipelineStep> cache;

        /// <summary>
        /// 使步骤缓存失效，下次查询时重新扫描程序集。
        /// 安装或卸载第三方包后应调用本方法。
        /// </summary>
        public static void InvalidateCache()
        {
            cacheValid = false;
            cache = null;
        }

        /// <summary>
        /// 获取全部已发现步骤；首次调用时执行程序集扫描。
        /// </summary>
        /// <returns>步骤只读列表；扫描失败时可能返回空列表。</returns>
        public static IReadOnlyList<IBuildPipelineStep> GetAll()
        {
            if (!cacheValid || cache == null)
            {
                RebuildCache();
            }
            return cache;
        }

        /// <summary>
        /// 步骤比较：Order 升序，相同 Order 按 Id 字典序。
        /// 供注册表排序与运行器对注入步骤列表做确定性排序复用。
        /// </summary>
        /// <param name="left">左步骤。</param>
        /// <param name="right">右步骤。</param>
        /// <returns>比较结果。</returns>
        public static int CompareSteps(IBuildPipelineStep left, IBuildPipelineStep right)
        {
            int stage = left.Stage.CompareTo(right.Stage);
            if (stage != 0)
            {
                return stage;
            }

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }
            return string.CompareOrdinal(left.Id, right.Id);
        }

        /// <summary>
        /// 重建步骤缓存：扫描程序集、实例化实现、去重并排序。
        /// </summary>
        private static void RebuildCache()
        {
            List<IBuildPipelineStep> built = new List<IBuildPipelineStep>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IBuildPipelineStep>())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                // 跳过测试程序集中的类型：测试假步骤不应进入生产注册表。
                string assemblyName = type.Assembly.GetName().Name;
                if (!string.IsNullOrEmpty(assemblyName)
                    && (assemblyName.EndsWith(
                            ".Tests",
                            StringComparison.Ordinal)
                        || assemblyName.IndexOf(
                            ".Tests.",
                            StringComparison.Ordinal) >= 0))
                {
                    continue;
                }

                IBuildPipelineStep instance;
                try
                {
                    instance = (IBuildPipelineStep)Activator.CreateInstance(type);
                }
                catch (Exception exception)
                {
                    Debug.Log(
                        $"构建步骤 {type.FullName} 实例化失败，已跳过：{exception.Message}");
                    continue;
                }

                string id = instance.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.Log(
                        $"构建步骤 {type.FullName} 的 Id 为空，已跳过。");
                    continue;
                }

                if (!ids.Add(id))
                {
                    Debug.Log(
                        $"构建步骤 Id '{id}' 重复（{type.FullName}），已跳过。");
                    continue;
                }

                built.Add(instance);
            }

            built.Sort(CompareSteps);
            cache = built;
            cacheValid = true;
        }
    }
}
