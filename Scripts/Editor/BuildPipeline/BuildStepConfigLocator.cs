using System;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建步骤配置定位器：从 Profile.Steps 查找步骤条目。
    /// 步骤 Id 大小写不敏感匹配；未配置条目、条目为 null 时均返回 null，由步骤自行决定回退行为。
    /// 同时提供类型安全的配置资产读取，类型不匹配时返回 null。
    /// </summary>
    public static class BuildStepConfigLocator
    {
        /// <summary>
        /// 查找 Profile.Steps 中指定 Id 的步骤条目；未配置时返回 null。
        /// </summary>
        /// <param name="profile">构建配置，可为空。</param>
        /// <param name="stepId">步骤唯一 Id。</param>
        /// <returns>找到时返回条目；未配置或 profile 为空时返回 null。</returns>
        public static BuildStepSettings FindEntry(
            UnityRFrameworkBuildProfile profile,
            string stepId)
        {
            if (profile == null || profile.Steps == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(stepId))
            {
                return null;
            }

            for (int i = 0; i < profile.Steps.Count; i++)
            {
                BuildStepSettings setting = profile.Steps[i];
                if (setting == null)
                {
                    continue;
                }

                if (string.Equals(
                    setting.StepId,
                    stepId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return setting;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断 Profile 是否配置了指定步骤的已启用条目。
        /// 用于第三方步骤的可用性判断：仅当用户显式启用对应条目时才参与构建，
        /// 避免导入第三方 Expansion 但未配置 Profile 时意外执行第三方步骤。
        /// </summary>
        /// <param name="profile">构建配置，可为空。</param>
        /// <param name="stepId">步骤唯一 Id。</param>
        /// <returns>条目存在且已启用时返回 true。</returns>
        public static bool HasEnabledEntry(
            UnityRFrameworkBuildProfile profile,
            string stepId)
        {
            BuildStepSettings entry = FindEntry(profile, stepId);
            return entry != null && entry.Enabled;
        }

        /// <summary>
        /// 获取指定步骤的类型化配置资产。
        /// </summary>
        public static T GetConfiguration<T>(
            UnityRFrameworkBuildProfile profile,
            string stepId)
            where T : UnityEngine.ScriptableObject
        {
            BuildStepSettings entry = FindEntry(profile, stepId);
            return entry != null ? entry.Configuration as T : null;
        }
    }
}
