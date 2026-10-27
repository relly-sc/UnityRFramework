using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建步骤抽象基类，提供能力声明的默认值。
    /// 派生类只需实现 Id 与 Execute；大多数步骤直接继承本类。
    /// </summary>
    public abstract class BuildPipelineStepBase : IBuildPipelineStep
    {
        private static readonly IReadOnlyList<string> EmptyDependencies =
            Array.Empty<string>();

        /// <summary>获取步骤唯一 Id。</summary>
        public abstract string Id { get; }

        /// <summary>获取步骤显示名称，默认与 Id 相同。</summary>
        public virtual string DisplayName
        {
            get
            {
                return Id;
            }
        }

        /// <summary>获取步骤所属固定阶段；自定义步骤默认归入数据准备阶段。</summary>
        public virtual BuildPipelineStage Stage => BuildPipelineStage.PrepareData;

        /// <summary>获取步骤排序值，默认 0。</summary>
        public virtual int Order
        {
            get
            {
                return 0;
            }
        }

        /// <summary>默认无步骤依赖。</summary>
        public virtual IReadOnlyList<string> Dependencies => EmptyDependencies;

        /// <summary>默认不要求私有配置资产。</summary>
        public virtual Type ConfigurationType => null;

        /// <summary>获取是否切换活动构建目标，默认否。</summary>
        public virtual bool SwitchesTarget
        {
            get
            {
                return false;
            }
        }

        /// <summary>获取是否触发脚本编译，默认否。</summary>
        public virtual bool TriggersCompilation
        {
            get
            {
                return false;
            }
        }

        /// <summary>获取是否触发 Domain Reload，默认否。</summary>
        public virtual bool TriggersDomainReload
        {
            get
            {
                return false;
            }
        }

        /// <summary>获取是否调用 Unity BuildPipeline，默认否。</summary>
        public virtual bool CallsBuildPipeline
        {
            get
            {
                return false;
            }
        }

        /// <summary>获取失败后是否允许重试，默认允许。</summary>
        public virtual bool CanRetry
        {
            get
            {
                return true;
            }
        }

        /// <summary>获取失败后是否需要人工清理，默认否。</summary>
        public virtual bool RequiresManualCleanup
        {
            get
            {
                return false;
            }
        }

        /// <summary>默认步骤始终可用于任何上下文。</summary>
        public virtual bool CanRun(BuildPipelineContext context)
        {
            return true;
        }

        /// <summary>默认步骤无前置校验。</summary>
        public virtual void Validate(
            BuildPipelineContext context,
            ICollection<BuildValidationIssue> issues)
        {
        }

        /// <summary>执行步骤主体逻辑。</summary>
        public abstract BuildStepResult Execute(BuildPipelineContext context);
    }
}
