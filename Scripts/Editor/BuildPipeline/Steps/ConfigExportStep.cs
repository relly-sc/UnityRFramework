using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// Config 配置导出步骤：复用 ConfigPipelineService 全量导出 Config 与 Localization。
    /// 从步骤条目绑定的 <see cref="ConfigExportBuildConfiguration"/> 读取导出路径与格式；
    /// Config 和 Localization 的 JSON、二进制内容都保留，由对应 Helper 决定运行时读取格式。
    /// 正式内容格式由 ConfigReleaseFormat 决定。导出失败立即停止，不使用旧配置产物继续构建。
    /// </summary>
    public sealed class ConfigExportStep : BuildPipelineStepBase
    {
        /// <summary>错误码：配置导出步骤。</summary>
        private const string StepCode = "CONFIG";

        /// <summary>校验分组：配置导出。</summary>
        private const string StepGroup = "配置导出";

        /// <summary>获取步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "config";
            }
        }

        /// <summary>获取步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "Config 配置导出";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.PrepareData;

        public override Type ConfigurationType =>
            typeof(ConfigExportBuildConfiguration);

        /// <summary>获取步骤排序值：位于切平台之后、应用参数之前。</summary>
        public override int Order
        {
            get
            {
                return 15;
            }
        }

        /// <summary>
        /// 判断步骤是否可用于当前构建上下文：仅要求 Profile 存在。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>Profile 非空时返回 true。</returns>
        public override bool CanRun(BuildPipelineContext context)
        {
            return context != null && context.Profile != null;
        }

        /// <summary>
        /// 执行前置校验：只读检查源目录存在性、输出目录区分与正式档 JSON 泄漏。
        /// 不解析 CSV、不写入任何文件。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        public override void Validate(
            BuildPipelineContext context,
            ICollection<BuildValidationIssue> issues)
        {
            if (context == null || context.Profile == null || issues == null)
            {
                return;
            }

            ConfigExportBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<ConfigExportBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "Config 步骤未绑定 ConfigExportBuildConfiguration 配置资产。",
                    StepGroup));
                return;
            }
            ConfigPipelineOptions options = settings.Options;
            if (options == null)
            {
                options = new ConfigPipelineOptions();
            }

            if (!IsAssetsDirectoryExisting(options.ConfigSourceDirectory))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    $"Config 源目录不存在：{options.ConfigSourceDirectory}。",
                    StepGroup));
            }

            if (settings.ExportTool == ConfigExportTool.Excel
                && FindExcelBridge() == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "已选择 Excel 配置表工具，但未导入 Expansion.ExcelDataReader。",
                    StepGroup));
            }

            if (!IsAssetsDirectoryExisting(options.LocalizationSourceDirectory))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    $"Localization 源目录不存在：{options.LocalizationSourceDirectory}。",
                    StepGroup));
            }

            try
            {
                ConfigProtectionExporter.Create(options, null);
            }
            catch (Exception exception)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    exception.Message,
                    StepGroup));
            }

        }

        /// <summary>
        /// 执行 Config/Localization 全量导出。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>导出成功返回成功结果；导出失败或异常返回失败结果。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            ConfigExportBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<ConfigExportBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                return BuildStepResult.Failed(
                    "Config 步骤未绑定 ConfigExportBuildConfiguration 配置资产。",
                    null);
            }
            ConfigPipelineOptions options = settings.Options;
            if (options == null)
            {
                options = new ConfigPipelineOptions();
            }

            try
            {
                int configWritten;
                ConfigPipelineReport localizationReport;
                if (settings.ExportTool == ConfigExportTool.Excel)
                {
                    configWritten = ExportExcel(options);
                    localizationReport = ConfigPipelineService.ExportLocalization(options);
                }
                else
                {
                    ConfigPipelineReport report = ConfigPipelineService.ExportAll(options);
                    configWritten = report.WrittenFileCount;
                    localizationReport = null;
                }

                int writtenCount = configWritten
                    + (localizationReport?.WrittenFileCount ?? 0);

                return BuildStepResult.Succeeded(
                    $"Config/Localization 导出完成：{writtenCount} 个文件变更，JSON 与二进制内容均已保留。");
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"Config/Localization 导出失败：{exception.Message}",
                    exception);
            }
        }

        private static Type FindExcelBridge()
        {
            const string typeName =
                "UnityRFramework.Expansion.ExcelConfigBuildBridge";
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName, false);
                if (type != null) return type;
            }

            return null;
        }

        private static int ExportExcel(ConfigPipelineOptions options)
        {
            Type bridge = FindExcelBridge()
                ?? throw new InvalidOperationException(
                    "未导入 Expansion.ExcelDataReader。请改用 CSV 配置表工具或导入扩展。");
            MethodInfo method = bridge.GetMethod(
                "Export",
                BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                throw new InvalidOperationException("Excel 配置表构建桥接不可用。");
            }

            return (int)method.Invoke(null, new object[] { options });
        }

        /// <summary>
        /// 判断 Assets 相对目录是否存在。
        /// </summary>
        /// <param name="assetsPath">Assets 相对目录。</param>
        /// <returns>目录存在时返回 true。</returns>
        private static bool IsAssetsDirectoryExisting(string assetsPath)
        {
            if (string.IsNullOrWhiteSpace(assetsPath))
            {
                return false;
            }

            if (!assetsPath.Equals("Assets", StringComparison.Ordinal)
                && !assetsPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                assetsPath.Replace('/', Path.DirectorySeparatorChar)));
            return Directory.Exists(fullPath);
        }

        /// <summary>
        /// 判断子路径是否位于父路径内部（含相等）。
        /// </summary>
        /// <param name="child">子路径。</param>
        /// <param name="parent">父路径。</param>
        /// <returns>位于内部或相等时返回 true。</returns>
        private static bool IsWithin(string child, string parent)
        {
            string normalizedChild = Path.GetFullPath(child)
                .TrimEnd(Path.DirectorySeparatorChar);
            string normalizedParent = Path.GetFullPath(parent)
                .TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(
                normalizedChild,
                normalizedParent,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalizedChild.StartsWith(
                normalizedParent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
