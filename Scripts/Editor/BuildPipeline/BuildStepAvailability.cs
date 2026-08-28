using System;
using System.Collections.Generic;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 基于步骤注册表的可用性查询。核心不维护第三方程序集或步骤白名单。
    /// </summary>
    public static class BuildStepAvailability
    {
        public static string GetFriendlyName(string stepId)
        {
            return TryFind(stepId, out IBuildPipelineStep step)
                ? step.DisplayName
                : stepId;
        }

        public static bool IsKnown(string stepId)
        {
            return TryFind(stepId, out _);
        }

        public static bool IsAvailable(string stepId)
        {
            return TryFind(stepId, out _);
        }

        public static string GetUnavailableReason(string stepId)
        {
            return IsAvailable(stepId)
                ? string.Empty
                : $"步骤 '{stepId}' 没有已加载的实现。请导入对应 Expansion，"
                  + "或在 Profile 中禁用该步骤。";
        }

        public static IReadOnlyList<(string Id, string FriendlyName)> EnumerateKnownSteps()
        {
            IReadOnlyList<IBuildPipelineStep> registered =
                BuildPipelineStepRegistry.GetAll();
            List<(string Id, string FriendlyName)> result =
                new List<(string Id, string FriendlyName)>(registered.Count);
            for (int i = 0; i < registered.Count; i++)
            {
                IBuildPipelineStep step = registered[i];
                if (step.Id.StartsWith("core.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add((step.Id, step.DisplayName));
            }

            return result;
        }

        public static void InvalidateCache()
        {
            BuildPipelineStepRegistry.InvalidateCache();
        }

        private static bool TryFind(
            string stepId,
            out IBuildPipelineStep result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(stepId))
            {
                return false;
            }

            IReadOnlyList<IBuildPipelineStep> registered =
                BuildPipelineStepRegistry.GetAll();
            for (int i = 0; i < registered.Count; i++)
            {
                if (string.Equals(
                        registered[i].Id,
                        stepId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result = registered[i];
                    return true;
                }
            }

            return false;
        }
    }
}
