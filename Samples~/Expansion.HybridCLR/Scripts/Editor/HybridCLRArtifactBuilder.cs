using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using RFramework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityRFramework.Expansion;

namespace UnityRFramework.Editor
{
    /// <summary>
    /// 配置 HybridCLR，并将当前平台产物整理为可由 ResourceModule 加载的资源。
    /// </summary>
    public static class HybridCLRArtifactBuilder
    {
        /// <summary>
        /// 校验当前 HybridCLR 设置和目标平台。
        /// </summary>
        [MenuItem("UnityRFramework/Expansion/HybridCLR/配置并校验")]
        public static void ConfigureAndValidate()
        {
            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = true;
            HybridCLRSettings.Save();
            if ((settings.hotUpdateAssemblyDefinitions == null
                    || settings.hotUpdateAssemblyDefinitions.Length == 0)
                && (settings.hotUpdateAssemblies == null
                    || settings.hotUpdateAssemblies.Length == 0))
            {
                throw new RFrameworkException(
                    "HybridCLR has no configured hot update assembly.");
            }

            if (settings.patchAOTAssemblies == null
                || settings.patchAOTAssemblies.Length == 0)
            {
                throw new RFrameworkException(
                    "HybridCLR has no configured AOT metadata assembly.");
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            if (PlayerSettings.GetScriptingBackend(group)
                != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(
                    group,
                    ScriptingImplementation.IL2CPP);
            }

            Debug.Log(
                $"[HybridCLR] Settings validated for '{target}'. "
                + $"Hot update assemblies: "
                + $"{string.Join(", ", SettingsUtil.HotUpdateAssemblyNamesExcludePreserved)}.");
        }

        /// <summary>
        /// 将调用方提供的 asmdef 与 AOT 元数据程序集并入 HybridCLR 设置。
        /// </summary>
        /// <param name="hotUpdateAsmdefPath">热更新 asmdef 的 Assets 路径。</param>
        /// <param name="patchAotAssemblies">需要补充元数据的 AOT 程序集名称。</param>
        public static void ConfigureProject(
            string hotUpdateAsmdefPath,
            string[] patchAotAssemblies)
        {
            AssemblyDefinitionAsset hotUpdateAsmdef =
                AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(
                    hotUpdateAsmdefPath);
            if (hotUpdateAsmdef == null)
            {
                throw new RFrameworkException(
                    $"HybridCLR hot update asmdef is missing: "
                    + $"'{hotUpdateAsmdefPath}'.");
            }

            if (patchAotAssemblies == null || patchAotAssemblies.Length == 0)
            {
                throw new RFrameworkException(
                    "HybridCLR AOT metadata assembly list is empty.");
            }

            HybridCLRSettings settings = HybridCLRSettings.Instance;
            settings.enable = true;
            settings.hotUpdateAssemblyDefinitions = AppendDistinct(
                settings.hotUpdateAssemblyDefinitions,
                hotUpdateAsmdef);
            settings.patchAOTAssemblies = AppendDistinct(
                settings.patchAOTAssemblies,
                patchAotAssemblies);
            HybridCLRSettings.Save();
            ConfigureAndValidate();
        }

        /// <summary>
        /// 执行 HybridCLR 当前平台完整生成流程。
        /// </summary>
        [MenuItem("UnityRFramework/Expansion/HybridCLR/生成当前平台代码产物")]
        public static void GenerateCurrentTarget()
        {
            ConfigureAndValidate();
            PrebuildCommand.GenerateAll();
            Debug.Log(
                $"[HybridCLR] Generated code artifacts for "
                + $"'{EditorUserBuildSettings.activeBuildTarget}'.");
        }

        /// <summary>
        /// 编译当前平台热更新程序集，并将它与现有 Player 的 AOT 裁剪产物复制到资源目录。
        /// </summary>
        /// <param name="assetOutputRoot">以 Assets/ 开头的产物根目录。</param>
        /// <param name="entryTypeName">热更新入口类型全名。</param>
        /// <param name="codeVersion">业务代码版本。</param>
        /// <param name="hotUpdateAssemblyNames">按依赖顺序排列的热更新程序集名称。</param>
        /// <param name="patchAotAssemblies">需要发布的 AOT 补充元数据程序集。</param>
        /// <param name="includePdb">是否同时发布并加载 Portable PDB 调试符号。</param>
        /// <returns>当前 BuildTarget 名称。</returns>
        public static string CompileAndStage(
            string assetOutputRoot,
            string entryTypeName,
            string codeVersion,
            string[] hotUpdateAssemblyNames,
            string[] patchAotAssemblies,
            bool includePdb = false)
        {
            ValidateAssetOutputRoot(assetOutputRoot);
            if (string.IsNullOrWhiteSpace(entryTypeName)
                || string.IsNullOrWhiteSpace(codeVersion))
            {
                throw new RFrameworkException(
                    "HybridCLR entry type or code version is empty.");
            }

            if (hotUpdateAssemblyNames == null
                || hotUpdateAssemblyNames.Length == 0
                || patchAotAssemblies == null
                || patchAotAssemblies.Length == 0)
            {
                throw new RFrameworkException(
                    "HybridCLR hot update or AOT assembly list is empty.");
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            HybridCLRPlayerBaseline.Validate(target, patchAotAssemblies);
            CompileDllCommand.CompileDll(target);
            string targetName = target.ToString();
            string targetAssetRoot =
                NormalizeAssetPath($"{assetOutputRoot}/{targetName}");
            string targetDiskRoot = AssetPathToDiskPath(targetAssetRoot);
            if (Directory.Exists(targetDiskRoot))
            {
                Directory.Delete(targetDiskRoot, true);
            }

            Directory.CreateDirectory(targetDiskRoot);

            HybridCLRHotUpdateAssemblyInfo[] hotUpdateInfos =
                StageHotUpdateAssemblies(
                target,
                targetDiskRoot,
                hotUpdateAssemblyNames,
                includePdb);
            HybridCLRAotMetadataInfo[] aotMetadata = StageAotMetadata(
                target,
                targetDiskRoot,
                patchAotAssemblies);

            HybridCLRHotUpdateManifest manifest = new HybridCLRHotUpdateManifest
            {
                codeVersion = codeVersion,
                buildTarget = targetName,
                entryTypeName = entryTypeName,
                aotMetadata = aotMetadata,
                hotUpdateAssemblies = hotUpdateInfos
            };
            manifest.Validate(targetName);
            File.WriteAllText(
                Path.Combine(targetDiskRoot, "Manifest.bytes"),
                JsonUtility.ToJson(manifest, true));

            AssetDatabase.Refresh();
            Debug.Log(
                $"[HybridCLR] Staged '{codeVersion}' artifacts at "
                + $"'{targetAssetRoot}'.");
            return targetName;
        }

        private static HybridCLRHotUpdateAssemblyInfo[] StageHotUpdateAssemblies(
            BuildTarget target,
            string targetDiskRoot,
            string[] assemblyNames,
            bool includePdb)
        {
            string sourceRoot = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            string assembliesDiskRoot = Path.Combine(targetDiskRoot, "Assemblies");
            Directory.CreateDirectory(assembliesDiskRoot);
            List<HybridCLRHotUpdateAssemblyInfo> result =
                new List<HybridCLRHotUpdateAssemblyInfo>();
            foreach (string assemblyName in assemblyNames)
            {
                string sourceDll = Path.Combine(sourceRoot, assemblyName + ".dll");
                if (!File.Exists(sourceDll))
                {
                    throw new RFrameworkException(
                        $"HybridCLR hot update DLL is missing: '{sourceDll}'.");
                }

                File.Copy(
                    sourceDll,
                    Path.Combine(
                        assembliesDiskRoot,
                        assemblyName + ".dll.bytes"),
                    true);

                string pdbLocation = string.Empty;
                string sourcePdb = Path.Combine(sourceRoot, assemblyName + ".pdb");
                if (includePdb && File.Exists(sourcePdb))
                {
                    File.Copy(
                        sourcePdb,
                        Path.Combine(
                            assembliesDiskRoot,
                            assemblyName + ".pdb.bytes"),
                        true);
                    pdbLocation =
                        $"HotUpdate/{target}/Assemblies/{assemblyName}.pdb";
                }

                result.Add(new HybridCLRHotUpdateAssemblyInfo
                {
                    assemblyName = assemblyName,
                    dllLocation =
                        $"HotUpdate/{target}/Assemblies/{assemblyName}.dll",
                    pdbLocation = pdbLocation
                });
            }

            return result.ToArray();
        }

        private static HybridCLRAotMetadataInfo[] StageAotMetadata(
            BuildTarget target,
            string targetDiskRoot,
            string[] assemblyNames)
        {
            string sourceRoot = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            string aotDiskRoot = Path.Combine(targetDiskRoot, "AOT");
            Directory.CreateDirectory(aotDiskRoot);

            List<HybridCLRAotMetadataInfo> result =
                new List<HybridCLRAotMetadataInfo>();
            foreach (string assemblyName in assemblyNames)
            {
                string sourceDll = Path.Combine(sourceRoot, assemblyName + ".dll");
                if (!File.Exists(sourceDll))
                {
                    throw new RFrameworkException(
                        $"HybridCLR stripped AOT DLL is missing: '{sourceDll}'.");
                }

                File.Copy(
                    sourceDll,
                    Path.Combine(aotDiskRoot, assemblyName + ".dll.bytes"),
                    true);
                result.Add(new HybridCLRAotMetadataInfo
                {
                    assemblyName = assemblyName,
                    location = $"HotUpdate/{target}/AOT/{assemblyName}.dll"
                });
            }

            return result.ToArray();
        }

        private static AssemblyDefinitionAsset[] AppendDistinct(
            AssemblyDefinitionAsset[] source,
            AssemblyDefinitionAsset value)
        {
            List<AssemblyDefinitionAsset> result = source?
                .Where(item => item != null)
                .ToList() ?? new List<AssemblyDefinitionAsset>();
            if (!result.Contains(value))
            {
                result.Add(value);
            }

            return result.ToArray();
        }

        private static string[] AppendDistinct(string[] source, string[] values)
        {
            HashSet<string> result = new HashSet<string>(
                source ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (string value in values)
            {
                result.Add(value);
            }

            return result.ToArray();
        }

        private static void ValidateAssetOutputRoot(string assetOutputRoot)
        {
            string normalized = NormalizeAssetPath(assetOutputRoot);
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || normalized.Contains(".."))
            {
                throw new RFrameworkException(
                    $"HybridCLR output root must be inside Assets: '{assetOutputRoot}'.");
            }
        }

        private static string AssetPathToDiskPath(string assetPath)
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizeAssetPath(string path)
        {
            return path?.Replace('\\', '/').TrimEnd('/');
        }
    }
}
