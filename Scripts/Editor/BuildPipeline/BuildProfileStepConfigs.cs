using System;
using UnityEngine;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 构建配置资产的内嵌步骤配置类集合。
    /// 将原 ScriptableObject 形式的步骤私有配置直接内嵌进 Profile，
    /// 消除「创建配置资产 + 挂载到步骤条目 Configuration 字段」的链路。
    /// 每个类对应一个步骤，顶层字段默认 new 初始化，永不为 null。
    /// </summary>
    public static class BuildProfileStepConfigs
    {
        // 本类为分组容器，无静态成员；实际类型定义在以下 partial / 同类文件中。
    }

    /// <summary>
    /// Config 导出步骤的内嵌配置：导出路径与格式、JSON 导出开关、JSON 泄漏检查。
    /// 字段与 <see cref="ConfigPipelineOptions"/> 一一对应，
    /// 保留 JSON 开关与泄漏检查以便正式档产物不携带开发 JSON。
    /// </summary>
    [Serializable]
    public sealed class ConfigExportBuildSettings
    {
        /// <summary>导出路径与格式配置，直接复用 ConfigPipelineOptions。</summary>
        [Tooltip("导出路径与格式配置。")]
        public ConfigPipelineOptions Options = new ConfigPipelineOptions();

        /// <summary>是否导出 JSON；关闭后导出完成会清除输出目录的 Json 子目录，仅保留二进制产物。</summary>
        [Tooltip("是否导出 JSON；关闭后导出完成会清除输出目录的 Json 子目录，仅保留二进制产物。")]
        public bool ExportJson = true;

        /// <summary>是否检查开发 JSON 泄漏；开启且 ExportJson 关闭时，校验阶段对残留 JSON 发出警告。</summary>
        [Tooltip("是否检查开发 JSON 泄漏；开启且 ExportJson 关闭时，校验阶段对残留 JSON 发出警告。")]
        public bool JsonLeakCheck = true;
    }

    /// <summary>
    /// HybridCLR 热更准备步骤的内嵌配置：输出路径、入口类型、版本、PDB 与仅生成开关。
    /// 热更程序集清单与 AOT 补充元数据以 HybridCLRSettings 为唯一事实源，本配置不维护平行副本。
    /// </summary>
    [Serializable]
    public sealed class HybridClrBuildSettings
    {
        /// <summary>热更产物输出根目录（Assets/ 相对路径），必填。如 "Assets/GameAssets/HotUpdate"。</summary>
        [Tooltip("热更产物输出根目录（Assets/ 相对路径），必填。如 Assets/GameAssets/HotUpdate。")]
        public string OutputAssetRoot = "Assets/GameAssets/HotUpdate";

        /// <summary>热更新入口类型全名，运行时通过该类型启动热更逻辑，必填。</summary>
        [Tooltip("热更新入口类型全名，必填。")]
        public string EntryTypeName = string.Empty;

        /// <summary>业务代码版本号；留空时自动生成时间戳版本（yyyy-MM-dd-HHmmss）。</summary>
        [Tooltip("业务代码版本号；留空时自动生成时间戳版本。")]
        public string CodeVersion = string.Empty;

        /// <summary>是否同时发布并加载 Portable PDB 调试符号。</summary>
        [Tooltip("是否同时发布并加载 Portable PDB 调试符号。")]
        public bool IncludePdb = false;

        /// <summary>
        /// 是否仅生成桥接代码：首次构建 Player 前应开启，只生成代码不编译热更；
        /// Player 基线建立后关闭，执行完整生成、编译与暂存流程。
        /// </summary>
        [Tooltip("是否仅生成桥接代码；首次构建 Player 前开启，建立基线后关闭。")]
        public bool GenerateOnly = false;
    }

    /// <summary>
    /// YooAsset 资源打包步骤的内嵌配置：Package 名称、版本与发布目录名。
    /// 包备注由 YooAsset Bundle Collector Setting 承载，本步骤不重复暴露。
    /// 收集规则由 YooAsset Bundle Collector Setting 配置，本步骤只构建已有 Package，不修改收集规则。
    /// </summary>
    [Serializable]
    public sealed class YooAssetBuildSettings
    {
        /// <summary>要构建的 YooAsset Package 名称，与 Bundle Collector Setting 中的 Package 对应，必填。</summary>
        [Tooltip("要构建的 YooAsset Package 名称，必填。")]
        public string PackageName = string.Empty;

        /// <summary>
        /// 资源包版本号；留空时采用 Builder 默认版本（见 <see cref="GetDefaultBuilderVersion"/>）。
        /// </summary>
        [Tooltip("资源包版本号；留空时采用 Builder 默认版本（yyyy-MM-dd-分钟数）。")]
        public string PackageVersion = string.Empty;

        /// <summary>
        /// 发布目录名；非空时构建完成后将产物复制到工程根/Bundles/{目录名}，
        /// 留空时只构建不发布。
        /// </summary>
        [Tooltip("发布目录名；非空时构建完成后复制到工程根/Bundles/ 下，留空时不发布。")]
        public string ServerDirectoryName = string.Empty;

        /// <summary>
        /// 构建管线名称；空则读取 Builder 当前值（默认 ScriptableBuildPipeline）。
        /// 执行前同步回 BundleBuilderSetting，避免每次修改都要打开插件窗口。
        /// 可选值固定为三条真机可用管线：
        /// ScriptableBuildPipeline / LegacyBuildPipeline / RawFileBuildPipeline。
        /// </summary>
        [Tooltip("构建管线名称；空则读取 Builder 当前值，执行前同步回插件。可选 ScriptableBuildPipeline/LegacyBuildPipeline/RawFileBuildPipeline。")]
        public string BuildPipelineName = string.Empty;

        /// <summary>
        /// 构建前是否清理 Build Cache；默认读取 Builder 当前值（清/不清）。
        /// 执行前同步回 BundleBuilderSetting。
        /// </summary>
        [Tooltip("构建前是否清理 Build Cache；默认读取 Builder 当前值，执行前同步回插件。")]
        public bool ClearBuildCache;

        /// <summary>
        /// 返回 Builder 默认资源包版本号，与 YooAsset 官方 BuildPipelineViewerBase
        /// GetDefaultPackageVersion 规则一致：日期加当日分钟数。
        /// </summary>
        /// <returns>默认资源包版本号。</returns>
        public static string GetDefaultBuilderVersion()
        {
            int totalMinutes = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
            return DateTime.Now.ToString("yyyy-MM-dd") + "-" + totalMinutes;
        }

        /// <summary>
        /// 返回真机可用的三条构建管线名称；ESBP 为编辑器模拟、IABP 仅团结引擎、
        /// AFBP 为归档管线，均不适用于真机构建，故不在此列出。
        /// </summary>
        /// <returns>真机构建管线名称数组。</returns>
        public static string[] GetBuildPipelineOptions()
        {
            return new string[]
            {
                "ScriptableBuildPipeline",
                "LegacyBuildPipeline",
                "RawFileBuildPipeline"
            };
        }
    }

    /// <summary>
    /// Obfuz 热更混淆步骤的内嵌配置：启用开关与混淆 Pass 选择。
    /// 混淆程序集清单仍由 Obfuz Settings（第三方窗口）维护为唯一事实源，
    /// 本配置仅暴露执行路径上最关键的开关与 Pass 级联选项，
    /// 执行前同步到 ObfuzSettings.Instance，避免每次修改都要打开插件设置窗口。
    /// </summary>
    [Serializable]
    public sealed class ObfuzBuildSettings
    {
        /// <summary>
        /// 是否启用 Obfuz 混淆管线；关闭后整个混淆步骤将被跳过。
        /// </summary>
        [Tooltip("是否启用 Obfuz 混淆管线；关闭则跳过整个混淆步骤。")]
        public bool Enable = true;

        /// <summary>
        /// 启用的混淆 Pass 类型，逗号分隔的 ObfuscationPassType 枚举名称列表。
        /// 可用值：ConstEncrypt / FieldEncrypt / SymbolObfus / CallObfus / ExprObfus / ControlFlowObfus / EvalStackObfus / RemoveConstField / WaterMark / All。
        /// 默认 "All" 表示启用全部 Pass；留空等同于 None。
        /// </summary>
        [Tooltip("启用的混淆 Pass 类型，逗号分隔枚举名；默认 \"All\"，可选 ConstEncrypt/FieldEncrypt/SymbolObfus/CallObfus/ExprObfus/ControlFlowObfus/EvalStackObfus/RemoveConstField/WaterMark。")]
        public string EnabledPasses = "All";
    }
}
