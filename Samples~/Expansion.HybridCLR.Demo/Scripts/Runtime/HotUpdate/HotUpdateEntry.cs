using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityRFramework.Expansion;

namespace UnityRFramework.Sample
{
    /// <summary>
    /// HybridCLR Demo 的热更新程序集入口。
    /// </summary>
    public sealed class HotUpdateEntry : IHotUpdateEntry
    {
        private const string ProbePrefabLocation = "HotUpdate/ProbePanel";

        private IHotUpdateContext context;
        private GameObject prefabAsset;
        private GameObject instance;
        private HotUpdateProbePanel panel;

        /// <summary>
        /// 加载热更新 UI，并把继续进入 Demo 的控制权交给热更新代码。
        /// </summary>
        public async Task StartAsync(IHotUpdateContext hotUpdateContext, CancellationToken ct)
        {
            context = hotUpdateContext
                ?? throw new ArgumentNullException(nameof(hotUpdateContext));
            prefabAsset = await context.Resource.LoadAssetAsync<GameObject>(
                ProbePrefabLocation,
                ct: ct);
            ct.ThrowIfCancellationRequested();

            instance = UnityEngine.Object.Instantiate(prefabAsset);
            panel = instance.GetComponent<HotUpdateProbePanel>();
            if (panel == null)
            {
                throw new InvalidOperationException(
                    "HybridCLR probe prefab is missing HotUpdateProbePanel.");
            }

            panel.Initialize(context.CodeVersion, ContinueAsync);
        }

        /// <summary>
        /// 销毁热更新 UI 并释放持有的 Prefab 资源。
        /// </summary>
        public void Shutdown()
        {
            if (panel != null)
            {
                panel.Shutdown();
                panel = null;
            }

            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
                instance = null;
            }

            if (prefabAsset != null && context?.Resource != null)
            {
                context.Resource.UnloadAsset<GameObject>(ProbePrefabLocation);
                prefabAsset = null;
            }

            context = null;
        }

        private async Task ContinueAsync(CancellationToken ct)
        {
            await context.ContinueAsync(ct);
        }
    }
}
