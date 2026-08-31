using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建方案，决定本次任务需要经过的固定阶段。
    /// </summary>
    public enum BuildRecipe
    {
        /// <summary>只构建 Unity Player。</summary>
        Player,

        /// <summary>只构建配置与资源。</summary>
        Assets,

        /// <summary>只构建代码和资源热更新内容。</summary>
        HotUpdate,

        /// <summary>执行完整发布流程。</summary>
        Release
    }

    /// <summary>
    /// 构建流水线固定阶段。Expansion 只能注册到这些阶段，不能改变阶段顺序。
    /// </summary>
    public enum BuildPipelineStage
    {
        Validate,
        SwitchTarget,
        ApplySettings,
        PrepareData,
        PrepareCode,
        BuildAssets,
        PreparePlayer,
        BuildPlayer,
        Finalize
    }

    /// <summary>
    /// Recipe 解析结果，包含已排序步骤和阻止执行的契约问题。
    /// </summary>
    public sealed class BuildRecipePlan
    {
        private readonly List<IBuildPipelineStep> steps;
        private readonly List<BuildValidationIssue> issues;
        private readonly List<string> stepIds;

        internal BuildRecipePlan(
            List<IBuildPipelineStep> steps,
            List<BuildValidationIssue> issues)
        {
            this.steps = steps ?? new List<IBuildPipelineStep>();
            this.issues = issues ?? new List<BuildValidationIssue>();
            stepIds = new List<string>(this.steps.Count);
            for (int i = 0; i < this.steps.Count; i++)
            {
                stepIds.Add(this.steps[i].Id);
            }
        }

        public IReadOnlyList<IBuildPipelineStep> Steps => steps;

        public IReadOnlyList<string> StepIds => stepIds;

        public IReadOnlyList<BuildValidationIssue> Issues => issues;

        public bool IsValid
        {
            get
            {
                for (int i = 0; i < issues.Count; i++)
                {
                    if (issues[i].Level == BuildValidationLevel.Error)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool Contains(string stepId)
        {
            return stepIds.Contains(stepId);
        }
    }

    /// <summary>
    /// 将 Profile、Recipe 和已注册步骤解析为确定执行计划。
    /// </summary>
    public static class BuildRecipePlanner
    {
        private const string IssueCode = "BUILD_RECIPE";
        private const string IssueGroup = "构建方案";

        /// <summary>
        /// 将 Profile、Recipe 和已注册步骤解析为确定执行计划。
        /// </summary>
        /// <param name="profile">构建配置。</param>
        /// <param name="availableSteps">可用步骤集合；为空时从注册表获取。</param>
        /// <param name="requireCoreSteps">是否强制校验 Recipe 必备核心步骤。</param>
        /// <param name="recipeOverride">运行期 Recipe 覆盖；为空时使用 Profile 保存的 Recipe，不修改 Profile 资产。</param>
        /// <returns>Recipe 执行计划。</returns>
        public static BuildRecipePlan Create(
            UnityRFrameworkBuildProfile profile,
            IEnumerable<IBuildPipelineStep> availableSteps = null,
            bool requireCoreSteps = true,
            BuildRecipe? recipeOverride = null)
        {
            BuildRecipe recipe = recipeOverride ?? profile.Recipe;
            List<BuildValidationIssue> issues = new List<BuildValidationIssue>();
            List<IBuildPipelineStep> empty = new List<IBuildPipelineStep>();
            if (profile == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    IssueCode,
                    "构建 Profile 为空，无法解析 Recipe。",
                    IssueGroup));
                return new BuildRecipePlan(empty, issues);
            }

            Dictionary<string, IBuildPipelineStep> registered =
                BuildStepMap(availableSteps ?? BuildPipelineStepRegistry.GetAll(), issues);
            ValidateEnabledEntries(profile, registered, issues);

            HashSet<string> selectedIds = SelectStepIds(
                profile,
                registered,
                recipe,
                selectAllAvailable: !requireCoreSteps);
            if (requireCoreSteps)
            {
                ValidateRequiredCoreSteps(
                    recipe,
                    registered,
                    selectedIds,
                    issues);
            }
            ValidateDependencies(registered, selectedIds, issues);

            List<IBuildPipelineStep> sorted = TopologicalSort(
                registered,
                selectedIds,
                issues);
            return new BuildRecipePlan(sorted, issues);
        }

        private static Dictionary<string, IBuildPipelineStep> BuildStepMap(
            IEnumerable<IBuildPipelineStep> availableSteps,
            ICollection<BuildValidationIssue> issues)
        {
            Dictionary<string, IBuildPipelineStep> result =
                new Dictionary<string, IBuildPipelineStep>(StringComparer.OrdinalIgnoreCase);
            foreach (IBuildPipelineStep step in availableSteps)
            {
                if (step == null || string.IsNullOrWhiteSpace(step.Id))
                {
                    issues.Add(BuildValidationIssue.Error(
                        IssueCode,
                        "发现 null 或 Id 为空的构建步骤实现。",
                        IssueGroup));
                    continue;
                }

                if (result.ContainsKey(step.Id))
                {
                    issues.Add(BuildValidationIssue.Error(
                        IssueCode,
                        $"构建步骤 Id '{step.Id}' 存在重复实现。",
                        IssueGroup));
                    continue;
                }

                result.Add(step.Id, step);
            }

            return result;
        }

        private static void ValidateEnabledEntries(
            UnityRFrameworkBuildProfile profile,
            IReadOnlyDictionary<string, IBuildPipelineStep> registered,
            ICollection<BuildValidationIssue> issues)
        {
            if (profile.Steps == null)
            {
                return;
            }

            for (int i = 0; i < profile.Steps.Count; i++)
            {
                BuildStepSettings entry = profile.Steps[i];
                if (entry == null || !entry.Enabled || string.IsNullOrWhiteSpace(entry.StepId))
                {
                    continue;
                }

                if (!registered.TryGetValue(entry.StepId, out IBuildPipelineStep step))
                {
                    issues.Add(BuildValidationIssue.Error(
                        IssueCode,
                        $"Profile 已启用步骤 '{entry.StepId}'，但当前工程没有对应实现。",
                        IssueGroup));
                    continue;
                }

                Type expectedType = step.ConfigurationType;
                if (expectedType == null)
                {
                    continue;
                }

                if (entry.Configuration == null)
                {
                    issues.Add(BuildValidationIssue.Error(
                        IssueCode,
                        $"步骤 '{entry.StepId}' 已启用，但未绑定 {expectedType.Name} 配置资产。",
                        IssueGroup));
                }
                else if (!expectedType.IsInstanceOfType(entry.Configuration))
                {
                    issues.Add(BuildValidationIssue.Error(
                        IssueCode,
                        $"步骤 '{entry.StepId}' 的配置类型应为 {expectedType.Name}，"
                        + $"实际为 {entry.Configuration.GetType().Name}。",
                        IssueGroup));
                }
            }
        }

        private static HashSet<string> SelectStepIds(
            UnityRFrameworkBuildProfile profile,
            IReadOnlyDictionary<string, IBuildPipelineStep> registered,
            BuildRecipe recipe,
            bool selectAllAvailable)
        {
            HashSet<string> enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (profile.Steps != null)
            {
                for (int i = 0; i < profile.Steps.Count; i++)
                {
                    BuildStepSettings entry = profile.Steps[i];
                    if (entry != null && entry.Enabled && !string.IsNullOrWhiteSpace(entry.StepId))
                    {
                        enabled.Add(entry.StepId);
                    }
                }
            }

            HashSet<string> selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, IBuildPipelineStep> pair in registered)
            {
                bool isCore = pair.Key.StartsWith("core.", StringComparison.OrdinalIgnoreCase);
                bool isAutomatic = pair.Value is IAutomaticBuildPipelineStep automatic
                    && automatic.ShouldInclude(profile, recipe);
                if ((selectAllAvailable || isCore || enabled.Contains(pair.Key) || isAutomatic)
                    && IncludesStage(recipe, pair.Value.Stage))
                {
                    selected.Add(pair.Key);
                }
            }

            return selected;
        }

        private static void ValidateRequiredCoreSteps(
            BuildRecipe recipe,
            IReadOnlyDictionary<string, IBuildPipelineStep> registered,
            ISet<string> selected,
            ICollection<BuildValidationIssue> issues)
        {
            string[] required = recipe == BuildRecipe.Player || recipe == BuildRecipe.Release
                ? new[]
                {
                    "core.validate",
                    "core.switch-target",
                    "core.apply-profile",
                    "core.build-player",
                    "core.finalize"
                }
                : new[] { "core.validate", "core.finalize" };

            for (int i = 0; i < required.Length; i++)
            {
                string id = required[i];
                if (!registered.ContainsKey(id) || !selected.Contains(id))
                {
                    issues.Add(BuildValidationIssue.Error(
                        IssueCode,
                        $"Recipe {recipe} 缺少必要核心步骤 '{id}'。",
                        IssueGroup));
                }
            }
        }

        private static void ValidateDependencies(
            IReadOnlyDictionary<string, IBuildPipelineStep> registered,
            ISet<string> selected,
            ICollection<BuildValidationIssue> issues)
        {
            foreach (string id in selected)
            {
                IBuildPipelineStep step = registered[id];
                IReadOnlyList<string> dependencies = step.Dependencies;
                if (dependencies == null)
                {
                    continue;
                }

                for (int i = 0; i < dependencies.Count; i++)
                {
                    string dependencyId = dependencies[i];
                    if (string.IsNullOrWhiteSpace(dependencyId)
                        || !selected.Contains(dependencyId)
                        || !registered.TryGetValue(dependencyId, out IBuildPipelineStep dependency))
                    {
                        issues.Add(BuildValidationIssue.Error(
                            IssueCode,
                            $"步骤 '{id}' 依赖 '{dependencyId}'，但依赖未启用或未实现。",
                            IssueGroup));
                        continue;
                    }

                    if (dependency.Stage > step.Stage)
                    {
                        issues.Add(BuildValidationIssue.Error(
                            IssueCode,
                            $"步骤 '{id}' 依赖后置阶段步骤 '{dependencyId}'，固定阶段顺序无法满足。",
                            IssueGroup));
                    }
                }
            }
        }

        private static List<IBuildPipelineStep> TopologicalSort(
            IReadOnlyDictionary<string, IBuildPipelineStep> registered,
            ISet<string> selected,
            ICollection<BuildValidationIssue> issues)
        {
            Dictionary<string, int> incoming =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> outgoing =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in selected)
            {
                incoming[id] = 0;
                outgoing[id] = new List<string>();
            }

            foreach (string id in selected)
            {
                IReadOnlyList<string> dependencies = registered[id].Dependencies;
                if (dependencies == null)
                {
                    continue;
                }

                for (int i = 0; i < dependencies.Count; i++)
                {
                    string dependencyId = dependencies[i];
                    if (!selected.Contains(dependencyId))
                    {
                        continue;
                    }

                    incoming[id]++;
                    outgoing[dependencyId].Add(id);
                }
            }

            List<IBuildPipelineStep> ready = new List<IBuildPipelineStep>();
            foreach (string id in selected)
            {
                if (incoming[id] == 0)
                {
                    ready.Add(registered[id]);
                }
            }

            List<IBuildPipelineStep> result = new List<IBuildPipelineStep>(selected.Count);
            while (ready.Count > 0)
            {
                ready.Sort(BuildPipelineStepRegistry.CompareSteps);
                IBuildPipelineStep step = ready[0];
                ready.RemoveAt(0);
                result.Add(step);

                List<string> dependents = outgoing[step.Id];
                for (int i = 0; i < dependents.Count; i++)
                {
                    string dependentId = dependents[i];
                    incoming[dependentId]--;
                    if (incoming[dependentId] == 0)
                    {
                        ready.Add(registered[dependentId]);
                    }
                }
            }

            if (result.Count != selected.Count)
            {
                issues.Add(BuildValidationIssue.Error(
                    IssueCode,
                    "构建步骤存在循环依赖，无法生成确定执行计划。",
                    IssueGroup));
            }

            return result;
        }

        public static bool IncludesStage(BuildRecipe recipe, BuildPipelineStage stage)
        {
            switch (recipe)
            {
                case BuildRecipe.Player:
                    return stage == BuildPipelineStage.Validate
                        || stage == BuildPipelineStage.SwitchTarget
                        || stage == BuildPipelineStage.ApplySettings
                        || stage == BuildPipelineStage.PreparePlayer
                        || stage == BuildPipelineStage.BuildPlayer
                        || stage == BuildPipelineStage.Finalize;
                case BuildRecipe.Assets:
                    return stage == BuildPipelineStage.Validate
                        || stage == BuildPipelineStage.PrepareData
                        || stage == BuildPipelineStage.BuildAssets
                        || stage == BuildPipelineStage.Finalize;
                case BuildRecipe.HotUpdate:
                    return stage == BuildPipelineStage.Validate
                        || stage == BuildPipelineStage.PrepareCode
                        || stage == BuildPipelineStage.BuildAssets
                        || stage == BuildPipelineStage.Finalize;
                case BuildRecipe.Release:
                    return true;
                default:
                    return false;
            }
        }
    }
}
