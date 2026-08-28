using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建 Profile 资产的查找、加载与创建辅助。
    /// 资产约定存放于 <see cref="UnityRFrameworkBuildProfile.DefaultAssetDirectory"/>。
    /// </summary>
    public static class BuildProfileEditorUtility
    {
        /// <summary>
        /// 查找工程内全部构建 Profile 资产。
        /// </summary>
        /// <returns>按名称排序的 Profile 列表；目录不存在时返回空列表。</returns>
        public static List<UnityRFrameworkBuildProfile> FindAllProfiles()
        {
            List<UnityRFrameworkBuildProfile> result = new List<UnityRFrameworkBuildProfile>();
            if (!AssetDatabase.IsValidFolder(UnityRFrameworkBuildProfile.DefaultAssetDirectory))
            {
                return result;
            }

            string[] guids = AssetDatabase.FindAssets(
                "t:UnityRFrameworkBuildProfile",
                new[] { UnityRFrameworkBuildProfile.DefaultAssetDirectory });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityRFrameworkBuildProfile profile =
                    AssetDatabase.LoadAssetAtPath<UnityRFrameworkBuildProfile>(path);
                if (profile != null)
                {
                    MigrateProfile(profile);
                    result.Add(profile);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        /// <summary>
        /// 按 GUID 加载 Profile 资产。
        /// </summary>
        /// <param name="guid">资产 GUID。</param>
        /// <returns>找到时返回 Profile；GUID 无效或资产缺失时返回 null。</returns>
        public static UnityRFrameworkBuildProfile LoadByGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            UnityRFrameworkBuildProfile profile =
                AssetDatabase.LoadAssetAtPath<UnityRFrameworkBuildProfile>(path);
            MigrateProfile(profile);
            return profile;
        }

        /// <summary>
        /// 获取 Profile 资产的 GUID；资产未保存时返回空字符串。
        /// </summary>
        /// <param name="profile">目标 Profile。</param>
        /// <returns>资产 GUID；资产为 null 或未保存时返回空字符串。</returns>
        public static string GetGuid(UnityRFrameworkBuildProfile profile)
        {
            if (profile == null)
            {
                return string.Empty;
            }

            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
        }

        /// <summary>
        /// 在默认目录创建新的 Profile 资产。
        /// </summary>
        /// <param name="name">资产名称，不含扩展名；空白时使用默认名称。</param>
        /// <returns>创建成功的 Profile；创建失败时返回 null。</returns>
        public static UnityRFrameworkBuildProfile CreateProfile(string name)
        {
            string safeName = string.IsNullOrWhiteSpace(name)
                ? "BuildProfile"
                : name.Trim();

            if (!AssetDatabase.IsValidFolder(UnityRFrameworkBuildProfile.DefaultAssetDirectory))
            {
                Directory.CreateDirectory(UnityRFrameworkBuildProfile.DefaultAssetDirectory);
                AssetDatabase.Refresh();
            }

            UnityRFrameworkBuildProfile profile =
                ScriptableObject.CreateInstance<UnityRFrameworkBuildProfile>();
            string path =
                $"{UnityRFrameworkBuildProfile.DefaultAssetDirectory}/{safeName}.asset";

            AssetDatabase.CreateAsset(profile, path);
            MigrateProfile(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }

        /// <summary>
        /// 迁移 Profile 数据模型，并为已导入步骤建立独立配置子资产。
        /// </summary>
        public static bool MigrateProfile(UnityRFrameworkBuildProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            int previousVersion = profile.SerializedVersion;
            profile.Migrate();
            bool changed = previousVersion != profile.SerializedVersion;
            if (profile.Steps == null)
            {
                return changed;
            }

            IReadOnlyList<IBuildPipelineStep> registered =
                BuildPipelineStepRegistry.GetAll();
            Dictionary<string, IBuildPipelineStep> byId =
                new Dictionary<string, IBuildPipelineStep>(
                    System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < registered.Count; i++)
            {
                byId[registered[i].Id] = registered[i];
            }

            bool migrateLegacyConfigurations =
                previousVersion < UnityRFrameworkBuildProfile.CurrentSerializedVersion;
            for (int i = 0; i < profile.Steps.Count; i++)
            {
                BuildStepSettings entry = profile.Steps[i];
                if (!migrateLegacyConfigurations
                    || entry == null
                    || entry.Configuration != null
                    || string.IsNullOrWhiteSpace(entry.StepId)
                    || !byId.TryGetValue(entry.StepId, out IBuildPipelineStep step)
                    || step.ConfigurationType == null
                    || !typeof(ScriptableObject).IsAssignableFrom(step.ConfigurationType))
                {
                    continue;
                }

                ScriptableObject configuration =
                    ScriptableObject.CreateInstance(step.ConfigurationType);
                configuration.name = entry.StepId + " Configuration";
                string legacyJson =
                    profile.GetLegacyStepConfigurationJson(entry.StepId);
                if (!string.IsNullOrEmpty(legacyJson))
                {
                    JsonUtility.FromJsonOverwrite(legacyJson, configuration);
                }

                entry.Configuration = configuration;
                AttachConfigurationToProfile(profile, configuration);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            return changed;
        }

        /// <summary>
        /// 为指定步骤显式创建独立配置子资产；无配置类型或已有配置时不创建。
        /// </summary>
        /// <param name="profile">配置所属 Profile。</param>
        /// <param name="stepId">步骤唯一 Id。</param>
        /// <returns>已存在或新创建的配置；无法创建时返回 null。</returns>
        public static ScriptableObject CreateStepConfiguration(
            UnityRFrameworkBuildProfile profile,
            string stepId)
        {
            if (profile == null || string.IsNullOrWhiteSpace(stepId))
            {
                return null;
            }

            BuildStepSettings entry = BuildStepConfigLocator.FindEntry(profile, stepId);
            if (entry == null)
            {
                return null;
            }

            if (entry.Configuration != null)
            {
                return entry.Configuration;
            }

            IReadOnlyList<IBuildPipelineStep> registered =
                BuildPipelineStepRegistry.GetAll();
            for (int i = 0; i < registered.Count; i++)
            {
                IBuildPipelineStep step = registered[i];
                if (!string.Equals(
                        step.Id,
                        stepId,
                        System.StringComparison.OrdinalIgnoreCase)
                    || step.ConfigurationType == null
                    || !typeof(ScriptableObject).IsAssignableFrom(
                        step.ConfigurationType))
                {
                    continue;
                }

                ScriptableObject configuration =
                    ScriptableObject.CreateInstance(step.ConfigurationType);
                configuration.name = step.Id + " Configuration";
                entry.Configuration = configuration;
                AttachConfigurationToProfile(profile, configuration);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                return configuration;
            }

            return null;
        }

        /// <summary>
        /// 将配置作为 Profile 子资产保存；内存 Profile 保持内存对象引用。
        /// </summary>
        private static void AttachConfigurationToProfile(
            UnityRFrameworkBuildProfile profile,
            ScriptableObject configuration)
        {
            string profilePath = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(profilePath))
            {
                return;
            }

            AssetDatabase.AddObjectToAsset(configuration, profile);
            EditorUtility.SetDirty(configuration);
        }
    }

    /// <summary>
    /// Domain Reload 后延迟迁移现有 Profile，避免旧资产长期停留在版本 1。
    /// </summary>
    [InitializeOnLoad]
    internal static class BuildProfileMigrationInitializer
    {
        static BuildProfileMigrationInitializer()
        {
            EditorApplication.delayCall += MigrateAll;
        }

        private static void MigrateAll()
        {
            BuildProfileEditorUtility.FindAllProfiles();
        }
    }
}
