using System;
using UnityEditor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建版本解析器：提供公共版本号与构建号的组合展示文本，以及构建成功后的
    /// 构建号自动递增提交。递增只允许在构建成功后调用，失败与取消的构建
    /// 不得调用本方法，避免消耗正式版本号。
    /// </summary>
    public static class BuildVersionResolver
    {
        /// <summary>
        /// 解析版本展示文本，用于报告与窗口展示。
        /// </summary>
        /// <param name="profile">构建配置，可为空。</param>
        /// <returns>版本展示文本；配置为空时返回空字符串。</returns>
        public static string ResolveDisplayVersion(
            UnityRFrameworkBuildProfile profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            return $"{profile.Platform.PublicVersion} "
                + $"(Build {profile.Platform.BuildNumber})";
        }

        /// <summary>
        /// 构建成功后提交构建号：开启自动递增时将构建号加一并持久化。
        /// 仅实际成功完成的构建调用；失败与取消的构建不消耗正式版本号。
        /// </summary>
        /// <param name="profile">构建配置。</param>
        /// <param name="persistAssets">是否持久化资产；测试注入临时配置时传 false 仅修改内存字段。</param>
        public static void CommitBuildNumber(
            UnityRFrameworkBuildProfile profile,
            bool persistAssets = true)
        {
            if (profile == null || !profile.Platform.AutoIncrementBuildNumber)
            {
                return;
            }

            profile.Platform.BuildNumber++;
            if (persistAssets)
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
