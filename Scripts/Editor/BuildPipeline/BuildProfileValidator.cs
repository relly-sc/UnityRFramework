using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建前工程状态快照：供主校验器检查"可预知错误在 BuildPlayer 前失败"。
    /// 使用结构体快照而非直接读静态属性，便于测试注入自定义状态。
    /// </summary>
    public struct BuildEnvironmentState
    {
        /// <summary>是否处于 Play Mode 或即将进入。</summary>
        public bool IsPlaying;

        /// <summary>是否正在编译脚本。</summary>
        public bool IsCompiling;

        /// <summary>是否正在导入资源。</summary>
        public bool IsUpdating;

        /// <summary>是否已有其他构建任务进行中。</summary>
        public bool IsBuildingPlayer;

        /// <summary>当前活动构建目标。</summary>
        public BuildTarget ActiveTarget;

        /// <summary>
        /// 从编辑器当前状态采集快照。
        /// </summary>
        /// <returns>当前工程状态快照。</returns>
        public static BuildEnvironmentState Capture()
        {
            return new BuildEnvironmentState
            {
                IsPlaying = EditorApplication.isPlayingOrWillChangePlaymode,
                IsCompiling = EditorApplication.isCompiling,
                IsUpdating = EditorApplication.isUpdating,
                IsBuildingPlayer = BuildPipeline.isBuildingPlayer,
                ActiveTarget = EditorUserBuildSettings.activeBuildTarget
            };
        }
    }

    /// <summary>
    /// 构建前校验结果，承载问题列表与是否可构建结论。
    /// CanBuild 为 true 表示不存在任何 Error 级问题。
    /// </summary>
    public readonly struct BuildValidationResult
    {
        /// <summary>
        /// 创建校验结果。
        /// </summary>
        /// <param name="issues">校验问题完整列表。</param>
        public BuildValidationResult(IReadOnlyList<BuildValidationIssue> issues)
        {
            Issues = issues ?? Array.Empty<BuildValidationIssue>();

            List<BuildValidationIssue> errors = new List<BuildValidationIssue>();
            List<BuildValidationIssue> warnings = new List<BuildValidationIssue>();
            for (int i = 0; i < Issues.Count; i++)
            {
                BuildValidationIssue issue = Issues[i];
                if (issue.Level == BuildValidationLevel.Error)
                {
                    errors.Add(issue);
                }
                else
                {
                    warnings.Add(issue);
                }
            }

            Errors = errors;
            Warnings = warnings;
        }

        /// <summary>获取校验问题完整列表。</summary>
        public IReadOnlyList<BuildValidationIssue> Issues { get; }

        /// <summary>获取 Error 级问题列表。</summary>
        public IReadOnlyList<BuildValidationIssue> Errors { get; }

        /// <summary>获取 Warning 级问题列表。</summary>
        public IReadOnlyList<BuildValidationIssue> Warnings { get; }

        /// <summary>获取是否可构建：不存在任何 Error 级问题。</summary>
        public bool CanBuild
        {
            get
            {
                return Errors.Count == 0;
            }
        }
    }

    /// <summary>
    /// 构建前校验主入口：编排内置校验组并合并第三方注册校验器结果。
    /// 只读校验，不修改 Profile、PlayerSettings、场景或输出目录。
    /// 内置校验组包括基础配置、场景、输出、平台专有、宏、工程状态与可选的资源健康。
    /// </summary>
    public static class BuildProfileValidator
    {
        /// <summary>错误码：基础配置。</summary>
        public const string ProfileCode = "PROFILE";

        /// <summary>错误码：场景。</summary>
        public const string SceneCode = "SCENE";

        /// <summary>错误码：输出。</summary>
        public const string OutputCode = "OUTPUT";

        /// <summary>错误码：Android 平台专有。</summary>
        public const string AndroidCode = "ANDROID";

        /// <summary>错误码：iOS 平台专有。</summary>
        public const string IosCode = "IOS";

        /// <summary>错误码：宏定义。</summary>
        public const string DefineCode = "DEFINE";

        /// <summary>错误码：工程状态。</summary>
        public const string EnvironmentCode = "ENV";

        /// <summary>错误码：资源健康。</summary>
        public const string AssetCode = "ASSET";

        /// <summary>错误码：第三方注册校验器。</summary>
        public const string ThirdPartyCode = "VALIDATOR";

        /// <summary>错误码：构建步骤可用性。</summary>
        public const string StepCode = "STEP";

        /// <summary>校验分组：基础配置。</summary>
        public const string GroupBasic = "基础配置";

        /// <summary>校验分组：场景。</summary>
        public const string GroupScenes = "场景";

        /// <summary>校验分组：输出。</summary>
        public const string GroupOutput = "输出";

        /// <summary>校验分组：平台专有。</summary>
        public const string GroupPlatform = "平台";

        /// <summary>校验分组：宏。</summary>
        public const string GroupDefines = "宏";

        /// <summary>校验分组：工程状态。</summary>
        public const string GroupEnvironment = "工程状态";

        /// <summary>校验分组：资源健康。</summary>
        public const string GroupAsset = "资源健康";

        /// <summary>校验分组：第三方。</summary>
        public const string GroupThirdParty = "第三方校验";

        /// <summary>校验分组：构建步骤。</summary>
        public const string GroupSteps = "构建步骤";

        /// <summary>输出文件名允许的扩展名集合（含点号）。</summary>
        private static readonly HashSet<string> WindowsExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".exe",
                string.Empty
            };

        /// <summary>
        /// 使用当前工程状态执行完整校验。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <returns>校验结果。</returns>
        public static BuildValidationResult Validate(
            UnityRFrameworkBuildProfile profile)
        {
            return Validate(profile, BuildEnvironmentState.Capture());
        }

        /// <summary>
        /// 使用指定工程状态执行完整校验。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="state">工程状态快照。</param>
        /// <returns>校验结果。</returns>
        public static BuildValidationResult Validate(
            UnityRFrameworkBuildProfile profile,
            BuildEnvironmentState state)
        {
            return Validate(profile, state, false);
        }

        /// <summary>
        /// 使用指定工程状态执行完整校验，可选包含资源健康检查。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="state">工程状态快照。</param>
        /// <param name="includeAssetHealth">是否执行资源健康检查（默认关闭，按需启用）。</param>
        /// <returns>校验结果。</returns>
        public static BuildValidationResult Validate(
            UnityRFrameworkBuildProfile profile,
            BuildEnvironmentState state,
            bool includeAssetHealth)
        {
            List<BuildValidationIssue> issues = new List<BuildValidationIssue>();
            if (profile == null)
            {
                issues.Add(BuildValidationIssue.Error(
                    ProfileCode,
                    "构建配置为空，无法校验。",
                    GroupBasic));
                return new BuildValidationResult(issues);
            }

            ValidateBasic(profile, issues);
            ValidateScenes(profile, issues);
            ValidateOutput(profile, state, issues, out BuildValidationContext context);
            ValidatePlatform(profile, context, issues);
            ValidateDefineSymbols(profile, issues);
            ValidateEnvironment(state, issues);
            ValidateRecipeAndSteps(profile, issues);

            if (includeAssetHealth)
            {
                ValidateAssetHealth(profile, issues);
            }

            ValidateThirdParty(context, issues);
            return new BuildValidationResult(issues);
        }

        /// <summary>
        /// 解析 Recipe 并调用每个选中步骤的只读校验。
        /// </summary>
        private static void ValidateRecipeAndSteps(
            UnityRFrameworkBuildProfile profile,
            ICollection<BuildValidationIssue> issues)
        {
            BuildRecipePlan plan = BuildRecipePlanner.Create(profile);
            for (int i = 0; i < plan.Issues.Count; i++)
            {
                issues.Add(plan.Issues[i]);
            }

            if (!plan.IsValid)
            {
                return;
            }

            Dictionary<string, IBuildPipelineStep> stepMap =
                new Dictionary<string, IBuildPipelineStep>(
                    StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                stepMap[plan.Steps[i].Id] = plan.Steps[i];
            }

            BuildPipelineContext pipelineContext = BuildPipelineContext.Create(
                profile,
                stepMap,
                System.Threading.CancellationToken.None);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                IBuildPipelineStep step = plan.Steps[i];
                try
                {
                    step.Validate(pipelineContext, issues);
                }
                catch (Exception exception)
                {
                    issues.Add(BuildValidationIssue.Error(
                        StepCode,
                        $"步骤 '{step.Id}' 的只读校验抛出异常：{exception.Message}",
                        GroupSteps));
                }
            }
        }

        /// <summary>
        /// 校验基础配置：复用 Profile 内置校验并转为 Error 级问题。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateBasic(
            UnityRFrameworkBuildProfile profile,
            ICollection<BuildValidationIssue> issues)
        {
            List<string> errors = profile.Validate();
            for (int i = 0; i < errors.Count; i++)
            {
                issues.Add(BuildValidationIssue.Error(
                    ProfileCode,
                    errors[i],
                    GroupBasic));
            }
        }

        /// <summary>
        /// 校验场景：启用场景引用可解析为磁盘上真实存在的场景资产。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateScenes(
            UnityRFrameworkBuildProfile profile,
            ICollection<BuildValidationIssue> issues)
        {
            if (profile.Scenes == null || profile.Scenes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < profile.Scenes.Count; i++)
            {
                BuildSceneEntry entry = profile.Scenes[i];
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                if (entry.Scene == null)
                {
                    continue;
                }

                string path = entry.ResolvePath();
                if (string.IsNullOrEmpty(path))
                {
                    issues.Add(BuildValidationIssue.Error(
                        SceneCode,
                        $"启用场景条目 {i} 的路径解析为空。",
                        GroupScenes));
                    continue;
                }

                SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (asset == null)
                {
                    issues.Add(BuildValidationIssue.Error(
                        SceneCode,
                        $"启用场景 '{path}' 在磁盘上不存在，请检查引用是否损坏。",
                        GroupScenes));
                }
            }
        }

        /// <summary>
        /// 校验输出配置：模板解析、扩展名与平台匹配、脚本后端目录隔离。
        /// 解析成功时输出 <paramref name="context"/> 供平台与第三方校验复用。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="state">工程状态快照。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        /// <param name="context">解析成功时输出校验上下文；失败时输出字段为空的上下文。</param>
        private static void ValidateOutput(
            UnityRFrameworkBuildProfile profile,
            BuildEnvironmentState state,
            ICollection<BuildValidationIssue> issues,
            out BuildValidationContext context)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputRoot = string.Empty;
            string directory = string.Empty;
            string fileName = string.Empty;
            bool outputResolved = true;

            BuildOutputToken token = new BuildOutputToken(
                profile.name,
                profile.Platform.ProductName,
                profile.Platform.Target.ToString(),
                profile.Platform.PublicVersion,
                profile.Platform.BuildNumber,
                profile.Platform.ScriptingBackend.ToString(),
                DateTime.Now);

            try
            {
                outputRoot = profile.Output.ResolveRootAbsolute(projectRoot);
            }
            catch (Exception exception)
            {
                outputResolved = false;
                issues.Add(BuildValidationIssue.Error(
                    OutputCode,
                    $"输出根目录校验失败：{exception.Message}",
                    GroupOutput));
            }

            if (outputResolved)
            {
                try
                {
                    directory = profile.Output.ResolveDirectory(token);
                    fileName = profile.Output.ResolveFileName(token);
                }
                catch (Exception exception)
                {
                    outputResolved = false;
                    issues.Add(BuildValidationIssue.Error(
                        OutputCode,
                        $"输出模板解析失败：{exception.Message}",
                        GroupOutput));
                }
            }

            if (outputResolved)
            {
                ValidateOutputExtension(profile, issues);
                AppendOutputIsolation(profile, issues);
            }

            context = new BuildValidationContext(
                profile,
                state.ActiveTarget,
                projectRoot,
                outputRoot,
                directory,
                fileName);
        }

        /// <summary>
        /// 校验输出文件名扩展名与目标平台、Android 产物类型匹配。
        /// 只校验原始模板末尾显式书写的扩展名：解析后文件名中的点号可能来自
        /// {Version} 等占位符值（如 1.0.0），不是扩展名，不能用于判断。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateOutputExtension(
            UnityRFrameworkBuildProfile profile,
            ICollection<BuildValidationIssue> issues)
        {
            BuildTarget target = profile.Platform.Target;
            string template = profile.Output.FileNameTemplate;
            string extension = Path.GetExtension(template);

            if (target == BuildTarget.StandaloneWindows64)
            {
                if (!WindowsExtensions.Contains(extension))
                {
                    issues.Add(BuildValidationIssue.Warning(
                        OutputCode,
                        $"Windows 输出文件名扩展名 '{extension}' 无效，"
                        + "Unity 将按 .exe 输出；建议文件名模板不含扩展名。",
                        GroupOutput));
                }

                return;
            }

            if (target == BuildTarget.Android)
            {
                string expected = profile.Platform.AndroidBuildAppBundle
                    ? ".aab"
                    : ".apk";
                if (!string.IsNullOrEmpty(extension)
                    && !string.Equals(
                        extension,
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(BuildValidationIssue.Error(
                        OutputCode,
                        $"Android 输出为 {expected}，但文件名模板扩展名为 '{extension}'，"
                        + "产物类型与文件名不匹配。",
                        GroupOutput));
                }

                return;
            }

            if (target == BuildTarget.StandaloneOSX)
            {
                if (!string.IsNullOrEmpty(extension)
                    && !string.Equals(
                        extension,
                        ".app",
                        StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(BuildValidationIssue.Warning(
                        OutputCode,
                        $"macOS 输出文件名扩展名 '{extension}' 无效，"
                        + "Unity 将按 .app 输出；建议文件名模板不含扩展名。",
                        GroupOutput));
                }

                return;
            }

            if (target == BuildTarget.iOS || target == BuildTarget.WebGL)
            {
                if (!string.IsNullOrEmpty(extension))
                {
                    string outputType = target == BuildTarget.iOS
                        ? "Xcode 工程"
                        : "WebGL 站点";
                    issues.Add(BuildValidationIssue.Warning(
                        OutputCode,
                        $"{target} 仅生成 {outputType}目录，文件名模板中的扩展名会被忽略；"
                        + "建议文件名模板不含扩展名。",
                        GroupOutput));
                }
            }
        }

        /// <summary>
        /// 追加输出目录四维隔离校验：目录模板必须包含 Profile、平台、脚本后端
        /// 与版本占位符，任一维度缺失均按 Error 处理，防止连续构建复用旧输出目录。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void AppendOutputIsolation(
            UnityRFrameworkBuildProfile profile,
            ICollection<BuildValidationIssue> issues)
        {
            List<BuildValidationIssue> isolationIssues =
                BuildOutputPathResolver.ValidateIsolation(profile);
            for (int i = 0; i < isolationIssues.Count; i++)
            {
                issues.Add(isolationIssues[i]);
            }
        }

        /// <summary>
        /// 校验平台专有参数：Android 架构、签名文件与密码环境变量。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="context">校验上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidatePlatform(
            UnityRFrameworkBuildProfile profile,
            BuildValidationContext context,
            ICollection<BuildValidationIssue> issues)
        {
            if (profile.Platform.Target == BuildTarget.Android)
            {
                ValidateAndroid(profile, context, issues);
            }
        }

        /// <summary>
        /// 校验 Android 专有参数：架构包含 ARM64、Keystore 文件与密码环境变量。
        /// 密码本体绝不写入问题描述，只报告缺失的环境变量名。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="context">校验上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateAndroid(
            UnityRFrameworkBuildProfile profile,
            BuildValidationContext context,
            ICollection<BuildValidationIssue> issues)
        {
            if ((profile.Platform.AndroidArchitecture & AndroidArchitecture.ARM64) == 0)
            {
                issues.Add(BuildValidationIssue.Error(
                    AndroidCode,
                    "Android 目标架构必须包含 ARM64。",
                    GroupPlatform));
            }

            if (string.IsNullOrWhiteSpace(profile.Platform.AndroidKeystoreName))
            {
                return;
            }

            string keystorePath = Path.IsPathRooted(profile.Platform.AndroidKeystoreName)
                ? profile.Platform.AndroidKeystoreName
                : Path.Combine(
                    context.ProjectRoot,
                    profile.Platform.AndroidKeystoreName);
            if (!File.Exists(keystorePath))
            {
                issues.Add(BuildValidationIssue.Error(
                    AndroidCode,
                    $"Android Keystore 文件不存在：'{keystorePath}'。",
                    GroupPlatform));
            }

            if (string.IsNullOrWhiteSpace(profile.Platform.AndroidKeystorePassEnvVar))
            {
                issues.Add(BuildValidationIssue.Error(
                    AndroidCode,
                    "已配置 Android Keystore，但 Keystore 密码来源环境变量名为空。",
                    GroupPlatform));
            }
            else if (string.IsNullOrEmpty(
                         Environment.GetEnvironmentVariable(
                             profile.Platform.AndroidKeystorePassEnvVar)))
            {
                issues.Add(BuildValidationIssue.Error(
                    AndroidCode,
                    $"Android Keystore 密码环境变量 "
                    + $"'{profile.Platform.AndroidKeystorePassEnvVar}' 未设置或为空。",
                    GroupPlatform));
            }

            if (string.IsNullOrWhiteSpace(profile.Platform.AndroidKeyAliasPassEnvVar))
            {
                issues.Add(BuildValidationIssue.Error(
                    AndroidCode,
                    "已配置 Android Keystore，但 Key Alias 密码来源环境变量名为空。",
                    GroupPlatform));
            }
            else if (string.IsNullOrEmpty(
                         Environment.GetEnvironmentVariable(
                             profile.Platform.AndroidKeyAliasPassEnvVar)))
            {
                issues.Add(BuildValidationIssue.Error(
                    AndroidCode,
                    $"Android Key Alias 密码环境变量 "
                    + $"'{profile.Platform.AndroidKeyAliasPassEnvVar}' 未设置或为空。",
                    GroupPlatform));
            }
        }

        /// <summary>
        /// 校验宏定义合法性：空条目提示，含非法字符条目报错。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateDefineSymbols(
            UnityRFrameworkBuildProfile profile,
            ICollection<BuildValidationIssue> issues)
        {
            ValidateSymbolList(profile.Platform.DefineSymbols, "公共宏", issues);
            ValidateSymbolList(
                profile.Platform.RemoveDefineSymbols,
                "移除宏",
                issues);
        }

        /// <summary>
        /// 校验单个宏列表的合法性。
        /// </summary>
        /// <param name="symbols">宏定义列表。</param>
        /// <param name="label">列表用途标签。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateSymbolList(
            IReadOnlyList<string> symbols,
            string label,
            ICollection<BuildValidationIssue> issues)
        {
            if (symbols == null)
            {
                return;
            }

            for (int i = 0; i < symbols.Count; i++)
            {
                string symbol = symbols[i];
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    issues.Add(BuildValidationIssue.Warning(
                        DefineCode,
                        $"{label}第 {i} 条为空，请清理。",
                        GroupDefines));
                    continue;
                }

                if (symbol.IndexOf(';') >= 0 || symbol.IndexOf(' ') >= 0)
                {
                    issues.Add(BuildValidationIssue.Error(
                        DefineCode,
                        $"{label}第 {i} 条 '{symbol}' 包含分号或空格，"
                        + "请拆分到独立条目。",
                        GroupDefines));
                    continue;
                }

                bool valid = true;
                for (int j = 0; j < symbol.Length; j++)
                {
                    char character = symbol[j];
                    if (!char.IsLetterOrDigit(character) && character != '_')
                    {
                        valid = false;
                        break;
                    }
                }

                if (!valid)
                {
                    issues.Add(BuildValidationIssue.Error(
                        DefineCode,
                        $"{label}第 {i} 条 '{symbol}' 包含非法字符，"
                        + "宏名只允许字母、数字与下划线。",
                        GroupDefines));
                }
            }
        }

        /// <summary>
        /// 校验工程状态：Play Mode、编译、资源导入或已有构建任务时阻止构建。
        /// </summary>
        /// <param name="state">工程状态快照。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateEnvironment(
            BuildEnvironmentState state,
            ICollection<BuildValidationIssue> issues)
        {
            if (state.IsPlaying)
            {
                issues.Add(BuildValidationIssue.Error(
                    EnvironmentCode,
                    "当前处于 Play Mode，请退出播放后再构建。",
                    GroupEnvironment));
            }

            if (state.IsCompiling)
            {
                issues.Add(BuildValidationIssue.Error(
                    EnvironmentCode,
                    "当前正在编译脚本，请等待编译完成后重试。",
                    GroupEnvironment));
            }

            if (state.IsUpdating)
            {
                issues.Add(BuildValidationIssue.Error(
                    EnvironmentCode,
                    "当前正在导入资源，请等待导入完成后重试。",
                    GroupEnvironment));
            }

            if (state.IsBuildingPlayer)
            {
                issues.Add(BuildValidationIssue.Error(
                    EnvironmentCode,
                    "已有其他 Player 构建任务进行中，单次只允许一个构建。",
                    GroupEnvironment));
            }
        }

        /// <summary>
        /// 执行资源健康检查：扫描项目 Prefab，汇总 Missing Script 与 Missing Material。
        /// 非确定性问题只报告 Warning，不阻止构建；默认关闭，按需启用。
        /// </summary>
        /// <param name="profile">待校验的构建配置。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        public static void ValidateAssetHealth(
            UnityRFrameworkBuildProfile profile,
            ICollection<BuildValidationIssue> issues)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int scanned = 0;
            int missingScriptPrefabs = 0;
            int missingMaterialPrefabs = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                scanned++;

                Component[] components =
                    prefab.GetComponentsInChildren<Component>(true);
                for (int j = 0; j < components.Length; j++)
                {
                    if (components[j] == null)
                    {
                        missingScriptPrefabs++;
                        break;
                    }
                }

                Renderer[] renderers =
                    prefab.GetComponentsInChildren<Renderer>(true);
                for (int j = 0; j < renderers.Length; j++)
                {
                    Material[] materials = renderers[j].sharedMaterials;
                    bool hasMissing = false;
                    for (int k = 0; k < materials.Length; k++)
                    {
                        if (materials[k] == null)
                        {
                            hasMissing = true;
                            break;
                        }
                    }

                    if (hasMissing)
                    {
                        missingMaterialPrefabs++;
                        break;
                    }
                }
            }

            if (missingScriptPrefabs > 0)
            {
                issues.Add(BuildValidationIssue.Warning(
                    AssetCode,
                    $"资源健康：{missingScriptPrefabs} 个 Prefab 存在 Missing Script"
                    + $"（共扫描 {scanned} 个），请检查引用。",
                    GroupAsset));
            }

            if (missingMaterialPrefabs > 0)
            {
                issues.Add(BuildValidationIssue.Warning(
                    AssetCode,
                    $"资源健康：{missingMaterialPrefabs} 个 Prefab 存在 Missing Material"
                    + $"（共扫描 {scanned} 个），请检查引用。",
                    GroupAsset));
            }

            if (scanned == 0 && guids.Length > 0)
            {
                issues.Add(BuildValidationIssue.Warning(
                    AssetCode,
                    "资源健康：未找到可扫描的项目 Prefab。",
                    GroupAsset));
            }
        }

        /// <summary>
        /// 执行第三方注册校验器；单个校验器异常不影响其余校验器。
        /// </summary>
        /// <param name="context">校验上下文。</param>
        /// <param name="issues">追加问题条目的目标集合。</param>
        private static void ValidateThirdParty(
            BuildValidationContext context,
            ICollection<BuildValidationIssue> issues)
        {
            IReadOnlyList<IBuildValidator> validators =
                BuildValidatorRegistry.GetAll();
            for (int i = 0; i < validators.Count; i++)
            {
                IBuildValidator validator = validators[i];
                try
                {
                    validator.Validate(context, issues);
                }
                catch (Exception exception)
                {
                    issues.Add(BuildValidationIssue.Error(
                        ThirdPartyCode,
                        $"第三方校验器 '{validator.Id}' 执行异常：{exception.Message}",
                        GroupThirdParty));
                }
            }
        }
    }
}
