using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HybridCLR;
using RFramework;
using UnityEngine;
using UnityRFramework.Runtime;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 从 ResourceModule 加载 AOT 补充元数据和 HybridCLR 热更新程序集。
    /// </summary>
    public sealed class HybridCLRAssemblyLoader
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Assembly> LoadedAssemblies =
            new Dictionary<string, Assembly>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> LoadedAssemblyVersions =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> LoadedAotMetadata =
            new HashSet<string>(StringComparer.Ordinal);

        private IHotUpdateEntry currentEntry;
        private bool isLoading;

        /// <summary>获取当前已启动的热更新入口。</summary>
        public IHotUpdateEntry CurrentEntry => currentEntry;

        /// <summary>
        /// 加载清单、补充元数据和热更新程序集，然后启动入口。
        /// </summary>
        /// <param name="resource">已完成初始化的资源组件。</param>
        /// <param name="manifestLocation">热更新 Manifest 资源位置。</param>
        /// <param name="context">AOT 运行上下文。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>已启动的热更新入口。</returns>
        public async Task<IHotUpdateEntry> LoadAndStartAsync(
            ResourceComponent resource,
            string manifestLocation,
            HybridCLRHotUpdateContext context,
            CancellationToken ct)
        {
            if (resource == null || context == null
                || string.IsNullOrWhiteSpace(manifestLocation))
            {
                throw new RFrameworkException(
                    "HybridCLR loader resource, manifest location or context is invalid.");
            }

            if (isLoading || currentEntry != null)
            {
                throw new RFrameworkException(
                    "HybridCLR loader is already loading or running an entry.");
            }

            isLoading = true;
            try
            {
                HybridCLRHotUpdateManifest manifest = await LoadManifestAsync(
                    resource,
                    manifestLocation,
                    ct);
                context.SetCodeVersion(manifest.codeVersion);
                await LoadAotMetadataAsync(resource, manifest.aotMetadata, ct);

                Assembly entryAssembly = null;
                Type entryType = null;
                for (int i = 0; i < manifest.hotUpdateAssemblies.Length; i++)
                {
                    Assembly assembly = await LoadAssemblyAsync(
                        resource,
                        manifest.hotUpdateAssemblies[i],
                        manifest.codeVersion,
                        ct);
                    Type configuredType = assembly.GetType(
                        manifest.entryTypeName,
                        false);
                    if (configuredType != null)
                    {
                        entryAssembly = assembly;
                        entryType = configuredType;
                        break;
                    }
                }

                if (entryAssembly == null)
                {
                    // Obfuz 可能重命名热更新类型，manifest 中保存的原始全名
                    // 因而不一定还能直接反射命中。仅在全名查找失败时，按协议
                    // 扫描实现 IHotUpdateEntry 的唯一候选类型。
                    List<Type> candidates = new List<Type>();
                    for (int i = 0; i < manifest.hotUpdateAssemblies.Length; i++)
                    {
                        Assembly assembly = await LoadAssemblyAsync(
                            resource,
                            manifest.hotUpdateAssemblies[i],
                            manifest.codeVersion,
                            ct);
                        Type[] types = assembly.GetTypes();
                        for (int j = 0; j < types.Length; j++)
                        {
                            Type candidate = types[j];
                            if (typeof(IHotUpdateEntry).IsAssignableFrom(candidate)
                                && !candidate.IsAbstract
                                && candidate.GetConstructor(Type.EmptyTypes) != null)
                            {
                                candidates.Add(candidate);
                            }
                        }
                    }

                    if (candidates.Count == 1)
                    {
                        entryType = candidates[0];
                        entryAssembly = entryType.Assembly;
                        Debug.LogWarning(
                            $"HybridCLR entry type '{manifest.entryTypeName}' was not found; "
                            + $"using the only IHotUpdateEntry candidate '{entryType.FullName}'.");
                    }
                    else
                    {
                        throw new RFrameworkException(
                            $"HybridCLR entry type '{manifest.entryTypeName}' was not found, "
                            + $"and discovered {candidates.Count} valid IHotUpdateEntry candidates.");
                    }
                }

                if (!typeof(IHotUpdateEntry).IsAssignableFrom(entryType)
                    || entryType.IsAbstract
                    || entryType.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new RFrameworkException(
                        $"HybridCLR entry type '{entryType.FullName}' must be a concrete "
                        + "IHotUpdateEntry with a public parameterless constructor.");
                }

                IHotUpdateEntry entry = null;
                try
                {
                    entry = (IHotUpdateEntry)Activator.CreateInstance(entryType);
                    await entry.StartAsync(context, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    TryShutdownEntry(entry);
                    throw;
                }
                catch (Exception exception)
                {
                    TryShutdownEntry(entry);
                    throw new RFrameworkException(
                        $"Failed to start HybridCLR entry '{entryType.FullName}'.",
                        exception);
                }

                currentEntry = entry;
                return entry;
            }
            finally
            {
                isLoading = false;
            }
        }

        /// <summary>
        /// 停止当前入口。已加载程序集和元数据保留到 Player 进程结束。
        /// </summary>
        public void Shutdown()
        {
            IHotUpdateEntry entry = currentEntry;
            currentEntry = null;
            if (entry == null)
            {
                return;
            }

            try
            {
                entry.Shutdown();
            }
            catch (Exception exception)
            {
                throw new RFrameworkException(
                    "HybridCLR entry shutdown failed.",
                    exception);
            }
        }

        private static async Task<HybridCLRHotUpdateManifest> LoadManifestAsync(
            ResourceComponent resource,
            string location,
            CancellationToken ct)
        {
            byte[] bytes = null;
            try
            {
                bytes = await resource.LoadAssetAsync<byte[]>(location, ct: ct);
                return HybridCLRHotUpdateManifest.Parse(
                    bytes,
                    GetRuntimeBuildTargetName());
            }
            finally
            {
                if (bytes != null)
                {
                    resource.UnloadAsset<byte[]>(location);
                }
            }
        }

        private static async Task LoadAotMetadataAsync(
            ResourceComponent resource,
            HybridCLRAotMetadataInfo[] metadata,
            CancellationToken ct)
        {
            for (int i = 0; i < metadata.Length; i++)
            {
                HybridCLRAotMetadataInfo item = metadata[i];
                lock (SyncRoot)
                {
                    if (LoadedAotMetadata.Contains(item.assemblyName))
                    {
                        continue;
                    }
                }

                byte[] bytes = null;
                try
                {
                    bytes = await resource.LoadAssetAsync<byte[]>(item.location, ct: ct);
                    ct.ThrowIfCancellationRequested();
#if ENABLE_IL2CPP && !UNITY_EDITOR
                    LoadImageErrorCode result;
                    try
                    {
                        result = RuntimeApi.LoadMetadataForAOTAssembly(
                            bytes,
                            HomologousImageMode.Consistent);
                    }
                    catch (Exception exception)
                    {
                        throw new RFrameworkException(
                            $"HybridCLR AOT metadata '{item.assemblyName}' "
                            + "does not match the current Player baseline.",
                            exception);
                    }

                    if (result != LoadImageErrorCode.OK
                        && result != LoadImageErrorCode.HOMOLOGOUS_ASSEMBLY_HAS_LOADED)
                    {
                        throw new RFrameworkException(
                            $"HybridCLR AOT metadata '{item.assemblyName}' failed: {result}.");
                    }
#endif
                    lock (SyncRoot)
                    {
                        LoadedAotMetadata.Add(item.assemblyName);
                    }
                }
                finally
                {
                    if (bytes != null)
                    {
                        resource.UnloadAsset<byte[]>(item.location);
                    }
                }
            }
        }

        private static async Task<Assembly> LoadAssemblyAsync(
            ResourceComponent resource,
            HybridCLRHotUpdateAssemblyInfo info,
            string codeVersion,
            CancellationToken ct)
        {
            lock (SyncRoot)
            {
                if (LoadedAssemblies.TryGetValue(info.assemblyName, out Assembly loaded))
                {
                    EnsureLoadedVersion(info.assemblyName, codeVersion);
                    return loaded;
                }
            }

#if UNITY_EDITOR
            Assembly editorAssembly = FindLoadedAssembly(info.assemblyName);
            if (editorAssembly != null)
            {
                lock (SyncRoot)
                {
                    LoadedAssemblies[info.assemblyName] = editorAssembly;
                    LoadedAssemblyVersions[info.assemblyName] = codeVersion;
                }

                return editorAssembly;
            }
#endif

            byte[] dllBytes = null;
            byte[] pdbBytes = null;
            try
            {
                dllBytes = await resource.LoadAssetAsync<byte[]>(info.dllLocation, ct: ct);
                if (!string.IsNullOrWhiteSpace(info.pdbLocation))
                {
                    pdbBytes = await resource.LoadAssetAsync<byte[]>(info.pdbLocation, ct: ct);
                }

                ct.ThrowIfCancellationRequested();
                Assembly assembly = pdbBytes == null
                    ? Assembly.Load(dllBytes)
                    : Assembly.Load(dllBytes, pdbBytes);
                if (!string.Equals(
                    assembly.GetName().Name,
                    info.assemblyName,
                    StringComparison.Ordinal))
                {
                    throw new RFrameworkException(
                        $"HybridCLR assembly identity '{assembly.GetName().Name}' does not "
                        + $"match manifest name '{info.assemblyName}'.");
                }

                lock (SyncRoot)
                {
                    LoadedAssemblies[info.assemblyName] = assembly;
                    LoadedAssemblyVersions[info.assemblyName] = codeVersion;
                }

                return assembly;
            }
            catch (RFrameworkException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new RFrameworkException(
                    $"Failed to load HybridCLR assembly '{info.assemblyName}'.",
                    exception);
            }
            finally
            {
                if (pdbBytes != null)
                {
                    resource.UnloadAsset<byte[]>(info.pdbLocation);
                }

                if (dllBytes != null)
                {
                    resource.UnloadAsset<byte[]>(info.dllLocation);
                }
            }
        }

        private static void EnsureLoadedVersion(
            string assemblyName,
            string requestedCodeVersion)
        {
            if (!LoadedAssemblyVersions.TryGetValue(
                    assemblyName,
                    out string loadedCodeVersion)
                || string.Equals(
                    loadedCodeVersion,
                    requestedCodeVersion,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new RFrameworkException(
                $"HybridCLR assembly '{assemblyName}' version '{loadedCodeVersion}' "
                + $"is already loaded, but manifest requests '{requestedCodeVersion}'. "
                + "Fully restart the Player to activate the new code version.");
        }

        private static Assembly FindLoadedAssembly(string assemblyName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(
                    assemblies[i].GetName().Name,
                    assemblyName,
                    StringComparison.Ordinal))
                {
                    return assemblies[i];
                }
            }

            return null;
        }

        private static void TryShutdownEntry(IHotUpdateEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            try
            {
                entry.Shutdown();
            }
            catch
            {
                // 保留启动阶段的原始异常，清理异常由下一次完整启动重新诊断。
            }
        }

        private static string GetRuntimeBuildTargetName()
        {
#if UNITY_EDITOR
            return EditorUserBuildSettings.activeBuildTarget.ToString();
#else
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                    return Environment.Is64BitProcess
                        ? "StandaloneWindows64"
                        : "StandaloneWindows";
                case RuntimePlatform.OSXPlayer:
                    return "StandaloneOSX";
                case RuntimePlatform.LinuxPlayer:
                    return "StandaloneLinux64";
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                default:
                    throw new RFrameworkException(
                        $"Unsupported HybridCLR runtime platform '{Application.platform}'.");
            }
#endif
        }
    }
}
