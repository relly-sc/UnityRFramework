using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// YooAsset 资源打包步骤：按插件自身持久化设置构建配置的 Package。
    /// 构建管线选择读取 BundleBuilderSetting.GetPackageBuildPipeline（与 YooAsset
    /// AssetBundleBuilder 窗口的选择一致，默认 SBP），压缩、命名、加密、拷贝等
    /// 参数同样从 BundleBuilderSetting 读取，不在本框架重复维护；
    /// 支持真机构建三条主流管线：ScriptableBuildPipeline / LegacyBuildPipeline /
    /// RawFileBuildPipeline，Bundle 类型跟随管线（SBP/Legacy=AssetBundle、
    /// RawFile=RawBundle），与官方窗口行为一致。
    /// 不修改收集规则（由 YooAsset 编辑器配置）。仅当 Profile 配置并启用了
    /// yooasset 步骤条目时参与构建；未配置条目时跳过。发布目录名非空时，
    /// 构建完成后将产物复制到 工程根/Bundles/{目录名}。
    /// </summary>
    public sealed class YooAssetBuildStep : BuildPipelineStepBase
    {
        /// <summary>错误码：YooAsset 资源打包。</summary>
        private const string StepCode = "YOOASSET";

        /// <summary>校验分组：构建步骤。</summary>
        private const string StepGroup = "构建步骤";

        /// <summary>获取步骤唯一 Id。</summary>
        public override string Id
        {
            get
            {
                return "yooasset";
            }
        }

        /// <summary>获取步骤显示名称。</summary>
        public override string DisplayName
        {
            get
            {
                return "YooAsset 资源打包";
            }
        }

        public override BuildPipelineStage Stage => BuildPipelineStage.BuildAssets;

        public override Type ConfigurationType =>
            typeof(YooAssetBuildConfiguration);

        /// <summary>获取步骤排序值：位于 HybridCLR 之后、Player 构建之前。</summary>
        public override int Order
        {
            get
            {
                return 28;
            }
        }

        /// <summary>获取失败后是否需要人工清理：构建中途失败可能残留未完成包目录。</summary>
        public override bool RequiresManualCleanup
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// 判断步骤是否可用于当前构建上下文：仅当 Profile 配置并启用了
        /// yooasset 步骤条目时可用。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>条目已启用时返回 true。</returns>
        public override bool CanRun(BuildPipelineContext context)
        {
            if (context == null || context.Profile == null)
            {
                return false;
            }

            return BuildStepConfigLocator.HasEnabledEntry(
                context.Profile,
                Id);
        }

        /// <summary>
        /// 执行前置校验：只读检查步骤配置完整性，不触发任何打包动作。
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

            YooAssetBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<YooAssetBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "YooAsset 步骤未绑定 YooAssetBuildConfiguration 配置资产。",
                    StepGroup));
                return;
            }
            if (string.IsNullOrWhiteSpace(settings.PackageName))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    "YooAsset 步骤配置缺少 PackageName。",
                    StepGroup));
                return;
            }

            if (!IsPackageConfigured(settings.PackageName))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    $"YooAsset Package '{settings.PackageName}' 不存在于 "
                    + "Bundle Collector Setting，请先在 YooAsset 编辑器中配置收集规则。",
                    StepGroup));
            }

            ValidateHybridClrOutputCollector(
                context.Profile,
                settings.PackageName,
                issues);

            string pipelineName = string.IsNullOrWhiteSpace(settings.BuildPipelineName)
                ? BundleBuilderSetting.GetPackageBuildPipeline(settings.PackageName)
                : settings.BuildPipelineName.Trim();
            if (!YooAssetBuildConfiguration.IsSupportedBuildPipeline(pipelineName))
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    $"YooAsset 构建管线 '{pipelineName}' 尚未接入本步骤，"
                    + "请选择 ScriptableBuildPipeline、LegacyBuildPipeline 或 "
                    + "RawFileBuildPipeline。",
                    StepGroup));
            }
        }

        /// <summary>
        /// 执行 YooAsset Package 构建，并按配置发布到工程 Bundles 目录。
        /// 构建管线读取 YooAsset 自身按 Package 持久化的设置，与官方窗口一致。
        /// </summary>
        /// <param name="context">构建上下文。</param>
        /// <returns>成功返回构建结果；配置缺失或构建失败返回失败结果。</returns>
        public override BuildStepResult Execute(BuildPipelineContext context)
        {
            YooAssetBuildConfiguration settings =
                BuildStepConfigLocator.GetConfiguration<YooAssetBuildConfiguration>(
                    context.Profile,
                    Id);
            if (settings == null)
            {
                return BuildStepResult.Failed(
                    "YooAsset 步骤未绑定 YooAssetBuildConfiguration 配置资产。",
                    null);
            }
            if (string.IsNullOrWhiteSpace(settings.PackageName))
            {
                return BuildStepResult.Failed(
                    "YooAsset 步骤未配置：请在 Profile 的 yooasset 步骤条目中"
                    + "填写 PackageName。",
                    null);
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return BuildStepResult.Failed(
                    "YooAsset 资源打包不能在 Play Mode 中执行。",
                    null);
            }

            try
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string packageName = settings.PackageName.Trim();
                string version = settings.GetEffectivePackageVersion();

                // 同步 Profile 配置回 YooAsset 插件设置：Profile 为空字段走 Builder 当前值
                // （默认 ScriptableBuildPipeline），非空字段覆盖插件按 Package 持久化的设置，
                // 避免每次修改都要打开插件窗口。执行前统一写回后再分发构建。
                string effectivePipeline = string.IsNullOrWhiteSpace(settings.BuildPipelineName)
                    ? BundleBuilderSetting.GetPackageBuildPipeline(packageName)
                    : settings.BuildPipelineName.Trim();
                BundleBuilderSetting.SetPackageBuildPipeline(packageName, effectivePipeline);
                BundleBuilderSetting.SetPackageClearBuildCache(
                    packageName,
                    effectivePipeline,
                    settings.ClearBuildCache);

                BuildResult result;
                switch (effectivePipeline)
                {
                    case nameof(EBuildPipeline.ScriptableBuildPipeline):
                        result = RunScriptablePipeline(
                            packageName,
                            version,
                            string.Empty,
                            context.Target);
                        break;
                    case nameof(EBuildPipeline.LegacyBuildPipeline):
                        result = RunLegacyPipeline(
                            packageName,
                            version,
                            string.Empty,
                            context.Target);
                        break;
                    case nameof(EBuildPipeline.RawFileBuildPipeline):
                        result = RunRawFilePipeline(
                            packageName,
                            version,
                            string.Empty,
                            context.Target);
                        break;
                    default:
                        return BuildStepResult.Failed(
                            $"YooAsset 构建管线 '{effectivePipeline}' 未接入本流水线："
                            + "ESBP 为编辑器模拟构建、IABP 仅团结引擎、AFBP 为归档管线，"
                            + "请在 YooAsset AssetBundleBuilder 窗口将该 Package 的构建管线"
                            + "切换为 ScriptableBuildPipeline / LegacyBuildPipeline / "
                            + "RawFileBuildPipeline。",
                            null);
                }

                if (!result.Success)
                {
                    return BuildStepResult.Failed(
                        $"YooAsset Package '{packageName}' 构建失败（管线 {effectivePipeline}）："
                        + $"Task: {result.FailedTask}, Error: {result.ErrorInfo}。",
                        null);
                }

                string message = $"YooAsset Package '{packageName}' 构建完成"
                    + $"（管线 {effectivePipeline}）：版本 {version}。";

                // 构建完成后固定复制到 工程根/Bundles/{PackageName}；发布目录只读，
                // 由框架统一处理，不再依赖 Profile 中可手填的 ServerDirectoryName。
                string serverRoot = ResolveServerRoot(packageName);
                PublishPackage(result.OutputPackageDirectory, serverRoot);
                message += $"已发布到 {serverRoot}。";

                return BuildStepResult.Succeeded(message);
            }
            catch (Exception exception)
            {
                return BuildStepResult.Failed(
                    $"YooAsset 打包失败：{exception.Message}",
                    exception);
            }
        }

        /// <summary>
        /// 运行可编程构建管线（SBP）：参数组装镜像官方
        /// ScriptableBuildPipelineViewer.ExecuteBuild。
        /// </summary>
        /// <param name="packageName">Package 名称。</param>
        /// <param name="version">包版本号。</param>
        /// <param name="packageNote">包备注。</param>
        /// <param name="target">构建平台。</param>
        /// <returns>YooAsset 构建结果。</returns>
        private static BuildResult RunScriptablePipeline(
            string packageName,
            string version,
            string packageNote,
            BuildTarget target)
        {
            string pipelineName = EBuildPipeline.ScriptableBuildPipeline.ToString();
            ScriptableBuildParameters parameters = new ScriptableBuildParameters();
            FillCommonParameters(
                parameters,
                packageName,
                version,
                packageNote,
                pipelineName,
                EBundleType.AssetBundle,
                target);
            parameters.EnableSharePackRule = true;
            parameters.WriteLinkXML = true;
            parameters.BuiltinShadersBundleName = GetBuiltinShaderBundleName(packageName);
            parameters.CompressOption = BundleBuilderSetting.GetPackageCompressOption(
                packageName,
                pipelineName);
            ScriptableBuildPipeline pipeline = new ScriptableBuildPipeline();
            return pipeline.Run(parameters, true);
        }

        /// <summary>
        /// 运行旧版构建管线（LBP）：参数组装镜像官方
        /// LegacyBuildPipelineViewer.ExecuteBuild。
        /// </summary>
        /// <param name="packageName">Package 名称。</param>
        /// <param name="version">包版本号。</param>
        /// <param name="packageNote">包备注。</param>
        /// <param name="target">构建平台。</param>
        /// <returns>YooAsset 构建结果。</returns>
        private static BuildResult RunLegacyPipeline(
            string packageName,
            string version,
            string packageNote,
            BuildTarget target)
        {
            string pipelineName = EBuildPipeline.LegacyBuildPipeline.ToString();
            LegacyBuildParameters parameters = new LegacyBuildParameters();
            FillCommonParameters(
                parameters,
                packageName,
                version,
                packageNote,
                pipelineName,
                EBundleType.AssetBundle,
                target);
            parameters.EnableSharePackRule = true;
            parameters.CompressOption = BundleBuilderSetting.GetPackageCompressOption(
                packageName,
                pipelineName);
            LegacyBuildPipeline pipeline = new LegacyBuildPipeline();
            return pipeline.Run(parameters, true);
        }

        /// <summary>
        /// 运行原生文件构建管线（RFBP）：参数组装镜像官方
        /// RawFileBuildPipelineViewer.ExecuteBuild；原生文件不压缩。
        /// </summary>
        /// <param name="packageName">Package 名称。</param>
        /// <param name="version">包版本号。</param>
        /// <param name="packageNote">包备注。</param>
        /// <param name="target">构建平台。</param>
        /// <returns>YooAsset 构建结果。</returns>
        private static BuildResult RunRawFilePipeline(
            string packageName,
            string version,
            string packageNote,
            BuildTarget target)
        {
            string pipelineName = EBuildPipeline.RawFileBuildPipeline.ToString();
            RawFileBuildParameters parameters = new RawFileBuildParameters();
            FillCommonParameters(
                parameters,
                packageName,
                version,
                packageNote,
                pipelineName,
                EBundleType.RawBundle,
                target);
            RawFileBuildPipeline pipeline = new RawFileBuildPipeline();
            return pipeline.Run(parameters, true);
        }

        /// <summary>
        /// 填充三条管线共享的基类构建参数（输出根、包信息、命名、拷贝、
        /// 缓存、加密器等），取值全部来自 YooAsset 自身的 BundleBuilderSetting
        /// 持久化设置；VerifyBuildingResult 与官方窗口一致写死为 true。
        /// </summary>
        /// <param name="parameters">待填充的构建参数（基类部分）。</param>
        /// <param name="packageName">Package 名称。</param>
        /// <param name="version">包版本号。</param>
        /// <param name="packageNote">包备注。</param>
        /// <param name="pipelineName">构建管线名称。</param>
        /// <param name="bundleType">构建资源包类型，跟随管线。</param>
        /// <param name="target">构建平台。</param>
        private static void FillCommonParameters(
            BuildParameters parameters,
            string packageName,
            string version,
            string packageNote,
            string pipelineName,
            EBundleType bundleType,
            BuildTarget target)
        {
            parameters.BuildOutputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
            parameters.BundledFileRoot = BundleBuilderHelper.GetStreamingAssetsRoot();
            parameters.BuildPipeline = pipelineName;
            parameters.BuildBundleType = (int)bundleType;
            parameters.BuildTarget = target;
            parameters.PackageName = packageName;
            parameters.PackageVersion = version;
            parameters.PackageNote = packageNote;
            parameters.VerifyBuildingResult = true;
            parameters.FileNameStyle = BundleBuilderSetting.GetPackageFileNameStyle(
                packageName,
                pipelineName);
            parameters.BundledCopyOption = BundleBuilderSetting.GetPackageBundledCopyOption(
                packageName,
                pipelineName);
            parameters.BundledCopyParams = BundleBuilderSetting.GetPackageBundledCopyParams(
                packageName,
                pipelineName);
            parameters.ClearBuildCacheFiles = BundleBuilderSetting.GetPackageClearBuildCache(
                packageName,
                pipelineName);
            parameters.UseAssetDependencyDB = BundleBuilderSetting.GetPackageUseAssetDependencyDB(
                packageName,
                pipelineName);
            parameters.BundleEncryptor = CreateBuilderSettingInstance<IBundleEncryptor>(
                BundleBuilderSetting.GetPackageBundleEncryptorClassName(
                    packageName,
                    pipelineName),
                "Bundle encryptor");
            parameters.ManifestEncryptor = CreateBuilderSettingInstance<IManifestEncryptor>(
                BundleBuilderSetting.GetPackageManifestEncryptorClassName(
                    packageName,
                    pipelineName),
                "Manifest encryptor");
            parameters.ManifestDecryptor = CreateBuilderSettingInstance<IManifestDecryptor>(
                BundleBuilderSetting.GetPackageManifestDecryptorClassName(
                    packageName,
                    pipelineName),
                "Manifest decryptor");
        }

        /// <summary>
        /// 计算内置着色器资源包名称，与 YooAsset 自动收集的着色器资源包名保持一致。
        /// </summary>
        /// <param name="packageName">Package 名称。</param>
        /// <returns>内置着色器资源包名称。</returns>
        private static string GetBuiltinShaderBundleName(string packageName)
        {
            return DefaultBundlePackRule
                .CreateShadersPackRuleResult()
                .GetBundleName(
                    packageName,
                    BundleCollectorSettingData.Setting.UniqueBundleName);
        }

        /// <summary>
        /// 判断指定 Package 是否已在 Bundle Collector Setting 中配置收集规则。
        /// </summary>
        /// <param name="packageName">Package 名称。</param>
        /// <returns>已配置时返回 true。</returns>
        private static bool IsPackageConfigured(string packageName)
        {
            return BundleCollectorSettingData.Setting.Packages.Any(item =>
                string.Equals(
                    item.PackageName,
                    packageName,
                    StringComparison.Ordinal));
        }

        /// <summary>
        /// 当 Profile 同时启用 HybridCLR 时，确认其产物目录会被当前 YooAsset
        /// Package 收集，避免热更步骤生成新文件但资源包仍打入旧文件。
        /// </summary>
        private static void ValidateHybridClrOutputCollector(
            UnityRFrameworkBuildProfile profile,
            string packageName,
            ICollection<BuildValidationIssue> issues)
        {
            BuildStepSettings hybridEntry = BuildStepConfigLocator.FindEntry(
                profile,
                "hybridclr");
            if (hybridEntry == null
                || !hybridEntry.Enabled
                || hybridEntry.Configuration == null)
            {
                return;
            }

            SerializedObject configuration =
                new SerializedObject(hybridEntry.Configuration);
            SerializedProperty outputRoot =
                configuration.FindProperty("OutputAssetRoot");
            string outputPath = NormalizeAssetPath(outputRoot?.stringValue);
            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            BundleCollectorPackage package = BundleCollectorSettingData.Setting
                .Packages.FirstOrDefault(item => string.Equals(
                    item.PackageName,
                    packageName,
                    StringComparison.Ordinal));
            bool collected = package?.Groups != null
                && package.Groups.Any(group => group?.Collectors != null
                    && group.Collectors.Any(collector =>
                        IsPathCollected(outputPath, collector?.CollectPath)));
            if (!collected)
            {
                issues.Add(BuildValidationIssue.Error(
                    StepCode,
                    $"HybridCLR 输出目录 '{outputPath}' 未被 YooAsset Package "
                    + $"'{packageName}' 的任何 Collector 收集。请统一 "
                    + "OutputAssetRoot 与 Bundle Collector Setting，"
                    + "否则资源包会继续使用旧热更文件。",
                    StepGroup));
            }
        }

        private static bool IsPathCollected(string assetPath, string collectPath)
        {
            string normalizedCollector = NormalizeAssetPath(collectPath);
            return !string.IsNullOrEmpty(normalizedCollector)
                && (string.Equals(
                        assetPath,
                        normalizedCollector,
                        StringComparison.OrdinalIgnoreCase)
                    || assetPath.StartsWith(
                        normalizedCollector + "/",
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// 解析发布目录的绝对路径：工程根/Bundles/{目录名}。
        /// </summary>
        /// <param name="serverDirectoryName">发布目录名。</param>
        /// <returns>发布目录绝对路径。</returns>
        private static string ResolveServerRoot(string serverDirectoryName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException(
                    "无法定位 Unity 工程根目录。");
            }

            return Path.Combine(
                projectRoot,
                "Bundles",
                serverDirectoryName);
        }

        /// <summary>
        /// 判断目录名是否安全：只允许字母、数字、下划线与短横线，禁止路径分隔符
        /// 与 ".."，防止发布路径穿越。
        /// </summary>
        /// <param name="directoryName">目录名。</param>
        /// <returns>安全时返回 true。</returns>
        private static bool IsSafeDirectoryName(string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                return false;
            }

            if (directoryName.IndexOfAny(
                    Path.GetInvalidFileNameChars()) >= 0
                || directoryName.Contains("..")
                || directoryName.IndexOf('/') >= 0
                || directoryName.IndexOf('\\') >= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 将构建产物目录完整复制到发布目录。
        /// </summary>
        /// <param name="sourceRoot">产物源目录。</param>
        /// <param name="targetRoot">发布目标目录。</param>
        private static void PublishPackage(string sourceRoot, string targetRoot)
        {
            Directory.CreateDirectory(targetRoot);
            foreach (string sourceFile in Directory.GetFiles(
                         sourceRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativePath = sourceFile.Substring(sourceRoot.Length)
                    .TrimStart(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
                string targetFile = Path.Combine(targetRoot, relativePath);
                string targetDirectory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(sourceFile, targetFile, true);
            }
        }

        /// <summary>
        /// 创建 YooAsset 构建设置实例（加密器/解密器），类名来自 Bundle Builder 设置。
        /// </summary>
        /// <typeparam name="T">设置实例接口类型。</typeparam>
        /// <param name="className">设置类全名。</param>
        /// <param name="settingName">设置名称，用于错误消息。</param>
        /// <returns>设置实例。</returns>
        private static T CreateBuilderSettingInstance<T>(
            string className,
            string settingName)
            where T : class
        {
            Type classType = EditorAssemblyUtility
                .GetAssignableTypes(typeof(T))
                .FirstOrDefault(type => string.Equals(
                    type.FullName,
                    className,
                    StringComparison.Ordinal));
            if (classType == null)
            {
                throw new InvalidOperationException(
                    $"{settingName} 类型未找到：'{className}'，"
                    + "请在 YooAsset Bundle Builder 中修正。");
            }

            return Activator.CreateInstance(classType) as T
                   ?? throw new InvalidOperationException(
                       $"创建 {settingName} 实例失败：'{className}'。");
        }

    }
}
