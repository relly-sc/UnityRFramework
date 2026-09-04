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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }

        /// <summary>
        /// 为指定步骤恢复或创建独立配置子资产；已有配置类型错误时自动修复引用。
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

                if (step.ConfigurationType.IsInstanceOfType(entry.Configuration))
                {
                    return entry.Configuration;
                }

                if (entry.Configuration != null)
                {
                    entry.Configuration = null;
                    EditorUtility.SetDirty(profile);
                }

                ScriptableObject reusable = FindReusableConfiguration(
                    profile,
                    step.Id,
                    step.ConfigurationType);
                if (reusable != null)
                {
                    entry.Configuration = reusable;
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                    return reusable;
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
        /// 查找删除步骤条目后仍保留在 Profile 内、且尚未被其他步骤引用的同类型配置子资产。
        /// 优先匹配标准名称，避免反复初始化产生重复配置。
        /// </summary>
        private static ScriptableObject FindReusableConfiguration(
            UnityRFrameworkBuildProfile profile,
            string stepId,
            System.Type configurationType)
        {
            string profilePath = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(profilePath) || configurationType == null)
            {
                return null;
            }

            string expectedName = stepId + " Configuration";
            ScriptableObject fallback = null;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(profilePath);
            for (int i = 0; i < assets.Length; i++)
            {
                ScriptableObject candidate = assets[i] as ScriptableObject;
                if (candidate == null
                    || candidate == profile
                    || !configurationType.IsInstanceOfType(candidate)
                    || IsConfigurationReferenced(profile, candidate))
                {
                    continue;
                }

                if (string.Equals(
                        candidate.name,
                        expectedName,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                fallback ??= candidate;
            }

            return fallback;
        }

        /// <summary>判断配置子资产是否已被 Profile 中任意步骤引用。</summary>
        private static bool IsConfigurationReferenced(
            UnityRFrameworkBuildProfile profile,
            ScriptableObject configuration)
        {
            if (profile.Steps == null)
            {
                return false;
            }

            for (int i = 0; i < profile.Steps.Count; i++)
            {
                if (profile.Steps[i]?.Configuration == configuration)
                {
                    return true;
                }
            }

            return false;
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

}
