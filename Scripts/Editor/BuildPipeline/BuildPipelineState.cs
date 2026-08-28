using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建流水线任务的当前阶段。
    /// </summary>
    public enum BuildPipelinePhase
    {
        /// <summary>任务进行中（含等待步骤执行）。</summary>
        Running,

        /// <summary>任务等待脚本重载或编译结束后恢复。</summary>
        WaitingForReload,

        /// <summary>任务已成功完成。</summary>
        Succeeded,

        /// <summary>任务已失败，流水线停止。</summary>
        Failed,

        /// <summary>任务已被取消。</summary>
        Cancelled,

        /// <summary>任务已被作废（用户放弃或无法安全恢复）。</summary>
        Abandoned
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
    /// </summary>
    [Serializable]
    public sealed class BuildPipelineState
    {
        /// <summary>当前序列化版本号，升级迁移时递增。</summary>
        public const int CurrentSerializedVersion = 1;

        /// <summary>序列化版本号。</summary>
        public int SerializedVersion = CurrentSerializedVersion;

        /// <summary>任务唯一 Id（Guid 无连字符形式）。</summary>
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

        /// <summary>任务阶段名称（BuildPipelinePhase 名称）。</summary>
        public string PhaseName = string.Empty;

        /// <summary>按执行顺序排列的全部步骤 Id。</summary>
        public List<string> StepIds = new List<string>();

        /// <summary>下一个待执行步骤在 StepIds 中的索引。</summary>
        public int CurrentStepIndex;

        /// <summary>已完成步骤的执行记录，按完成顺序排列。</summary>
        public List<BuildStepRecord> CompletedSteps = new List<BuildStepRecord>();

        /// <summary>失败步骤的执行记录；未失败时为空。</summary>
        public BuildStepRecord FailedStep;

        /// <summary>任务失败原因的用户可见描述。</summary>
        public string ErrorMessage = string.Empty;

        /// <summary>获取任务阶段。</summary>
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

        /// <summary>任务是否已进入终态（成功、失败、取消或作废）。</summary>
        public bool IsFinished
        {
            get
            {
                BuildPipelinePhase phase = Phase;
                return phase == BuildPipelinePhase.Succeeded
                    || phase == BuildPipelinePhase.Failed
                    || phase == BuildPipelinePhase.Cancelled
                    || phase == BuildPipelinePhase.Abandoned;
            }
        }
    }
}
