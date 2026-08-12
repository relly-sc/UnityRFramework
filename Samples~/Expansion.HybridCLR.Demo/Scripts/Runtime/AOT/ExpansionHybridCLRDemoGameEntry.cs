using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using UnityEngine;
using UnityRFramework.Expansion;
using UnityRFramework.Runtime;

namespace UnityRFramework.Expansion
{
    /// <summary>
    /// 在 ExpansionDemo 资源更新完成后加载 HybridCLR 代码热更新入口。
    /// </summary>
    public sealed class ExpansionHybridCLRDemoGameEntry : ExpansionDemoGameEntry
    {
        private static readonly string[] StartupTags = { "preload", "hotupdate" };

        [SerializeField]
        [Tooltip("当前平台的 HybridCLR 热更新清单资源位置。")]
        private string manifestLocation =
            "HotUpdate/StandaloneWindows64/Manifest";

        private readonly HybridCLRAssemblyLoader loader =
            new HybridCLRAssemblyLoader();

        /// <summary>
        /// 将代码清单、热更新程序集和 AOT 元数据纳入启动下载检查。
        /// </summary>
        protected override string[] GetStartupResourceTags()
        {
            return StartupTags;
        }

        /// <summary>
        /// 资源就绪后加载并启动热更新入口。
        /// </summary>
        protected override async Task OnResourcesReadyAsync(CancellationToken ct)
        {
            HybridCLRHotUpdateContext context = new HybridCLRHotUpdateContext(
                GameEntry.Resource,
                ContinueToDemoAsync);
            await loader.LoadAndStartAsync(
                GameEntry.Resource,
                manifestLocation,
                context,
                ct);
        }

        /// <summary>
        /// 由热更新入口确认后继续进入现有 AOT Demo。
        /// </summary>
        private Task ContinueToDemoAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            StartDemo();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 场景或框架退出时先停止热更新入口，再清理基础启动流程。
        /// </summary>
        protected override void OnDestroy()
        {
            try
            {
                loader.Shutdown();
            }
            catch (Exception exception)
            {
                if (RFrameworkLog.IsInitialized)
                {
                    Log.Error(
                        "[ExpansionHybridCLRDemo] Hot update shutdown failed: {0}",
                        exception);
                }
            }
            finally
            {
                base.OnDestroy();
            }
        }
    }
}
