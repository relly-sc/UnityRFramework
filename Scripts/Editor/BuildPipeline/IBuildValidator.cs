using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建前校验器契约：第三方 Expansion 通过实现本接口参与构建前校验，
    /// 核心程序集不引用任何第三方类型。实现类会被自动发现并实例化。
    /// Config/Localization、StreamingAssets、YooAsset、HybridCLR 等检查
    /// 由各自核心或 Expansion 实现本接口注册，不在构建工具中复制其业务逻辑。
    /// </summary>
    public interface IBuildValidator
    {
        /// <summary>获取校验器唯一 Id，用于报告归并与重复检测。</summary>
        string Id { get; }

        /// <summary>
        /// 执行校验并向集合追加问题条目。
        /// 实现必须保证：只读、不修改 Profile、PlayerSettings、场景或输出目录。
        /// </summary>
        /// <param name="context">校验上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        void Validate(
            BuildValidationContext context,
            ICollection<BuildValidationIssue> issues);
    }

    /// <summary>
    /// 构建前校验上下文，承载 Profile 与已解析的工程、输出信息。
    /// 输出相关字段在主校验器解析完成后填充；解析失败时字段为空字符串，
    /// 第三方校验器不应假设这些字段非空。
    /// </summary>
    public sealed class BuildValidationContext
    {
        /// <summary>
        /// 创建校验上下文。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="activeTarget">当前活动构建目标。</param>
        /// <param name="projectRoot">Unity 工程根目录绝对路径。</param>
        /// <param name="outputRootAbsolute">输出根目录绝对路径；解析失败时为空字符串。</param>
        /// <param name="outputDirectory">解析后的输出目录（相对输出根）；失败时为空字符串。</param>
        /// <param name="outputFileName">解析后的输出文件名（不含扩展名）；失败时为空字符串。</param>
        /// <param name="recipe">本次实际构建方案；为空时使用 Profile 保存值。</param>
        public BuildValidationContext(
            UnityRFrameworkBuildProfile profile,
            BuildTarget activeTarget,
            string projectRoot,
            string outputRootAbsolute,
            string outputDirectory,
            string outputFileName,
            BuildRecipe? recipe = null)
        {
            Profile = profile;
            ActiveTarget = activeTarget;
            Recipe = recipe ?? profile?.Recipe ?? BuildRecipe.Player;
            ProjectRoot = projectRoot ?? string.Empty;
            OutputRootAbsolute = outputRootAbsolute ?? string.Empty;
            OutputDirectory = outputDirectory ?? string.Empty;
            OutputFileName = outputFileName ?? string.Empty;
        }

        /// <summary>获取待校验的构建配置。</summary>
        public UnityRFrameworkBuildProfile Profile { get; }

        /// <summary>获取当前活动构建目标。</summary>
        public BuildTarget ActiveTarget { get; }

        /// <summary>获取本次校验实际使用的构建方案。</summary>
        public BuildRecipe Recipe { get; }

        /// <summary>获取当前活动构建目标对应的组。</summary>
        public BuildTargetGroup ActiveGroup
        {
            get
            {
                return BuildPipeline.GetBuildTargetGroup(ActiveTarget);
            }
        }

        /// <summary>获取 Unity 工程根目录绝对路径。</summary>
        public string ProjectRoot { get; }

        /// <summary>获取输出根目录绝对路径；解析失败时为空字符串。</summary>
        public string OutputRootAbsolute { get; }

        /// <summary>获取解析后的输出目录（相对输出根）；失败时为空字符串。</summary>
        public string OutputDirectory { get; }

        /// <summary>获取解析后的输出文件名（不含扩展名）；失败时为空字符串。</summary>
        public string OutputFileName { get; }
    }

    /// <summary>
    /// 构建校验器注册表：通过 TypeCache 自动发现全部 <see cref="IBuildValidator"/> 实现。
    /// 带程序集级缓存；安装或卸载包后调用 <see cref="InvalidateCache"/> 重新扫描。
    /// 实例化失败或 Id 重复的校验器会被跳过并记录警告，不影响其余校验器执行。
    /// </summary>
    public static class BuildValidatorRegistry
    {
        /// <summary>缓存是否有效。</summary>
        private static bool cacheValid;

        /// <summary>校验器实例缓存。</summary>
        private static List<IBuildValidator> cache;

        /// <summary>
        /// 使校验器缓存失效，下次查询时重新扫描程序集。
        /// 安装或卸载第三方包后应调用本方法。
        /// </summary>
        public static void InvalidateCache()
        {
            cacheValid = false;
            cache = null;
        }

        /// <summary>
        /// 获取全部已发现的校验器；首次调用时执行程序集扫描。
        /// </summary>
        /// <returns>校验器只读列表；扫描失败时可能返回空列表。</returns>
        public static IReadOnlyList<IBuildValidator> GetAll()
        {
            if (!cacheValid || cache == null)
            {
                RebuildCache();
            }

            return cache;
        }

        /// <summary>
        /// 重建校验器缓存：扫描程序集、实例化实现并去重。
        /// </summary>
        private static void RebuildCache()
        {
            List<IBuildValidator> built = new List<IBuildValidator>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IBuildValidator>())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                IBuildValidator instance;
                try
                {
                    instance = (IBuildValidator)Activator.CreateInstance(type);
                }
                catch (Exception exception)
                {
                    Debug.Log(
                        $"构建校验器 {type.FullName} 实例化失败，已跳过：{exception.Message}");
                    continue;
                }

                string id = instance.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.Log(
                        $"构建校验器 {type.FullName} 的 Id 为空，已跳过。");
                    continue;
                }

                if (!ids.Add(id))
                {
                    Debug.Log(
                        $"构建校验器 Id '{id}' 重复（{type.FullName}），已跳过。");
                    continue;
                }

                built.Add(instance);
            }

            built.Sort(
                (left, right) =>
                    string.CompareOrdinal(left.Id, right.Id));

            cache = built;
            cacheValid = true;
        }
    }
}
