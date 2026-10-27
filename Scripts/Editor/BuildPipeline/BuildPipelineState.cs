using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建流水线任务的当前状态。状态机共七态：
    /// 运行、等待编辑器、失败、取消、成功、人工处理和作废。
    /// </summary>
    public enum BuildPipelinePhase
    {
        /// <summary>任务进行中（含等待步骤执行）。</summary>
        Running,

        /// <summary>任务等待编辑器完成编译、资源导入或 Domain Reload 后恢复。</summary>
        WaitingForEditor,

        /// <summary>任务已成功完成；成功后清理状态与锁。</summary>
        Succeeded,

        /// <summary>任务已失败；失败任务保留状态与锁，等待重试、继续或作废。</summary>
        Failed,

        /// <summary>任务已被用户取消；保留状态与锁，等待继续或作废。</summary>
        Cancelled,

        /// <summary>任务进入人工处理状态（如回滚失败），需要维护者处理后才能继续。</summary>
        ManualIntervention,

        /// <summary>任务已被作废（用户放弃或无法安全恢复）；作废后清理状态与锁。</summary>
        Abandoned
    }

    /// <summary>
    /// 构建设置回滚状态。记录设置事务的回滚进展，
    /// 供阶段 4 的设置事务消费；回滚失败时任务进入人工处理状态。
    /// </summary>
    public enum BuildRollbackState
    {
        /// <summary>本任务没有需要回滚的设置事务。</summary>
        NotRequired,

        /// <summary>已应用临时设置，构建结束后待回滚。</summary>
        Pending,

        /// <summary>设置已成功恢复。</summary>
        Succeeded,

        /// <summary>恢复失败，需要人工处理。</summary>
        Failed
    }

    /// <summary>
    /// 单个构建步骤的执行记录，是检查点的最小单元。
    /// 状态、消息与异常文本均为字符串，保证可被 JsonUtility 序列化落盘。
    /// </summary>
    [Serializable]
    public sealed class BuildStepRecord
    {
        /// <summary>步骤唯一 Id。</summary>
        public string StepId = string.Empty;

        /// <summary>步骤执行结果状态名称（BuildStepStatus 名称）。</summary>
        public string Status = string.Empty;

        /// <summary>步骤返回的用户可见消息。</summary>
        public string Message = string.Empty;

        /// <summary>步骤输出路径；未提供时为空字符串。</summary>
        public string OutputPath = string.Empty;

        /// <summary>步骤失败时的完整异常文本；成功时为空字符串。</summary>
        public string ExceptionText = string.Empty;

        /// <summary>步骤开始时刻（ISO 8601 字符串）。</summary>
        public string StartedAt = string.Empty;

        /// <summary>步骤结束时刻（ISO 8601 字符串）。</summary>
        public string FinishedAt = string.Empty;
    }

    /// <summary>
    /// 构建流水线任务状态，持久化为 Library/UnityRFramework/BuildPipeline/task.json。
    /// 全部字段使用可序列化基元类型与字符串，JsonUtility 可直接往返。
    /// 状态绑定唯一 TaskId；会话标记同时保存该 TaskId，恢复前必须匹配。
    /// </summary>
    [Serializable]
    public sealed class BuildPipelineState
    {
        /// <summary>当前序列化版本号。版本 3 引入实际 Recipe 与任务级覆盖。</summary>
        public const int CurrentSerializedVersion = 3;

        /// <summary>序列化版本号；与当前版本不一致时拒绝恢复。</summary>
        public int SerializedVersion = CurrentSerializedVersion;

        /// <summary>任务唯一 Id（Guid 无连字符形式）；会话标记与锁文件都引用该 Id。</summary>
        public string TaskId = string.Empty;

        /// <summary>构建配置资产的 GUID；资产缺失时为空字符串。</summary>
        public string ProfileGuid = string.Empty;

        /// <summary>构建配置资产的工程相对路径；资产缺失时为空字符串。</summary>
        public string ProfileAssetPath = string.Empty;

        /// <summary>构建配置资产名称。</summary>
        public string ProfileName = string.Empty;

        /// <summary>目标平台名称（BuildTarget 名称）。</summary>
        public string TargetName = string.Empty;

        /// <summary>构建用途分档名称（BuildProfileFlavor 名称）。</summary>
        public string FlavorName = string.Empty;

        /// <summary>本任务实际 Recipe 名称，恢复时不再读取 Profile 当前值。</summary>
        public string RecipeName = BuildRecipe.Player.ToString();

        /// <summary>本任务命令行覆盖；恢复时重新应用到内存 Profile 副本。</summary>
        public BuildTaskOverrides TaskOverrides = new BuildTaskOverrides();

        /// <summary>临时构建设置是否已开始应用，用于 Domain Reload 后决定回滚。</summary>
        public bool SettingsApplied;

        /// <summary>本任务是否已成功产出 Player。</summary>
        public bool PlayerProduced;

        /// <summary>任务创建时冻结的公共版本号。</summary>
        public string PublicVersion = string.Empty;

        /// <summary>任务创建时冻结的平台构建号。</summary>
        public int BuildNumber;

        /// <summary>输出根目录绝对路径；解析失败时为空字符串。</summary>
        public string OutputRootAbsolute = string.Empty;

        /// <summary>解析后的输出目录（相对输出根）；失败时为空字符串。</summary>
        public string OutputDirectory = string.Empty;

        /// <summary>解析后的输出文件名（不含扩展名）；失败时为空字符串。</summary>
        public string OutputFileName = string.Empty;

        /// <summary>任务创建时刻（ISO 8601 字符串）。</summary>
        public string CreatedAt = string.Empty;

        /// <summary>任务最近更新时刻（ISO 8601 字符串）。</summary>
        public string UpdatedAt = string.Empty;

        /// <summary>任务状态名称（BuildPipelinePhase 名称）。</summary>
        public string PhaseName = string.Empty;

        /// <summary>
        /// 任务处于等待编辑器状态时的原因说明
        /// （如"等待脚本编译"、"等待资源导入"、步骤返回的等待消息）；非等待状态为空字符串。
        /// </summary>
        public string WaitingReason = string.Empty;

        /// <summary>设置回滚状态名称（BuildRollbackState 名称）。</summary>
        public string RollbackStateName = BuildRollbackState.NotRequired.ToString();

        /// <summary>按执行顺序排列的全部步骤 Id。</summary>
        public List<string> StepIds = new List<string>();

        /// <summary>下一个待执行步骤在 StepIds 中的索引。</summary>
        public int CurrentStepIndex;

        /// <summary>已完成（已提交）步骤的执行记录，按完成顺序排列。</summary>
        public List<BuildStepRecord> CompletedSteps = new List<BuildStepRecord>();

        /// <summary>失败步骤的执行记录；未失败时为空。</summary>
        public BuildStepRecord FailedStep;

        /// <summary>
        /// 失败、人工处理或作废原因的用户可见描述；其他状态为空字符串。
        /// </summary>
        public string ErrorMessage = string.Empty;

        /// <summary>获取或设置任务状态。</summary>
        public BuildPipelinePhase Phase
        {
            get
            {
                if (Enum.TryParse(PhaseName, out BuildPipelinePhase value))
                {
                    return value;
                }
                return BuildPipelinePhase.Running;
            }
            set
            {
                PhaseName = value.ToString();
            }
        }

        /// <summary>获取或设置设置回滚状态。</summary>
        public BuildRollbackState RollbackState
        {
            get
            {
                if (Enum.TryParse(RollbackStateName, out BuildRollbackState value))
                {
                    return value;
                }
                return BuildRollbackState.NotRequired;
            }
            set
            {
                RollbackStateName = value.ToString();
            }
        }

        /// <summary>获取或设置本任务实际 Recipe。</summary>
        public BuildRecipe Recipe
        {
            get
            {
                return Enum.TryParse(RecipeName, out BuildRecipe value)
                    ? value
                    : BuildRecipe.Player;
            }
            set
            {
                RecipeName = value.ToString();
            }
        }

        /// <summary>
        /// 任务是否已进入终态。终态任务不再执行任何步骤：
        /// 成功或作废；失败、取消与人工处理保留状态等待后续决策，不属于终态。
        /// </summary>
        public bool IsTerminal
        {
            get
            {
                BuildPipelinePhase phase = Phase;
                return phase == BuildPipelinePhase.Succeeded
                    || phase == BuildPipelinePhase.Abandoned;
            }
        }

        /// <summary>
        /// 本次运行是否已结束（既非运行也非等待编辑器）。
        /// 失败、取消、人工处理与终态任务都满足该条件，可以重新启动新任务决策流程。
        /// </summary>
        public bool HasEndedRun
        {
            get
            {
                BuildPipelinePhase phase = Phase;
                return phase != BuildPipelinePhase.Running
                    && phase != BuildPipelinePhase.WaitingForEditor;
            }
        }

    }
}
